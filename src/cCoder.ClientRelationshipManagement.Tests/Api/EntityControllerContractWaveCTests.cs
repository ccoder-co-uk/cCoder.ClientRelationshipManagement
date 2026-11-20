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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.EntityFrameworkCore.Query;
using Xunit;

namespace cCoder.ClientRelationshipManagement.Tests.Api;

public sealed partial class EntityControllerContractWaveCTests
{
    static readonly Type[] ControllerTypes =
    [
        typeof(AddressesController),
        typeof(ClientAccountsController),
        typeof(CompaniesController),
        typeof(CompanyContactsController),
        typeof(CompanyEvidenceController),
        typeof(CompanyHistoryController),
        typeof(EmailRecipientsController),
        typeof(LeadContactsController),
        typeof(LeadsController),
        typeof(OpportunitiesController),
        typeof(RelationshipContactsController),
        typeof(TenantCompanyRelationshipsController)
    ];

    public static TheoryData<string, string, string> RuntimeCases
    {
        get
        {
            TheoryData<string, string, string> cases = new();

            foreach (Type controllerType in ControllerTypes)
            {
                foreach (MethodInfo action in Actions(controllerType))
                {
                    string token = action.MetadataToken.ToString(provider: null);

                    foreach (string outcome in new[] { "Success", "Validation", "Argument", "Authentication", "Security", "Unexpected" })
                        cases.Add(controllerType.AssemblyQualifiedName!, token, outcome);
                }
            }

            return cases;
        }
    }

    public static TheoryData<string, string> MissingCases
    {
        get
        {
            TheoryData<string, string> cases = new();

            foreach (Type controllerType in ControllerTypes)
            {
                foreach (MethodInfo action in Actions(controllerType).Where(action => action.Name is "Put" or "Patch" or "Delete"))
                    cases.Add(controllerType.AssemblyQualifiedName!, action.MetadataToken.ToString(provider: null));
            }

            return cases;
        }
    }

    public static TheoryData<string, string> ProtectedPatchCases
    {
        get
        {
            TheoryData<string, string> cases = new();

            foreach (Type controllerType in ControllerTypes)
            {
                MethodInfo patch = Actions(controllerType).Single(action => action.Name == "Patch");
                cases.Add(controllerType.AssemblyQualifiedName!, patch.MetadataToken.ToString(provider: null));
            }

            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(RuntimeCases))]
    public async Task Action_ShouldApplyRuntimeResponseAndLoggingContract(
        string controllerTypeName,
        string actionToken,
        string outcome)
    {
        (object controller, MethodInfo action, ContractProxy logging) = CreateController(
            controllerTypeName: controllerTypeName,
            actionToken: actionToken,
            outcome: outcome);

        IActionResult result = await InvokeAsync(
            controller: controller,
            action: action,
            arguments: action.GetParameters().Select(CreateArgument).ToArray());

        if (outcome == "Success")
        {
            Assert.InRange(GetStatusCode(result), low: 200, high: 299);
            AssertLogCounts(logging, information: 1, warning: 0, error: 0);
        }
        else if (outcome == "Validation")
        {
            Assert.Equal(StatusCodes.Status400BadRequest, GetStatusCode(result));
            AssertLogCounts(logging, information: 0, warning: 1, error: 0);
        }
        else if (outcome == "Argument")
        {
            BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("The request is invalid.", badRequest.Value);
            Assert.DoesNotContain("Sensitive argument detail", badRequest.Value?.ToString());
            AssertLogCounts(logging, information: 0, warning: 1, error: 0);
        }
        else if (outcome == "Authentication")
        {
            UnauthorizedObjectResult unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Equal("Authentication is required.", unauthorized.Value);
            AssertLogCounts(logging, information: 0, warning: 1, error: 0);
        }
        else if (outcome == "Security")
        {
            Assert.Equal(StatusCodes.Status403Forbidden, GetStatusCode(result));
            AssertLogCounts(logging, information: 0, warning: 1, error: 0);
        }
        else
        {
            Assert.Equal(
                StatusCodes.Status500InternalServerError,
                Assert.IsType<StatusCodeResult>(result).StatusCode);
            AssertLogCounts(logging, information: 0, warning: 0, error: 1);
            Assert.Contains(logging.Exceptions, exception => exception is InvalidOperationException);
        }
    }

    [Theory]
    [MemberData(nameof(MissingCases))]
    public async Task MissingEntity_ShouldReturnNotFoundAndLogOneWarning(
        string controllerTypeName,
        string actionToken)
    {
        (object controller, MethodInfo action, ContractProxy logging) = CreateController(
            controllerTypeName: controllerTypeName,
            actionToken: actionToken,
            outcome: "Missing");

        IActionResult result = await InvokeAsync(
            controller: controller,
            action: action,
            arguments: action.GetParameters().Select(CreateArgument).ToArray());

        Assert.IsType<NotFoundResult>(result);
        AssertLogCounts(logging, information: 0, warning: 1, error: 0);
    }

