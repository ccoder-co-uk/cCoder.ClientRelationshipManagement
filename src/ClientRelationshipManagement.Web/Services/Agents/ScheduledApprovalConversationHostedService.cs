using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using cCoder.ClientRelationshipManagement.Services.Foundations.Platform;
using ClientRelationshipManagement.Web.Brokers.Loggings;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.Web.Services.Agents;

public sealed class ScheduledApprovalConversationHostedService(
    IServiceScopeFactory serviceScopeFactory,
    ILoggingBroker<ScheduledApprovalConversationHostedService> loggingBroker) : BackgroundService
{
    static readonly TimeSpan FailedTurnRetryDelay = TimeSpan.FromMinutes(5);
    readonly Dictionary<Guid, DateTimeOffset> lastAttempts = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverAbandonedRunsAsync(stoppingToken);
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(10));
        while (!stoppingToken.IsCancellationRequested)
        {
            await ReviewWaitingConversationAsync(stoppingToken);
            try { if (!await timer.WaitForNextTickAsync(stoppingToken)) break; }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    async Task RecoverAbandonedRunsAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = serviceScopeFactory.CreateScope();
        IAgentRunJournalService journal = scope.ServiceProvider.GetRequiredService<IAgentRunJournalService>();
        int recovered = await journal.FailAbandonedAsync(
            AgentRunKind.ProcessOptimiser,
            DateTimeOffset.UtcNow.AddMinutes(-5),
            cancellationToken);
        if (recovered > 0)
            loggingBroker.LogInformation("Recovered {RunCount} abandoned approval conversation run(s).", recovered);
    }

    async Task ReviewWaitingConversationAsync(CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = serviceScopeFactory.CreateScope();
            cCoder.ClientRelationshipManagement.Services.Entities.IAgentMessageOrchestrationService messages =
                scope.ServiceProvider.GetRequiredService<cCoder.ClientRelationshipManagement.Services.Entities.IAgentMessageOrchestrationService>();
            var candidates = await messages.RetrieveAll().AsNoTracking().Include(item => item.Entries)
                .Where(item => item.State == AgentMessageState.Pending)
                .OrderBy(item => item.LastUpdated).Take(25).ToListAsync(cancellationToken);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var waitingMessages = candidates.Where(message =>
            {
                bool retryDue = !lastAttempts.TryGetValue(message.Id, out DateTimeOffset attemptedOn)
                    || now - attemptedOn >= FailedTurnRetryDelay;
                return AgentConversationTurnPolicy.IsAgentTurn(message) && retryDue;
            }).ToList();
            if (waitingMessages.Count == 0)
                return;

            var conversation = waitingMessages[0];
            lastAttempts[conversation.Id] = now;

            IAgentWorkflowRunner runner = scope.ServiceProvider.GetRequiredService<IAgentWorkflowRunner>();
            await runner.RunProcessOptimiserAsync(conversation.Id, cancellationToken);

            foreach (Guid messageId in lastAttempts.Keys.Except(candidates.Select(item => item.Id)).ToList())
                lastAttempts.Remove(messageId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            loggingBroker.LogError(exception, "Approval conversation review failed.");
        }
    }
}
