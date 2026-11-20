using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;
using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using cCoder.ClientRelationshipManagement.Runtime.Brokers.Loggings;
using cCoder.ClientRelationshipManagement.Runtime.Configuration;
using cCoder.ClientRelationshipManagement.Runtime.Models.Imports;
using cCoder.ClientRelationshipManagement.Runtime.Services.Mail;
using cCoder.Security.Models;
using ClientRelationshipManagement.Web.Controllers;
using ClientRelationshipManagement.Web.Models.AgentWorkflow;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Options;
using Xunit;

namespace cCoder.ClientRelationshipManagement.Tests.Api;

public sealed class WaveAControllerRuntimeEvidenceTests
{
    static readonly Guid EntityId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly Guid RelatedId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    static readonly string[] MissingAgentHappyActions =
    [
        nameof(AgentWorkflowController.ReconcileSentEmail),
        nameof(AgentWorkflowController.GetMessages),
        nameof(AgentWorkflowController.AppendMessageEntry),
        nameof(AgentWorkflowController.CompleteTask),
        nameof(AgentWorkflowController.SaveTaskDraftEmail),
        nameof(AgentWorkflowController.CreateReplacementEmailDraft),
        nameof(AgentWorkflowController.CreateMessage),
        nameof(AgentWorkflowController.CreateProcessDraftProposal),
        nameof(AgentWorkflowController.AppendLeadResearchFinding)
    ];

    static readonly string[] AgentActionsWithoutExistingDirectAuthenticationEvidence =
    [
        nameof(AgentWorkflowController.GetSentEmailReconciliation),
        nameof(AgentWorkflowController.ReconcileSentEmail),
        nameof(AgentWorkflowController.CancelUnverifiedEmail),
        nameof(AgentWorkflowController.GetMessages),
        nameof(AgentWorkflowController.AppendMessageEntry),
        nameof(AgentWorkflowController.CompleteTask),
        nameof(AgentWorkflowController.GetTaskEmailEvidence),
        nameof(AgentWorkflowController.SaveTaskDraftEmail),
        nameof(AgentWorkflowController.CreateReplacementEmailDraft),
        nameof(AgentWorkflowController.GetRelatedDraftEmails),
        nameof(AgentWorkflowController.RefreshRelatedDraftEmails),
        nameof(AgentWorkflowController.CreateMessage),
        nameof(AgentWorkflowController.GetProcessMetrics),
        nameof(AgentWorkflowController.CreateProcessDraftProposal),
        nameof(AgentWorkflowController.UpdateLeadResearch),
        nameof(AgentWorkflowController.AppendLeadResearchFinding)
    ];

    public static IEnumerable<object[]> MissingAgentHappyCases => MissingAgentHappyActions.Select(name => new object[] { name });
    public static IEnumerable<object[]> AgentAuthenticationCases => AgentActionsWithoutExistingDirectAuthenticationEvidence.Select(name => new object[] { name });
    public static IEnumerable<object[]> ImportApiActions => PublicActions(typeof(ImportsApiController)).Select(method => new object[] { method.Name });

    [Theory]
    [MemberData(nameof(MissingAgentHappyCases))]
    public async Task AgentWorkflowAction_WhenTailoredDependenciesSucceed_ShouldReturn2xxAndLogInformation(string actionName)
    {
        RuntimeHarness harness = new(actionName, authenticated: true);
        AgentWorkflowController controller = harness.CreateAgentWorkflowController();

        IActionResult result = await Invoke(controller, actionName);

        Assert.True(StatusCode(result) is >= 200 and <= 299, harness.Error?.ToString() ?? $"Unexpected status {StatusCode(result)}.");
        harness.AssertLogging(information: 1, warning: 0, error: 0);
    }

    [Theory]
    [MemberData(nameof(AgentAuthenticationCases))]
    public async Task AgentWorkflowAction_WithoutBearerAuthentication_ShouldReturn401AndLogWarning(string actionName)
    {
        RuntimeHarness harness = new(actionName, authenticated: false);
        AgentWorkflowController controller = harness.CreateAgentWorkflowController();

        IActionResult result = await Invoke(controller, actionName);

        Assert.Equal(StatusCodes.Status401Unauthorized, StatusCode(result));
        harness.AssertLogging(information: 0, warning: 1, error: 0);
    }

    [Theory]
    [MemberData(nameof(ImportApiActions))]
    public async Task ImportsApiAction_WhenTailoredDependenciesSucceed_ShouldReturn2xxAndLogInformation(string actionName)
    {
        RuntimeHarness harness = new(actionName, authenticated: true);
        ImportsApiController controller = harness.CreateImportsApiController();

        IActionResult result = await Invoke(controller, actionName);

        Assert.True(StatusCode(result) is >= 200 and <= 299, harness.Error?.ToString() ?? $"Unexpected status {StatusCode(result)}.");
        harness.AssertLogging(information: 1, warning: 0, error: 0);
    }

