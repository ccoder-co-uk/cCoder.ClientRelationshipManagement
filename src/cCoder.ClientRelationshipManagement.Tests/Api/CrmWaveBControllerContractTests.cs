// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;
using System.Security;
using System.Security.Authentication;
using cCoder.ClientRelationshipManagement.Api.Controllers;
using cCoder.ClientRelationshipManagement.Runtime.Brokers.Loggings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Moq;
using Xunit;

namespace cCoder.ClientRelationshipManagement.Tests.Api;

public sealed class CrmWaveBControllerContractTests
{
    static readonly Type[] ControllerTypes =
    [
        typeof(ProcessInstancesController),
        typeof(ProcessStepsController),
        typeof(ProcessTasksController),
        typeof(ProcessTransitionsController),
        typeof(ProcessStepTaskAttemptsController),
        typeof(ProcessStepTaskRunsController),
        typeof(ProcessStepTasksController),
        typeof(ActivitiesController),
        typeof(SourcesController),
        typeof(EmailsController)
    ];

    public static IEnumerable<object[]> FailureCases()
    {
        foreach (Type controllerType in ControllerTypes)
        {
            foreach (MethodInfo action in Actions(controllerType))
            {
                yield return [controllerType, ActionId(action), "Validation"];
                yield return [controllerType, ActionId(action), "Authentication"];
                yield return [controllerType, ActionId(action), "Authorization"];
                yield return [controllerType, ActionId(action), "Unexpected"];
            }
        }
    }

    public static IEnumerable<object[]> SuccessCases() =>
        ControllerTypes.SelectMany(controllerType => Actions(controllerType)
            .Select(action => new object[] { controllerType, ActionId(action) }));

    public static IEnumerable<object[]> DirectRejectionCases() =>
        ControllerTypes.Select(controllerType => new object[] { controllerType });

    [Fact]
    public void Inventory_ShouldContainEveryWaveBApiAction()
    {
        Assert.Equal(expected: 60, actual: ControllerTypes.Sum(type => Actions(type).Length));
        Assert.DoesNotContain(typeof(ProcessDefinitionsController), ControllerTypes);
    }

    [Theory]
    [MemberData(nameof(FailureCases))]
    public async Task Action_WhenDependencyFails_ShouldApplyFailureContract(
        Type controllerType,
        string actionId,
        string outcome)
    {
        // Given
        Exception failure = Failure(outcome);
        ControllerFixture fixture = CreateController(controllerType, failure, returnEmptyCollections: false);
        MethodInfo action = Actions(controllerType).Single(method => ActionId(method) == actionId);

        // When
        IActionResult result = await Invoke(action, fixture.Controller, Arguments(action));

        // Then
        AssertFailure(result, fixture.Logging, failure, outcome);
    }

    [Theory]
    [MemberData(nameof(SuccessCases))]
    public async Task Action_WhenDependencySucceeds_ShouldReturnSuccessAndLogInformation(
        Type controllerType,
        string actionId)
    {
        // Given
        ControllerFixture fixture = CreateController(controllerType, failure: null, returnEmptyCollections: false);
        MethodInfo action = Actions(controllerType).Single(method => ActionId(method) == actionId);

        // When
        IActionResult result = await Invoke(action, fixture.Controller, Arguments(action));

        // Then
        int? statusCode = Assert.IsAssignableFrom<IStatusCodeActionResult>(result).StatusCode;
        Assert.True(statusCode is null or >= 200 and < 300, result.GetType().Name);
        Assert.Equal(1, InvocationCount(fixture.Logging, nameof(ILoggingBroker<object>.LogInformation)));
        Assert.Equal(0, InvocationCount(fixture.Logging, nameof(ILoggingBroker<object>.LogWarning)));
        Assert.Equal(0, InvocationCount(fixture.Logging, nameof(ILoggingBroker<object>.LogError)));
    }

