using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.Web.Services.Agents;

public sealed class ProcessDraftService(IPlatformDbContextFactory dbContextFactory) : IProcessDraftService
{
    public async ValueTask<ProcessDefinition> CreateDraftAsync(
        Guid sourceProcessDefinitionId,
        string proposedBy,
        string proposedByAgent,
        string changeSummary,
        string name,
        string description,
        IReadOnlyList<ProcessStepDraftUpdate> stepUpdates,
        CancellationToken cancellationToken = default)
    {
        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        ProcessDefinition source = await context.ProcessDefinitions
            .Include(item => item.Steps)
            .FirstOrDefaultAsync(item => item.Id == sourceProcessDefinitionId, cancellationToken);

        if (source is null)
            return null;

        List<ProcessTransition> sourceTransitions = await context.ProcessTransitions
            .Where(item => item.ProcessStep.ProcessDefinitionId == source.Id)
            .ToListAsync(cancellationToken);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        Guid familyId = source.FamilyId ?? source.Id;

        int nextVersion = await context.ProcessDefinitions
            .Where(item => item.FamilyId == familyId || item.Id == familyId)
            .MaxAsync(item => (int?)item.VersionNumber, cancellationToken) ?? 1;

        ProcessDefinition draft = new()
        {
            Id = Guid.NewGuid(),
            TenantId = source.TenantId,
            ScopeType = source.ScopeType,
            FamilyId = familyId,
            SupersedesProcessDefinitionId = source.Id,
            VersionNumber = nextVersion + 1,
            LifecycleState = ProcessDefinitionLifecycleState.Draft,
            Name = string.IsNullOrWhiteSpace(name) ? source.Name : name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? source.Description : description.Trim(),
            IsDefault = false,
            IsActive = false,
            ChangeSummary = string.IsNullOrWhiteSpace(changeSummary) ? null : changeSummary.Trim(),
            ProposedByAgent = string.IsNullOrWhiteSpace(proposedByAgent) ? null : proposedByAgent.Trim(),
            CreatedBy = proposedBy,
            LastUpdatedBy = proposedBy,
            CreatedOn = now,
            LastUpdated = now
        };

        context.ProcessDefinitions.Add(draft);

        Dictionary<Guid, Guid> stepMap = [];
        foreach (ProcessStep sourceStep in source.Steps.OrderBy(item => item.Sequence))
        {
            ProcessStepDraftUpdate update = stepUpdates.FirstOrDefault(item =>
                string.Equals(item.Key, sourceStep.Key, StringComparison.OrdinalIgnoreCase));

            ProcessStep draftStep = new()
            {
                Id = Guid.NewGuid(),
                ProcessDefinitionId = draft.Id,
                Key = sourceStep.Key,
                Name = string.IsNullOrWhiteSpace(update?.Name) ? sourceStep.Name : update.Name.Trim(),
                Sequence = sourceStep.Sequence,
                IsEntryPoint = sourceStep.IsEntryPoint,
                IsActive = sourceStep.IsActive,
                ActionType = sourceStep.ActionType,
                RelationshipStatusOnActivate = sourceStep.RelationshipStatusOnActivate,
                SalesStageOnActivate = sourceStep.SalesStageOnActivate,
                ClientAccountStatusOnActivate = sourceStep.ClientAccountStatusOnActivate,
                DueAfterDays = sourceStep.DueAfterDays,
                DueAfterHours = sourceStep.DueAfterHours,
                TaskTitleTemplate = sourceStep.TaskTitleTemplate,
                TaskInstructionsTemplate = update?.TaskInstructionsTemplate ?? sourceStep.TaskInstructionsTemplate,
                EmailSubjectTemplate = update?.EmailSubjectTemplate ?? sourceStep.EmailSubjectTemplate,
                EmailBodyTemplate = update?.EmailBodyTemplate ?? sourceStep.EmailBodyTemplate,
                CallScriptTemplate = update?.CallScriptTemplate ?? sourceStep.CallScriptTemplate,
                QuestionSetTemplate = update?.QuestionSetTemplate ?? sourceStep.QuestionSetTemplate,
                CreatedBy = proposedBy,
                LastUpdatedBy = proposedBy,
                CreatedOn = now,
                LastUpdated = now
            };

            context.ProcessSteps.Add(draftStep);
            stepMap[sourceStep.Id] = draftStep.Id;
        }

        foreach (ProcessTransition sourceTransition in sourceTransitions)
        {
            context.ProcessTransitions.Add(new ProcessTransition
            {
                Id = Guid.NewGuid(),
                ProcessStepId = stepMap[sourceTransition.ProcessStepId],
                NextProcessStepId = sourceTransition.NextProcessStepId.HasValue
                    ? stepMap.GetValueOrDefault(sourceTransition.NextProcessStepId.Value)
                    : null,
                OutcomeKey = sourceTransition.OutcomeKey,
                OutcomeLabel = sourceTransition.OutcomeLabel,
                IsDefaultOutcome = sourceTransition.IsDefaultOutcome,
                IsTerminal = sourceTransition.IsTerminal,
                Effect = sourceTransition.Effect,
                ResultingRelationshipStatus = sourceTransition.ResultingRelationshipStatus,
                ResultingSalesStage = sourceTransition.ResultingSalesStage,
                ResultingClientAccountStatus = sourceTransition.ResultingClientAccountStatus,
                CreatedBy = proposedBy,
                LastUpdatedBy = proposedBy,
                CreatedOn = now,
                LastUpdated = now
            });
        }

        if (source.FamilyId is null)
        {
            source.FamilyId = familyId;
            source.VersionNumber = Math.Max(source.VersionNumber, 1);
            source.LifecycleState = source.IsActive
                ? ProcessDefinitionLifecycleState.Active
                : ProcessDefinitionLifecycleState.Archived;
            source.LastUpdatedBy = proposedBy;
            source.LastUpdated = now;
        }

        await context.SaveChangesAsync(cancellationToken);
        return draft;
    }

