using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Linq.Expressions;
using System.Security;
using System.Security.Authentication;
using System.Security.Claims;
using System.Runtime.CompilerServices;
using cCoder.ClientRelationshipManagement.Api.Controllers;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using cCoder.ClientRelationshipManagement.Runtime.Brokers.Loggings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.EntityFrameworkCore.Query;
using Xunit;
using WebAccountController = ClientRelationshipManagement.Web.Controllers.AccountController;

namespace cCoder.ClientRelationshipManagement.Tests.Api;

public sealed class CrmControllerDirectContractTests
{
    static readonly string[] Names = ["AccountController", "AdminController", "AgentAutomationSettingsController", "AgentMessageEntriesController", "AgentMessagesController", "AgentRunsController", "AgentWorkflowController", "DocumentationController", "HomeController", "ImportsController", "ImportsApiController", "ImportLinksController", "HandoffPacksController", "MailboxMessageRecordsController", "MaterialsController"];
    public static IEnumerable<object[]> AllActions => Actions().Select(action => new object[] { action });
    public static IEnumerable<object[]> ApiActions => Actions().Where(action => action.ControllerType.Namespace == "cCoder.ClientRelationshipManagement.Api.Controllers").Select(action => new object[] { action });
    public static IEnumerable<object[]> ApiMissingEntityActions => Actions().Where(action => action.ControllerType.Namespace == "cCoder.ClientRelationshipManagement.Api.Controllers" && new[] { "Put", "Patch", "Delete" }.Contains(action.Method.Name) && !(action.ControllerType.Name == "AgentMessagesController" && action.Method.Name == "Put")).Select(action => new object[] { action });
    public static IEnumerable<object[]> DependencyActions => Actions().Where(action => action.HasDependency).Select(action => new object[] { action });

    [Theory, MemberData(nameof(ApiActions))]
    public async Task ApiAction_WhenDependenciesComplete_ShouldReturnSuccessAndLogInformation(ActionCase action)
    {
        Outcome outcome = await Invoke(action, null);
        int status = (outcome.Result as IStatusCodeActionResult)?.StatusCode ?? StatusCodes.Status200OK;
        Assert.False(status == 500, outcome.Logging.LastError?.ToString());
        Assert.InRange(status, 200, 399);
        Assert.Equal(1, outcome.Logging.InformationCount);
        Assert.Equal(0, outcome.Logging.WarningCount);
        Assert.Equal(0, outcome.Logging.ErrorCount);
    }

    [Theory, MemberData(nameof(ApiMissingEntityActions))]
    public async Task ApiMutation_WhenEntityDoesNotExist_ShouldReturn404AndWarn(ActionCase action)
    {
        Outcome outcome = await Invoke(action, null, emptyQueries: true);
        Assert.Equal(404, (outcome.Result as IStatusCodeActionResult)?.StatusCode);
        Assert.Equal(0, outcome.Logging.InformationCount);
        Assert.Equal(1, outcome.Logging.WarningCount);
        Assert.Equal(0, outcome.Logging.ErrorCount);
    }

