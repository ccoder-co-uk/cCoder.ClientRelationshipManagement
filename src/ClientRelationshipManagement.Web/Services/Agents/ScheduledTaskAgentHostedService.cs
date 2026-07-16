using ClientRelationshipManagement.Web.Brokers.Loggings;
using ClientRelationshipManagement.Web.Configuration;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using Microsoft.Extensions.Options;

namespace ClientRelationshipManagement.Web.Services.Agents;

public sealed class ScheduledTaskAgentHostedService(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<AgentWorkflowOptions> options,
    ILoggingBroker<ScheduledTaskAgentHostedService> loggingBroker)
    : BackgroundService
{
    int isRunning;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        AgentWorkflowOptions workflowOptions = options.Value;
        TimeSpan interval = TimeSpan.FromMinutes(Math.Max(1, workflowOptions.TaskAgentIntervalMinutes));
        using PeriodicTimer timer = new(interval);
        loggingBroker.LogInformation(
            "Scheduled task agent hosted service started with interval {IntervalMinutes} minute(s).",
            interval.TotalMinutes);

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

        loggingBroker.LogInformation("Scheduled task agent hosted service stopped.");
    }

    async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref isRunning, 1, 0) != 0)
        {
            loggingBroker.LogInformation("Scheduled task agent hosted service skipped because a previous run is still in progress.");
            return;
        }

        try
        {
            loggingBroker.LogInformation("Scheduled task agent hosted service tick started.");
            using IServiceScope scope = serviceScopeFactory.CreateScope();
            AgentWorkflowOptions workflowOptions = options.Value;
            IReadOnlyList<AiWorkLaneSelection> lanes = await scope.ServiceProvider
                .GetRequiredService<IAiProviderSelectionService>()
                .GetWorkLanesAsync(workflowOptions.ExecutionUserId, cancellationToken);
            Task<Guid?>[] workers =
            [
                .. lanes
                    .Where(lane => lane.IsEnabled)
                    .SelectMany(lane => Enumerable.Range(
                            0,
                            Math.Min(lane.Concurrency, lane.Profile.MaxConcurrency))
                        .Select(_ => RunLaneWorkerAsync(lane.Lane, cancellationToken)))
            ];
            Guid?[] runIds = workers.Length == 0
                ? []
                : await Task.WhenAll(workers);
            loggingBroker.LogInformation(
                "Scheduled task agent hosted service tick completed. Worker count: {WorkerCount}. Run count: {RunCount}.",
                workers.Length,
                runIds.Count(runId => runId.HasValue));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            loggingBroker.LogError(exception, "Task agent hosted service failed during execution.");
        }
        finally
        {
            Interlocked.Exchange(ref isRunning, 0);
        }
    }

    async Task<Guid?> RunLaneWorkerAsync(
        AgentWorkLane lane,
        CancellationToken cancellationToken)
    {
        using IServiceScope scope = serviceScopeFactory.CreateScope();
        return await scope.ServiceProvider
            .GetRequiredService<IAgentWorkflowRunner>()
            .RunTaskAgentAsync(lane, cancellationToken);
    }
}