    public async ValueTask<ProcessDefinition> ActivateDraftAsync(
        Guid draftProcessDefinitionId,
        string approvedBy,
        string approvalNotes,
        CancellationToken cancellationToken = default)
    {
        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        ProcessDefinition draft = await context.ProcessDefinitions
            .FirstOrDefaultAsync(item => item.Id == draftProcessDefinitionId, cancellationToken);

        if (draft is null || draft.LifecycleState != ProcessDefinitionLifecycleState.Draft)
            return null;

        Guid familyId = draft.FamilyId ?? draft.Id;
        List<ProcessDefinition> activeDefinitions = await context.ProcessDefinitions
            .Where(item =>
                (item.FamilyId == familyId || item.Id == familyId)
                && item.LifecycleState == ProcessDefinitionLifecycleState.Active)
            .ToListAsync(cancellationToken);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        bool shouldRemainDefault = draft.SupersedesProcessDefinitionId.HasValue
            && await context.ProcessDefinitions
                .Where(item => item.Id == draft.SupersedesProcessDefinitionId.Value)
                .Select(item => item.IsDefault)
                .FirstOrDefaultAsync(cancellationToken);

        foreach (ProcessDefinition active in activeDefinitions)
        {
            active.IsActive = false;
            active.IsDefault = false;
            active.LifecycleState = ProcessDefinitionLifecycleState.Archived;
            active.LastUpdatedBy = approvedBy;
            active.LastUpdated = now;
        }

        draft.IsActive = true;
        draft.IsDefault = shouldRemainDefault || draft.IsDefault;
        draft.LifecycleState = ProcessDefinitionLifecycleState.Active;
        draft.ApprovedBy = approvedBy;
        draft.ApprovedOn = now;
        draft.ApprovalNotes = string.IsNullOrWhiteSpace(approvalNotes) ? null : approvalNotes.Trim();
        draft.LastUpdatedBy = approvedBy;
        draft.LastUpdated = now;

        await context.SaveChangesAsync(cancellationToken);
        return draft;
    }
}
