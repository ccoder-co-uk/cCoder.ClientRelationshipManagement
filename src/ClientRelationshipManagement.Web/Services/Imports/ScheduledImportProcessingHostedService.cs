using ClientRelationshipManagement.Web.Brokers.Loggings;
using ClientRelationshipManagement.Web.Configuration;
using Microsoft.Extensions.Options;

namespace ClientRelationshipManagement.Web.Services.Imports;

public sealed class ScheduledImportProcessingHostedService(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<ImportWorkflowOptions> options,
    ILoggingBroker<ScheduledImportProcessingHostedService> loggingBroker)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan interval = TimeSpan.FromMinutes(Math.Max(1, options.Value.ProcessingIntervalMinutes));
        loggingBroker.LogInformation("Import processing hosted service started with interval {IntervalMinutes} minute(s).", interval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken);
            await Task.Delay(interval, stoppingToken);
        }
    }

    async ValueTask RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = serviceScopeFactory.CreateScope();
            IImportProcessingService service = scope.ServiceProvider.GetRequiredService<IImportProcessingService>();
            int processed = await service.ProcessReadyImportsAsync(cancellationToken);
            loggingBroker.LogInformation("Import processing hosted service tick completed. Processed {ProcessedCount} import(s).", processed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            loggingBroker.LogError(exception, "Import processing hosted service tick failed.");
        }
    }
}