    [Theory, MemberData(nameof(AllActions))]
    public void Action_ShouldOwnEveryClassifiedCatch(ActionCase action)
    {
        MethodInfo implementation = action.Method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic)! ?? action.Method;
        Type[] catches = implementation.GetMethodBody()!.ExceptionHandlingClauses
            .Where(clause => clause.Flags == ExceptionHandlingClauseOptions.Clause)
            .Select(clause => clause.CatchType!).ToArray();
        Assert.Contains(typeof(ValidationException), catches);
        Assert.Contains(typeof(ArgumentException), catches);
        Assert.Contains(typeof(AuthenticationException), catches);
        Assert.Contains(typeof(UnauthorizedAccessException), catches);
        Assert.Contains(typeof(SecurityException), catches);
        Assert.Contains(typeof(Exception), catches);
    }

    [Fact]
    public async Task DocumentationIndex_WhenInvokedDirectly_ShouldReturnViewAndLogInformation()
    {
        ActionCase action = Actions().Single(candidate => candidate.ControllerType.Name == "DocumentationController" && candidate.Method.Name == "Index");
        Outcome outcome = await Invoke(action, null);
        Assert.IsType<ViewResult>(outcome.Result);
        Assert.Equal(1, outcome.Logging.InformationCount);
        Assert.Equal(0, outcome.Logging.WarningCount);
        Assert.Equal(0, outcome.Logging.ErrorCount);
    }

    [Theory, MemberData(nameof(DependencyActions))]
    public async Task Action_WhenDependencyThrowsValidation_ShouldReturn400AndWarn(ActionCase action) =>
        await AssertFailure(action, new ValidationException("Safe reason."), 400, "Safe reason.", 0, 1, 0);

    [Theory, MemberData(nameof(DependencyActions))]
    public async Task Action_WhenDependencyThrowsArgument_ShouldReturnGeneric400AndWarn(ActionCase action) =>
        await AssertFailure(action, new ArgumentException("Sensitive."), 400, "The request is invalid.", 0, 1, 0);

    [Theory, MemberData(nameof(DependencyActions))]
    public async Task Action_WhenDependencyThrowsAuthentication_ShouldReturn401AndWarn(ActionCase action) =>
        await AssertFailure(action, new AuthenticationException("Safe reason."), 401, "Safe reason.", 0, 1, 0);

    [Theory, MemberData(nameof(DependencyActions))]
    public async Task Action_WhenDependencyThrowsUnauthorizedAccess_ShouldReturn403AndWarn(ActionCase action) =>
        await AssertFailure(action, new UnauthorizedAccessException("Sensitive."), 403, null, 0, 1, 0);

    [Theory, MemberData(nameof(DependencyActions))]
    public async Task Action_WhenDependencyThrowsSecurity_ShouldReturn403AndWarn(ActionCase action) =>
        await AssertFailure(action, new SecurityException("Sensitive."), 403, null, 0, 1, 0);

    [Theory, MemberData(nameof(DependencyActions))]
    public async Task Action_WhenDependencyThrowsUnexpected_ShouldReturn500WithoutDetailAndLogError(ActionCase action) =>
        await AssertFailure(action, new InvalidOperationException("Sensitive."), 500, null, 0, 0, 1);

    static async Task AssertFailure(ActionCase action, Exception failure, int status, string payload, int information, int warning, int error)
    {
        Outcome outcome = await Invoke(action, failure);
        Assert.Equal(status, (outcome.Result as IStatusCodeActionResult)?.StatusCode);
        if (payload is not null) Assert.Equal(payload, Assert.IsType<ObjectResult>(outcome.Result).Value);
        if (status == 500) Assert.False(outcome.Result is ObjectResult);
        Assert.Equal(information, outcome.Logging.InformationCount);
        Assert.Equal(warning, outcome.Logging.WarningCount);
        Assert.Equal(error, outcome.Logging.ErrorCount);
    }

    static IEnumerable<ActionCase> Actions()
    {
        Assembly[] assemblies = [typeof(MaterialsController).Assembly, typeof(WebAccountController).Assembly];
        return assemblies.SelectMany(assembly => assembly.GetTypes()).Where(type => Names.Contains(type.Name))
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => method.ReturnType.ToString().Contains("IActionResult"))
                .Select(method => new ActionCase(type, method, type.GetConstructors().Single().GetParameters().Any(parameter => !IsLogging(parameter.ParameterType)))))
            .ToArray();
    }

    static async Task<Outcome> Invoke(ActionCase action, Exception failure, bool emptyQueries = false)
    {
        ParameterInfo[] constructorParameters = action.ControllerType.GetConstructors().Single().GetParameters();
        Type loggingType = constructorParameters.Single(parameter => IsLogging(parameter.ParameterType)).ParameterType;
        ContractProxy logging = CreateProxy(loggingType, null);
        object[] dependencies = constructorParameters
            .Select(parameter => IsLogging(parameter.ParameterType) ? logging : CreateProxy(parameter.ParameterType, failure, emptyQueries)).ToArray();
        ControllerBase controller = (ControllerBase)Activator.CreateInstance(action.ControllerType, dependencies)!;
        DefaultHttpContext httpContext = new();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "test-user"),
            new Claim(ClaimTypes.Name, "test-user"),
            new Claim(ClaimTypes.Email, "test@example.com")
        ], "ContractTest"));
        httpContext.Request.Headers.Authorization = "Bearer contract-test-token";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        object[] arguments = action.Method.GetParameters().Select(parameter => Value(parameter.ParameterType)).ToArray();
        object invocation = action.Method.Invoke(controller, arguments)!;
        IActionResult result = await Resolve(invocation);
        return new(result, logging);
    }

    static bool IsLogging(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ILoggingBroker<>);
    static ContractProxy CreateProxy(Type type, Exception failure, bool emptyQueries = false)
    {
        Type interfaceType = IsLogging(type) ? type : type;
        ContractProxy proxy = (ContractProxy)DispatchProxy.Create(interfaceType, typeof(ContractProxy));
        proxy.Failure = failure;
        proxy.EmptyQueries = emptyQueries;
        return proxy;
    }

    static object Value(Type type)
    {
        if (type == typeof(ODataActionParameters))
            return new ODataActionParameters
            {
                ["body"] = "value",
                ["state"] = default(AgentMessageState),
                ["responseNotes"] = "value",
                ["auditNote"] = "value"
            };
        if (type == typeof(string)) return "value";
        if (type == typeof(CancellationToken)) return CancellationToken.None;
        if (type == typeof(Task)) return Task.CompletedTask;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(type.GetGenericArguments()[0])
                .Invoke(null, [Value(type.GetGenericArguments()[0])])!;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueTask<>))
            return Activator.CreateInstance(type, Value(type.GetGenericArguments()[0]))!;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IQueryable<>))
        {
            Type itemType = type.GetGenericArguments()[0];
            Array items = Array.CreateInstance(itemType, 1);
            items.SetValue(Value(itemType), 0);
            return Activator.CreateInstance(typeof(TestAsyncEnumerable<>).MakeGenericType(itemType), items)!;
        }
        if (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(IEnumerable<>) || type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>) || type.GetGenericTypeDefinition() == typeof(IList<>)))
        {
            Type itemType = type.GetGenericArguments()[0];
            Array items = Array.CreateInstance(itemType, 1);
            items.SetValue(Value(itemType), 0);
            return items;
        }
        if (type.IsValueType) return Activator.CreateInstance(type)!;
        if (type.IsArray) return Array.CreateInstance(type.GetElementType()!, 0);
        try { return Activator.CreateInstance(type)!; } catch { return null; }
    }

    static async Task<IActionResult> Resolve(object invocation)
    {
        if (invocation is IActionResult result) return result;
        Task task = invocation is Task direct ? direct : (Task)invocation.GetType().GetMethod("AsTask")!.Invoke(invocation, null)!;
        await task;
        return (IActionResult)task.GetType().GetProperty("Result")!.GetValue(task)!;
    }

    public sealed record ActionCase(Type ControllerType, MethodInfo Method, bool HasDependency);
    sealed record Outcome(IActionResult Result, ContractProxy Logging);

    public class ContractProxy : DispatchProxy
    {
        public Exception Failure { get; set; }
        public bool EmptyQueries { get; set; }
        public int InformationCount { get; private set; }
        public int WarningCount { get; private set; }
        public int ErrorCount { get; private set; }
        public Exception LastError { get; private set; }
        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            if (targetMethod.Name == "LogInformation") { InformationCount++; return null; }
            if (targetMethod.Name == "LogWarning") { WarningCount++; return null; }
            if (targetMethod.Name == "LogError") { ErrorCount++; LastError = args?.OfType<Exception>().FirstOrDefault(); return null; }
            if (Failure is not null) throw Failure;
            if (EmptyQueries && targetMethod.ReturnType.IsGenericType && targetMethod.ReturnType.GetGenericTypeDefinition() == typeof(IQueryable<>))
            {
                Type itemType = targetMethod.ReturnType.GetGenericArguments()[0];
                return Activator.CreateInstance(typeof(TestAsyncEnumerable<>).MakeGenericType(itemType), Array.CreateInstance(itemType, 0));
            }
            return Value(targetMethod.ReturnType);
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
            object result = typeof(IQueryProvider).GetMethods().Single(method => method.Name == nameof(IQueryProvider.Execute) && method.IsGenericMethod)
                .MakeGenericMethod(resultType).Invoke(inner, [expression])!;
            return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(resultType).Invoke(null, [result])!;
        }
    }

    sealed class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public TestAsyncEnumerable(IEnumerable<T> enumerable) : base(enumerable) { }
        public TestAsyncEnumerable(Expression expression) : base(expression) { }
        IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
            new TestAsyncEnumerator<T>(Enumerable.AsEnumerable(this).GetEnumerator());
    }

    sealed class TestAsyncEnumerator<T>(IEnumerator<T> inner) : IAsyncEnumerator<T>
    {
        public T Current => inner.Current;
        public ValueTask DisposeAsync() { inner.Dispose(); return ValueTask.CompletedTask; }
        public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(inner.MoveNext());
    }
}