    [Theory]
    [MemberData(nameof(DirectRejectionCases))]
    public async Task Put_WhenEntityDoesNotExist_ShouldReturnNotFoundAndOnlyLogWarning(Type controllerType)
    {
        // Given
        ControllerFixture fixture = CreateController(controllerType, failure: null, returnEmptyCollections: true);
        MethodInfo action = Actions(controllerType).Single(method => method.Name == "Put");

        // When
        IActionResult result = await Invoke(action, fixture.Controller, Arguments(action));

        // Then
        Assert.Equal(StatusCodes.Status404NotFound,
            Assert.IsAssignableFrom<IStatusCodeActionResult>(result).StatusCode);
        Assert.Equal(1, InvocationCount(fixture.Logging, nameof(ILoggingBroker<object>.LogWarning)));
        Assert.Equal(0, InvocationCount(fixture.Logging, nameof(ILoggingBroker<object>.LogInformation)));
        Assert.Equal(0, InvocationCount(fixture.Logging, nameof(ILoggingBroker<object>.LogError)));
    }

    static ControllerFixture CreateController(
        Type controllerType,
        Exception failure,
        bool returnEmptyCollections)
    {
        ConstructorInfo constructor = controllerType.GetConstructors().Single();
        ParameterInfo loggingParameter = constructor.GetParameters()
            .Single(parameter => parameter.ParameterType.IsGenericType
                && parameter.ParameterType.GetGenericTypeDefinition() == typeof(ILoggingBroker<>));
        Mock logging = (Mock)Activator.CreateInstance(
            typeof(Mock<>).MakeGenericType(loggingParameter.ParameterType))!;
        object[] dependencies = constructor.GetParameters()
            .Select(parameter => parameter == loggingParameter
                ? logging.Object
                : DependencyProxy.Create(parameter.ParameterType, failure, returnEmptyCollections))
            .ToArray();

        return new ControllerFixture(
            Controller: (ControllerBase)constructor.Invoke(dependencies),
            Logging: logging);
    }

    static MethodInfo[] Actions(Type controllerType) => controllerType
        .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
        .Where(method => typeof(IActionResult).IsAssignableFrom(method.ReturnType)
            || method.ReturnType == typeof(Task<IActionResult>)
            || method.ReturnType == typeof(ValueTask<IActionResult>))
        .OrderBy(method => method.MetadataToken)
        .ToArray();

    static string ActionId(MethodInfo method) =>
        $"{method.Name}({string.Join(',', method.GetParameters().Select(parameter => parameter.ParameterType.Name))})";

    static object[] Arguments(MethodInfo action) => action.GetParameters()
        .Select(parameter => parameter.ParameterType == typeof(CancellationToken)
            ? CancellationToken.None
            : parameter.ParameterType.IsValueType
                ? Activator.CreateInstance(parameter.ParameterType)
                : Activator.CreateInstance(parameter.ParameterType))
        .ToArray();

