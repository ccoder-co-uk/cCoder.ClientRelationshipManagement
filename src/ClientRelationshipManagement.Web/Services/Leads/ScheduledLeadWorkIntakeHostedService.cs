using ClientRelationshipManagement.Web.Brokers.Loggings;
using ClientRelationshipManagement.Web.Configuration;
using Microsoft.Extensions.Options;

namespace ClientRelationshipManagement.Web.Services.Leads;

public sealed class ScheduledLeadWorkIntakeHostedService(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<AuthorityDataOptions> options,
    ILoggingBroker<ScheduledLeadWorkIntakeHostedService> loggingBroker)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan interval = TimeSpan.FromSeconds(Math.Max(15, options.Value.LeadIntakeIntervalSeconds));
        using PeriodicTimer timer = new(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using IServiceScope scope = serviceScopeFactory.CreateScope();
                LeadWorkIntakeResult result = await scope.ServiceProvider
                    .GetRequiredService<ILeadWorkIntakeService>()
                    .EnsureCapacityAsync(stoppingToken);
                loggingBroker.LogInformation(
                    "Lead work intake tick completed. Active: {Active}; runnable: {Runnable}; promoted: {Promoted}.",
                    result.ActiveWorkItems,
                    result.RunnableWorkItems,
                    result.PromotedCompanyCount);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                loggingBroker.LogError(exception, "Lead work intake tick failed.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
                break;
        }
    }
}
