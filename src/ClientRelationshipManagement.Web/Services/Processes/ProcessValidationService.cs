using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using cCoder.ClientRelationshipManagement.Services.Foundations.Platform;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.Web.Services.Processes;

public sealed class ProcessValidationService(IProcessCoordinationService processWorkspace)
    : IProcessValidationService
{
    static readonly string[] InitialCompanyFacts =
    [
        "company.identity",
        "company.status",
        "company.registered-address",
        "company.sic",
        "company.registry-record"
    ];

    public async ValueTask<ProcessValidationResult> ValidateAsync(
        IReadOnlyCollection<string> tenantIds,
        CancellationToken cancellationToken = default)
    {
        if (tenantIds.Count == 0)
            return new ProcessValidationResult { ValidatedOn = DateTimeOffset.UtcNow };

        List<ProcessDefinition> definitions = await processWorkspace.RetrieveDefinitions()
            .AsNoTracking()
            .Where(definition => tenantIds.Contains(definition.TenantId) && definition.IsActive)
            .Include(definition => definition.Steps)
                .ThenInclude(step => step.OutgoingTransitions)
            .Include(definition => definition.Steps)
                .ThenInclude(step => step.StepTasks)
            .OrderBy(definition => definition.TenantId)
            .ThenBy(definition => definition.ScopeType)
            .ThenByDescending(definition => definition.VersionNumber)
            .ToListAsync(cancellationToken);

        List<ProcessValidationIssue> issues = [];
        foreach (IGrouping<string, ProcessDefinition> tenantProcesses in definitions.GroupBy(item => item.TenantId))
        {
            HashSet<string> availableFacts = new(InitialCompanyFacts, StringComparer.OrdinalIgnoreCase);
            foreach (ProcessDefinition definition in tenantProcesses.OrderBy(item => item.ScopeType))
            {
                AddScopeFacts(definition.ScopeType, availableFacts);
                ValidateDefinition(definition, availableFacts, issues);
                foreach (string fact in definition.Steps.Where(step => step.IsActive).SelectMany(step => SplitFacts(step.ProducedFacts)))
                    availableFacts.Add(fact);
            }
        }

        return new ProcessValidationResult
        {
            ValidatedOn = DateTimeOffset.UtcNow,
            Issues = issues
                .OrderByDescending(issue => issue.Severity)
                .ThenBy(issue => issue.TenantId)
                .ThenBy(issue => issue.ProcessName)
                .ThenBy(issue => issue.StepName)
                .ToList()
        };
    }

    public async ValueTask<ProcessValidationResult> ValidateDefinitionAsync(
        Guid processDefinitionId,
        CancellationToken cancellationToken = default)
    {
        ProcessDefinition definition = await processWorkspace.RetrieveDefinitions()
            .AsNoTracking()
            .Where(item => item.Id == processDefinitionId)
            .Include(item => item.Steps)
                .ThenInclude(step => step.OutgoingTransitions)
            .Include(item => item.Steps)
                .ThenInclude(step => step.StepTasks)
            .FirstOrDefaultAsync(cancellationToken);
        if (definition is null)
        {
            return new ProcessValidationResult
            {
                ValidatedOn = DateTimeOffset.UtcNow,
                Issues =
                [
                    new ProcessValidationIssue
                    {
                        Severity = ProcessValidationSeverity.Error,
                        ProcessDefinitionId = processDefinitionId,
                        Code = "missing-process",
                        Message = "The process definition no longer exists."
                    }
                ]
            };
        }

        List<ProcessDefinition> priorLanes = await processWorkspace.RetrieveDefinitions()
            .AsNoTracking()
            .Where(item => item.TenantId == definition.TenantId
                && item.IsActive
                && item.ScopeType < definition.ScopeType)
            .Include(item => item.Steps.Where(step => step.IsActive))
            .OrderBy(item => item.ScopeType)
            .ToListAsync(cancellationToken);
        HashSet<string> availableFacts = new(InitialCompanyFacts, StringComparer.OrdinalIgnoreCase);
        AddScopeFacts(definition.ScopeType, availableFacts);
        foreach (string fact in priorLanes.SelectMany(item => item.Steps).SelectMany(step => SplitFacts(step.ProducedFacts)))
            availableFacts.Add(fact);

        List<ProcessValidationIssue> issues = [];
        ValidateDefinition(definition, availableFacts, issues);
        return new ProcessValidationResult
        {
            ValidatedOn = DateTimeOffset.UtcNow,
            Issues = issues
                .OrderByDescending(issue => issue.Severity)
                .ThenBy(issue => issue.StepName)
                .ToList()
        };
    }

    static void ValidateDefinition(
        ProcessDefinition definition,
        HashSet<string> inheritedFacts,
        List<ProcessValidationIssue> issues)
    {
        List<ProcessStep> steps = definition.Steps
            .Where(step => step.IsActive)
            .OrderBy(step => step.Sequence)
            .ThenBy(step => step.Name)
            .ToList();
        HashSet<Guid> stepIds = [.. steps.Select(step => step.Id)];
        List<ProcessStep> entries = [.. steps.Where(step => step.IsEntryPoint)];

        if (entries.Count != 1)
            AddIssue(issues, ProcessValidationSeverity.Error, definition, null, "entry-count", $"Expected one active entry point but found {entries.Count}.");

        foreach (ProcessStep step in steps)
        {
            ValidateStepTasks(definition, step, issues);
            if (string.IsNullOrWhiteSpace(step.Objective))
                AddIssue(issues, ProcessValidationSeverity.Warning, definition, step, "missing-objective", "The step does not declare what it is trying to achieve.");
            if (string.IsNullOrWhiteSpace(step.ProducedFacts))
                AddIssue(issues, ProcessValidationSeverity.Warning, definition, step, "missing-output", "The step does not declare any information that it adds to company history.");
            if (string.IsNullOrWhiteSpace(step.ViabilityImpact))
                AddIssue(issues, ProcessValidationSeverity.Warning, definition, step, "missing-impact", "The step does not explain its impact on viability or progression.");

            List<ProcessTransition> outgoing = [.. step.OutgoingTransitions];
            if (outgoing.Count == 0)
                AddIssue(issues, ProcessValidationSeverity.Error, definition, step, "dead-end", "The step has no outcome transition.");
            if (outgoing.Count(item => item.IsDefaultOutcome) > 1)
                AddIssue(issues, ProcessValidationSeverity.Error, definition, step, "multiple-defaults", "The step has more than one default outcome.");
            if (outgoing.Count > 1 && outgoing.All(item => !item.IsDefaultOutcome))
                AddIssue(issues, ProcessValidationSeverity.Warning, definition, step, "missing-default", "The step has several outcomes but no declared default.");

            foreach (ProcessTransition transition in outgoing.Where(item => !item.IsTerminal))
            {
                if (!transition.NextProcessStepId.HasValue || !stepIds.Contains(transition.NextProcessStepId.Value))
                    AddIssue(issues, ProcessValidationSeverity.Error, definition, step, "invalid-target", $"Outcome '{transition.OutcomeLabel}' does not link to an active step in this process.");
            }
        }

        if (entries.Count == 1)
        {
            HashSet<Guid> reachable = [];
            Queue<Guid> pending = new([entries[0].Id]);
            while (pending.TryDequeue(out Guid stepId))
            {
                if (!reachable.Add(stepId))
                    continue;
                ProcessStep current = steps.First(step => step.Id == stepId);
                foreach (Guid next in current.OutgoingTransitions
                    .Where(item => !item.IsTerminal && item.NextProcessStepId.HasValue && stepIds.Contains(item.NextProcessStepId.Value))
                    .Select(item => item.NextProcessStepId!.Value))
                    pending.Enqueue(next);
            }

            foreach (ProcessStep unreachable in steps.Where(step => !reachable.Contains(step.Id)))
                AddIssue(issues, ProcessValidationSeverity.Error, definition, unreachable, "unreachable", "The step cannot be reached from the process entry point.");
        }

        HashSet<string> facts = new(inheritedFacts, StringComparer.OrdinalIgnoreCase);
        foreach (ProcessStep step in steps)
        {
            List<string> missing = SplitFacts(step.RequiredFacts).Where(fact => !facts.Contains(fact)).ToList();
            if (missing.Count > 0)
                AddIssue(issues, ProcessValidationSeverity.Error, definition, step, "missing-upstream-facts", $"Required information is not produced upstream: {string.Join(", ", missing)}.");
            foreach (string fact in SplitFacts(step.ProducedFacts))
                facts.Add(fact);
        }
    }

    public async ValueTask<ProcessValidationResult> ValidateActivationAsync(
        Guid processDefinitionId,
        CancellationToken cancellationToken = default)
    {
        ProcessDefinition candidate = await processWorkspace.RetrieveDefinitions().AsNoTracking()
            .Include(item => item.Steps).ThenInclude(step => step.OutgoingTransitions)
            .Include(item => item.Steps).ThenInclude(step => step.StepTasks)
            .FirstOrDefaultAsync(item => item.Id == processDefinitionId, cancellationToken);
        if (candidate is null)
            return await ValidateDefinitionAsync(processDefinitionId, cancellationToken);

        List<ProcessDefinition> model = await processWorkspace.RetrieveDefinitions().AsNoTracking()
            .Where(item => item.TenantId == candidate.TenantId && item.IsActive
                && item.ScopeType != candidate.ScopeType)
            .Include(item => item.Steps).ThenInclude(step => step.OutgoingTransitions)
            .Include(item => item.Steps).ThenInclude(step => step.StepTasks)
            .ToListAsync(cancellationToken);
        model.Add(candidate);

        HashSet<string> facts = new(InitialCompanyFacts, StringComparer.OrdinalIgnoreCase);
        List<ProcessValidationIssue> issues = [];
        foreach (ProcessDefinition definition in model.OrderBy(item => item.ScopeType))
        {
            AddScopeFacts(definition.ScopeType, facts);
            ValidateDefinition(definition, facts, issues);
            foreach (string fact in definition.Steps.Where(step => step.IsActive)
                .SelectMany(step => SplitFacts(step.ProducedFacts)))
                facts.Add(fact);
        }

        return new ProcessValidationResult
        {
            ValidatedOn = DateTimeOffset.UtcNow,
            Issues = issues.OrderByDescending(issue => issue.Severity)
                .ThenBy(issue => issue.ProcessName).ThenBy(issue => issue.StepName).ToList()
        };
    }

    static void AddScopeFacts(ProcessScopeType scopeType, HashSet<string> facts)
    {
        if (scopeType < ProcessScopeType.Lead)
            return;

        // A lead process starts with a persisted lead and its company status. These are
        // input context, not facts that an earlier workflow step needs to manufacture.
        facts.Add("lead.identity");
        facts.Add("lead.company-status");
    }

    static void ValidateStepTasks(
        ProcessDefinition definition,
        ProcessStep step,
        List<ProcessValidationIssue> issues)
    {
        List<ProcessStepTask> tasks = step.StepTasks.Where(item => item.IsActive).OrderBy(item => item.Sequence).ToList();
        if (tasks.Count == 0)
            return;
        HashSet<string> keys = [.. tasks.Select(item => item.Key)];
        if (keys.Count != tasks.Count)
            AddIssue(issues, ProcessValidationSeverity.Error, definition, step, "duplicate-task-key", "The step contains duplicate task keys.");
        if (tasks.GroupBy(item => item.Sequence).Any(group => group.Count() > 1))
            AddIssue(issues, ProcessValidationSeverity.Warning, definition, step, "duplicate-task-sequence", "Two or more tasks share the same sequence.");
        foreach (ProcessStepTask task in tasks)
        {
            if (task.MaxAttempts < 1)
                AddIssue(issues, ProcessValidationSeverity.Error, definition, step, "invalid-task-attempts", $"Task '{task.Name}' must permit at least one attempt.");
            if (task.Type != ProcessStepTaskType.Flow && string.IsNullOrWhiteSpace(task.HandlerKey))
                AddIssue(issues, ProcessValidationSeverity.Error, definition, step, "missing-task-handler", $"Task '{task.Name}' has no registered handler.");
            if (!string.IsNullOrWhiteSpace(task.NextTaskKey) && !keys.Contains(task.NextTaskKey))
                AddIssue(issues, ProcessValidationSeverity.Error, definition, step, "invalid-next-task", $"Task '{task.Name}' routes to missing task '{task.NextTaskKey}'.");
            if (!string.IsNullOrWhiteSpace(task.FailureTaskKey) && !keys.Contains(task.FailureTaskKey))
                AddIssue(issues, ProcessValidationSeverity.Error, definition, step, "invalid-failure-task", $"Task '{task.Name}' routes failures to missing task '{task.FailureTaskKey}'.");
        }
        if (step.ActionType == ProcessActionType.Email
            && (!tasks.Any(item => item.Type == ProcessStepTaskType.Validation)
                || !tasks.Any(item => item.HandlerKey == "CRM.CreateEmailDraft")))
        {
            AddIssue(issues, ProcessValidationSeverity.Error, definition, step, "unsafe-email-tasks",
                "An email step must validate recipient content before invoking CRM.CreateEmailDraft.");
        }
    }

    static IReadOnlyList<string> SplitFacts(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    static void AddIssue(
        List<ProcessValidationIssue> issues,
        ProcessValidationSeverity severity,
        ProcessDefinition definition,
        ProcessStep step,
        string code,
        string message) =>
        issues.Add(new ProcessValidationIssue
        {
            Severity = severity,
            TenantId = definition.TenantId,
            ProcessDefinitionId = definition.Id,
            ProcessName = definition.Name,
            ProcessStepId = step?.Id,
            StepName = step?.Name ?? string.Empty,
            Code = code,
            Message = message
        });
}