    [Theory]
    [MemberData(nameof(ImportApiActions))]
    public async Task ImportsApiAction_WhenUserIsGuest_ShouldReturn401AndLogWarning(string actionName)
    {
        RuntimeHarness harness = new(actionName, authenticated: false);
        ImportsApiController controller = harness.CreateImportsApiController();

        IActionResult result = await Invoke(controller, actionName);

        Assert.Equal(StatusCodes.Status401Unauthorized, StatusCode(result));
        harness.AssertLogging(information: 0, warning: 1, error: 0);
    }

    [Fact]
    public async Task ImportsIndex_WhenQueriesSucceed_ShouldReturnViewAndLogInformation()
    {
        RuntimeHarness harness = new(nameof(ImportsController.Index), authenticated: true);
        ImportsController controller = harness.CreateImportsController();

        IActionResult result = await controller.Index();

        Assert.True(result is ViewResult, harness.Error?.ToString() ?? $"Unexpected result {result.GetType().Name}.");
        harness.AssertLogging(information: 1, warning: 0, error: 0);
    }

    static IEnumerable<MethodInfo> PublicActions(Type controllerType) => controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
        .Where(method => typeof(Task<IActionResult>).IsAssignableFrom(method.ReturnType));

    static async Task<IActionResult> Invoke(ControllerBase controller, string actionName)
    {
        MethodInfo method = PublicActions(controller.GetType()).Single(candidate => candidate.Name == actionName);
        object[] arguments = method.GetParameters().Select(parameter => ParameterValue(parameter.ParameterType, actionName)).ToArray();
        return await (Task<IActionResult>)method.Invoke(controller, arguments)!;
    }

    static object ParameterValue(Type type, string actionName)
    {
        if (type == typeof(Guid)) return EntityId;
        if (type == typeof(Guid?)) return null;
        if (type == typeof(DateTimeOffset?)) return null;
        if (type == typeof(int)) return 25;
        if (type == typeof(CancellationToken)) return CancellationToken.None;
        if (type == typeof(ReconcileSentEmailRequest)) return new ReconcileSentEmailRequest { MailboxExternalId = "mailbox-message" };
        if (type == typeof(CancelUnverifiedEmailRequest)) return new CancelUnverifiedEmailRequest { Reason = "No provider evidence." };
        if (type == typeof(AppendAgentMessageEntryRequest)) return new AppendAgentMessageEntryRequest { Body = "A useful response." };
        if (type == typeof(CompleteAgentTaskRequest)) return new CompleteAgentTaskRequest { OutcomeKey = "complete" };
        if (type == typeof(AgentTaskDraftEmailRequest)) return new AgentTaskDraftEmailRequest { Subject = "Subject", Body = "Recipient-ready body.", ToAddresses = "recipient@example.com" };
        if (type == typeof(AgentReplacementEmailDraftRequest)) return new AgentReplacementEmailDraftRequest { Subject = "Replacement", Body = "Corrected recipient-ready body." };
        if (type == typeof(CreateAgentMessageRequest)) return new CreateAgentMessageRequest { Title = "Title", Body = "Body" };
        if (type == typeof(CreateProcessDraftProposalRequest)) return new CreateProcessDraftProposalRequest { ChangeSummary = "Improve the process." };
        if (type == typeof(UpdateLeadResearchRequest)) return new UpdateLeadResearchRequest();
        if (type == typeof(AppendLeadResearchFindingRequest)) return new AppendLeadResearchFindingRequest { SectionKey = "market", Finding = "Verified finding." };
        if (type == typeof(CreateImportRequest)) return new CreateImportRequest { FileName = "evidence.csv", ContentType = "text/csv", SizeBytes = 32 };
        if (type == typeof(SaveImportMappingRequest)) return new SaveImportMappingRequest { MappingSnapshotJson = "{}" };
        return type.IsValueType ? Activator.CreateInstance(type)! : Activator.CreateInstance(type)!;
    }

    static int StatusCode(IActionResult result) => (result as IStatusCodeActionResult)?.StatusCode ?? StatusCodes.Status200OK;

    sealed class RuntimeHarness
    {
        readonly string actionName;
        readonly bool authenticated;
        readonly Dictionary<Type, RuntimeProxy> proxies = [];

        public RuntimeHarness(string actionName, bool authenticated)
        {
            this.actionName = actionName;
            this.authenticated = authenticated;
        }

