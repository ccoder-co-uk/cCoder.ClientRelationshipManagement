using ClientRelationshipManagement.Web.Brokers.Loggings;
using ClientRelationshipManagement.Web.Configuration;
using Microsoft.Extensions.Options;

namespace ClientRelationshipManagement.Web.Services.Agents;

public sealed class ScheduledProcessOptimiserHostedService(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<AgentWorkflowOptions> options,
    ILoggingBroker<ScheduledProcessOptimiserHostedService> loggingBroker)
    : BackgroundService
{
    int isRunning;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        AgentWorkflowOptions workflowOptions = options.Value;
        TimeSpan interval = TimeSpan.FromMinutes(Math.Max(1, workflowOptions.ProcessOptimiserIntervalMinutes));
        using PeriodicTimer timer = new(interval);
        loggingBroker.LogInformation(
            "Scheduled process optimiser hosted service started with interval {IntervalMinutes} minute(s).",
            interval.TotalMinutes);

        try
        {
            if (!await timer.WaitForNextTickAsync(stoppingToken))
                return;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken);
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

        loggingBroker.LogInformation("Scheduled process optimiser hosted service stopped.");
    }

    async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref isRunning, 1, 0) != 0)
        {
            loggingBroker.LogInformation("Scheduled process optimiser hosted service skipped because a previous run is still in progress.");
            return;
        }

        try
        {
            loggingBroker.LogInformation("Scheduled process optimiser hosted service tick started.");
            using IServiceScope scope = serviceScopeFactory.CreateScope();
            IAgentWorkflowRunner runner = scope.ServiceProvider.GetRequiredService<IAgentWorkflowRunner>();
            Guid? runId = await runner.RunProcessOptimiserAsync(cancellationToken);
            loggingBroker.LogInformation(
                "Scheduled process optimiser hosted service tick completed. RunId: {RunId}.",
                runId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            loggingBroker.LogError(exception, "Process optimiser hosted service failed during execution.");
        }
        finally
        {
            Interlocked.Exchange(ref isRunning, 0);
        }
    }
}