    static async Task<IActionResult> Invoke(MethodInfo action, ControllerBase controller, object[] arguments)
    {
        object invocation = action.Invoke(controller, arguments);

        return invocation switch
        {
            IActionResult result => result,
            Task<IActionResult> task => await task,
            ValueTask<IActionResult> task => await task,
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

    static void AssertFailure(IActionResult result, Mock logging, Exception failure, string outcome)
    {
        int? statusCode = Assert.IsAssignableFrom<IStatusCodeActionResult>(result).StatusCode;

        if (outcome == "Validation")
        {
            Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
            Assert.Equal(failure.Message, Assert.IsType<BadRequestObjectResult>(result).Value);
            Assert.Equal(1, InvocationCount(logging, nameof(ILoggingBroker<object>.LogWarning)));
        }
        else if (outcome == "Authentication")
        {
            Assert.Equal(StatusCodes.Status401Unauthorized, statusCode);
            Assert.Equal(failure.Message, Assert.IsType<UnauthorizedObjectResult>(result).Value);
            Assert.Equal(1, InvocationCount(logging, nameof(ILoggingBroker<object>.LogWarning)));
        }
        else if (outcome == "Authorization")
        {
            Assert.Equal(StatusCodes.Status403Forbidden, statusCode);
            Assert.Equal(1, InvocationCount(logging, nameof(ILoggingBroker<object>.LogWarning)));
        }
        else
        {
            Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
            Assert.IsNotType<ObjectResult>(result);
            Assert.Equal(1, InvocationCount(logging, nameof(ILoggingBroker<object>.LogError)));
        }

        Assert.Equal(0, InvocationCount(logging, nameof(ILoggingBroker<object>.LogInformation)));
    }

    static int InvocationCount(Mock logging, string methodName) =>
        logging.Invocations.Count(invocation => invocation.Method.Name == methodName);

    sealed record ControllerFixture(ControllerBase Controller, Mock Logging);

    class DependencyProxy : DispatchProxy
    {
        Exception failure;
        bool returnEmptyCollections;

        public static object Create(Type interfaceType, Exception failure, bool returnEmptyCollections)
        {
            object proxy = Create(interfaceType, typeof(DependencyProxy));
            DependencyProxy dependency = (DependencyProxy)proxy;
            dependency.failure = failure;
            dependency.returnEmptyCollections = returnEmptyCollections;
            return proxy;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            if (failure is not null)
            {
                throw failure;
            }

            return SuccessValue(targetMethod.ReturnType, returnEmptyCollections);
        }

        static object SuccessValue(Type type, bool returnEmptyCollections)
        {
            if (type == typeof(Task)) return Task.CompletedTask;
            if (type == typeof(ValueTask)) return ValueTask.CompletedTask;

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            {
                Type resultType = type.GetGenericArguments()[0];
                return typeof(Task).GetMethod(nameof(Task.FromResult))!
                    .MakeGenericMethod(resultType)
                    .Invoke(null, [SuccessValue(resultType, returnEmptyCollections)]);
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueTask<>))
            {
                Type resultType = type.GetGenericArguments()[0];
                return Activator.CreateInstance(type, SuccessValue(resultType, returnEmptyCollections));
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IQueryable<>))
            {
                Type entityType = type.GetGenericArguments()[0];
                Array values = Array.CreateInstance(entityType, returnEmptyCollections ? 0 : 1);

                if (!returnEmptyCollections)
                {
                    values.SetValue(Activator.CreateInstance(entityType), 0);
                }

                return Activator.CreateInstance(
                    typeof(TestAsyncEnumerable<>).MakeGenericType(entityType),
                    values);
            }

            return type.IsValueType ? Activator.CreateInstance(type) : Activator.CreateInstance(type);
        }
    }

    sealed class TestAsyncEnumerable<T>
        : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public TestAsyncEnumerable(IEnumerable<T> enumerable) : base(enumerable) { }

        public TestAsyncEnumerable(Expression expression) : base(expression) { }

        IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
            new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
    }

    sealed class TestAsyncQueryProvider<T>(IQueryProvider inner) : IAsyncQueryProvider
    {
        public IQueryable CreateQuery(Expression expression) => new TestAsyncEnumerable<T>(expression);

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression) =>
            new TestAsyncEnumerable<TElement>(expression);

        public object Execute(Expression expression) => inner.Execute(expression);

        public TResult Execute<TResult>(Expression expression) => inner.Execute<TResult>(expression);

        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        {
            Type resultType = typeof(TResult).GetGenericArguments()[0];
            object result = typeof(IQueryProvider)
                .GetMethods()
                .Single(method => method.Name == nameof(IQueryProvider.Execute)
                    && method.IsGenericMethod)
                .MakeGenericMethod(resultType)
                .Invoke(inner, [expression]);

            return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(resultType)
                .Invoke(null, [result]);
        }
    }

    sealed class TestAsyncEnumerator<T>(IEnumerator<T> inner) : IAsyncEnumerator<T>
    {
        public T Current => inner.Current;

        public ValueTask DisposeAsync()
        {
            inner.Dispose();
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(inner.MoveNext());
    }
}