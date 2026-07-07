using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.Web.Services.Agents;

public sealed class AgentRunJournalService(IPlatformDbContextFactory dbContextFactory) : IAgentRunJournalService
{
    public async ValueTask<AgentRun> StartAsync(
        AgentRunKind kind,
        string executionUserId,
        string provider,
        string model,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        AgentRun run = new()
        {
            Id = Guid.NewGuid(),
            Kind = kind,
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

        context.AgentRuns.Add(run);
        await context.SaveChangesAsync(cancellationToken);
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
        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        AgentRun run = await context.AgentRuns.FirstOrDefaultAsync(item => item.Id == runId, cancellationToken);
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

        await context.SaveChangesAsync(cancellationToken);
    }
}
