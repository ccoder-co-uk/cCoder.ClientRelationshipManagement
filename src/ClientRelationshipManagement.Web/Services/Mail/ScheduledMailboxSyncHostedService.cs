using ClientRelationshipManagement.Web.Brokers.Loggings;
using ClientRelationshipManagement.Web.Configuration;
using Microsoft.Extensions.Options;

namespace ClientRelationshipManagement.Web.Services.Mail;

public sealed class ScheduledMailboxSyncHostedService(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<MailOptions> options,
    ILoggingBroker<ScheduledMailboxSyncHostedService> loggingBroker)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan interval = TimeSpan.FromSeconds(Math.Max(10, options.Value.MailboxSyncIntervalSeconds));
        using PeriodicTimer timer = new(interval);
        loggingBroker.LogInformation(
            "Scheduled mailbox sync started with interval {IntervalSeconds} second(s).",
            interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using IServiceScope scope = serviceScopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IMailboxSyncProcessor>().SyncAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                loggingBroker.LogError(exception, "Scheduled mailbox sync tick failed.");
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
