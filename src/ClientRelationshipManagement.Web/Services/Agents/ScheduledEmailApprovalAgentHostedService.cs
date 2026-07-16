using ClientRelationshipManagement.Web.Brokers.Loggings;
using ClientRelationshipManagement.Web.Configuration;
using Microsoft.Extensions.Options;

namespace ClientRelationshipManagement.Web.Services.Agents;

public sealed class ScheduledEmailApprovalAgentHostedService(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<AgentWorkflowOptions> options,
    ILoggingBroker<ScheduledEmailApprovalAgentHostedService> loggingBroker)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan interval = TimeSpan.FromMinutes(Math.Max(1, options.Value.EmailApprovalAgentIntervalMinutes));
        using PeriodicTimer timer = new(interval);
        loggingBroker.LogInformation(
            "Scheduled email approval agent started with interval {IntervalMinutes} minute(s).",
            interval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using IServiceScope scope = serviceScopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IEmailApprovalAgent>().RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                loggingBroker.LogError(exception, "Scheduled email approval agent tick failed.");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
