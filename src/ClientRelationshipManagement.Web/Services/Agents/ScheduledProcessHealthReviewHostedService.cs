using ClientRelationshipManagement.Web.Brokers.Loggings;
using ClientRelationshipManagement.Web.Configuration;
using Microsoft.Extensions.Options;

namespace ClientRelationshipManagement.Web.Services.Agents;

public sealed class ScheduledProcessHealthReviewHostedService(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<AgentWorkflowOptions> options,
    ILoggingBroker<ScheduledProcessHealthReviewHostedService> loggingBroker) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan interval = TimeSpan.FromHours(Math.Max(1, options.Value.ProcessHealthReviewIntervalHours));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using IServiceScope scope = serviceScopeFactory.CreateScope();
                IProcessHealthReviewService service = scope.ServiceProvider.GetRequiredService<IProcessHealthReviewService>();
                int created = await service.CreateDailyReviewsAsync(options.Value.ExecutionUserId ?? "system", stoppingToken);
                loggingBroker.LogInformation("Created {ConversationCount} scheduled process-health conversation(s).", created);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                loggingBroker.LogError(exception, "Scheduled process-health review failed.");
            }

            try { await Task.Delay(interval, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
