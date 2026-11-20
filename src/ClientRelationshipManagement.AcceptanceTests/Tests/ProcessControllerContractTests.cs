// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security;
using System.Security.Authentication;
using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using cCoder.ClientRelationshipManagement.Runtime.Brokers.Loggings;
using ClientRelationshipManagement.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Moq;
using Xunit;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class ProcessControllerContractTests
{
    static readonly string[] Outcomes =
    [
        "Validation", "Authentication", "Authorization", "Unexpected"
    ];

    public static IEnumerable<object[]> FailureCases() =>
        Actions().SelectMany(action => Outcomes.Select(outcome =>
            new object[] { ActionId(action), outcome }));

    [Fact]
    public void Inventory_ShouldContainEveryProcessAction()
    {
        // Given
        MethodInfo[] actions = Actions();

        // When
        int actionCount = actions.Length;

        // Then
        Assert.Equal(expected: 13, actual: actionCount);
    }

    [Theory]
    [MemberData(nameof(FailureCases))]
    public async Task Action_WhenDependencyFails_ShouldCatchAndApplyContract(
        string actionId,
        string outcome)
    {
        // Given
        Exception failure = Failure(outcome);
        Mock<ILoggingBroker<ProcessController>> logging = new();
        ProcessController controller = CreateController(logging, failure);
        MethodInfo action = Actions().Single(method => ActionId(method) == actionId);

        // When
        IActionResult result = await Invoke(action, controller, Arguments(action));

        // Then
        AssertFailure(result, logging, failure, outcome);
    }

    static ProcessController CreateController(
        Mock<ILoggingBroker<ProcessController>> logging,
        Exception failure)
    {
        ConstructorInfo constructor = typeof(ProcessController).GetConstructors().Single();
        Mock<ICRMAuthInfo> authInfo = new();
        authInfo.SetupGet(info => info.SSOUserId).Returns("contract-test-user");
        authInfo.SetupGet(info => info.ReadableTenants).Returns(["contract-test-tenant"]);
        authInfo.SetupGet(info => info.WriteableTenants).Returns(["contract-test-tenant"]);
        object[] dependencies = constructor.GetParameters()
            .Select(parameter => parameter.ParameterType == typeof(ILoggingBroker<ProcessController>)
                ? logging.Object
                : parameter.ParameterType == typeof(ICRMAuthInfo)
                    ? authInfo.Object
                    : DependencyProxy.Create(parameter.ParameterType, failure))
            .ToArray();
        ProcessController controller = (ProcessController)constructor.Invoke(dependencies);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return controller;
    }

    static MethodInfo[] Actions() => typeof(ProcessController)
        .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
        .Where(method => method.GetCustomAttributes(inherit: true)
            .Any(attribute => attribute is HttpMethodAttribute))
        .OrderBy(method => method.MetadataToken)
        .ToArray();

    static string ActionId(MethodInfo method) =>
        $"{method.Name}({string.Join(',', method.GetParameters().Select(parameter => parameter.ParameterType.Name))})";

    static object[] Arguments(MethodInfo action) => action.GetParameters()
        .Select(parameter => parameter.ParameterType == typeof(CancellationToken)
            ? CancellationToken.None
            : parameter.ParameterType == typeof(string)
                ? "contract test"
                : parameter.ParameterType == typeof(ProcessTransitionEffect)
                    ? ProcessTransitionEffect.DeferLead
                    : parameter.ParameterType.IsValueType
                        ? Activator.CreateInstance(parameter.ParameterType)
                        : Activator.CreateInstance(parameter.ParameterType))
        .ToArray();

    static async Task<IActionResult> Invoke(
        MethodInfo action,
        ProcessController controller,
        object[] arguments)
    {
        object invocation = action.Invoke(controller, arguments);

        return invocation switch
        {
            Task<IActionResult> task => await task,
            IActionResult result => result,
            _ => throw new InvalidOperationException($"Unsupported action result for {action.Name}.")
        };
    }

    static Exception Failure(string outcome) => outcome switch
    {
        "Validation" => new ValidationException("The request is invalid."),
        "Authentication" => new AuthenticationException("Authentication failed."),
        "Authorization" => new SecurityException("Permission denied."),
        _ => new InvalidOperationException("Storage unavailable.")
    };

    static void AssertFailure(
        IActionResult result,
        Mock<ILoggingBroker<ProcessController>> logging,
        Exception failure,
        string outcome)
    {
        int? statusCode = Assert.IsAssignableFrom<IStatusCodeActionResult>(result).StatusCode;

        if (outcome == "Validation")
        {
            Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
            Assert.Equal(failure.Message, Assert.IsType<BadRequestObjectResult>(result).Value);
            VerifySingleLog(logging, nameof(ILoggingBroker<ProcessController>.LogWarning));
        }
        else if (outcome == "Authentication")
        {
            Assert.Equal(StatusCodes.Status401Unauthorized, statusCode);
            Assert.Equal(failure.Message, Assert.IsType<UnauthorizedObjectResult>(result).Value);
            VerifySingleLog(logging, nameof(ILoggingBroker<ProcessController>.LogWarning));
        }
        else if (outcome == "Authorization")
        {
            Assert.Equal(StatusCodes.Status403Forbidden, statusCode);
            VerifySingleLog(logging, nameof(ILoggingBroker<ProcessController>.LogWarning));
        }
        else
        {
            Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
            Assert.IsNotType<ObjectResult>(result);
            VerifySingleLog(logging, nameof(ILoggingBroker<ProcessController>.LogError));
        }
    }

    static void VerifySingleLog(
        Mock<ILoggingBroker<ProcessController>> logging,
        string methodName)
    {
        Assert.Equal(1, logging.Invocations.Count(invocation => invocation.Method.Name == methodName));
        Assert.Equal(1, logging.Invocations.Count);
    }

    class DependencyProxy : DispatchProxy
    {
        Exception failure;

        public static object Create(Type interfaceType, Exception failure)
        {
            object proxy = Create(interfaceType, typeof(DependencyProxy));
            ((DependencyProxy)proxy).failure = failure;
            return proxy;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] args) => throw failure;
    }
}