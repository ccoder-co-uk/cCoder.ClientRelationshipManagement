// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;
using System.Security;
using System.Security.Authentication;
using cCoder.ClientRelationshipManagement.Runtime.Brokers.Loggings;
using ClientRelationshipManagement.Web.Controllers;
using ClientRelationshipManagement.Web.Models.Clients;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Query;
using Xunit;

namespace cCoder.ClientRelationshipManagement.Tests.Api;

public sealed partial class WebEntityControllerContractWaveCTests
{
    static readonly Type[] ControllerTypes = [typeof(ClientsController), typeof(CompaniesController)];

    public static TheoryData<string, string, string> FailureCases
    {
        get
        {
            TheoryData<string, string, string> cases = new();

            foreach (Type controllerType in ControllerTypes)
            {
                foreach (MethodInfo action in Actions(controllerType).Where(action => action.Name != "Create"))
                {
                    foreach (string outcome in new[] { "Validation", "Argument", "Authentication", "Security", "Unexpected" })
                        cases.Add(controllerType.AssemblyQualifiedName!, action.MetadataToken.ToString(), outcome);
                }
            }

            return cases;
        }
    }

    public static TheoryData<string, string, string> CreateFailureCases
    {
        get
        {
            TheoryData<string, string, string> cases = new();

            foreach (MethodInfo action in Actions(typeof(ClientsController)).Where(action => action.Name == "Create"))
            {
                foreach (string outcome in new[] { "Validation", "Argument", "Authentication", "Security", "Unexpected" })
                    cases.Add(typeof(ClientsController).AssemblyQualifiedName!, action.MetadataToken.ToString(), outcome);
            }

            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(FailureCases))]
    public async Task Action_WhenAuthenticationContextFails_ShouldApplyResponseAndLoggingContract(
        string controllerTypeName,
        string actionToken,
        string outcome)
    {
        (object controller, MethodInfo action, ContractProxy logging) = CreateController(
            controllerTypeName,
            actionToken,
            outcome,
            serviceOutcome: "Success");

        IActionResult result = await InvokeAsync(controller, action, action.GetParameters().Select(CreateArgument).ToArray());
        int expected = outcome switch
        {
            "Validation" => StatusCodes.Status400BadRequest,
            "Argument" => StatusCodes.Status400BadRequest,
            "Authentication" => StatusCodes.Status401Unauthorized,
            "Security" => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };

        Assert.Equal(expected, GetStatusCode(result));

        if (outcome == "Authentication")
            Assert.Equal("Authentication is required.", Assert.IsType<UnauthorizedObjectResult>(result).Value);
        else if (outcome == "Argument")
        {
            BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("The request is invalid.", badRequest.Value);
            Assert.DoesNotContain("Sensitive argument detail", badRequest.Value?.ToString());
        }
        else if (outcome == "Unexpected")
            Assert.IsType<StatusCodeResult>(result);

        AssertLogCounts(
            logging,
            information: 0,
            warning: outcome == "Unexpected" ? 0 : 1,
            error: outcome == "Unexpected" ? 1 : 0);
    }

