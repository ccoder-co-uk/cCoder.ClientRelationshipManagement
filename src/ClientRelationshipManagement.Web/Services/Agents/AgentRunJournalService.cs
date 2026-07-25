using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using cCoder.ClientRelationshipManagement.Services.Foundations.Platform;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.Web.Services.Agents;

public sealed class AgentRunJournalService(
    IOperationsCoordinationService operations,
    IProcessCoordinationService processes) : IAgentRunJournalService
{
    public async ValueTask<int> FailAbandonedAsync(
        AgentRunKind kind,
        DateTimeOffset startedBefore,
        CancellationToken cancellationToken = default)
    {
        List<AgentRun> abandoned = await operations.RetrieveAllAgentRuns()
            .Where(item => item.Kind == kind
                && item.State == AgentRunState.Running
                && item.StartedOn < startedBefore)
            .ToListAsync(cancellationToken);
        if (abandoned.Count == 0)
            return 0;

        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach (AgentRun run in abandoned)
        {
            run.State = AgentRunState.Failed;
            run.ErrorMessage = "Recovered as abandoned after the agent host stopped before recording completion.";
            run.CompletedOn = now;
            run.LastUpdatedBy = "agent-run-recovery";
            run.LastUpdated = now;
        }

        Guid[] taskIds = [.. abandoned.Where(run => run.ProcessTaskId.HasValue)
            .Select(run => run.ProcessTaskId!.Value).Distinct()];
        if (taskIds.Length > 0)
        {
            await processes.RetrieveTasks()
                .Where(task => taskIds.Contains(task.Id) && task.State == ProcessTaskState.Pending)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(task => task.AgentClaimId, (Guid?)null)
                    .SetProperty(task => task.AgentClaimedBy, (string)null)
                    .SetProperty(task => task.AgentClaimedOn, (DateTimeOffset?)null)
                    .SetProperty(task => task.AgentClaimExpiresOn, (DateTimeOffset?)null),
                    cancellationToken);
        }

        await operations.SaveAsync(cancellationToken);
        return abandoned.Count;
    }

    public async ValueTask<AgentRun> StartAsync(
        AgentRunKind kind,
        string executionUserId,
        string provider,
        string model,
        string workingDirectory,
        CancellationToken cancellationToken = default,
        AgentWorkLane? workLane = null,
        Guid? processTaskId = null,
        Guid? processStepId = null,
        string processStepKey = null)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        AgentRun run = new()
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            WorkLane = workLane,
            ProcessTaskId = processTaskId,
            ProcessStepId = processStepId,
            ProcessStepKey = processStepKey?.Trim(),
            State = AgentRunState.Running,
            ExecutionUserId = string.IsNullOrWhiteSpace(executionUserId) ? "system" : executionUserId,
            Provider = provider ?? string.Empty,
            Model = model ?? string.Empty,
            WorkingDirectory = workingDirectory ?? string.Empty,
            StartedOn = now,
            CreatedBy = executionUserId ?? "system",
            LastUpdatedBy = executionUserId ?? "system",
            CreatedOn = now,
            LastUpdated = now
        };

        operations.Add(run);
        await operations.SaveAsync(cancellationToken);
        return run;
    }

    public async ValueTask CompleteAsync(
        Guid runId,
        AgentRunState state,
        int iterations,
        string summary,
        string errorMessage,
        int processedItemCount,
        CancellationToken cancellationToken = default)
    {
        AgentRun run = await operations.RetrieveAllAgentRuns().FirstOrDefaultAsync(item => item.Id == runId, cancellationToken);
        if (run is null)
            return;

        run.State = state;
        run.Iterations = iterations;
        run.Summary = summary?.Trim();
        run.ErrorMessage = errorMessage?.Trim();
        run.ProcessedItemCount = processedItemCount;
        run.CompletedOn = DateTimeOffset.UtcNow;
        run.LastUpdatedBy = run.ExecutionUserId;
        run.LastUpdated = DateTimeOffset.UtcNow;

        await operations.SaveAsync(cancellationToken);
    }
}
