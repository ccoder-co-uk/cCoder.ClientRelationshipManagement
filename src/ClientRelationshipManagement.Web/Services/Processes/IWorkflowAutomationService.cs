using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using PlatformEntities = cCoder.ClientRelationshipManagement.Platform.Models.Entities;

namespace ClientRelationshipManagement.Web.Services.Processes;

public interface IWorkflowAutomationService
{
    ValueTask EnsureSeedProcessesAsync(CancellationToken cancellationToken = default);
    ValueTask EnsureCoverageAsync(
        Guid? leadId = null,
        Guid? opportunityId = null,
        Guid? clientAccountId = null,
        bool forceCreate = false,
        CancellationToken cancellationToken = default);
    ValueTask<int> ReevaluateDeferredLeadsAsync(
        string tenantId,
        CancellationToken cancellationToken = default);
    ValueTask<int> ReschedulePendingTasksForStepAsync(
        Guid processStepId,
        CancellationToken cancellationToken = default);
    ValueTask<RelatedEmailDraftContext> GetRelatedEmailDraftContextAsync(
        Guid agentMessageId,
        CancellationToken cancellationToken = default);
    ValueTask<RelatedEmailDraftRefreshResult> RefreshRelatedEmailDraftsAsync(
        Guid agentMessageId,
        CancellationToken cancellationToken = default);
    ValueTask<int> EnsureDefinitionCoverageAsync(
        Guid processDefinitionId,
        CancellationToken cancellationToken = default);
    ValueTask<PlatformEntities.ProcessTask> CompleteTaskAsync(
        ProcessTaskCompletionCommand command,
        CancellationToken cancellationToken = default);
    ValueTask<bool> CompleteEmailTaskAsync(Guid emailId, CancellationToken cancellationToken = default);
}

public sealed record RelatedEmailDraftContext(
    Guid AgentMessageId,
    string TenantId,
    Guid ProcessDefinitionId,
    string ProcessName,
    Guid ProcessStepId,
    string ProcessStepKey,
    string ProcessStepName,
    string EmailSubjectTemplate,
    string EmailBodyTemplate,
    RelatedEmailCorrectionReference ApprovedCorrection,
    string LiveTemplateRenderedSubject,
    string LiveTemplateRenderedBody,
    bool LiveTemplateMatchesApprovedCorrection,
    IReadOnlyList<RelatedEmailDraftItem> Drafts);

public sealed record RelatedEmailCorrectionReference(
    Guid EmailId,
    EmailState State,
    string Subject,
    string Body);

public sealed record RelatedEmailDraftItem(
    Guid ProcessTaskId,
    Guid EmailId,
    string CompanyName,
    string ToAddresses,
    string Subject,
    EmailState State,
    bool ContainsInternalDraftingGuidance);

public sealed record RelatedEmailDraftRefreshResult(
    RelatedEmailDraftContext Context,
    int InspectedCount,
    int UpdatedCount,
    IReadOnlyList<Guid> UpdatedEmailIds);

public sealed class ProcessTaskCompletionCommand
{
    public Guid ProcessTaskId { get; init; }
    public string OutcomeKey { get; init; }
    public string CompletionNote { get; init; }
}