    [Theory]
    [MemberData(nameof(CreateFailureCases))]
    public void ClientsCreate_WhenCompletionFails_ShouldApplyResponseAndLoggingContract(
        string controllerTypeName,
        string actionToken,
        string outcome)
    {
        (object controller, MethodInfo action, ContractProxy logging) = CreateController(
            controllerTypeName,
            actionToken,
            authOutcome: "Success",
            serviceOutcome: "Success",
            loggingOutcome: $"Logging{outcome}");

        IActionResult result = (IActionResult)action.Invoke(
            controller,
            action.GetParameters().Select(CreateArgument).ToArray())!;
        int expected = outcome switch
        {
            "Validation" => StatusCodes.Status400BadRequest,
            "Argument" => StatusCodes.Status400BadRequest,
            "Authentication" => StatusCodes.Status401Unauthorized,
            "Security" => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };

        Assert.Equal(expected, GetStatusCode(result));

        if (outcome == "Authentication")
            Assert.Equal("Authentication is required.", Assert.IsType<UnauthorizedObjectResult>(result).Value);
        else if (outcome == "Argument")
        {
            BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("The request is invalid.", badRequest.Value);
            Assert.DoesNotContain("Sensitive argument detail", badRequest.Value?.ToString());
        }
        else if (outcome == "Unexpected")
            Assert.IsType<StatusCodeResult>(result);

        AssertLogCounts(
            logging,
            information: 1,
            warning: outcome == "Unexpected" ? 0 : 1,
            error: outcome == "Unexpected" ? 1 : 0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ClientsCreate_ShouldPreserveRedirectAndLogInformation(int overload)
    {
        MethodInfo action = Actions(typeof(ClientsController))
            .Where(method => method.Name == "Create")
            .OrderBy(method => method.GetParameters().Length)
            .ElementAt(overload);
        (object controller, _, ContractProxy logging) = CreateController(
            typeof(ClientsController).AssemblyQualifiedName!,
            action.MetadataToken.ToString(),
            authOutcome: "Success",
            serviceOutcome: "Success");

        IActionResult result = (IActionResult)action.Invoke(controller, action.GetParameters().Select(CreateArgument).ToArray())!;

        Assert.IsType<RedirectToActionResult>(result);
        AssertLogCounts(logging, information: 1, warning: 0, error: 0);
    }

    [Fact]
    public async Task ClientsEdit_WhenIdentifierMissing_ShouldReturnBadRequestAndLogWarning()
    {
        MethodInfo action = Actions(typeof(ClientsController)).Single(method => method.Name == "Edit"
            && method.GetParameters().Single().ParameterType == typeof(ClientEditorViewModel));
        (object controller, _, ContractProxy logging) = CreateController(
            typeof(ClientsController).AssemblyQualifiedName!,
            action.MetadataToken.ToString(),
            authOutcome: "Success",
            serviceOutcome: "Success");

        IActionResult result = await InvokeAsync(controller, action, [new ClientEditorViewModel()]);

        Assert.Equal(StatusCodes.Status400BadRequest, GetStatusCode(result));
        AssertLogCounts(logging, information: 0, warning: 1, error: 0);
    }

    [Fact]
    public async Task CompaniesDetails_WhenCompanyMissing_ShouldReturnNotFoundAndLogWarning()
    {
        MethodInfo action = Actions(typeof(CompaniesController)).Single(method => method.Name == "Details");
        (object controller, _, ContractProxy logging) = CreateController(
            typeof(CompaniesController).AssemblyQualifiedName!,
            action.MetadataToken.ToString(),
            authOutcome: "Success",
            serviceOutcome: "Missing");

        IActionResult result = await InvokeAsync(controller, action, [Guid.Empty, CancellationToken.None]);

        Assert.Equal(StatusCodes.Status404NotFound, GetStatusCode(result));
        AssertLogCounts(logging, information: 0, warning: 1, error: 0);
    }

    static (object Controller, MethodInfo Action, ContractProxy Logging) CreateController(
        string controllerTypeName,
        string actionToken,
        string authOutcome,
        string serviceOutcome,
        string loggingOutcome = "Logging")
    {
        Type controllerType = Type.GetType(controllerTypeName, throwOnError: true)!;
        MethodInfo action = (MethodInfo)controllerType.Module.ResolveMethod(int.Parse(actionToken))!;
        ConstructorInfo constructor = controllerType.GetConstructors().Single();
        ContractProxy logging = null!;
        object[] dependencies = constructor.GetParameters().Select(parameter =>
        {
            string outcome = parameter.ParameterType.Name switch
            {
                "ISSOAuthInfo" => authOutcome,
                _ when parameter.ParameterType.IsGenericType
                    && parameter.ParameterType.GetGenericTypeDefinition() == typeof(ILoggingBroker<>) => loggingOutcome,
                _ => serviceOutcome
            };
            ContractProxy proxy = CreateProxy(parameter.ParameterType, outcome);
            if (outcome.StartsWith("Logging", StringComparison.Ordinal)) logging = proxy;
            return proxy.Proxy;
        }).ToArray();

        return (constructor.Invoke(dependencies), action, logging);
    }

    static IEnumerable<MethodInfo> Actions(Type controllerType) => controllerType
        .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
        .Where(method => method.ReturnType == typeof(IActionResult)
            || method.ReturnType == typeof(Task<IActionResult>));

    static ContractProxy CreateProxy(Type interfaceType, string outcome)
    {
        object proxy = DispatchProxy.Create(interfaceType, typeof(ContractProxy));
        ContractProxy contract = (ContractProxy)proxy;
        contract.Proxy = proxy;
        contract.Outcome = outcome;
        return contract;
    }

    static object? CreateArgument(ParameterInfo parameter)
    {
        Type type = parameter.ParameterType;
        if (type == typeof(string)) return null;
        if (type == typeof(Guid)) return Guid.Empty;
        if (type == typeof(CancellationToken)) return CancellationToken.None;
        if (type.IsValueType) return Activator.CreateInstance(type);
        return Activator.CreateInstance(type);
    }

    static async Task<IActionResult> InvokeAsync(object controller, MethodInfo action, object?[] arguments)
    {
        object? invocation = action.Invoke(controller, arguments);
        return invocation is Task<IActionResult> task ? await task : (IActionResult)invocation!;
    }

    static int GetStatusCode(IActionResult result) => result switch
    {
        ObjectResult objectResult => objectResult.StatusCode ?? StatusCodes.Status200OK,
        StatusCodeResult statusCodeResult => statusCodeResult.StatusCode,
        _ => StatusCodes.Status200OK
    };

    static void AssertLogCounts(ContractProxy logging, int information, int warning, int error)
    {
        Assert.Equal(information, logging.Calls.Count(call => call == "LogInformation"));
        Assert.Equal(warning, logging.Calls.Count(call => call == "LogWarning"));
        Assert.Equal(error, logging.Calls.Count(call => call == "LogError"));
    }

    public class ContractProxy : DispatchProxy
    {
        public object Proxy { get; set; } = null!;
        public string Outcome { get; set; } = string.Empty;
        public List<string> Calls { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Calls.Add(targetMethod!.Name);
            if (targetMethod.Name.StartsWith("Log", StringComparison.Ordinal))
            {
                if (targetMethod.Name == "LogInformation")
                {
                    if (Outcome == "LoggingValidation") throw new ValidationException("Invalid request.");
                    if (Outcome == "LoggingArgument") throw new ArgumentException("Sensitive argument detail must not leave the server.");
                    if (Outcome == "LoggingAuthentication") throw new AuthenticationException("Authentication failed.");
                    if (Outcome == "LoggingSecurity") throw new SecurityException("Permission denied.");
                    if (Outcome == "LoggingUnexpected") throw new InvalidOperationException("Dependency failed.");
                }

                return null;
            }
            if (targetMethod.Name.StartsWith("get_", StringComparison.Ordinal))
            {
                if (Outcome == "Validation") throw new ValidationException("Invalid request.");
                if (Outcome == "Argument") throw new ArgumentException("Sensitive argument detail must not leave the server.");
                if (Outcome == "Authentication") throw new AuthenticationException("Authentication failed.");
                if (Outcome == "Security") throw new SecurityException("Permission denied.");
                if (Outcome == "Unexpected") throw new InvalidOperationException("Dependency failed.");
                if (targetMethod.ReturnType == typeof(string)) return "test-user";
                if (targetMethod.ReturnType == typeof(string[])) return new[] { "default" };
            }

            return CreateReturnValue(targetMethod.ReturnType, Outcome == "Missing");
        }

        static object? CreateReturnValue(Type returnType, bool empty)
        {
            if (returnType == typeof(void)) return null;
            if (returnType == typeof(Task)) return Task.CompletedTask;
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(IQueryable<>))
            {
                Type elementType = returnType.GetGenericArguments()[0];
                Array values = Array.CreateInstance(elementType, empty ? 0 : 1);
                if (!empty) values.SetValue(Activator.CreateInstance(elementType), 0);
                return Activator.CreateInstance(typeof(TestAsyncEnumerable<>).MakeGenericType(elementType), values)!;
            }

            return returnType.IsValueType ? Activator.CreateInstance(returnType) : null;
        }
    }

    sealed class TestAsyncQueryProvider<TEntity>(IQueryProvider inner) : IAsyncQueryProvider
    {
        public IQueryable CreateQuery(Expression expression) => new TestAsyncEnumerable<TEntity>(expression);
        public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new TestAsyncEnumerable<TElement>(expression);
        public object Execute(Expression expression) => inner.Execute(expression)!;
        public TResult Execute<TResult>(Expression expression) => inner.Execute<TResult>(expression);
        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        {
            Type resultType = typeof(TResult).GetGenericArguments()[0];
            object result = typeof(IQueryProvider).GetMethods()
                .Single(method => method.Name == nameof(IQueryProvider.Execute) && method.IsGenericMethod)
                .MakeGenericMethod(resultType).Invoke(inner, [expression])!;
            return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(resultType).Invoke(null, [result])!;
        }
    }

    sealed class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public TestAsyncEnumerable(IEnumerable<T> enumerable) : base(enumerable) { }
        public TestAsyncEnumerable(Expression expression) : base(expression) { }
        IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
            new TestAsyncEnumerator<T>(((IEnumerable<T>)this).GetEnumerator());
    }

    sealed class TestAsyncEnumerator<T>(IEnumerator<T> inner) : IAsyncEnumerator<T>
    {
        public T Current => inner.Current;
        public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(inner.MoveNext());
        public ValueTask DisposeAsync() { inner.Dispose(); return ValueTask.CompletedTask; }
    }
}
