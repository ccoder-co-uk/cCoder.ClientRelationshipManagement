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
    ValueTask<PlatformEntities.ProcessTask> CompleteTaskAsync(
        ProcessTaskCompletionCommand command,
        CancellationToken cancellationToken = default);
    ValueTask<bool> CompleteEmailTaskAsync(Guid emailId, CancellationToken cancellationToken = default);
}

public sealed class ProcessTaskCompletionCommand
{
    public Guid ProcessTaskId { get; init; }
    public string OutcomeKey { get; init; }
    public string CompletionNote { get; init; }
}
