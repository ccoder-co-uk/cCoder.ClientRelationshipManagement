using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
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

public sealed class ProcessController(
    IPlatformDbContextFactory dbContextFactory,
    IWorkflowAutomationService workflowAutomationService,
    ICRMAuthInfo authInfo)
    : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        await workflowAutomationService.EnsureSeedProcessesAsync();

        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        return View(await CreateIndexModelAsync(context));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        await workflowAutomationService.EnsureSeedProcessesAsync();

        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        ProcessEditPageViewModel model = await CreateEditModelAsync(context, id);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDefinition(SaveProcessDefinitionRequest request)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        string tenantId = ResolveTenantId(request.TenantId);

        PlatformEntities.ProcessDefinition definition = request.Id.HasValue
            ? await context.ProcessDefinitions.FirstOrDefaultAsync(item => item.Id == request.Id.Value)
            : null;

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

            context.ProcessDefinitions.Add(definition);
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
            List<PlatformEntities.ProcessDefinition> otherDefaults = await context.ProcessDefinitions
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

        await context.SaveChangesAsync();

        TempData["ProcessNotice"] = request.Id.HasValue
            ? "Process updated."
            : "Process created.";
        return RedirectToAction(nameof(Edit), new { id = definition.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDefinition(Guid id)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        PlatformEntities.ProcessDefinition definition = await context.ProcessDefinitions
            .FirstOrDefaultAsync(item => item.Id == id);

        if (definition is null || !GetWriteableTenantIds().Contains(definition.TenantId))
            return NotFound();

        bool hasInstances = await context.ProcessInstances.AnyAsync(item => item.ProcessDefinitionId == id);
        if (hasInstances)
        {
            TempData["ProcessNotice"] = "This process is already in use and cannot be deleted.";
            return RedirectToAction(nameof(Index));
        }

        List<Guid> stepIds = await context.ProcessSteps
            .Where(item => item.ProcessDefinitionId == id)
            .Select(item => item.Id)
            .ToListAsync();

        List<PlatformEntities.ProcessTransition> transitions = await context.ProcessTransitions
            .Where(item => stepIds.Contains(item.ProcessStepId))
            .ToListAsync();

        List<PlatformEntities.ProcessStep> steps = await context.ProcessSteps
            .Where(item => item.ProcessDefinitionId == id)
            .ToListAsync();

        context.ProcessTransitions.RemoveRange(transitions);
        context.ProcessSteps.RemoveRange(steps);
        context.ProcessDefinitions.Remove(definition);
        await context.SaveChangesAsync();

        TempData["ProcessNotice"] = "Process deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveStep(SaveProcessStepRequest request)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        PlatformEntities.ProcessDefinition definition = await context.ProcessDefinitions
            .FirstOrDefaultAsync(item => item.Id == request.ProcessDefinitionId);

        if (definition is null || !GetWriteableTenantIds().Contains(definition.TenantId))
            return NotFound();

        PlatformEntities.ProcessStep step = request.Id.HasValue
            ? await context.ProcessSteps.FirstOrDefaultAsync(item => item.Id == request.Id.Value)
            : null;

        if (step is null)
        {
            step = new PlatformEntities.ProcessStep
            {
                Id = Guid.NewGuid(),
                CreatedBy = CurrentUserId,
                CreatedOn = DateTimeOffset.UtcNow,
            };

            context.ProcessSteps.Add(step);
        }

        step.ProcessDefinitionId = request.ProcessDefinitionId;
        step.Key = request.Key.Trim();
        step.Name = request.Name.Trim();
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
        step.EmailSubjectTemplate = request.EmailSubjectTemplate?.Trim();
        step.EmailBodyTemplate = request.EmailBodyTemplate?.Trim();
        step.CallScriptTemplate = request.CallScriptTemplate?.Trim();
        step.QuestionSetTemplate = request.QuestionSetTemplate?.Trim();
        step.LastUpdatedBy = CurrentUserId;
        step.LastUpdated = DateTimeOffset.UtcNow;

        if (step.IsEntryPoint)
        {
            List<PlatformEntities.ProcessStep> otherEntrySteps = await context.ProcessSteps
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

        await context.SaveChangesAsync();

        TempData["ProcessNotice"] = request.Id.HasValue
            ? "Process step updated."
            : "Process step created.";
        return RedirectToAction(nameof(Edit), new { id = request.ProcessDefinitionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteStep(Guid id)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        PlatformEntities.ProcessStep step = await context.ProcessSteps
            .FirstOrDefaultAsync(item => item.Id == id);

        if (step is null)
            return NotFound();

        PlatformEntities.ProcessDefinition definition = await context.ProcessDefinitions
            .FirstOrDefaultAsync(item => item.Id == step.ProcessDefinitionId);

        if (definition is null || !GetWriteableTenantIds().Contains(definition.TenantId))
            return NotFound();

        bool hasUsage = await context.ProcessTasks.AnyAsync(item => item.ProcessStepId == id)
            || await context.ProcessInstances.AnyAsync(item => item.CurrentProcessStepId == id);

        if (hasUsage)
        {
            TempData["ProcessNotice"] = "This step is already in use and cannot be deleted.";
            return RedirectToAction(nameof(Edit), new { id = step.ProcessDefinitionId });
        }

        List<PlatformEntities.ProcessTransition> transitions = await context.ProcessTransitions
            .Where(item => item.ProcessStepId == id || item.NextProcessStepId == id)
            .ToListAsync();

        context.ProcessTransitions.RemoveRange(transitions);
        context.ProcessSteps.Remove(step);
        await context.SaveChangesAsync();

        TempData["ProcessNotice"] = "Process step deleted.";
        return RedirectToAction(nameof(Edit), new { id = step.ProcessDefinitionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTransition(SaveProcessTransitionRequest request)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        PlatformEntities.ProcessStep step = await context.ProcessSteps
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.ProcessStepId);

        if (step is null)
            return RedirectToAction(nameof(Index));

        PlatformEntities.ProcessDefinition definition = await context.ProcessDefinitions
            .FirstOrDefaultAsync(item => item.Id == step.ProcessDefinitionId);

        if (definition is null || !GetWriteableTenantIds().Contains(definition.TenantId))
            return NotFound();

        PlatformEntities.ProcessTransition transition = request.Id.HasValue
            ? await context.ProcessTransitions.FirstOrDefaultAsync(item => item.Id == request.Id.Value)
            : null;

        if (transition is null)
        {
            transition = new PlatformEntities.ProcessTransition
            {
                Id = Guid.NewGuid(),
                CreatedBy = CurrentUserId,
                CreatedOn = DateTimeOffset.UtcNow,
            };

            context.ProcessTransitions.Add(transition);
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
            List<PlatformEntities.ProcessTransition> otherDefaults = await context.ProcessTransitions
                .Where(item => item.Id != transition.Id && item.ProcessStepId == request.ProcessStepId && item.IsDefaultOutcome)
                .ToListAsync();

            foreach (PlatformEntities.ProcessTransition other in otherDefaults)
            {
                other.IsDefaultOutcome = false;
                other.LastUpdatedBy = CurrentUserId;
                other.LastUpdated = DateTimeOffset.UtcNow;
            }
        }

        await context.SaveChangesAsync();

        TempData["ProcessNotice"] = request.Id.HasValue
            ? "Transition updated."
            : "Transition created.";
        return RedirectToAction(nameof(Edit), new { id = step.ProcessDefinitionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTransition(Guid id)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        PlatformEntities.ProcessTransition transition = await context.ProcessTransitions
            .FirstOrDefaultAsync(item => item.Id == id);

        if (transition is null)
            return NotFound();

        PlatformEntities.ProcessStep step = await context.ProcessSteps
            .FirstOrDefaultAsync(item => item.Id == transition.ProcessStepId);

        if (step is null)
            return RedirectToAction(nameof(Index));

        PlatformEntities.ProcessDefinition definition = await context.ProcessDefinitions
            .FirstOrDefaultAsync(item => item.Id == step.ProcessDefinitionId);

        if (definition is null || !GetWriteableTenantIds().Contains(definition.TenantId))
            return NotFound();

        context.ProcessTransitions.Remove(transition);
        await context.SaveChangesAsync();

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

    async Task<ProcessIndexPageViewModel> CreateIndexModelAsync(PlatformDbContext context)
    {
        List<PlatformEntities.ProcessDefinition> definitions = await context.ProcessDefinitions
            .AsNoTracking()
            .Where(item =>
                GetReadableTenantIds().Contains(item.TenantId)
                && item.LifecycleState != ProcessDefinitionLifecycleState.Draft)
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

    async Task<ProcessEditPageViewModel> CreateEditModelAsync(PlatformDbContext context, Guid id)
    {
        PlatformEntities.ProcessDefinition definition = await context.ProcessDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id);

        if (definition is null || !GetReadableTenantIds().Contains(definition.TenantId))
            return null;

        List<PlatformEntities.ProcessStep> steps = await context.ProcessSteps
            .AsNoTracking()
            .Where(item => item.ProcessDefinitionId == definition.Id)
            .OrderBy(item => item.Sequence)
            .ThenBy(item => item.Name)
            .ToListAsync();

        List<PlatformEntities.ProcessTransition> transitions = await context.ProcessTransitions
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
                    EmailSubjectTemplate = step.EmailSubjectTemplate ?? string.Empty,
                    EmailBodyTemplate = step.EmailBodyTemplate ?? string.Empty,
                    CallScriptTemplate = step.CallScriptTemplate ?? string.Empty,
                    QuestionSetTemplate = step.QuestionSetTemplate ?? string.Empty,
                    ActionTypeOptions = BuildActionTypeOptions(step.ActionType),
                    RelationshipStatusOptions = BuildRelationshipStatusOptions(step.RelationshipStatusOnActivate),
                    SalesStageOptions = BuildSalesStageOptions(step.SalesStageOnActivate),
                    ClientAccountStatusOptions = BuildClientAccountStatusOptions(step.ClientAccountStatusOnActivate),
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