    [Theory]
    [MemberData(nameof(ProtectedPatchCases))]
    public async Task ProtectedPatch_ShouldReturnBadRequestAndLogOneWarning(
        string controllerTypeName,
        string actionToken)
    {
        (object controller, MethodInfo action, ContractProxy logging) = CreateController(
            controllerTypeName: controllerTypeName,
            actionToken: actionToken,
            outcome: "Success");
        object?[] arguments = action.GetParameters().Select(CreateArgument).ToArray();
        object delta = arguments.Single(argument => argument?.GetType().IsGenericType == true
            && argument.GetType().GetGenericTypeDefinition() == typeof(Delta<>))!;
        bool changed = (bool)delta.GetType().GetMethod("TrySetPropertyValue")!
            .Invoke(delta, ["Id", Guid.NewGuid()])!;

        Assert.True(changed);
        IActionResult result = await InvokeAsync(controller, action, arguments);

        Assert.Equal(StatusCodes.Status400BadRequest, GetStatusCode(result));
        AssertLogCounts(logging, information: 0, warning: 1, error: 0);
    }

    static (object Controller, MethodInfo Action, ContractProxy Logging) CreateController(
        string controllerTypeName,
        string actionToken,
        string outcome)
    {
        Type controllerType = Type.GetType(controllerTypeName, throwOnError: true)!;
        MethodInfo action = (MethodInfo)controllerType.Module.ResolveMethod(int.Parse(actionToken))!;
        ConstructorInfo constructor = controllerType.GetConstructors().Single();
        ParameterInfo[] parameters = constructor.GetParameters();
        ContractProxy service = CreateProxy(parameters[0].ParameterType, outcome);
        ContractProxy logging = CreateProxy(parameters[1].ParameterType, "Logging");
        object controller = constructor.Invoke([service.Proxy, logging.Proxy]);

        return (controller, action, logging);
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

        if (type == typeof(Guid))
            return Guid.Empty;

        if (type == typeof(CancellationToken))
            return CancellationToken.None;

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Delta<>))
            return Activator.CreateInstance(type);

        return type.IsValueType ? Activator.CreateInstance(type) : Activator.CreateInstance(type);
    }

    static async Task<IActionResult> InvokeAsync(object controller, MethodInfo action, object?[] arguments)
    {
        object? invocation = action.Invoke(controller, arguments);

        return invocation switch
        {
            IActionResult result => result,
            Task<IActionResult> task => await task,
            _ => throw new InvalidOperationException($"Unsupported action return type '{action.ReturnType}'.")
        };
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
        public List<Exception> Exceptions { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Calls.Add(targetMethod!.Name);

            if (targetMethod.Name.StartsWith("Log", StringComparison.Ordinal))
            {
                Exceptions.AddRange(args?.OfType<Exception>() ?? []);
                return null;
            }

            if (Outcome == "Validation")
                throw new ValidationException("Invalid request.");
            if (Outcome == "Argument")
                throw new ArgumentException("Sensitive argument detail must not leave the server.");
            if (Outcome == "Authentication")
                throw new AuthenticationException("Authentication failed.");
            if (Outcome == "Security")
                throw new SecurityException("Permission denied.");
            if (Outcome == "Unexpected")
                throw new InvalidOperationException("Dependency failed.");

            return CreateReturnValue(targetMethod.ReturnType, Outcome == "Missing");
        }

        static object? CreateReturnValue(Type returnType, bool empty)
        {
            if (returnType == typeof(void))
                return null;
            if (returnType == typeof(Task))
                return Task.CompletedTask;
            if (returnType == typeof(ValueTask))
                return ValueTask.CompletedTask;

            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(IQueryable<>))
            {
                Type elementType = returnType.GetGenericArguments()[0];
                Array values = Array.CreateInstance(elementType, empty ? 0 : 1);
                if (!empty)
                    values.SetValue(Activator.CreateInstance(elementType), 0);

                Type queryType = typeof(TestAsyncEnumerable<>).MakeGenericType(elementType);
                return Activator.CreateInstance(queryType, values)!;
            }

            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
            {
                Type valueType = returnType.GetGenericArguments()[0];
                object? value = empty ? null : Activator.CreateInstance(valueType);
                return Activator.CreateInstance(returnType, value);
            }

            return returnType.IsValueType ? Activator.CreateInstance(returnType) : Activator.CreateInstance(returnType);
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
            object result = typeof(IQueryProvider)
                .GetMethods()
                .Single(method => method.Name == nameof(IQueryProvider.Execute) && method.IsGenericMethod)
                .MakeGenericMethod(resultType)
                .Invoke(inner, [expression])!;

            return (TResult)typeof(Task)
                .GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(resultType)
                .Invoke(null, [result])!;
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
        public ValueTask DisposeAsync()
        {
            inner.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
