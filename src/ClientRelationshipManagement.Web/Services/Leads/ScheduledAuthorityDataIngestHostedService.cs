using ClientRelationshipManagement.Web.Brokers.Loggings;
using ClientRelationshipManagement.Web.Configuration;
using Microsoft.Extensions.Options;

namespace ClientRelationshipManagement.Web.Services.Leads;

public sealed class ScheduledAuthorityDataIngestHostedService(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<AuthorityDataOptions> options,
    ILoggingBroker<ScheduledAuthorityDataIngestHostedService> loggingBroker)
    : BackgroundService
{
    int isRunning;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        AuthorityDataOptions authorityDataOptions = options.Value;
        TimeSpan interval = TimeSpan.FromHours(Math.Max(1, authorityDataOptions.IntervalHours));
        using PeriodicTimer timer = new(interval);

        loggingBroker.LogInformation(
            "Scheduled authority data ingest hosted service started with interval {IntervalHours} hour(s).",
            interval.TotalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken);

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

        loggingBroker.LogInformation("Scheduled authority data ingest hosted service stopped.");
    }

    async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            loggingBroker.LogInformation("Scheduled authority data ingest hosted service skipped because the feature is disabled.");
            return;
        }

        if (Interlocked.CompareExchange(ref isRunning, 1, 0) != 0)
        {
            loggingBroker.LogInformation("Scheduled authority data ingest hosted service skipped because a previous run is still in progress.");
            return;
        }

        try
        {
            loggingBroker.LogInformation("Scheduled authority data ingest hosted service tick started.");
            using IServiceScope scope = serviceScopeFactory.CreateScope();
            IAuthorityDataImportService service = scope.ServiceProvider.GetRequiredService<IAuthorityDataImportService>();
            int importedFileCount = await service.RunPendingImportsAsync(cancellationToken);
            loggingBroker.LogInformation(
                "Scheduled authority data ingest hosted service tick completed. Imported {ImportedFileCount} file(s).",
                importedFileCount);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            loggingBroker.LogError(exception, "Scheduled authority data ingest hosted service failed during execution.");
        }
        finally
        {
            Interlocked.Exchange(ref isRunning, 0);
        }
    }
}
