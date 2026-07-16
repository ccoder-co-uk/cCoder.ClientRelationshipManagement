using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using cCoder.ClientRelationshipManagement.Services.Foundations.Platform;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.Web.Services.Processes;

internal static class ProcessVersionMigration
{
    public static async ValueTask<int> MoveActiveInstancesAsync(
        IProcessCoordinationService processes,
        ProcessDefinition liveDefinition,
        IReadOnlyCollection<Guid> obsoleteDefinitionIds,
        string changedBy,
        CancellationToken cancellationToken = default)
    {
        if (obsoleteDefinitionIds.Count == 0)
            return 0;

        List<ProcessInstance> instances = await processes.RetrieveInstances()
            .Include(instance => instance.CurrentProcessStep)
            .Where(instance => obsoleteDefinitionIds.Contains(instance.ProcessDefinitionId)
                && instance.State == ProcessInstanceState.Active)
            .ToListAsync(cancellationToken);

        if (instances.Count == 0)
            return 0;

        Dictionary<string, ProcessStep> liveSteps = await processes.RetrieveSteps()
            .Where(step => step.ProcessDefinitionId == liveDefinition.Id && step.IsActive)
            .ToDictionaryAsync(step => step.Key, StringComparer.OrdinalIgnoreCase, cancellationToken);

        List<string> unmappedStepKeys = instances
            .Where(instance => instance.CurrentProcessStep is null
                || string.IsNullOrWhiteSpace(instance.CurrentProcessStep.Key)
                || !liveSteps.ContainsKey(instance.CurrentProcessStep.Key))
            .Select(instance => instance.CurrentProcessStep?.Key ?? "<no current step>")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key)
            .ToList();

        if (unmappedStepKeys.Count > 0)
        {
            throw new InvalidOperationException(
                $"The live {liveDefinition.ScopeType} process cannot replace the previous version because active work is on unmapped step(s): {string.Join(", ", unmappedStepKeys)}. Retain those step keys or finish the affected work before publishing.");
        }

        Guid[] instanceIds = [.. instances.Select(instance => instance.Id)];
        List<ProcessTask> pendingTasks = await processes.RetrieveTasks()
            .Include(task => task.Email)
            .Where(task => instanceIds.Contains(task.ProcessInstanceId)
                && task.State == ProcessTaskState.Pending)
            .ToListAsync(cancellationToken);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach (ProcessTask task in pendingTasks)
        {
            task.State = ProcessTaskState.Cancelled;
            task.CompletionOutcomeKey = "process-version-migrated";
            task.CompletionNotes = $"Cancelled when process version {liveDefinition.VersionNumber} became live; replacement work will be created from the live graph.";
            task.CompletedBy = changedBy;
            task.CompletedOn = now;
            task.AgentClaimId = null;
            task.AgentClaimedBy = null;
            task.AgentClaimedOn = null;
            task.AgentClaimExpiresOn = null;
            task.LastUpdatedBy = changedBy;
            task.LastUpdated = now;

            if (task.Email is not null
                && task.Email.State is EmailState.Draft or EmailState.Approved or EmailState.Failed)
            {
                task.Email.State = EmailState.Cancelled;
                task.Email.LastUpdatedBy = changedBy;
                task.Email.LastUpdated = now;
            }
        }

        foreach (ProcessInstance instance in instances)
        {
            ProcessStep liveStep = liveSteps[instance.CurrentProcessStep!.Key];
            instance.ProcessDefinitionId = liveDefinition.Id;
            instance.CurrentProcessStepId = liveStep.Id;
            instance.CurrentProcessTaskId = null;
            instance.LastUpdatedBy = changedBy;
            instance.LastUpdated = now;
        }

        return instances.Count;
    }
}