        public AgentWorkflowController CreateAgentWorkflowController() => (AgentWorkflowController)CreateController(typeof(AgentWorkflowController));
        public ImportsApiController CreateImportsApiController() => (ImportsApiController)CreateController(typeof(ImportsApiController));
        public Exception Error => proxies.Values.Select(proxy => proxy.Error).FirstOrDefault(error => error is not null);

        public ImportsController CreateImportsController()
        {
            ImportsController controller = (ImportsController)CreateController(typeof(ImportsController));
            controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                controller.HttpContext,
                (Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider)Proxy(typeof(Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider)));
            return controller;
        }

        public void AssertLogging(int information, int warning, int error)
        {
            RuntimeProxy logging = proxies.Values.Single(proxy => proxy.IsLogging);
            Assert.Equal(information, logging.InformationCount);
            Assert.Equal(warning, logging.WarningCount);
            Assert.Equal(error, logging.ErrorCount);
        }

        ControllerBase CreateController(Type controllerType)
        {
            object[] arguments = controllerType.GetConstructors().Single().GetParameters().Select(parameter => Dependency(parameter.ParameterType)).ToArray();
            ControllerBase controller = (ControllerBase)Activator.CreateInstance(controllerType, arguments)!;
            DefaultHttpContext context = new();
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "test-user")], authenticated ? "Evidence" : null));
            if (authenticated)
                context.Request.Headers.Authorization = "Bearer evidence-token";
            controller.ControllerContext = new ControllerContext { HttpContext = context };
            return controller;
        }

        object Dependency(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IOptions<>))
            {
                Type optionType = type.GetGenericArguments()[0];
                object option = Activator.CreateInstance(optionType)!;
                return typeof(Options).GetMethods(BindingFlags.Public | BindingFlags.Static).Single(method => method.Name == nameof(Options.Create) && method.IsGenericMethod)
                    .MakeGenericMethod(optionType).Invoke(null, [option])!;
            }

            return Proxy(type);
        }

        object Proxy(Type type)
        {
            object proxyObject = DispatchProxy.Create(type, typeof(RuntimeProxy));
            RuntimeProxy proxy = (RuntimeProxy)proxyObject;
            proxy.Configure(type, actionName, authenticated, Resolve);
            proxies[type] = proxy;
            return proxyObject;
        }

        object Resolve(Type returnType, string methodName)
        {
            if (returnType == typeof(void)) return null;
            if (returnType == typeof(string)) return authenticated ? "test-user" : "Guest";
            if (returnType == typeof(string[])) return new[] { "tenant" };
            if (returnType == typeof(Task)) return Task.CompletedTask;
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                Type resultType = returnType.GetGenericArguments()[0];
                object value = ResultValue(resultType, methodName);
                return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(resultType).Invoke(null, [value])!;
            }
            if (returnType == typeof(ValueTask)) return ValueTask.CompletedTask;
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
            {
                Type resultType = returnType.GetGenericArguments()[0];
                return Activator.CreateInstance(returnType, ResultValue(resultType, methodName))!;
            }
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(IQueryable<>))
            {
                Type itemType = returnType.GetGenericArguments()[0];
                IList items = Items(itemType, methodName);
                return typeof(WaveAControllerRuntimeEvidenceTests).GetMethod(nameof(AsAsyncQueryable), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(itemType).Invoke(null, [items])!;
            }
            if (returnType.IsGenericType && (returnType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>) || returnType.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
                return Items(returnType.GetGenericArguments()[0], methodName);
            if (returnType == typeof(bool)) return true;
            if (returnType == typeof(IDictionary<string, object>)) return new Dictionary<string, object>();
            if (returnType.IsValueType) return Activator.CreateInstance(returnType)!;
            return ResultValue(returnType, methodName);
        }

        object ResultValue(Type type, string methodName)
        {
            if (type == typeof(bool)) return true;
            if (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>) || type.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
                return Items(type.GetGenericArguments()[0], methodName);
            if (type == typeof(Import)) return ImportEntity();
            if (type == typeof(ImportStatusResponse)) return new ImportStatusResponse { Id = EntityId };
            if (type == typeof(ImportUploadSessionResponse)) return new ImportUploadSessionResponse { ImportId = EntityId };
            if (type == typeof(AgentMessageEntry)) return new AgentMessageEntry { Id = EntityId, AgentMessageId = RelatedId };
            if (type == typeof(AgentMessage)) return AgentMessageEntity();
            if (type == typeof(Email)) return EmailEntity(EmailState.Draft);
            if (type == typeof(ProcessTask)) return ProcessTaskEntity();
            if (type == typeof(ProcessDefinition)) return new ProcessDefinition { Id = EntityId, Name = "Draft", VersionNumber = 2 };
            if (type.IsArray) return Array.CreateInstance(type.GetElementType()!, 0);
            return Activator.CreateInstance(type)!;
        }

        IList Items(Type itemType, string methodName)
        {
            IList list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType))!;
            if (actionName == nameof(ImportsController.Index))
                return list;
            object item = itemType == typeof(Import) ? ImportEntity()
                : itemType == typeof(Email) ? EmailEntity(
                    actionName == nameof(AgentWorkflowController.CreateReplacementEmailDraft) ? EmailState.Rejected : EmailState.Sent,
                    actionName == nameof(AgentWorkflowController.ReconcileSentEmail) ? EntityId : RelatedId)
                : itemType == typeof(AgentMessage) ? AgentMessageEntity()
                : itemType == typeof(ProcessTask) ? ProcessTaskEntity()
                : itemType == typeof(Lead) ? new Lead { Id = EntityId, QualificationNotes = string.Empty }
                : itemType == typeof(MailboxMessage) ? new MailboxMessage
                {
                    ExternalId = "mailbox-message",
                    InternetMessageId = "internet-message",
                    Subject = "Subject",
                    ToAddresses = "recipient@example.com",
                    FromAddress = "sender@example.com",
                    ReceivedOn = DateTimeOffset.UtcNow
                }
                : null;
            if (item is not null)
                list.Add(item);
            return list;
        }

        static Import ImportEntity() => new() { Id = EntityId, OriginalFileName = "evidence.csv", ContentType = "text/csv", JobStatus = ImportJobStatus.Draft };

        static AgentMessage AgentMessageEntity() => new()
        {
            Id = EntityId,
            EmailId = RelatedId,
            OpportunityId = RelatedId,
            ProcessDefinitionId = RelatedId,
            State = AgentMessageState.Pending,
            TenantId = "tenant",
            Entries = []
        };

        static Email EmailEntity(EmailState state, Guid? id = null)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            return new Email
            {
                Id = id ?? RelatedId,
                State = state,
                Subject = "Subject",
                ToAddresses = "recipient@example.com",
                FromEmailAddress = "sender@example.com",
                SentOn = now,
                LastUpdated = now,
                OpportunityId = RelatedId,
                TenantCompanyRelationshipId = EntityId,
                TenantCompanyRelationship = new TenantCompanyRelationship { Id = EntityId, TenantId = "tenant" }
            };
        }

        static ProcessTask ProcessTaskEntity() => new()
        {
            Id = EntityId,
            TenantCompanyRelationshipId = RelatedId,
            ProcessStepId = RelatedId,
            ProcessStep = new ProcessStep { Id = RelatedId, Key = "work", ExecutionMode = ProcessStepExecutionMode.AgentManaged },
            ActionType = ProcessActionType.Research,
            State = ProcessTaskState.Completed
        };
    }

    public class RuntimeProxy : DispatchProxy
    {
        Func<Type, string, object> resolver;
        public bool IsLogging { get; private set; }
        public int InformationCount { get; private set; }
        public int WarningCount { get; private set; }
        public int ErrorCount { get; private set; }
        public Exception Error { get; private set; }

        public void Configure(Type interfaceType, string actionName, bool authenticated, Func<Type, string, object> resolver)
        {
            IsLogging = interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(ILoggingBroker<>);
            this.resolver = resolver;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            if (targetMethod.Name == "LogInformation") { InformationCount++; return null; }
            if (targetMethod.Name == "LogWarning") { WarningCount++; return null; }
            if (targetMethod.Name == "LogError") { ErrorCount++; Error = args?.OfType<Exception>().FirstOrDefault(); return null; }
            return resolver(targetMethod.ReturnType, targetMethod.Name);
        }
    }

    static IQueryable<T> AsAsyncQueryable<T>(IList values) => new TestAsyncEnumerable<T>(values.Cast<T>());

    sealed class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public TestAsyncEnumerable(IEnumerable<T> enumerable) : base(enumerable) { }
        public TestAsyncEnumerable(Expression expression) : base(expression) { }
        IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) => new TestAsyncEnumerator<T>(((IEnumerable<T>)this).GetEnumerator());
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
            object result = typeof(IQueryProvider).GetMethod(nameof(IQueryProvider.Execute), 1, [typeof(Expression)])!
                .MakeGenericMethod(resultType).Invoke(inner, [expression])!;
            return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(resultType).Invoke(null, [result])!;
        }
    }

    sealed class TestAsyncEnumerator<T>(IEnumerator<T> enumerator) : IAsyncEnumerator<T>
    {
        public T Current => enumerator.Current;
        public ValueTask DisposeAsync() { enumerator.Dispose(); return ValueTask.CompletedTask; }
        public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(enumerator.MoveNext());
    }
}
