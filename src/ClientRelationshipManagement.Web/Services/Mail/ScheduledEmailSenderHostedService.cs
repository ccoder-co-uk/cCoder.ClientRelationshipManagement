using ClientRelationshipManagement.Web.Brokers.Loggings;
using ClientRelationshipManagement.Web.Configuration;
using Microsoft.Extensions.Options;

namespace ClientRelationshipManagement.Web.Services.Mail;

public sealed class ScheduledEmailSenderHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<MailOptions> options,
    ILoggingBroker<ScheduledEmailSenderHostedService> loggingBroker)
    : BackgroundService
{
    int isRunning;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan interval = TimeSpan.FromSeconds(Math.Max(15, options.Value.PollIntervalSeconds));

        using PeriodicTimer timer = new(interval);
        loggingBroker.LogInformation(
            "Scheduled email sender hosted service started with interval {IntervalSeconds} second(s).",
            interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (Interlocked.CompareExchange(ref isRunning, 1, 0) != 0)
            {
                loggingBroker.LogInformation("Scheduled email sender skipped because a previous run is still in progress.");
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;

                continue;
            }

            try
            {
                loggingBroker.LogInformation("Scheduled email sender hosted service tick started.");
                using IServiceScope scope = scopeFactory.CreateScope();
                IEmailDispatchProcessor processor = scope.ServiceProvider.GetRequiredService<IEmailDispatchProcessor>();
                int dispatched = await processor.DispatchDueEmailsAsync(stoppingToken);
                loggingBroker.LogInformation(
                    "Scheduled email sender hosted service tick completed. Dispatched {DispatchedCount} email(s).",
                    dispatched);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                loggingBroker.LogError(ex, "Scheduled email sender encountered an unexpected error.");
            }
            finally
            {
                Interlocked.Exchange(ref isRunning, 0);
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        loggingBroker.LogInformation("Scheduled email sender hosted service stopped.");
    }
}
