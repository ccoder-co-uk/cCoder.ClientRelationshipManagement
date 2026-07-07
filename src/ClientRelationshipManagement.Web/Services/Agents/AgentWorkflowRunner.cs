using cCoder.AI.Models.Requests;
using cCoder.AI.Services.Orchestrations;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using ClientRelationshipManagement.Web.Brokers.Loggings;
using ClientRelationshipManagement.Web.Configuration;
using ClientRelationshipManagement.Web.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClientRelationshipManagement.Web.Services.Agents;

public sealed class AgentWorkflowRunner(
    IAgentOrchestrationService agentOrchestrationService,
    IAgentExecutionTokenService agentExecutionTokenService,
    IAgentWorkspaceService agentWorkspaceService,
    IAgentSessionArchiveService agentSessionArchiveService,
    IAgentRunJournalService agentRunJournalService,
    IPlatformDbContextFactory dbContextFactory,
    IOptions<AgentWorkflowOptions> options,
    ILoggingBroker<AgentWorkflowRunner> loggingBroker)
    : IAgentWorkflowRunner
{
    const int TaskExecutionSafetyBufferSeconds = 30;

    public async ValueTask<Guid?> RunTaskAgentAsync(CancellationToken cancellationToken = default)
    {
        AgentWorkflowOptions workflowOptions = options.Value;
        if (!workflowOptions.Enabled || !workflowOptions.TaskAgentEnabled)
        {
            loggingBroker.LogInformation("Task agent run skipped because the workflow or task agent is disabled.");
            return null;
        }

        int dueTaskCount = await CountDueTasksAsync(cancellationToken);
        loggingBroker.LogInformation(
            "Task agent run requested. {DueTaskCount} due workflow task(s) currently require attention.",
            dueTaskCount);

        if (dueTaskCount == 0)
            return await RecordSkippedAsync(
                AgentRunKind.TaskAgent,
                workflowOptions,
                workflowOptions.TaskAgentProvider,
                workflowOptions.TaskAgentModel,
                agentWorkspaceService.GetTaskAgentWorkingDirectory(),
                "No due workflow tasks were available.",
                cancellationToken);

        string workingDirectory = agentWorkspaceService.GetTaskAgentWorkingDirectory();
        string prompt = await agentWorkspaceService.ReadTaskAgentPromptAsync(cancellationToken);
        DateTimeOffset startedOn = DateTimeOffset.UtcNow;
        TimeSpan runBudget = TimeSpan.FromMinutes(Math.Max(1, workflowOptions.TaskAgentIntervalMinutes))
            - TimeSpan.FromSeconds(TaskExecutionSafetyBufferSeconds);

        if (runBudget <= TimeSpan.Zero)
            runBudget = TimeSpan.FromMinutes(1);

        Guid? lastRunId = null;
        Guid? previousTaskId = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            DueTaskSnapshot nextTask = await GetNextDueTaskAsync(cancellationToken);
            if (nextTask is null)
            {
                if (!lastRunId.HasValue)
                {
                    return await RecordSkippedAsync(
                        AgentRunKind.TaskAgent,
                        workflowOptions,
                        workflowOptions.TaskAgentProvider,
                        workflowOptions.TaskAgentModel,
                        workingDirectory,
                        "No due workflow tasks were available.",
                        cancellationToken);
                }

                break;
            }

            if (previousTaskId.HasValue && previousTaskId == nextTask.Id)
            {
                loggingBroker.LogInformation(
                    "Task agent stopped after task {ProcessTaskId} remained the highest-priority due task, indicating that human review is likely required.",
                    nextTask.Id);
                break;
            }

            if (lastRunId.HasValue && DateTimeOffset.UtcNow - startedOn >= runBudget)
            {
                loggingBroker.LogInformation(
                    "Task agent stopped after reaching the configured execution budget of {RunBudget}.",
                    runBudget);
                break;
            }

            string instructions =
                "Fetch the single highest-priority due CRM workflow task by calling GET /Api/AgentWorkflow/Tasks/Due?limit=1. "
                + $"Process only that one task in this run. The expected top task is '{nextTask.Title}' for '{nextTask.CompanyName}' due {nextTask.DueOn:O}. "
                + "If you can safely complete it, do so through the API. "
                + "If it requires a human decision, create the approval message or draft needed and then stop. "
                + "Never try to clear more than one task in this run.";

            lastRunId = await ExecuteAsync(
                AgentRunKind.TaskAgent,
                workflowOptions.ExecutionUserId,
                workflowOptions.TaskAgentProvider,
                workflowOptions.TaskAgentModel,
                workingDirectory,
                prompt,
                instructions,
                1,
                workflowOptions,
                cancellationToken);

            previousTaskId = nextTask.Id;
        }

        return lastRunId;
    }

    public async ValueTask<Guid?> RunProcessOptimiserAsync(CancellationToken cancellationToken = default)
    {
        AgentWorkflowOptions workflowOptions = options.Value;
        if (!workflowOptions.Enabled || !workflowOptions.ProcessOptimiserEnabled)
        {
            loggingBroker.LogInformation("Process optimiser run skipped because the workflow or optimiser is disabled.");
            return null;
        }

        string workingDirectory = agentWorkspaceService.GetProcessOptimiserWorkingDirectory();
        string prompt = await agentWorkspaceService.ReadProcessOptimiserPromptAsync(cancellationToken);
        loggingBroker.LogInformation("Process optimiser run requested.");
        return await ExecuteAsync(
            AgentRunKind.ProcessOptimiser,
            workflowOptions.ExecutionUserId,
            workflowOptions.ProcessOptimiserProvider,
            workflowOptions.ProcessOptimiserModel,
            workingDirectory,
            prompt,
            "Inspect CRM workflow performance and create conservative process draft proposals when the current live process appears not to be working.",
            0,
            workflowOptions,
            cancellationToken);
    }

    async ValueTask<Guid?> ExecuteAsync(
        AgentRunKind kind,
        string executionUserId,
        string provider,
        string model,
        string workingDirectory,
        string prompt,
        string instructions,
        int processedItemCount,
        AgentWorkflowOptions workflowOptions,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(workflowOptions.CrmApiBaseUrl) || string.IsNullOrWhiteSpace(executionUserId))
        {
            loggingBroker.LogWarning("{Kind} agent is enabled but CRM API execution configuration is incomplete.", kind);

            return await RecordSkippedAsync(
                kind,
                workflowOptions,
                provider,
                model,
                workingDirectory,
                "CRM API execution configuration is incomplete.",
                cancellationToken);
        }

        string issuedExecutionToken = await agentExecutionTokenService.IssueAsync(executionUserId);
        if (string.IsNullOrWhiteSpace(issuedExecutionToken))
        {
            loggingBroker.LogWarning(
                "{Kind} agent could not obtain an execution token for user {ExecutionUserId}.",
                kind,
                executionUserId);

            return await RecordSkippedAsync(
                kind,
                workflowOptions,
                provider,
                model,
                workingDirectory,
                $"Unable to issue an execution token for {executionUserId}.",
                cancellationToken);
        }

        var run = await agentRunJournalService.StartAsync(
            kind,
            executionUserId,
            provider,
            model,
            workingDirectory,
            cancellationToken);

        loggingBroker.LogInformation(
            "{Kind} agent run {RunId} started for user {ExecutionUserId} using provider {Provider} and model {Model}.",
            kind,
            run.Id,
            executionUserId,
            provider,
            model);

        try
        {
            var response = await agentOrchestrationService.RunAsync(
                new AgentRunRequest
                {
                    Provider = provider,
                    Model = model,
                    WorkingDirectory = workingDirectory,
                    Instructions = instructions,
                    SystemPrompt = prompt,
                    MaxIterations = workflowOptions.MaxIterations,
                    EnvironmentVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["CRM_AGENT_API_BASE_URL"] = workflowOptions.CrmApiBaseUrl,
                        ["CRM_AGENT_EXECUTION_TOKEN"] = issuedExecutionToken,
                        ["CRM_AGENT_EXECUTION_USER_ID"] = executionUserId
                    }
                },
                cancellationToken);

            await agentRunJournalService.CompleteAsync(
                run.Id,
                response.Succeeded ? AgentRunState.Succeeded : AgentRunState.Failed,
                response.Iterations,
                response.FinalMessage,
                response.Succeeded ? null : response.FinalMessage,
                processedItemCount,
                cancellationToken);

            await agentSessionArchiveService.ArchiveCompletedRunAsync(
                kind,
                run.Id,
                executionUserId,
                provider,
                model,
                workingDirectory,
                prompt,
                instructions,
                processedItemCount,
                response,
                cancellationToken);

            loggingBroker.LogInformation(
                "{Kind} agent run {RunId} completed with state {State} after {Iterations} iteration(s). Processed item count: {ProcessedItemCount}.",
                kind,
                run.Id,
                response.Succeeded ? AgentRunState.Succeeded : AgentRunState.Failed,
                response.Iterations,
                processedItemCount);

            return run.Id;
        }
        catch (Exception exception)
        {
            loggingBroker.LogError(exception, "{Kind} agent execution failed.", kind);

            await agentRunJournalService.CompleteAsync(
                run.Id,
                AgentRunState.Failed,
                0,
                null,
                exception.Message,
                processedItemCount,
                cancellationToken);

            await agentSessionArchiveService.ArchiveFailedRunAsync(
                kind,
                run.Id,
                executionUserId,
                provider,
                model,
                workingDirectory,
                prompt,
                instructions,
                processedItemCount,
                exception,
                cancellationToken);

            loggingBroker.LogWarning(
                "{Kind} agent run {RunId} failed after processing {ProcessedItemCount} item(s).",
                kind,
                run.Id,
                processedItemCount);

            return run.Id;
        }
    }

    async ValueTask<Guid?> RecordSkippedAsync(
        AgentRunKind kind,
        AgentWorkflowOptions workflowOptions,
        string provider,
        string model,
        string workingDirectory,
        string summary,
        CancellationToken cancellationToken)
    {
        var run = await agentRunJournalService.StartAsync(
            kind,
            workflowOptions.ExecutionUserId,
            provider,
            model,
            workingDirectory,
            cancellationToken);

        loggingBroker.LogInformation(
            "{Kind} agent run {RunId} recorded as skipped. Reason: {Summary}",
            kind,
            run.Id,
            summary);

        await agentRunJournalService.CompleteAsync(
            run.Id,
            AgentRunState.Skipped,
            0,
            summary,
            null,
            0,
            cancellationToken);

        return run.Id;
    }

    async ValueTask<int> CountDueTasksAsync(CancellationToken cancellationToken)
    {
        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return await context.ProcessTasks
            .Where(item => item.State == ProcessTaskState.Pending && item.DueOn <= now)
            .CountAsync(cancellationToken);
    }

    async ValueTask<DueTaskSnapshot> GetNextDueTaskAsync(CancellationToken cancellationToken)
    {
        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        var task = await context.ProcessTasks
            .AsNoTracking()
            .Include(item => item.Lead)
            .Include(item => item.TenantCompanyRelationship)
                .ThenInclude(item => item.Company)
            .Where(item => item.State == ProcessTaskState.Pending && item.DueOn <= now)
            .OrderBy(item =>
                item.OpportunityId.HasValue || item.ClientAccountId.HasValue
                    ? 0
                    : item.TenantCompanyRelationshipId.HasValue
                        ? 1
                        : 2)
            .ThenBy(item => item.DueOn)
            .ThenBy(item => item.RenderedTitle)
            .FirstOrDefaultAsync(cancellationToken);

        if (task is null)
            return null;

        return new DueTaskSnapshot(
            task.Id,
            task.DueOn,
            task.RenderedTitle,
            task.TenantCompanyRelationship?.Company is not null
                ? CompanyNames.ResolvePreferredName(task.TenantCompanyRelationship.Company)
                : task.Lead?.RawCompanyName ?? string.Empty);
    }

    sealed record DueTaskSnapshot(Guid Id, DateTimeOffset DueOn, string Title, string CompanyName);
}
