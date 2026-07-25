using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using cCoder.ClientRelationshipManagement.Services.Foundations.Platform;
using ClientRelationshipManagement.Web.Services.Processes;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.Web.Services.Agents;

public sealed class ProcessDraftService(IProcessCoordinationService processes) : IProcessDraftService
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
        ProcessDefinition source = await processes.RetrieveDefinitions()
            .Include(item => item.Steps).ThenInclude(step => step.StepTasks)
            .FirstOrDefaultAsync(item => item.Id == sourceProcessDefinitionId, cancellationToken);

        if (source is null)
            return null;

        string proposedName = string.IsNullOrWhiteSpace(name) ? source.Name : name.Trim();
        string proposedDescription = string.IsNullOrWhiteSpace(description) ? source.Description : description.Trim();
        bool definitionChanged = !string.Equals(proposedName, source.Name, StringComparison.Ordinal)
            || !string.Equals(proposedDescription, source.Description, StringComparison.Ordinal);
        bool stepChanged = source.Steps.Any(sourceStep =>
        {
            ProcessStepDraftUpdate update = stepUpdates.FirstOrDefault(item =>
                item.Id == sourceStep.Id || (!string.IsNullOrWhiteSpace(item.Key) &&
                string.Equals(item.Key, sourceStep.Key, StringComparison.OrdinalIgnoreCase)));
            return update is not null && HasEffectiveChange(sourceStep, update);
        });

        if (!definitionChanged && !stepChanged)
            throw new InvalidOperationException("The proposed draft contains no effective changes. Supply at least one value that differs from the current process.");

        List<ProcessTransition> sourceTransitions = await processes.RetrieveTransitions()
            .Where(item => item.ProcessStep.ProcessDefinitionId == source.Id)
            .ToListAsync(cancellationToken);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        Guid familyId = source.FamilyId ?? source.Id;

        int nextVersion = await processes.RetrieveDefinitions()
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
            Name = proposedName,
            Description = proposedDescription,
            IsDefault = false,
            IsActive = false,
            ChangeSummary = string.IsNullOrWhiteSpace(changeSummary) ? null : changeSummary.Trim(),
            ProposedByAgent = string.IsNullOrWhiteSpace(proposedByAgent) ? null : proposedByAgent.Trim(),
            CreatedBy = proposedBy,
            LastUpdatedBy = proposedBy,
            CreatedOn = now,
            LastUpdated = now
        };

        processes.Add(draft);

        Dictionary<Guid, Guid> stepMap = [];
        foreach (ProcessStep sourceStep in source.Steps.OrderBy(item => item.Sequence))
        {
            ProcessStepDraftUpdate update = stepUpdates.FirstOrDefault(item =>
                item.Id == sourceStep.Id || (!string.IsNullOrWhiteSpace(item.Key) &&
                string.Equals(item.Key, sourceStep.Key, StringComparison.OrdinalIgnoreCase)));

            ProcessStep draftStep = new()
            {
                Id = Guid.NewGuid(),
                ProcessDefinitionId = draft.Id,
                Key = sourceStep.Key,
                Name = string.IsNullOrWhiteSpace(update?.Name) ? sourceStep.Name : update.Name.Trim(),
                Objective = update?.Objective ?? sourceStep.Objective,
                RequiredFacts = update?.RequiredFacts ?? sourceStep.RequiredFacts,
                ProducedFacts = update?.ProducedFacts ?? sourceStep.ProducedFacts,
                ViabilityImpact = update?.ViabilityImpact ?? sourceStep.ViabilityImpact,
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
                EmailRecipientTarget = sourceStep.EmailRecipientTarget,
                EmailSubjectTemplate = update?.EmailSubjectTemplate ?? sourceStep.EmailSubjectTemplate,
                EmailBodyTemplate = update?.EmailBodyTemplate ?? sourceStep.EmailBodyTemplate,
                CallScriptTemplate = update?.CallScriptTemplate ?? sourceStep.CallScriptTemplate,
                QuestionSetTemplate = update?.QuestionSetTemplate ?? sourceStep.QuestionSetTemplate,
                CreatedBy = proposedBy,
                LastUpdatedBy = proposedBy,
                CreatedOn = now,
                LastUpdated = now
            };

            processes.Add(draftStep);
            stepMap[sourceStep.Id] = draftStep.Id;
        }

        foreach (ProcessTransition sourceTransition in sourceTransitions)
        {
            processes.Add(new ProcessTransition
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

        await processes.SaveAsync(cancellationToken);
        return draft;
    }

    private static bool HasEffectiveChange(ProcessStep source, ProcessStepDraftUpdate update) =>
        DifferentWhenSupplied(update.Name, source.Name, trim: true)
        || DifferentWhenSupplied(update.Objective, source.Objective)
        || DifferentWhenSupplied(update.RequiredFacts, source.RequiredFacts)
        || DifferentWhenSupplied(update.ProducedFacts, source.ProducedFacts)
        || DifferentWhenSupplied(update.ViabilityImpact, source.ViabilityImpact)
        || DifferentWhenSupplied(update.TaskInstructionsTemplate, source.TaskInstructionsTemplate)
        || DifferentWhenSupplied(update.EmailSubjectTemplate, source.EmailSubjectTemplate)
        || DifferentWhenSupplied(update.EmailBodyTemplate, source.EmailBodyTemplate)
        || DifferentWhenSupplied(update.CallScriptTemplate, source.CallScriptTemplate)
        || DifferentWhenSupplied(update.QuestionSetTemplate, source.QuestionSetTemplate);

    private static bool DifferentWhenSupplied(string proposed, string current, bool trim = false) =>
        proposed is not null &&
        !string.Equals(trim ? proposed.Trim() : proposed, current, StringComparison.Ordinal);

    public async ValueTask<ProcessDefinition> ActivateDraftAsync(
        Guid draftProcessDefinitionId,
        string approvedBy,
        string approvalNotes,
        CancellationToken cancellationToken = default)
    {
        ProcessDefinition draft = await processes.RetrieveWritableDefinitions()
            .FirstOrDefaultAsync(item => item.Id == draftProcessDefinitionId, cancellationToken);

        if (draft is null || draft.LifecycleState != ProcessDefinitionLifecycleState.Draft)
            return null;

        Guid familyId = draft.FamilyId ?? draft.Id;
        List<ProcessDefinition> familyDefinitions = await processes.RetrieveWritableDefinitions()
            .Where(item =>
                item.FamilyId == familyId || item.Id == familyId)
            .ToListAsync(cancellationToken);
        List<ProcessDefinition> activeDefinitions = familyDefinitions
            .Where(item => item.LifecycleState == ProcessDefinitionLifecycleState.Active)
            .ToList();

        await ProcessVersionMigration.MoveActiveInstancesAsync(
            processes,
            draft,
            [.. familyDefinitions.Where(item => item.Id != draft.Id).Select(item => item.Id)],
            approvedBy,
            cancellationToken);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        bool shouldRemainDefault = draft.SupersedesProcessDefinitionId.HasValue
            && await processes.RetrieveDefinitions()
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

        await processes.SaveAsync(cancellationToken);
        return draft;
    }

    public async ValueTask<ProcessDefinition> RejectDraftAsync(
        Guid draftProcessDefinitionId,
        string rejectedBy,
        string rejectionNotes,
        CancellationToken cancellationToken = default)
    {
        ProcessDefinition draft = await processes.RetrieveWritableDefinitions()
            .FirstOrDefaultAsync(item => item.Id == draftProcessDefinitionId, cancellationToken);

        if (draft is null || draft.LifecycleState != ProcessDefinitionLifecycleState.Draft)
            return null;

        DateTimeOffset now = DateTimeOffset.UtcNow;
        draft.IsActive = false;
        draft.IsDefault = false;
        draft.LifecycleState = ProcessDefinitionLifecycleState.Archived;
        draft.ApprovalNotes = string.IsNullOrWhiteSpace(rejectionNotes)
            ? $"Rejected by {rejectedBy}."
            : $"Rejected by {rejectedBy}: {rejectionNotes.Trim()}";
        draft.LastUpdatedBy = rejectedBy;
        draft.LastUpdated = now;
        await processes.SaveAsync(cancellationToken);
        return draft;
    }
}
