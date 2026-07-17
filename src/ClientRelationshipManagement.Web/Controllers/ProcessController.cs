using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using cCoder.ClientRelationshipManagement.Services.Foundations.Platform;
using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.Security.Objects;
using ClientRelationshipManagement.Web.Models.Process;
using ClientRelationshipManagement.Web.Services.Processes;
using ClientRelationshipManagement.Web.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PlatformEntities = cCoder.ClientRelationshipManagement.Platform.Models.Entities;

namespace ClientRelationshipManagement.Web.Controllers;

[Route("Admin/Process")]
public sealed class ProcessController(
    IProcessCoordinationService processWorkspace,
    ISalesCoordinationService salesWorkspace,
    IWorkflowAutomationService workflowAutomationService,
    IProcessValidationService processValidationService,
    ICRMAuthInfo authInfo)
    : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        await workflowAutomationService.EnsureSeedProcessesAsync();

        return View(await CreateIndexModelAsync());
    }

    [HttpGet("Designer")]
    public async Task<IActionResult> Designer(CancellationToken cancellationToken)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        await workflowAutomationService.EnsureSeedProcessesAsync(cancellationToken);
        IReadOnlyCollection<string> tenantIds = GetReadableTenantIds();
        List<PlatformEntities.ProcessDefinition> definitions = await processWorkspace.RetrieveDefinitions()
            .AsNoTracking()
            .Where(item => tenantIds.Contains(item.TenantId) && item.IsActive)
            .Include(item => item.Steps.Where(step => step.IsActive))
                .ThenInclude(step => step.OutgoingTransitions)
            .Include(item => item.Steps.Where(step => step.IsActive))
                .ThenInclude(step => step.StepTasks)
            .OrderBy(item => item.ScopeType)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);
        List<Guid> stepIds = [.. definitions.SelectMany(item => item.Steps).Select(item => item.Id)];
        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<ProcessDesignerStepHealthRow> healthRows = stepIds.Count == 0
            ? []
            : await processWorkspace.RetrieveTasks()
                .AsNoTracking()
                .Where(item => stepIds.Contains(item.ProcessStepId))
                .GroupBy(item => item.ProcessStepId)
                .Select(group => new ProcessDesignerStepHealthRow
                {
                    StepId = group.Key,
                    Pending = group.Count(item => item.State == ProcessTaskState.Pending),
                    Overdue = group.Count(item => item.State == ProcessTaskState.Pending && item.DueOn <= now),
                    Completed = group.Count(item => item.State == ProcessTaskState.Completed),
                    Cancelled = group.Count(item => item.State == ProcessTaskState.Cancelled),
                    AverageMinutes = group
                        .Where(item => item.CompletedOn.HasValue)
                        .Average(item => (double?)EF.Functions.DateDiffSecond(item.CreatedOn, item.CompletedOn!.Value) / 60.0)
                })
                .ToListAsync(cancellationToken);
        Dictionary<Guid, ProcessDesignerStepHealthRow> health = healthRows.ToDictionary(item => item.StepId);
        ProcessValidationResult validation = await processValidationService.ValidateAsync(tenantIds, cancellationToken);

        return View(new ProcessDesignerPageViewModel
        {
            ValidatedOn = validation.ValidatedOn,
            IsValid = validation.IsValid,
            Issues = validation.Issues,
            Lanes = definitions.Select(definition => new ProcessDesignerLaneViewModel
            {
                ProcessDefinitionId = definition.Id,
                TenantId = definition.TenantId,
                ScopeType = definition.ScopeType,
                Name = definition.Name,
                Description = definition.Description ?? string.Empty,
                CssClass = definition.ScopeType switch
                {
                    ProcessScopeType.Lead => "process-lane--lead",
                    ProcessScopeType.Opportunity => "process-lane--opportunity",
                    _ => "process-lane--client"
                },
                Steps = definition.Steps
                    .OrderBy(step => step.Sequence)
                    .ThenBy(step => step.Name)
                    .Select(step =>
                    {
                        health.TryGetValue(step.Id, out ProcessDesignerStepHealthRow stepHealth);
                        return new ProcessDesignerStepViewModel
                        {
                            Id = step.Id,
                            ProcessDefinitionId = step.ProcessDefinitionId,
                            Key = step.Key,
                            Name = step.Name,
                            Sequence = step.Sequence,
                            IsEntryPoint = step.IsEntryPoint,
                            ActionType = DisplayText.Humanize(step.ActionType),
                            Objective = step.Objective ?? string.Empty,
                            RequiredFacts = step.RequiredFacts ?? string.Empty,
                            ProducedFacts = step.ProducedFacts ?? string.Empty,
                            ViabilityImpact = step.ViabilityImpact ?? string.Empty,
                            Health = new ProcessDesignerStepHealthViewModel
                            {
                                Pending = stepHealth?.Pending ?? 0,
                                Overdue = stepHealth?.Overdue ?? 0,
                                Completed = stepHealth?.Completed ?? 0,
                                Cancelled = stepHealth?.Cancelled ?? 0,
                                AverageTurnaroundMinutes = stepHealth?.AverageMinutes
                            },
                            Transitions = step.OutgoingTransitions.Select(transition => new ProcessDesignerTransitionViewModel
                            {
                                Id = transition.Id,
                                NextProcessStepId = transition.NextProcessStepId,
                                OutcomeLabel = transition.OutcomeLabel,
                                IsDefault = transition.IsDefaultOutcome,
                                IsTerminal = transition.IsTerminal
                            }).ToList()
                        };
                    }).ToList()
            }).ToList()
        });
    }

    [HttpGet("WorkflowModel")]
    public async Task<IActionResult> WorkflowModel(CancellationToken cancellationToken)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        Response.Headers.Pragma = "no-cache";
        await workflowAutomationService.EnsureSeedProcessesAsync(cancellationToken);
        IReadOnlyCollection<string> tenantIds = GetReadableTenantIds();
        List<PlatformEntities.ProcessDefinition> definitions = await processWorkspace.RetrieveDefinitions()
            .AsNoTracking()
            .Where(item => tenantIds.Contains(item.TenantId)
                && item.IsActive
                && item.LifecycleState == ProcessDefinitionLifecycleState.Active)
            .Include(item => item.Steps.Where(step => step.IsActive))
                .ThenInclude(step => step.OutgoingTransitions)
            .OrderBy(item => item.ScopeType)
            .ThenByDescending(item => item.IsDefault)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);

        List<Guid> definitionIds = [.. definitions.Select(item => item.Id)];
        DateTimeOffset now = DateTimeOffset.UtcNow;

        List<WorkflowStepIdentityProjection> stepIdentities = await processWorkspace.RetrieveSteps()
            .AsNoTracking()
            .Where(step => tenantIds.Contains(step.ProcessDefinition.TenantId)
                && step.ProcessDefinition.LifecycleState != ProcessDefinitionLifecycleState.Draft)
            .Select(step => new WorkflowStepIdentityProjection
            {
                StepId = step.Id,
                ProcessDefinitionId = step.ProcessDefinitionId,
                TenantId = step.ProcessDefinition.TenantId,
                ScopeType = step.ProcessDefinition.ScopeType,
                StepKey = step.Key
            })
            .ToListAsync(cancellationToken);
        List<Guid> historicalStepIds = [.. stepIdentities.Select(item => item.StepId)];
        Dictionary<Guid, WorkflowStepIdentityProjection> stepIdentityById = stepIdentities
            .ToDictionary(item => item.StepId);

        List<WorkflowStepHealthProjection> healthRows = historicalStepIds.Count == 0
            ? []
            : await processWorkspace.RetrieveTasks()
                .AsNoTracking()
                .Where(item => historicalStepIds.Contains(item.ProcessStepId))
                .GroupBy(item => item.ProcessStepId)
                .Select(group => new WorkflowStepHealthProjection
                {
                    StepId = group.Key,
                    Pending = group.Count(item => item.State == ProcessTaskState.Pending),
                    Running = group.Count(item => item.State == ProcessTaskState.Pending
                        && item.AgentClaimExpiresOn.HasValue
                        && item.AgentClaimExpiresOn > now),
                    Overdue = group.Count(item => item.State == ProcessTaskState.Pending && item.DueOn <= now),
                    Completed = group.Count(item => item.State == ProcessTaskState.Completed),
                    Failed = group.Count(item => item.State == ProcessTaskState.Cancelled),
                    AverageMinutes = group
                        .Where(item => item.CompletedOn.HasValue)
                        .Average(item => (double?)EF.Functions.DateDiffSecond(item.CreatedOn, item.CompletedOn!.Value) / 60.0)
                })
                .ToListAsync(cancellationToken);
        Dictionary<string, WorkflowStepHealthProjection> health = healthRows
            .Where(row => stepIdentityById.ContainsKey(row.StepId))
            .GroupBy(row => LogicalStepKey(stepIdentityById[row.StepId]), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    List<double> averages = group
                        .Where(item => item.AverageMinutes.HasValue)
                        .Select(item => item.AverageMinutes!.Value)
                        .ToList();
                    return new WorkflowStepHealthProjection
                    {
                        Pending = group.Sum(item => item.Pending),
                        Running = group.Sum(item => item.Running),
                        Overdue = group.Sum(item => item.Overdue),
                        Completed = group.Sum(item => item.Completed),
                        Failed = group.Sum(item => item.Failed),
                        AverageMinutes = averages.Count == 0 ? null : averages.Average()
                    };
                },
                StringComparer.OrdinalIgnoreCase);

        List<WorkflowLaneInstanceProjection> instanceRows = await processWorkspace.RetrieveInstances()
            .AsNoTracking()
            .Where(item => tenantIds.Contains(item.ProcessDefinition.TenantId)
                && item.ProcessDefinition.LifecycleState != ProcessDefinitionLifecycleState.Draft)
            .GroupBy(item => new { item.ProcessDefinition.TenantId, item.ProcessDefinition.ScopeType })
            .Select(group => new WorkflowLaneInstanceProjection
            {
                TenantId = group.Key.TenantId,
                ScopeType = group.Key.ScopeType,
                Active = group.Count(item => item.State == ProcessInstanceState.Active),
                Completed = group.Count(item => item.State == ProcessInstanceState.Completed)
            })
            .ToListAsync(cancellationToken);
        Dictionary<string, WorkflowLaneInstanceProjection> instances = instanceRows
            .ToDictionary(item => LogicalLaneKey(item.TenantId, item.ScopeType), StringComparer.OrdinalIgnoreCase);

        List<WorkflowCurrentStepProjection> currentStepRows = definitionIds.Count == 0
            ? []
            : await processWorkspace.RetrieveInstances()
                .AsNoTracking()
                .Where(item => definitionIds.Contains(item.ProcessDefinitionId)
                    && item.State == ProcessInstanceState.Active)
                .GroupBy(item => new { item.ProcessDefinitionId, item.CurrentProcessStepId })
                .Select(group => new WorkflowCurrentStepProjection
                {
                    ProcessDefinitionId = group.Key.ProcessDefinitionId,
                    CurrentProcessStepId = group.Key.CurrentProcessStepId,
                    Count = group.Count()
                })
                .ToListAsync(cancellationToken);
        Dictionary<Guid, int> currentInstancesByStep = currentStepRows
            .Where(item => item.CurrentProcessStepId.HasValue)
            .GroupBy(item => item.CurrentProcessStepId!.Value)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
        Dictionary<Guid, int> unmappedInstancesByDefinition = currentStepRows
            .Where(item => !item.CurrentProcessStepId.HasValue)
            .GroupBy(item => item.ProcessDefinitionId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Count));

        List<WorkflowTransitionOutcomeProjection> outcomeRows = historicalStepIds.Count == 0
            ? []
            : await processWorkspace.RetrieveTasks()
                .AsNoTracking()
                .Where(item => historicalStepIds.Contains(item.ProcessStepId)
                    && item.State == ProcessTaskState.Completed
                    && item.CompletionOutcomeKey != null
                    && item.CompletionOutcomeKey != string.Empty)
                .GroupBy(item => new { item.ProcessStepId, item.CompletionOutcomeKey })
                .Select(group => new WorkflowTransitionOutcomeProjection
                {
                    StepId = group.Key.ProcessStepId,
                    OutcomeKey = group.Key.CompletionOutcomeKey,
                    Count = group.Count()
                })
                .ToListAsync(cancellationToken);
        Dictionary<string, int> historicalOutcomeCounts = outcomeRows
            .Where(row => stepIdentityById.ContainsKey(row.StepId))
            .GroupBy(row => LogicalOutcomeKey(stepIdentityById[row.StepId], row.OutcomeKey), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Count), StringComparer.OrdinalIgnoreCase);

        List<WorkflowLeadCompanyProjection> leadRows = await salesWorkspace.RetrieveLeads()
            .AsNoTracking()
            .Where(lead => tenantIds.Contains(lead.TenantId))
            .Select(lead => new WorkflowLeadCompanyProjection
            {
                CompanyId = lead.CompanyId,
                Status = lead.Status,
                LastUpdated = lead.LastUpdated
            })
            .ToListAsync(cancellationToken);
        Dictionary<Guid, WorkflowLeadCompanyProjection> latestLeads = leadRows
            .GroupBy(item => item.CompanyId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.LastUpdated).First());

        List<WorkflowOpportunityCompanyProjection> opportunityRows = await salesWorkspace.RetrieveOpportunities()
            .AsNoTracking()
            .Where(opportunity => tenantIds.Contains(opportunity.TenantCompanyRelationship.TenantId))
            .Select(opportunity => new WorkflowOpportunityCompanyProjection
            {
                CompanyId = opportunity.TenantCompanyRelationship.CompanyId,
                Stage = opportunity.Stage,
                LastUpdated = opportunity.LastUpdated
            })
            .ToListAsync(cancellationToken);
        Dictionary<Guid, WorkflowOpportunityCompanyProjection> latestOpportunities = opportunityRows
            .GroupBy(item => item.CompanyId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.LastUpdated).First());

        List<WorkflowClientCompanyProjection> clientRows = await salesWorkspace.RetrieveClientAccounts()
            .AsNoTracking()
            .Where(client => tenantIds.Contains(client.TenantCompanyRelationship.TenantId))
            .Select(client => new WorkflowClientCompanyProjection
            {
                CompanyId = client.TenantCompanyRelationship.CompanyId,
                Status = client.Status,
                LastUpdated = client.LastUpdated
            })
            .ToListAsync(cancellationToken);
        Dictionary<Guid, WorkflowClientCompanyProjection> latestClients = clientRows
            .GroupBy(item => item.CompanyId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.LastUpdated).First());

        HashSet<Guid> relationshipCompanyIds = await salesWorkspace.RetrieveRelationships()
            .AsNoTracking()
            .Where(relationship => tenantIds.Contains(relationship.TenantId))
            .Select(relationship => relationship.CompanyId)
            .ToHashSetAsync(cancellationToken);
        HashSet<Guid> clientCompanyIds = [.. latestClients.Keys];
        HashSet<Guid> opportunityCompanyIds = [.. latestOpportunities.Keys.Where(id => !clientCompanyIds.Contains(id))];
        HashSet<Guid> leadCompanyIds =
        [
            .. latestLeads.Keys.Where(id => !clientCompanyIds.Contains(id) && !opportunityCompanyIds.Contains(id))
        ];
        HashSet<Guid> manualRelationshipCompanyIds =
        [
            .. relationshipCompanyIds.Where(id => !clientCompanyIds.Contains(id)
                && !opportunityCompanyIds.Contains(id)
                && !latestLeads.ContainsKey(id))
        ];
        HashSet<Guid> visibleLifecycleCompanyIds =
        [
            .. latestLeads.Keys,
            .. relationshipCompanyIds,
            .. latestOpportunities.Keys,
            .. latestClients.Keys
        ];

        long totalCompanies = await salesWorkspace.RetrieveCompanies().AsNoTracking().LongCountAsync(cancellationToken);
        long candidateCompanies = await salesWorkspace.RetrieveCompanies().AsNoTracking()
            .LongCountAsync(company => !company.IsProspectingSuppressed
                && !company.Relationships.Any()
                && !salesWorkspace.RetrieveLeads().Any(lead => lead.CompanyId == company.Id), cancellationToken);
        long excludedCompanies = await salesWorkspace.RetrieveCompanies().AsNoTracking()
            .LongCountAsync(company => company.IsProspectingSuppressed
                && !company.Relationships.Any()
                && !salesWorkspace.RetrieveLeads().Any(lead => lead.CompanyId == company.Id), cancellationToken);
        long unclassifiedCompanies = Math.Max(
            0,
            totalCompanies - candidateCompanies - excludedCompanies - visibleLifecycleCompanyIds.Count);

        WorkflowCompanyCoverageViewModel companyCoverage = new()
        {
            TotalCompanies = totalCompanies,
            CandidateCompanies = candidateCompanies,
            LeadCompanies = leadCompanyIds.Count,
            OpportunityCompanies = opportunityCompanyIds.Count,
            ClientCompanies = clientCompanyIds.Count,
            ExcludedCompanies = excludedCompanies,
            ManualRelationshipCompanies = manualRelationshipCompanyIds.Count,
            UnclassifiedCompanies = unclassifiedCompanies
        };

        long CurrentStateCount(ProcessScopeType scopeType, ProcessTransitionEffect effect) =>
            (scopeType, effect) switch
            {
                (ProcessScopeType.Lead, ProcessTransitionEffect.DeferLead) => latestLeads.Values.Count(item => item.Status == LeadStatus.Deferred),
                (ProcessScopeType.Lead, ProcessTransitionEffect.RejectLead) => latestLeads.Values.Count(item => item.Status == LeadStatus.Rejected),
                (ProcessScopeType.Lead, ProcessTransitionEffect.QualifyLeadAndCreateOpportunity) => latestLeads.Values.Count(item => item.Status == LeadStatus.Converted),
                (ProcessScopeType.Opportunity, ProcessTransitionEffect.CloseOpportunityAsLost) => latestOpportunities.Values.Count(item => item.Stage == SalesPipelineStage.Lost),
                (ProcessScopeType.Opportunity, ProcessTransitionEffect.CreateClientAccount) => latestClients.Count,
                (ProcessScopeType.Opportunity, ProcessTransitionEffect.CloseOpportunityAsWon) => latestOpportunities.Values.Count(item => item.Stage == SalesPipelineStage.Won),
                (ProcessScopeType.ClientAccount, ProcessTransitionEffect.CloseClientAccount) => latestClients.Values.Count(item => item.Status == ClientAccountStatus.Closed),
                _ => 0
            };

        Dictionary<string, PlatformEntities.ProcessStep> entrySteps = definitions
            .GroupBy(item => $"{item.TenantId}|{item.ScopeType}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.SelectMany(item => item.Steps)
                    .OrderByDescending(item => item.IsEntryPoint)
                    .ThenBy(item => item.Sequence)
                    .FirstOrDefault(),
                StringComparer.OrdinalIgnoreCase);
        Dictionary<Guid, PlatformEntities.ProcessStep> allSteps = definitions
            .SelectMany(item => item.Steps)
            .ToDictionary(item => item.Id);

        ProcessValidationResult validation = await processValidationService.ValidateAsync(tenantIds, cancellationToken);
        WorkflowModelPageViewModel model = new()
        {
            GeneratedOn = now,
            IsValid = validation.IsValid,
            Issues = validation.Issues,
            CompanyCoverage = companyCoverage,
            Processes = definitions.Select(definition =>
            {
                instances.TryGetValue(
                    LogicalLaneKey(definition.TenantId, definition.ScopeType),
                    out WorkflowLaneInstanceProjection processInstances);
                unmappedInstancesByDefinition.TryGetValue(definition.Id, out int unmappedInstances);
                return new WorkflowProcessViewModel
                {
                    Id = definition.Id,
                    TenantId = definition.TenantId,
                    ScopeType = definition.ScopeType,
                    VersionNumber = definition.VersionNumber,
                    Name = definition.Name,
                    Description = definition.Description ?? string.Empty,
                    LaneKey = definition.ScopeType switch
                    {
                        ProcessScopeType.Lead => "lead",
                        ProcessScopeType.Opportunity => "opportunity",
                        _ => "client"
                    },
                    LaneLabel = definition.ScopeType switch
                    {
                        ProcessScopeType.Lead => "Leads",
                        ProcessScopeType.Opportunity => "Opportunities",
                        ProcessScopeType.ClientAccount => "Clients",
                        _ => DisplayText.Humanize(definition.ScopeType)
                    },
                    ActiveInstances = processInstances?.Active ?? 0,
                    CompletedInstances = processInstances?.Completed ?? 0,
                    UnmappedActiveInstances = unmappedInstances,
                    PortalCount = definition.ScopeType switch
                    {
                        ProcessScopeType.Lead => companyCoverage.CandidateCompanies,
                        ProcessScopeType.Opportunity => companyCoverage.OpportunityCompanies,
                        ProcessScopeType.ClientAccount => companyCoverage.ClientCompanies,
                        _ => 0
                    },
                    Steps = definition.Steps
                        .OrderBy(step => step.Sequence)
                        .ThenBy(step => step.Name)
                        .Select(step =>
                        {
                            string logicalStepKey = LogicalStepKey(definition.TenantId, definition.ScopeType, step.Key);
                            health.TryGetValue(logicalStepKey, out WorkflowStepHealthProjection stepHealth);
                            currentInstancesByStep.TryGetValue(step.Id, out int currentInstances);
                            return new WorkflowStepViewModel
                            {
                                Id = step.Id,
                                Key = step.Key,
                                Name = step.Name,
                                Sequence = step.Sequence,
                                IsEntryPoint = step.IsEntryPoint,
                                ActionType = DisplayText.Humanize(step.ActionType),
                                Objective = step.Objective ?? string.Empty,
                                RequiredFacts = step.RequiredFacts ?? string.Empty,
                                ProducedFacts = step.ProducedFacts ?? string.Empty,
                                ViabilityImpact = step.ViabilityImpact ?? string.Empty,
                                TaskTitleTemplate = step.TaskTitleTemplate ?? string.Empty,
                                TaskInstructionsTemplate = step.TaskInstructionsTemplate ?? string.Empty,
                                QuestionSetTemplate = step.QuestionSetTemplate ?? string.Empty,
                                Tasks = step.StepTasks.Where(task => task.IsActive)
                                    .OrderBy(task => task.Sequence)
                                    .Select(task => new WorkflowStepTaskViewModel
                                    {
                                        Key = task.Key,
                                        Name = task.Name,
                                        Sequence = task.Sequence,
                                        Type = DisplayText.Humanize(task.Type),
                                        HandlerKey = task.HandlerKey ?? string.Empty,
                                        MaxAttempts = task.MaxAttempts
                                    }).ToList(),
                                DueAfterDays = step.DueAfterDays,
                                DueAfterHours = step.DueAfterHours,
                                StateOnActivate = DescribeState(
                                    step.RelationshipStatusOnActivate,
                                    step.SalesStageOnActivate,
                                    step.ClientAccountStatusOnActivate),
                                CurrentInstances = currentInstances,
                                Health = new WorkflowStepHealthViewModel
                                {
                                    Pending = stepHealth?.Pending ?? 0,
                                    Running = stepHealth?.Running ?? 0,
                                    Overdue = stepHealth?.Overdue ?? 0,
                                    Completed = stepHealth?.Completed ?? 0,
                                    Failed = stepHealth?.Failed ?? 0,
                                    AverageTurnaroundMinutes = stepHealth?.AverageMinutes
                                },
                                Transitions = step.OutgoingTransitions
                                    .OrderByDescending(item => item.IsDefaultOutcome)
                                    .ThenBy(item => item.OutcomeLabel)
                                    .Select(transition =>
                                    {
                                        allSteps.TryGetValue(transition.NextProcessStepId ?? Guid.Empty, out PlatformEntities.ProcessStep nextStep);
                                        (string graphTargetId, string destinationLabel) = DescribeDestination(
                                            transition,
                                            nextStep,
                                            entrySteps,
                                            definition.TenantId);
                                        historicalOutcomeCounts.TryGetValue(
                                            LogicalOutcomeKey(definition.TenantId, definition.ScopeType, step.Key, transition.OutcomeKey),
                                            out int historicalOutcomeCount);
                                        return new WorkflowTransitionViewModel
                                        {
                                            Id = transition.Id,
                                            OutcomeKey = transition.OutcomeKey,
                                            OutcomeLabel = transition.OutcomeLabel,
                                            NextProcessStepId = transition.NextProcessStepId,
                                            NextStepName = nextStep?.Name ?? string.Empty,
                                            IsDefault = transition.IsDefaultOutcome,
                                            IsTerminal = transition.IsTerminal,
                                            Effect = transition.Effect,
                                            EffectLabel = DisplayText.Humanize(transition.Effect),
                                            ResultingState = DescribeState(
                                                transition.ResultingRelationshipStatus,
                                                transition.ResultingSalesStage,
                                                transition.ResultingClientAccountStatus),
                                            GraphTargetId = graphTargetId,
                                            DestinationLabel = destinationLabel,
                                            HistoricalCompletedCount = historicalOutcomeCount,
                                            CurrentStateCount = CurrentStateCount(definition.ScopeType, transition.Effect)
                                        };
                                    })
                                    .ToList()
                            };
                        })
                        .ToList()
                };
            }).ToList()
        };

        return View(model);
    }

    static string LogicalLaneKey(string tenantId, ProcessScopeType scopeType) =>
        $"{tenantId}|{(int)scopeType}";

    static string LogicalStepKey(WorkflowStepIdentityProjection step) =>
        LogicalStepKey(step.TenantId, step.ScopeType, step.StepKey);

    static string LogicalStepKey(string tenantId, ProcessScopeType scopeType, string stepKey) =>
        $"{LogicalLaneKey(tenantId, scopeType)}|{stepKey}";

    static string LogicalOutcomeKey(WorkflowStepIdentityProjection step, string outcomeKey) =>
        LogicalOutcomeKey(step.TenantId, step.ScopeType, step.StepKey, outcomeKey);

    static string LogicalOutcomeKey(
        string tenantId,
        ProcessScopeType scopeType,
        string stepKey,
        string outcomeKey) =>
        $"{LogicalStepKey(tenantId, scopeType, stepKey)}|{outcomeKey}";

    static (string GraphTargetId, string DestinationLabel) DescribeDestination(
        PlatformEntities.ProcessTransition transition,
        PlatformEntities.ProcessStep nextStep,
        IReadOnlyDictionary<string, PlatformEntities.ProcessStep> entrySteps,
        string tenantId)
    {
        if (nextStep is not null)
            return ($"step-{nextStep.Id:N}", nextStep.Name);

        ProcessScopeType? handoffScope = transition.Effect switch
        {
            ProcessTransitionEffect.QualifyLeadAndCreateOpportunity => ProcessScopeType.Opportunity,
            ProcessTransitionEffect.CreateClientAccount => ProcessScopeType.ClientAccount,
            ProcessTransitionEffect.CloseOpportunityAsWon => ProcessScopeType.ClientAccount,
            _ => null
        };

        if (handoffScope.HasValue
            && entrySteps.TryGetValue($"{tenantId}|{handoffScope.Value}", out PlatformEntities.ProcessStep handoffStep)
            && handoffStep is not null)
        {
            string processLabel = handoffScope == ProcessScopeType.Opportunity
                ? "Opportunity process"
                : "Client process";
            string portal = $"portal-{handoffStep.ProcessDefinitionId:N}";
            return (portal, $"{processLabel} · {handoffStep.Name}");
        }

        string destination = transition.Effect == ProcessTransitionEffect.None
            ? "Process ends"
            : DisplayText.Humanize(transition.Effect);
        return ($"exit-{transition.Id:N}", destination);
    }

    static string DescribeState(
        RelationshipStatus? relationshipStatus,
        SalesPipelineStage? salesStage,
        ClientAccountStatus? clientAccountStatus)
    {
        List<string> values = [];
        if (relationshipStatus.HasValue)
            values.Add($"Relationship: {DisplayText.Humanize(relationshipStatus.Value)}");
        if (salesStage.HasValue)
            values.Add($"Sales stage: {DisplayText.Humanize(salesStage.Value)}");
        if (clientAccountStatus.HasValue)
            values.Add($"Client: {DisplayText.Humanize(clientAccountStatus.Value)}");
        return values.Count == 0 ? "No state change" : string.Join(" · ", values);
    }

    [HttpPost("ReorderSteps")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReorderSteps([FromBody] ReorderProcessStepsRequest request, CancellationToken cancellationToken)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        PlatformEntities.ProcessDefinition definition = await processWorkspace.RetrieveWritableDefinitions()
            .FirstOrDefaultAsync(item => item.Id == request.ProcessDefinitionId, cancellationToken);
        if (definition is null || !GetWriteableTenantIds().Contains(definition.TenantId))
            return NotFound();

        List<PlatformEntities.ProcessStep> steps = await processWorkspace.RetrieveSteps()
            .Where(item => item.ProcessDefinitionId == definition.Id && item.IsActive)
            .ToListAsync(cancellationToken);
        if (request.StepIds.Count != steps.Count || request.StepIds.Distinct().Count() != steps.Count
            || steps.Any(step => !request.StepIds.Contains(step.Id)))
            return BadRequest("The reordered list must contain every active step exactly once.");

        DateTimeOffset now = DateTimeOffset.UtcNow;
        for (int index = 0; index < request.StepIds.Count; index++)
        {
            PlatformEntities.ProcessStep step = steps.First(item => item.Id == request.StepIds[index]);
            step.Sequence = (index + 1) * 10;
            step.LastUpdatedBy = CurrentUserId;
            step.LastUpdated = now;
        }

        await processWorkspace.SaveAsync(cancellationToken);
        return Ok();
    }

    [HttpPost("ConnectSteps")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConnectSteps([FromBody] ConnectProcessStepsRequest request, CancellationToken cancellationToken)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        PlatformEntities.ProcessDefinition definition = await processWorkspace.RetrieveWritableDefinitions()
            .FirstOrDefaultAsync(item => item.Id == request.ProcessDefinitionId, cancellationToken);
        if (definition is null || !GetWriteableTenantIds().Contains(definition.TenantId))
            return NotFound();

        List<PlatformEntities.ProcessStep> steps = await processWorkspace.RetrieveSteps()
            .Where(item => item.ProcessDefinitionId == definition.Id
                && (item.Id == request.FromStepId || item.Id == request.ToStepId)
                && item.IsActive)
            .ToListAsync(cancellationToken);
        if (steps.Count != 2 || request.FromStepId == request.ToStepId)
            return BadRequest("Both steps must be active steps in the same lane.");

        bool exists = await processWorkspace.RetrieveTransitions().AnyAsync(item =>
            item.ProcessStepId == request.FromStepId && item.NextProcessStepId == request.ToStepId,
            cancellationToken);
        if (exists)
            return Ok();

        PlatformEntities.ProcessStep from = steps.First(item => item.Id == request.FromStepId);
        PlatformEntities.ProcessStep to = steps.First(item => item.Id == request.ToStepId);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        bool hasOutgoing = await processWorkspace.RetrieveTransitions().AnyAsync(item => item.ProcessStepId == from.Id, cancellationToken);
        processWorkspace.Add(new PlatformEntities.ProcessTransition
        {
            Id = Guid.NewGuid(),
            ProcessStepId = from.Id,
            NextProcessStepId = to.Id,
            OutcomeKey = $"continue-to-{to.Key}"[..Math.Min(128, $"continue-to-{to.Key}".Length)],
            OutcomeLabel = $"Continue to {to.Name}",
            IsDefaultOutcome = !hasOutgoing,
            IsTerminal = false,
            ProcessStep = from,
            NextProcessStep = to,
            CreatedBy = CurrentUserId,
            LastUpdatedBy = CurrentUserId,
            CreatedOn = now,
            LastUpdated = now
        });
        await processWorkspace.SaveAsync(cancellationToken);
        return Ok();
    }

    [HttpGet("Edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        await workflowAutomationService.EnsureSeedProcessesAsync();

        ProcessEditPageViewModel model = await CreateEditModelAsync(id);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost("SaveDefinition")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDefinition(SaveProcessDefinitionRequest request)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        string tenantId = ResolveTenantId(request.TenantId);

        PlatformEntities.ProcessDefinition definition = request.Id.HasValue
            ? await processWorkspace.RetrieveWritableDefinitions().FirstOrDefaultAsync(item => item.Id == request.Id.Value)
            : null;

        if (definition is not null
            && definition.IsActive
            && !request.IsActive
            && await processWorkspace.RetrieveInstances().AnyAsync(item =>
                item.ProcessDefinitionId == definition.Id && item.State == ProcessInstanceState.Active))
        {
            TempData["ProcessNotice"] = "This is the live graph and cannot be deactivated while companies are following it. Publish an approved replacement version instead.";
            return RedirectToAction(nameof(Edit), new { id = definition.Id });
        }

        if (request.IsActive
            && await processWorkspace.RetrieveDefinitions().AnyAsync(item =>
                item.Id != (request.Id ?? Guid.Empty)
                && item.TenantId == tenantId
                && item.ScopeType == request.ScopeType
                && item.IsActive
                && item.LifecycleState == ProcessDefinitionLifecycleState.Active))
        {
            TempData["ProcessNotice"] = "A live graph already exists for this lane. Create and approve a replacement version so active companies can be migrated safely.";
            return request.Id.HasValue
                ? RedirectToAction(nameof(Edit), new { id = request.Id.Value })
                : RedirectToAction(nameof(Index));
        }

        if (definition is null)
        {
            definition = new PlatformEntities.ProcessDefinition
            {
                Id = Guid.NewGuid(),
                FamilyId = null,
                VersionNumber = 1,
                LifecycleState = request.IsActive
                    ? ProcessDefinitionLifecycleState.Active
                    : ProcessDefinitionLifecycleState.Draft,
                CreatedBy = CurrentUserId,
                CreatedOn = DateTimeOffset.UtcNow,
            };

            processWorkspace.Add(definition);
            definition.FamilyId = definition.Id;
        }

        definition.TenantId = tenantId;
        definition.ScopeType = request.ScopeType;
        definition.Name = request.Name.Trim();
        definition.Description = request.Description?.Trim();
        definition.IsDefault = request.IsDefault;
        definition.IsActive = request.IsActive;
        definition.LifecycleState = request.IsActive
            ? ProcessDefinitionLifecycleState.Active
            : ProcessDefinitionLifecycleState.Draft;
        definition.LastUpdatedBy = CurrentUserId;
        definition.LastUpdated = DateTimeOffset.UtcNow;

        if (definition.IsDefault)
        {
            List<PlatformEntities.ProcessDefinition> otherDefaults = await processWorkspace.RetrieveWritableDefinitions()
                .Where(item =>
                    item.Id != definition.Id
                    && item.TenantId == tenantId
                    && item.ScopeType == request.ScopeType
                    && item.IsDefault)
                .ToListAsync();

            foreach (PlatformEntities.ProcessDefinition other in otherDefaults)
            {
                other.IsDefault = false;
                other.LastUpdatedBy = CurrentUserId;
                other.LastUpdated = DateTimeOffset.UtcNow;
            }
        }

        await processWorkspace.SaveAsync();

        TempData["ProcessNotice"] = request.Id.HasValue
            ? "Process updated."
            : "Process created.";
        return RedirectToAction(nameof(Edit), new { id = definition.Id });
    }

    [HttpPost("DeleteDefinition")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDefinition(Guid id)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        PlatformEntities.ProcessDefinition definition = await processWorkspace.RetrieveWritableDefinitions()
            .FirstOrDefaultAsync(item => item.Id == id);

        if (definition is null || !GetWriteableTenantIds().Contains(definition.TenantId))
            return NotFound();

        bool hasInstances = await processWorkspace.RetrieveInstances().AnyAsync(item => item.ProcessDefinitionId == id);
        if (hasInstances)
        {
            TempData["ProcessNotice"] = "This process is already in use and cannot be deleted.";
            return RedirectToAction(nameof(Index));
        }

        List<Guid> stepIds = await processWorkspace.RetrieveSteps()
            .Where(item => item.ProcessDefinitionId == id)
            .Select(item => item.Id)
            .ToListAsync();

        List<PlatformEntities.ProcessTransition> transitions = await processWorkspace.RetrieveTransitions()
            .Where(item => stepIds.Contains(item.ProcessStepId))
            .ToListAsync();

        List<PlatformEntities.ProcessStep> steps = await processWorkspace.RetrieveSteps()
            .Where(item => item.ProcessDefinitionId == id)
            .ToListAsync();

        processWorkspace.Delete(transitions);
        processWorkspace.Delete(steps);
        processWorkspace.Delete(definition);
        await processWorkspace.SaveAsync();

        TempData["ProcessNotice"] = "Process deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("SaveStep")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveStep(SaveProcessStepRequest request)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        PlatformEntities.ProcessDefinition definition = await processWorkspace.RetrieveWritableDefinitions()
            .FirstOrDefaultAsync(item => item.Id == request.ProcessDefinitionId);

        if (definition is null || !GetWriteableTenantIds().Contains(definition.TenantId))
            return NotFound();

        PlatformEntities.ProcessStep step = request.Id.HasValue
            ? await processWorkspace.RetrieveSteps().FirstOrDefaultAsync(item => item.Id == request.Id.Value)
            : null;
        bool isExistingStep = step is not null;
        bool dueOffsetChanged = isExistingStep
            && (step.DueAfterDays != request.DueAfterDays || step.DueAfterHours != request.DueAfterHours);

        if (isExistingStep
            && step.IsActive
            && !request.IsActive
            && await processWorkspace.RetrieveInstances().AnyAsync(item =>
                item.CurrentProcessStepId == step.Id && item.State == ProcessInstanceState.Active))
        {
            TempData["ProcessNotice"] = "This node has active companies and cannot be disabled. Publish a replacement graph with a safe mapping for the active work.";
            return RedirectToAction(nameof(Edit), new { id = request.ProcessDefinitionId });
        }

        if (step is null)
        {
            step = new PlatformEntities.ProcessStep
            {
                Id = Guid.NewGuid(),
                CreatedBy = CurrentUserId,
                CreatedOn = DateTimeOffset.UtcNow,
            };

            processWorkspace.Add(step);
        }

        step.ProcessDefinitionId = request.ProcessDefinitionId;
        step.Key = request.Key.Trim();
        step.Name = request.Name.Trim();
        step.Objective = request.Objective?.Trim();
        step.RequiredFacts = request.RequiredFacts?.Trim();
        step.ProducedFacts = request.ProducedFacts?.Trim();
        step.ViabilityImpact = request.ViabilityImpact?.Trim();
        step.Sequence = request.Sequence;
        step.IsEntryPoint = request.IsEntryPoint;
        step.IsActive = request.IsActive;
        step.ActionType = request.ActionType;
        step.RelationshipStatusOnActivate = request.RelationshipStatusOnActivate;
        step.SalesStageOnActivate = request.SalesStageOnActivate;
        step.ClientAccountStatusOnActivate = request.ClientAccountStatusOnActivate;
        step.DueAfterDays = request.DueAfterDays;
        step.DueAfterHours = request.DueAfterHours;
        step.TaskTitleTemplate = request.TaskTitleTemplate.Trim();
        step.TaskInstructionsTemplate = request.TaskInstructionsTemplate?.Trim();
        step.EmailRecipientTarget = request.EmailRecipientTarget;
        step.EmailSubjectTemplate = request.EmailSubjectTemplate?.Trim();
        step.EmailBodyTemplate = request.EmailBodyTemplate?.Trim();
        step.CallScriptTemplate = request.CallScriptTemplate?.Trim();
        step.QuestionSetTemplate = request.QuestionSetTemplate?.Trim();
        step.LastUpdatedBy = CurrentUserId;
        step.LastUpdated = DateTimeOffset.UtcNow;

        if (step.IsEntryPoint)
        {
            List<PlatformEntities.ProcessStep> otherEntrySteps = await processWorkspace.RetrieveSteps()
                .Where(item =>
                    item.Id != step.Id
                    && item.ProcessDefinitionId == request.ProcessDefinitionId
                    && item.IsEntryPoint)
                .ToListAsync();

            foreach (PlatformEntities.ProcessStep other in otherEntrySteps)
            {
                other.IsEntryPoint = false;
                other.LastUpdatedBy = CurrentUserId;
                other.LastUpdated = DateTimeOffset.UtcNow;
            }
        }

        await processWorkspace.SaveAsync();

        int rescheduledTaskCount = dueOffsetChanged
            ? await workflowAutomationService.ReschedulePendingTasksForStepAsync(step.Id)
            : 0;

        if (definition.ScopeType == ProcessScopeType.Lead
            && request.Id.HasValue
            && (step.Key == "qualify-lead" || step.Key == "commercial-fit" || step.Key == "company-scale"))
        {
            await workflowAutomationService.ReevaluateDeferredLeadsAsync(definition.TenantId);
        }

        TempData["ProcessNotice"] = request.Id.HasValue
            ? rescheduledTaskCount == 0
                ? "Process step updated."
                : $"Process step updated and {rescheduledTaskCount} pending action{(rescheduledTaskCount == 1 ? "" : "s")} rescheduled."
            : "Process step created.";
        return RedirectToAction(nameof(Edit), new { id = request.ProcessDefinitionId });
    }

    [HttpPost("DeleteStep")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteStep(Guid id)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        PlatformEntities.ProcessStep step = await processWorkspace.RetrieveSteps()
            .FirstOrDefaultAsync(item => item.Id == id);

        if (step is null)
            return NotFound();

        PlatformEntities.ProcessDefinition definition = await processWorkspace.RetrieveWritableDefinitions()
            .FirstOrDefaultAsync(item => item.Id == step.ProcessDefinitionId);

        if (definition is null || !GetWriteableTenantIds().Contains(definition.TenantId))
            return NotFound();

        bool hasUsage = await processWorkspace.RetrieveTasks().AnyAsync(item => item.ProcessStepId == id)
            || await processWorkspace.RetrieveInstances().AnyAsync(item => item.CurrentProcessStepId == id);

        if (hasUsage)
        {
            TempData["ProcessNotice"] = "This step is already in use and cannot be deleted.";
            return RedirectToAction(nameof(Edit), new { id = step.ProcessDefinitionId });
        }

        List<PlatformEntities.ProcessTransition> transitions = await processWorkspace.RetrieveTransitions()
            .Where(item => item.ProcessStepId == id || item.NextProcessStepId == id)
            .ToListAsync();

        processWorkspace.Delete(transitions);
        processWorkspace.Delete(step);
        await processWorkspace.SaveAsync();

        TempData["ProcessNotice"] = "Process step deleted.";
        return RedirectToAction(nameof(Edit), new { id = step.ProcessDefinitionId });
    }

    [HttpPost("SaveTransition")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTransition(SaveProcessTransitionRequest request)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        PlatformEntities.ProcessStep step = await processWorkspace.RetrieveSteps()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.ProcessStepId);

        if (step is null)
            return RedirectToAction(nameof(Index));

        PlatformEntities.ProcessDefinition definition = await processWorkspace.RetrieveWritableDefinitions()
            .FirstOrDefaultAsync(item => item.Id == step.ProcessDefinitionId);

        if (definition is null || !GetWriteableTenantIds().Contains(definition.TenantId))
            return NotFound();

        PlatformEntities.ProcessTransition transition = request.Id.HasValue
            ? await processWorkspace.RetrieveTransitions().FirstOrDefaultAsync(item => item.Id == request.Id.Value)
            : null;

        if (transition is null)
        {
            transition = new PlatformEntities.ProcessTransition
            {
                Id = Guid.NewGuid(),
                CreatedBy = CurrentUserId,
                CreatedOn = DateTimeOffset.UtcNow,
            };

            processWorkspace.Add(transition);
        }

        bool isTerminal = request.IsTerminal || !request.NextProcessIdIsSupplied();

        transition.ProcessStepId = request.ProcessStepId;
        transition.NextProcessStepId = isTerminal ? null : request.NextProcessStepId;
        transition.OutcomeKey = request.OutcomeKey.Trim();
        transition.OutcomeLabel = request.OutcomeLabel.Trim();
        transition.IsDefaultOutcome = request.IsDefaultOutcome;
        transition.IsTerminal = isTerminal;
        transition.Effect = request.Effect;
        transition.ResultingRelationshipStatus = request.ResultingRelationshipStatus;
        transition.ResultingSalesStage = request.ResultingSalesStage;
        transition.ResultingClientAccountStatus = request.ResultingClientAccountStatus;
        transition.LastUpdatedBy = CurrentUserId;
        transition.LastUpdated = DateTimeOffset.UtcNow;

        if (transition.IsDefaultOutcome)
        {
            List<PlatformEntities.ProcessTransition> otherDefaults = await processWorkspace.RetrieveTransitions()
                .Where(item => item.Id != transition.Id && item.ProcessStepId == request.ProcessStepId && item.IsDefaultOutcome)
                .ToListAsync();

            foreach (PlatformEntities.ProcessTransition other in otherDefaults)
            {
                other.IsDefaultOutcome = false;
                other.LastUpdatedBy = CurrentUserId;
                other.LastUpdated = DateTimeOffset.UtcNow;
            }
        }

        await processWorkspace.SaveAsync();

        TempData["ProcessNotice"] = request.Id.HasValue
            ? "Transition updated."
            : "Transition created.";
        return RedirectToAction(nameof(Edit), new { id = step.ProcessDefinitionId });
    }

    [HttpPost("DeleteTransition")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTransition(Guid id)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        PlatformEntities.ProcessTransition transition = await processWorkspace.RetrieveTransitions()
            .FirstOrDefaultAsync(item => item.Id == id);

        if (transition is null)
            return NotFound();

        PlatformEntities.ProcessStep step = await processWorkspace.RetrieveSteps()
            .FirstOrDefaultAsync(item => item.Id == transition.ProcessStepId);

        if (step is null)
            return RedirectToAction(nameof(Index));

        PlatformEntities.ProcessDefinition definition = await processWorkspace.RetrieveWritableDefinitions()
            .FirstOrDefaultAsync(item => item.Id == step.ProcessDefinitionId);

        if (definition is null || !GetWriteableTenantIds().Contains(definition.TenantId))
            return NotFound();

        processWorkspace.Delete(transition);
        await processWorkspace.SaveAsync();

        TempData["ProcessNotice"] = "Transition deleted.";
        return RedirectToAction(nameof(Edit), new { id = step.ProcessDefinitionId });
    }

    IActionResult RedirectIfUnauthenticated()
    {
        if (!string.IsNullOrWhiteSpace(authInfo?.SSOUserId)
            && !string.Equals(authInfo.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            return null;

        string returnUrl = $"{Request.Path}{Request.QueryString}";
        return RedirectToAction("Login", "Account", new { returnUrl });
    }

    async Task<ProcessIndexPageViewModel> CreateIndexModelAsync()
    {
        List<PlatformEntities.ProcessDefinition> definitions = await processWorkspace.RetrieveDefinitions()
            .AsNoTracking()
            .Where(item =>
                GetReadableTenantIds().Contains(item.TenantId)
                && item.LifecycleState == ProcessDefinitionLifecycleState.Active
                && item.IsActive)
            .OrderBy(item => item.ScopeType)
            .ThenByDescending(item => item.IsDefault)
            .ThenBy(item => item.Name)
            .ToListAsync();

        return new ProcessIndexPageViewModel
        {
            Notice = TempData["ProcessNotice"]?.ToString() ?? string.Empty,
            Definitions =
            [
                .. definitions.Select(definition => new ProcessDefinitionSummaryViewModel
                {
                    Id = definition.Id,
                    TenantId = definition.TenantId,
                    ScopeType = definition.ScopeType,
                    Name = definition.Name,
                    IsDefault = definition.IsDefault,
                    IsActive = definition.IsActive
                })
            ],
            NewDefinition = new ProcessDefinitionEditorViewModel
            {
                TenantId = GetWriteableTenantIds().FirstOrDefault() ?? "default",
                ScopeType = ProcessScopeType.Opportunity,
                IsActive = true,
                ScopeTypeOptions = BuildScopeTypeOptions(ProcessScopeType.Opportunity)
            }
        };
    }

    async Task<ProcessEditPageViewModel> CreateEditModelAsync(Guid id)
    {
        PlatformEntities.ProcessDefinition definition = await processWorkspace.RetrieveDefinitions()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id);

        if (definition is null || !GetReadableTenantIds().Contains(definition.TenantId))
            return null;

        List<PlatformEntities.ProcessStep> steps = await processWorkspace.RetrieveSteps()
            .AsNoTracking()
            .Include(item => item.StepTasks)
            .Where(item => item.ProcessDefinitionId == definition.Id)
            .OrderBy(item => item.Sequence)
            .ThenBy(item => item.Name)
            .ToListAsync();

        List<PlatformEntities.ProcessTransition> transitions = await processWorkspace.RetrieveTransitions()
            .AsNoTracking()
            .Where(item => steps.Select(step => step.Id).Contains(item.ProcessStepId))
            .OrderBy(item => item.OutcomeLabel)
            .ToListAsync();

        return new ProcessEditPageViewModel
        {
            Notice = TempData["ProcessNotice"]?.ToString() ?? string.Empty,
            SupportedTokens = WorkflowTemplateRenderer.SupportedTokens,
            Definition = new ProcessDefinitionEditorViewModel
            {
                Id = definition.Id,
                TenantId = definition.TenantId,
                ScopeType = definition.ScopeType,
                Name = definition.Name,
                Description = definition.Description ?? string.Empty,
                IsDefault = definition.IsDefault,
                IsActive = definition.IsActive,
                ScopeTypeOptions = BuildScopeTypeOptions(definition.ScopeType)
            },
            Steps =
            [
                .. steps.Select(step => new ProcessStepEditorViewModel
                {
                    Id = step.Id,
                    ProcessDefinitionId = step.ProcessDefinitionId,
                    Key = step.Key,
                    Name = step.Name,
                    Objective = step.Objective ?? string.Empty,
                    RequiredFacts = step.RequiredFacts ?? string.Empty,
                    ProducedFacts = step.ProducedFacts ?? string.Empty,
                    ViabilityImpact = step.ViabilityImpact ?? string.Empty,
                    Sequence = step.Sequence,
                    IsEntryPoint = step.IsEntryPoint,
                    IsActive = step.IsActive,
                    ActionType = step.ActionType,
                    RelationshipStatusOnActivate = step.RelationshipStatusOnActivate,
                    SalesStageOnActivate = step.SalesStageOnActivate,
                    ClientAccountStatusOnActivate = step.ClientAccountStatusOnActivate,
                    DueAfterDays = step.DueAfterDays,
                    DueAfterHours = step.DueAfterHours,
                    TaskTitleTemplate = step.TaskTitleTemplate,
                    TaskInstructionsTemplate = step.TaskInstructionsTemplate ?? string.Empty,
                    EmailRecipientTarget = step.EmailRecipientTarget,
                    EmailSubjectTemplate = step.EmailSubjectTemplate ?? string.Empty,
                    EmailBodyTemplate = step.EmailBodyTemplate ?? string.Empty,
                    CallScriptTemplate = step.CallScriptTemplate ?? string.Empty,
                    QuestionSetTemplate = step.QuestionSetTemplate ?? string.Empty,
                    ActionTypeOptions = BuildActionTypeOptions(step.ActionType),
                    EmailRecipientTargetOptions = BuildEmailRecipientTargetOptions(step.EmailRecipientTarget),
                    RelationshipStatusOptions = BuildRelationshipStatusOptions(step.RelationshipStatusOnActivate),
                    SalesStageOptions = BuildSalesStageOptions(step.SalesStageOnActivate),
                    ClientAccountStatusOptions = BuildClientAccountStatusOptions(step.ClientAccountStatusOnActivate),
                    Tasks = [.. step.StepTasks.Where(item => item.IsActive).OrderBy(item => item.Sequence).Select(item => new ProcessStepTaskViewModel
                    {
                        Key = item.Key, Name = item.Name, Sequence = item.Sequence, Type = DisplayText.Humanize(item.Type),
                        HandlerKey = item.HandlerKey ?? string.Empty, RequiredContextKeys = item.RequiredContextKeys ?? string.Empty,
                        ProducedContextKeys = item.ProducedContextKeys ?? string.Empty, MaxAttempts = item.MaxAttempts,
                        NextTaskKey = item.NextTaskKey ?? string.Empty, FailureTaskKey = item.FailureTaskKey ?? string.Empty
                    })],
                    Transitions =
                    [
                        .. transitions
                            .Where(transition => transition.ProcessStepId == step.Id)
                            .Select(transition => new ProcessTransitionEditorViewModel
                            {
                                Id = transition.Id,
                                ProcessStepId = transition.ProcessStepId,
                                NextProcessStepId = transition.NextProcessStepId,
                                OutcomeKey = transition.OutcomeKey,
                                OutcomeLabel = transition.OutcomeLabel,
                                IsDefaultOutcome = transition.IsDefaultOutcome,
                                IsTerminal = transition.IsTerminal,
                                Effect = transition.Effect,
                                ResultingRelationshipStatus = transition.ResultingRelationshipStatus,
                                ResultingSalesStage = transition.ResultingSalesStage,
                                ResultingClientAccountStatus = transition.ResultingClientAccountStatus,
                                NextStepOptions = BuildNextStepOptions(steps, step.Id, transition.NextProcessStepId),
                                EffectOptions = BuildTransitionEffectOptions(transition.Effect),
                                RelationshipStatusOptions = BuildRelationshipStatusOptions(transition.ResultingRelationshipStatus),
                                SalesStageOptions = BuildSalesStageOptions(transition.ResultingSalesStage),
                                ClientAccountStatusOptions = BuildClientAccountStatusOptions(transition.ResultingClientAccountStatus)
                            }),
                        new ProcessTransitionEditorViewModel
                        {
                            ProcessStepId = step.Id,
                            Effect = ProcessTransitionEffect.None,
                            NextStepOptions = BuildNextStepOptions(steps, step.Id, null),
                            EffectOptions = BuildTransitionEffectOptions(ProcessTransitionEffect.None),
                            RelationshipStatusOptions = BuildRelationshipStatusOptions(null),
                            SalesStageOptions = BuildSalesStageOptions(null),
                            ClientAccountStatusOptions = BuildClientAccountStatusOptions(null)
                        }
                    ]
                }),
                BuildNewStepEditor(definition.Id)
            ]
        };
    }

    ProcessStepEditorViewModel BuildNewStepEditor(Guid definitionId) =>
        new()
        {
            ProcessDefinitionId = definitionId,
            Sequence = 999,
            IsActive = true,
            ActionType = ProcessActionType.ManualTask,
            ActionTypeOptions = BuildActionTypeOptions(ProcessActionType.ManualTask),
            EmailRecipientTarget = ProcessEmailRecipientTarget.PrimaryContact,
            EmailRecipientTargetOptions = BuildEmailRecipientTargetOptions(ProcessEmailRecipientTarget.PrimaryContact),
            RelationshipStatusOptions = BuildRelationshipStatusOptions(null),
            SalesStageOptions = BuildSalesStageOptions(null),
            ClientAccountStatusOptions = BuildClientAccountStatusOptions(null)
        };

    static IReadOnlyList<SelectListItem> BuildNextStepOptions(
        IReadOnlyList<PlatformEntities.ProcessStep> steps,
        Guid stepId,
        Guid? selectedStepId) =>
        new List<SelectListItem>
        {
            new("Terminal outcome", string.Empty, !selectedStepId.HasValue)
        }
        .Concat(steps
            .Where(candidate => candidate.Id != stepId)
            .OrderBy(candidate => candidate.Sequence)
            .Select(candidate => new SelectListItem(
                $"{candidate.Sequence} | {candidate.Name}",
                candidate.Id.ToString(),
                selectedStepId == candidate.Id)))
        .ToList();

    static IReadOnlyList<SelectListItem> BuildScopeTypeOptions(ProcessScopeType selectedValue) =>
        Enum.GetValues<ProcessScopeType>()
            .Select(value => new SelectListItem(
                DisplayText.Humanize(value),
                value.ToString(),
                value == selectedValue))
            .ToList();

    static IReadOnlyList<SelectListItem> BuildActionTypeOptions(ProcessActionType selectedValue) =>
        Enum.GetValues<ProcessActionType>()
            .Select(value => new SelectListItem(
                DisplayText.Humanize(value),
                value.ToString(),
                value == selectedValue))
            .ToList();

    static IReadOnlyList<SelectListItem> BuildEmailRecipientTargetOptions(ProcessEmailRecipientTarget selectedValue) =>
        Enum.GetValues<ProcessEmailRecipientTarget>()
            .Select(value => new SelectListItem(
                DisplayText.Humanize(value),
                value.ToString(),
                value == selectedValue))
            .ToList();

    static IReadOnlyList<SelectListItem> BuildTransitionEffectOptions(ProcessTransitionEffect selectedValue) =>
        Enum.GetValues<ProcessTransitionEffect>()
            .Select(value => new SelectListItem(
                DisplayText.Humanize(value),
                value.ToString(),
                value == selectedValue))
            .ToList();

    static IReadOnlyList<SelectListItem> BuildRelationshipStatusOptions(RelationshipStatus? selectedValue) =>
    [
        new("No change", string.Empty, !selectedValue.HasValue),
        .. Enum.GetValues<RelationshipStatus>()
            .Select(value => new SelectListItem(
                DisplayText.Humanize(value),
                value.ToString(),
                selectedValue == value))
    ];

    static IReadOnlyList<SelectListItem> BuildSalesStageOptions(SalesPipelineStage? selectedValue) =>
    [
        new("No change", string.Empty, !selectedValue.HasValue),
        .. Enum.GetValues<SalesPipelineStage>()
            .Select(value => new SelectListItem(
                DisplayText.Humanize(value),
                value.ToString(),
                selectedValue == value))
    ];

    static IReadOnlyList<SelectListItem> BuildClientAccountStatusOptions(ClientAccountStatus? selectedValue) =>
    [
        new("No change", string.Empty, !selectedValue.HasValue),
        .. Enum.GetValues<ClientAccountStatus>()
            .Select(value => new SelectListItem(
                DisplayText.Humanize(value),
                value.ToString(),
                selectedValue == value))
    ];

    string ResolveTenantId(string tenantId)
    {
        string normalized = string.IsNullOrWhiteSpace(tenantId) ? string.Empty : tenantId.Trim();

        if (!string.IsNullOrWhiteSpace(normalized))
            return normalized;

        return GetWriteableTenantIds().FirstOrDefault() ?? "default";
    }

    string[] GetReadableTenantIds()
    {
        if (authInfo.WriteableTenants.Length > 0)
            return authInfo.WriteableTenants;

        if (authInfo.ReadableTenants.Length > 0)
            return authInfo.ReadableTenants;

        return ["default"];
    }

    string[] GetWriteableTenantIds()
    {
        if (authInfo.WriteableTenants.Length > 0)
            return authInfo.WriteableTenants;

        return GetReadableTenantIds();
    }

    string CurrentUserId =>
        string.IsNullOrWhiteSpace(authInfo?.SSOUserId)
            ? "system"
            : authInfo.SSOUserId;
}

static class SaveProcessTransitionRequestExtensions
{
    public static bool NextProcessIdIsSupplied(this SaveProcessTransitionRequest request) =>
        request.NextProcessStepId.HasValue;
}
