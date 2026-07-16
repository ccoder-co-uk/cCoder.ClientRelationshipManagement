using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.Eventing;
using cCoder.Eventing.Models;
using cCoder.Security.Objects;
using ClientRelationshipManagement.Web.Brokers.Loggings;
using ClientRelationshipManagement.Web.Services.Execution;
using Microsoft.EntityFrameworkCore;
using ClientRelationshipManagement.Web.Services.Mail;
using ClientRelationshipManagement.Web.Utilities;
using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using cCoder.ClientRelationshipManagement.Services.Foundations.Platform;
using ClientRelationshipManagement.Web.Brokers.Storages;
using PlatformEntities = cCoder.ClientRelationshipManagement.Platform.Models.Entities;

namespace ClientRelationshipManagement.Web.Services.Processes;

public sealed class WorkflowAutomationService(
    IWorkflowBroker storage,
    IProcessCoordinationService processWorkspace,
    ICurrentUserMailProfileProvider currentUserMailProfileProvider,
    ICRMAuthInfo authInfo,
    IEventHub eventHub,
    ICurrentExecutionUserAccessor currentExecutionUserAccessor,
    ILoggingBroker<WorkflowAutomationService> loggingBroker)
    : IWorkflowAutomationService
{
    const string DefaultTenantId = "default";
    static readonly EmailState[] ValidEmailStates =
    [
        EmailState.Draft,
        EmailState.Approved,
        EmailState.Sending,
        EmailState.Sent,
        EmailState.Failed,
        EmailState.Cancelled
    ];

    public async ValueTask EnsureSeedProcessesAsync(CancellationToken cancellationToken = default)
    {
        await using var transaction = await storage.BeginTransactionAsync(cancellationToken);
        await storage.ExecuteSqlRawAsync(
            """
            EXEC sp_getapplock
                @Resource = N'crm.process-seed',
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 30000;
            """,
            cancellationToken);

        HashSet<string> tenantIds =
        [
            DefaultTenantId,
            .. authInfo.ReadableTenants,
            .. authInfo.WriteableTenants,
            .. await storage.Leads.AsNoTracking().Select(lead => lead.TenantId).Distinct().ToListAsync(cancellationToken),
            .. await storage.TenantCompanyRelationships.AsNoTracking().Select(relationship => relationship.TenantId).Distinct().ToListAsync(cancellationToken)
        ];

        await ArchiveDuplicateActiveDefinitionsAsync(storage, tenantIds, cancellationToken);

        foreach (string tenantId in tenantIds.Where(tenantId => !string.IsNullOrWhiteSpace(tenantId)))
        {
            await EnsureLeadProcessAsync(storage, tenantId, cancellationToken);
            await EnsureOpportunityProcessAsync(storage, tenantId, cancellationToken);
            await EnsureClientProcessAsync(storage, tenantId, cancellationToken);
        }

        await storage.SaveAsync(cancellationToken);

        foreach (string tenantId in tenantIds.Where(tenantId => !string.IsNullOrWhiteSpace(tenantId)))
            await EnsureProcessContractsAsync(storage, tenantId, cancellationToken);

        await CancelOrphanedDraftsFromArchivedStepsAsync(storage, tenantIds, cancellationToken);
        await MoveActiveInstancesToLiveDefinitionsAsync(storage, tenantIds, cancellationToken);
        await ReclassifyLegacyScoringRejectionsAsync(storage, cancellationToken);
        await storage.SaveAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    async ValueTask CancelOrphanedDraftsFromArchivedStepsAsync(
        IWorkflowBroker storage,
        IReadOnlyCollection<string> tenantIds,
        CancellationToken cancellationToken)
    {
        EmailState[] cancellableStates = [EmailState.Draft, EmailState.Approved, EmailState.Failed];
        List<PlatformEntities.ProcessTask> orphanedTasks = await storage.ProcessTasks
            .Include(task => task.ProcessStep).ThenInclude(step => step.ProcessDefinition)
            .Include(task => task.Email)
            .Where(task => task.State != ProcessTaskState.Pending
                && task.EmailId.HasValue
                && task.Email != null
                && cancellableStates.Contains(task.Email.State)
                && task.ProcessStep.ProcessDefinition.LifecycleState == ProcessDefinitionLifecycleState.Archived
                && tenantIds.Contains(task.ProcessStep.ProcessDefinition.TenantId))
            .ToListAsync(cancellationToken);

        if (orphanedTasks.Count == 0)
            return;

        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach (PlatformEntities.ProcessTask task in orphanedTasks)
        {
            task.Email!.State = EmailState.Cancelled;
            task.Email.ApprovedBy = null;
            task.Email.ApprovedOn = null;
            task.Email.ScheduledSendTimeUtc = null;
            task.Email.LastUpdatedBy = CurrentUserId;
            task.Email.LastUpdated = now;
        }
    }

    async ValueTask MoveActiveInstancesToLiveDefinitionsAsync(
        IWorkflowBroker storage,
        IReadOnlyCollection<string> tenantIds,
        CancellationToken cancellationToken)
    {
        List<PlatformEntities.ProcessDefinition> definitions = await storage.ProcessDefinitions
            .Where(definition => tenantIds.Contains(definition.TenantId))
            .OrderByDescending(definition => definition.IsDefault)
            .ThenByDescending(definition => definition.VersionNumber)
            .ToListAsync(cancellationToken);

        foreach (IGrouping<(string TenantId, ProcessScopeType ScopeType), PlatformEntities.ProcessDefinition> lane in definitions
            .GroupBy(definition => (definition.TenantId, definition.ScopeType)))
        {
            PlatformEntities.ProcessDefinition liveDefinition = lane.FirstOrDefault(definition =>
                definition.IsActive && definition.LifecycleState == ProcessDefinitionLifecycleState.Active);
            if (liveDefinition is null)
                continue;

            await ProcessVersionMigration.MoveActiveInstancesAsync(
                processWorkspace,
                liveDefinition,
                [.. lane.Where(definition => definition.Id != liveDefinition.Id
                        && definition.LifecycleState == ProcessDefinitionLifecycleState.Archived)
                    .Select(definition => definition.Id)],
                CurrentUserId,
                cancellationToken);
        }

        await processWorkspace.SaveAsync(cancellationToken);
    }

    public async ValueTask<int> ReschedulePendingTasksForStepAsync(
        Guid processStepId,
        CancellationToken cancellationToken = default)
    {
        PlatformEntities.ProcessStep step = await storage.ProcessSteps
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == processStepId, cancellationToken);

        if (step is null)
            return 0;

        List<PlatformEntities.ProcessTask> pendingTasks = await storage.ProcessTasks
            .Where(item => item.ProcessStepId == processStepId && item.State == ProcessTaskState.Pending)
            .ToListAsync(cancellationToken);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        int rescheduled = 0;

        foreach (PlatformEntities.ProcessTask task in pendingTasks)
        {
            DateTimeOffset revisedDueOn = task.CreatedOn
                .AddDays(step.DueAfterDays)
                .AddHours(step.DueAfterHours);

            if (task.DueOn == revisedDueOn)
                continue;

            task.DueOn = revisedDueOn;
            task.LastUpdatedBy = CurrentUserId;
            task.LastUpdated = now;
            rescheduled++;
        }

        if (rescheduled > 0)
            await storage.SaveAsync(cancellationToken);

        return rescheduled;
    }

    public async ValueTask<RelatedEmailDraftContext> GetRelatedEmailDraftContextAsync(
        Guid agentMessageId,
        CancellationToken cancellationToken = default)
    {
        RelatedEmailDraftSource source = await ResolveRelatedEmailDraftSourceAsync(
            storage,
            agentMessageId,
            cancellationToken);
        if (source is null)
            return null;

        await storage.SaveAsync(cancellationToken);
        return MapRelatedEmailDraftContext(source);
    }

    public async ValueTask<RelatedEmailDraftRefreshResult> RefreshRelatedEmailDraftsAsync(
        Guid agentMessageId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await storage.BeginTransactionAsync(cancellationToken);
        RelatedEmailDraftSource source = await ResolveRelatedEmailDraftSourceAsync(
            storage,
            agentMessageId,
            cancellationToken);
        if (source is null)
            return null;

        string[] writeableTenants = authInfo.WriteableTenants.Length > 0
            ? authInfo.WriteableTenants
            : [DefaultTenantId];
        if (!writeableTenants.Contains(source.Definition.TenantId, StringComparer.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("The agent cannot change email drafts for this tenant.");

        if (source.ApprovedCorrection is not null && !source.LiveTemplateMatchesApprovedCorrection)
        {
            throw new InvalidOperationException(
                $"The human-approved correction does not match the current live template for {source.Step.Name} ({source.Step.Key}). " +
                "Create a draft process proposal for this exact step and wait for human approval before refreshing related drafts.");
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<Guid> updatedEmailIds = [];
        foreach (PlatformEntities.ProcessTask task in source.Tasks)
        {
            TaskRenderContext renderContext = await BuildTaskRenderContextAsync(
                storage,
                task.LeadId,
                task.TenantCompanyRelationshipId,
                task.OpportunityId,
                task.ClientAccountId,
                cancellationToken);
            TaskRenderValues rendered = RenderTaskValues(source.Step, renderContext, now);
            string subject = string.IsNullOrWhiteSpace(rendered.EmailSubject)
                ? task.RenderedEmailSubject
                : rendered.EmailSubject.Trim();
            string body = string.IsNullOrWhiteSpace(rendered.EmailBody)
                ? task.RenderedEmailBody
                : rendered.EmailBody.Trim();

            if (string.IsNullOrWhiteSpace(subject)
                || string.IsNullOrWhiteSpace(body)
                || RecipientEmailContentValidator.ContainsInternalDraftingGuidance(body))
            {
                continue;
            }

            PlatformEntities.Email email = task.Email;
            bool changed = !string.Equals(email.Subject, subject, StringComparison.Ordinal)
                || !string.Equals(email.BodyText ?? email.BodyHtml, body, StringComparison.Ordinal)
                || email.State != EmailState.Draft;
            if (!changed)
                continue;

            task.RenderedTitle = string.IsNullOrWhiteSpace(rendered.Title) ? source.Step.Name : rendered.Title;
            task.RenderedInstructions = rendered.Instructions;
            task.RenderedEmailSubject = subject;
            task.RenderedEmailBody = body;
            task.RenderedCallScript = rendered.CallScript;
            task.RenderedQuestionSet = rendered.QuestionSet;
            task.LastUpdatedBy = CurrentUserId;
            task.LastUpdated = now;

            email.Subject = subject;
            email.BodyHtml = body;
            email.BodyText = body;
            email.IsBodyHtml = false;
            email.State = EmailState.Draft;
            email.ApprovedBy = null;
            email.ApprovedOn = null;
            email.ScheduledSendTimeUtc = null;
            email.LastError = null;
            email.LastUpdatedBy = CurrentUserId;
            email.LastUpdated = now;

            if (email.Material is not null)
            {
                email.Material.Name = subject;
                email.Material.Notes = body;
                email.Material.Status = MaterialStatus.Draft;
                email.Material.LastUpdatedBy = CurrentUserId;
                email.Material.LastUpdated = now;
            }

            PlatformEntities.Activity activity = email.MaterialId.HasValue
                ? await storage.Activities.FirstOrDefaultAsync(
                    item => item.MaterialId == email.MaterialId.Value,
                    cancellationToken)
                : null;
            if (activity is not null)
            {
                activity.Summary = subject;
                activity.Outcome = body;
                activity.LastUpdatedBy = CurrentUserId;
                activity.LastUpdated = now;
            }

            updatedEmailIds.Add(email.Id);
        }

        storage.Add(new PlatformEntities.AgentMessageEntry
        {
            Id = Guid.NewGuid(),
            AgentMessageId = source.Message.Id,
            Role = "System",
            Body = $"CRM inspected {source.Tasks.Count} unsent email draft(s) from {source.Step.Name} ({source.Step.Key}) and refreshed {updatedEmailIds.Count} from the current live template. Refreshed emails remain Draft and require human approval.",
            CreatedBy = CurrentUserId,
            LastUpdatedBy = CurrentUserId,
            CreatedOn = now,
            LastUpdated = now
        });
        source.Message.LastUpdatedBy = CurrentUserId;
        source.Message.LastUpdated = now;
        await storage.SaveAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new RelatedEmailDraftRefreshResult(
            MapRelatedEmailDraftContext(source),
            source.Tasks.Count,
            updatedEmailIds.Count,
            updatedEmailIds);
    }

    public async ValueTask<int> EnsureDefinitionCoverageAsync(
        Guid processDefinitionId,
        CancellationToken cancellationToken = default)
    {
        List<PlatformEntities.ProcessInstance> instances = await storage.ProcessInstances
            .Where(instance => instance.ProcessDefinitionId == processDefinitionId
                && instance.State == ProcessInstanceState.Active)
            .ToListAsync(cancellationToken);

        foreach (PlatformEntities.ProcessInstance instance in instances)
            await EnsurePendingTaskAsync(storage, instance, cancellationToken);

        await storage.SaveAsync(cancellationToken);
        return instances.Count;
    }

    async ValueTask<RelatedEmailDraftSource> ResolveRelatedEmailDraftSourceAsync(
        IWorkflowBroker storage,
        Guid agentMessageId,
        CancellationToken cancellationToken)
    {
        PlatformEntities.AgentMessage message = await storage.AgentMessages
            .Include(item => item.Email).ThenInclude(item => item.TenantCompanyRelationship).ThenInclude(item => item.Company)
            .Include(item => item.Email).ThenInclude(item => item.CompanyContact)
            .Include(item => item.Email).ThenInclude(item => item.Opportunity)
            .Include(item => item.Email).ThenInclude(item => item.ClientAccount)
            .FirstOrDefaultAsync(item => item.Id == agentMessageId, cancellationToken);
        if (message?.Email is null)
            return null;

        string[] readableTenants = authInfo.ReadableTenants.Length > 0
            ? authInfo.ReadableTenants
            : authInfo.WriteableTenants.Length > 0 ? authInfo.WriteableTenants : [DefaultTenantId];
        string tenantId = message.Email.TenantCompanyRelationship?.TenantId ?? message.TenantId;
        if (!readableTenants.Contains(tenantId, StringComparer.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("The agent cannot inspect email drafts for this tenant.");

        PlatformEntities.ProcessTask provenanceTask = message.ProcessTaskId.HasValue
            ? await storage.ProcessTasks
                .Include(item => item.ProcessStep)
                .Include(item => item.ProcessInstance).ThenInclude(item => item.ProcessDefinition)
                .FirstOrDefaultAsync(item => item.Id == message.ProcessTaskId.Value, cancellationToken)
            : null;
        provenanceTask ??= await storage.ProcessTasks
            .Include(item => item.ProcessStep)
            .Include(item => item.ProcessInstance).ThenInclude(item => item.ProcessDefinition)
            .OrderByDescending(item => item.CreatedOn)
            .FirstOrDefaultAsync(item => item.EmailId == message.EmailId, cancellationToken);

        Guid? definitionId = provenanceTask?.ProcessInstance?.ProcessDefinitionId ?? message.ProcessDefinitionId;
        PlatformEntities.ProcessDefinition sourceDefinition = definitionId.HasValue
            ? await storage.ProcessDefinitions.FirstOrDefaultAsync(item => item.Id == definitionId.Value, cancellationToken)
            : null;
        if (sourceDefinition is null)
        {
            List<PlatformEntities.ProcessDefinition> activeDefinitions = await storage.ProcessDefinitions
                .AsNoTracking()
                .Include(item => item.Steps)
                .Where(item => item.TenantId == tenantId
                    && item.IsActive
                    && item.LifecycleState == ProcessDefinitionLifecycleState.Active)
                .ToListAsync(cancellationToken);
            List<PlatformEntities.ProcessDefinition> subjectMatches = activeDefinitions
                .Where(definition => definition.Steps.Any(step =>
                    step.IsActive
                    && step.ActionType == ProcessActionType.Email
                    && string.Equals(
                        WorkflowTemplateRenderer.Render(
                            step.EmailSubjectTemplate,
                            null,
                            message.Email.TenantCompanyRelationship?.Company,
                            message.Email.CompanyContact,
                            message.Email.TenantCompanyRelationship,
                            message.Email.Opportunity,
                            message.Email.ClientAccount,
                            DateTimeOffset.UtcNow,
                            null)?.Trim(),
                        message.Email.Subject?.Trim(),
                        StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (subjectMatches.Count == 1)
                sourceDefinition = subjectMatches[0];
        }
        sourceDefinition ??= await storage.ProcessDefinitions
            .Where(item => item.TenantId == tenantId
                && item.IsActive
                && item.LifecycleState == ProcessDefinitionLifecycleState.Active
                && (message.Email.OpportunityId.HasValue
                    ? item.ScopeType == ProcessScopeType.Opportunity
                    : item.ScopeType == ProcessScopeType.ClientAccount))
            .OrderByDescending(item => item.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (sourceDefinition is null)
            throw new InvalidOperationException("CRM could not identify the live process that produced the rejected email.");

        Guid familyId = sourceDefinition.FamilyId ?? sourceDefinition.Id;
        PlatformEntities.ProcessDefinition liveDefinition = await storage.ProcessDefinitions
            .Where(item => (item.Id == familyId || item.FamilyId == familyId || item.Id == sourceDefinition.Id)
                && item.IsActive
                && item.LifecycleState == ProcessDefinitionLifecycleState.Active)
            .OrderByDescending(item => item.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken)
            ?? sourceDefinition;

        string stepKey = message.ProcessStepId.HasValue
            ? await storage.ProcessSteps.Where(item => item.Id == message.ProcessStepId.Value)
                .Select(item => item.Key).FirstOrDefaultAsync(cancellationToken)
            : provenanceTask?.ProcessStep?.Key;
        PlatformEntities.ProcessStep liveStep = !string.IsNullOrWhiteSpace(stepKey)
            ? await storage.ProcessSteps.FirstOrDefaultAsync(
                item => item.ProcessDefinitionId == liveDefinition.Id && item.Key == stepKey,
                cancellationToken)
            : null;

        if (liveStep is null)
        {
            List<PlatformEntities.ProcessStep> emailSteps = await storage.ProcessSteps
                .Where(item => item.ProcessDefinitionId == liveDefinition.Id
                    && item.IsActive
                    && item.ActionType == ProcessActionType.Email)
                .OrderBy(item => item.Sequence)
                .ToListAsync(cancellationToken);
            List<PlatformEntities.ProcessStep> subjectMatches = emailSteps.Where(step =>
            {
                string renderedSubject = WorkflowTemplateRenderer.Render(
                    step.EmailSubjectTemplate,
                    null,
                    message.Email.TenantCompanyRelationship?.Company,
                    message.Email.CompanyContact,
                    message.Email.TenantCompanyRelationship,
                    message.Email.Opportunity,
                    message.Email.ClientAccount,
                    DateTimeOffset.UtcNow,
                    null);
                return string.Equals(
                    renderedSubject?.Trim(),
                    message.Email.Subject?.Trim(),
                    StringComparison.OrdinalIgnoreCase);
            }).ToList();

            liveStep = subjectMatches.Count == 1
                ? subjectMatches[0]
                : emailSteps.Count(step => step.EmailRecipientTarget != ProcessEmailRecipientTarget.AccountOwner) == 1
                    ? emailSteps.Single(step => step.EmailRecipientTarget != ProcessEmailRecipientTarget.AccountOwner)
                    : null;
        }

        if (liveStep is null)
            throw new InvalidOperationException("CRM could not identify one unambiguous source process step for the rejected email.");

        message.ProcessDefinitionId = liveDefinition.Id;
        message.ProcessStepId = liveStep.Id;
        message.ProcessTaskId ??= provenanceTask?.Id;
        message.LastUpdatedBy = CurrentUserId;
        message.LastUpdated = DateTimeOffset.UtcNow;

        Guid[] familyDefinitionIds = await storage.ProcessDefinitions
            .Where(item => item.Id == familyId || item.FamilyId == familyId || item.Id == liveDefinition.Id)
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);
        EmailState[] eligibleStates = [EmailState.Draft, EmailState.Approved, EmailState.Failed];
        List<PlatformEntities.ProcessTask> tasks = await storage.ProcessTasks
            .Include(item => item.ProcessStep)
            .Include(item => item.ProcessInstance)
            .Include(item => item.Email).ThenInclude(item => item.Material)
            .Include(item => item.Email).ThenInclude(item => item.TenantCompanyRelationship).ThenInclude(item => item.Company)
            .Where(item => item.State == ProcessTaskState.Pending
                && item.EmailId.HasValue
                && item.Email != null
                && eligibleStates.Contains(item.Email.State)
                && familyDefinitionIds.Contains(item.ProcessInstance.ProcessDefinitionId)
                && item.ProcessStep.Key == liveStep.Key)
            .OrderBy(item => item.CreatedOn)
            .ToListAsync(cancellationToken);

        PlatformEntities.AgentMessage correctionMessage = await storage.AgentMessages
            .AsNoTracking()
            .Include(item => item.Email)
            .Where(item => item.CorrelationKey == $"approval:replacement-email:{message.Id}"
                && item.EmailId.HasValue)
            .OrderByDescending(item => item.CreatedOn)
            .FirstOrDefaultAsync(cancellationToken);
        PlatformEntities.Email approvedCorrection = correctionMessage?.Email is not null
            && correctionMessage.Email.State is EmailState.Approved or EmailState.Sending or EmailState.Sent
                ? correctionMessage.Email
                : null;

        TaskRenderContext sourceRenderContext = await BuildTaskRenderContextAsync(
            storage,
            provenanceTask?.LeadId,
            message.Email.TenantCompanyRelationshipId,
            message.Email.OpportunityId ?? message.OpportunityId,
            message.Email.ClientAccountId,
            cancellationToken);
        TaskRenderValues liveRendered = RenderTaskValues(liveStep, sourceRenderContext, DateTimeOffset.UtcNow);
        string liveSubject = liveRendered.EmailSubject?.Trim();
        string liveBody = liveRendered.EmailBody?.Trim();
        bool liveTemplateMatchesApprovedCorrection = approvedCorrection is null
            || (EmailContentEquals(liveSubject, approvedCorrection.Subject)
                && EmailContentEquals(liveBody, approvedCorrection.BodyText ?? approvedCorrection.BodyHtml));

        return new RelatedEmailDraftSource(
            message,
            liveDefinition,
            liveStep,
            tasks,
            approvedCorrection,
            liveSubject,
            liveBody,
            liveTemplateMatchesApprovedCorrection);
    }

    static bool EmailContentEquals(string left, string right) => string.Equals(
        NormalizeEmailContent(left),
        NormalizeEmailContent(right),
        StringComparison.Ordinal);

    static string NormalizeEmailContent(string value) => string.IsNullOrWhiteSpace(value)
        ? string.Empty
        : value.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();

    static RelatedEmailDraftContext MapRelatedEmailDraftContext(RelatedEmailDraftSource source) => new(
        source.Message.Id,
        source.Definition.TenantId,
        source.Definition.Id,
        source.Definition.Name,
        source.Step.Id,
        source.Step.Key,
        source.Step.Name,
        source.Step.EmailSubjectTemplate,
        source.Step.EmailBodyTemplate,
        source.ApprovedCorrection is null
            ? null
            : new RelatedEmailCorrectionReference(
                source.ApprovedCorrection.Id,
                source.ApprovedCorrection.State,
                source.ApprovedCorrection.Subject,
                source.ApprovedCorrection.BodyText ?? source.ApprovedCorrection.BodyHtml),
        source.LiveTemplateRenderedSubject,
        source.LiveTemplateRenderedBody,
        source.LiveTemplateMatchesApprovedCorrection,
        [.. source.Tasks.Select(task => new RelatedEmailDraftItem(
            task.Id,
            task.Email.Id,
            CompanyNames.ResolvePreferredName(task.Email.TenantCompanyRelationship?.Company),
            task.Email.ToAddresses,
            task.Email.Subject,
            task.Email.State,
            RecipientEmailContentValidator.ContainsInternalDraftingGuidance(task.Email.BodyText ?? task.Email.BodyHtml))) ]);

    async ValueTask EnsureProcessContractsAsync(
        IWorkflowBroker storage,
        string tenantId,
        CancellationToken cancellationToken)
    {
        await EnsureRequiredEvidenceStepsAsync(storage, tenantId, cancellationToken);
        await storage.SaveAsync(cancellationToken);

        List<PlatformEntities.ProcessStep> steps = await storage.ProcessSteps
            .Where(step =>
                step.ProcessDefinition.TenantId == tenantId
                && step.ProcessDefinition.IsActive
                && step.IsActive)
            .Include(step => step.ProcessDefinition)
            .ToListAsync(cancellationToken);

        foreach (PlatformEntities.ProcessStep step in steps)
        {
            ProcessStepContract contract = GetSeedContract(step.ProcessDefinition.ScopeType, step.Key);
            if (contract is null)
                continue;

            step.Objective = contract.Objective;
            step.RequiredFacts = contract.RequiredFacts;
            step.ProducedFacts = contract.ProducedFacts;
            step.ViabilityImpact = contract.ViabilityImpact;
            step.LastUpdatedBy = CurrentUserId;
            step.LastUpdated = DateTimeOffset.UtcNow;
        }
    }

    async ValueTask EnsureRequiredEvidenceStepsAsync(
        IWorkflowBroker storage,
        string tenantId,
        CancellationToken cancellationToken)
    {
        List<PlatformEntities.ProcessDefinition> definitions = await storage.ProcessDefinitions
            .Where(definition => definition.TenantId == tenantId && definition.IsActive)
            .Include(definition => definition.Steps)
                .ThenInclude(step => step.OutgoingTransitions)
            .ToListAsync(cancellationToken);

        PlatformEntities.ProcessDefinition lead = definitions.FirstOrDefault(item => item.ScopeType == ProcessScopeType.Lead);
        if (lead is not null && lead.Steps.All(step => step.Key != "company-scale"))
        {
            PlatformEntities.ProcessStep activity = lead.Steps.FirstOrDefault(step => step.Key == "company-activity");
            PlatformEntities.ProcessStep quality = lead.Steps.FirstOrDefault(step => step.Key == "verify-company");
            if (activity is not null && quality is not null)
            {
                PlatformEntities.ProcessStep scale = NewStep(
                    lead, "company-scale", "Assess Company Scale", 25, false, ProcessActionType.Research,
                    null, null, null, 0, 0,
                    "Assess the organisational scale of {{Lead.RawCompanyName}}",
                    "Record the strongest available evidence for employee count, annual revenue, and scale band. Use known registry, accounts, website, and existing CRM evidence only. Never guess: record unknown where evidence is unavailable, include the source, and give a confidence level.",
                    null, null, null,
                    "Employee count: integer or unknown.\nAnnual revenue: amount and currency or unknown.\nScale band: enterprise, large, medium, small, micro, or unknown.\nEvidence: concise source description.\nConfidence: high, medium, or low.");
                storage.Add(scale);
                foreach (PlatformEntities.ProcessTransition transition in activity.OutgoingTransitions.Where(item => item.NextProcessStepId == quality.Id))
                    transition.NextProcessStepId = scale.Id;
                storage.Add(NewTransition(scale, quality, "scale-assessed", "Company scale assessed", true, ProcessTransitionEffect.None));
            }
        }

        if (lead is not null)
        {
            PlatformEntities.ProcessStep qualify = lead.Steps.FirstOrDefault(step => step.Key == "qualify-lead");
            if (qualify is not null)
            {
                foreach (PlatformEntities.ProcessTransition transition in qualify.OutgoingTransitions)
                    transition.IsDefaultOutcome = transition.OutcomeKey == "deferred";

                if (qualify.OutgoingTransitions.All(transition => transition.OutcomeKey != "deferred"))
                {
                    storage.Add(NewTransition(
                        qualify,
                        null,
                        "deferred",
                        "Defer until qualification criteria change",
                        true,
                        ProcessTransitionEffect.DeferLead,
                        true));
                }
            }
        }

        PlatformEntities.ProcessDefinition opportunity = definitions.FirstOrDefault(item => item.ScopeType == ProcessScopeType.Opportunity);
        if (opportunity is not null && opportunity.Steps.All(step => step.Key != "opportunity-summary"))
        {
            PlatformEntities.ProcessStep handoff = opportunity.Steps.FirstOrDefault(step => step.Key == "handoff-account-owner");
            if (handoff is not null)
            {
                PlatformEntities.ProcessStep summary = NewStep(
                    opportunity, "opportunity-summary", "Build Opportunity Summary", 45, false, ProcessActionType.Review,
                    RelationshipStatus.ActiveOpportunity, SalesPipelineStage.DiscoveryBooked, null, 0, 0,
                    "Build the opportunity brief for {{Company.OfficialName}}",
                    "Using only the company history and the confirmed response, write a compact factual summary of what the contact wants, the pain or need, why our offer could help, and the evidence that they are interested in at least a demo. Mark unknown commercial values as unknown; do not invent them.",
                    null, null, null,
                    "Opportunity summary: two sentences maximum.\nPain or need: one sentence or unknown.\nValue hypothesis: one sentence or unknown.\nDemo interest evidence: exact concise evidence.\nEstimated annual value: amount or unknown.\nConfidence: high, medium, or low.");
                storage.Add(summary);

                foreach (PlatformEntities.ProcessStep step in opportunity.Steps)
                foreach (PlatformEntities.ProcessTransition transition in step.OutgoingTransitions.Where(item => item.NextProcessStepId == handoff.Id))
                    transition.NextProcessStepId = summary.Id;

                storage.Add(NewTransition(summary, handoff, "summary-ready", "Opportunity brief ready", true, ProcessTransitionEffect.None));
            }
        }

        PlatformEntities.ProcessDefinition client = definitions.FirstOrDefault(item => item.ScopeType == ProcessScopeType.ClientAccount);
        if (client is not null && client.Steps.All(step => step.Key != "client-baseline"))
        {
            PlatformEntities.ProcessStep review = client.Steps.FirstOrDefault(step => step.Key == "client-status-review");
            if (review is not null)
            {
                PlatformEntities.ProcessStep baseline = NewStep(
                    client, "client-baseline", "Record Client Baseline", 5, true, ProcessActionType.Review,
                    RelationshipStatus.Onboarding, SalesPipelineStage.Won, ClientAccountStatus.Onboarding, 0, 0,
                    "Record the relationship baseline for {{Company.OfficialName}}",
                    "From the approved opportunity handoff and contract decision, record the agreed service scope, key client and internal contacts, expected review cadence, and any known commitments or risks. Record unknown where the signed position is not available and assign a human follow-up instead of guessing.",
                    null, null, null,
                    "Service scope: concise agreed scope or unknown.\nKey contacts: names and responsibilities.\nReview cadence: interval or unknown.\nCommitments and risks: concise list or none known.\nConfidence: high, medium, or low.");
                review.IsEntryPoint = false;
                storage.Add(baseline);
                storage.Add(NewTransition(baseline, review, "baseline-recorded", "Client baseline recorded", true, ProcessTransitionEffect.None));
            }
        }
    }

    async ValueTask ReclassifyLegacyScoringRejectionsAsync(
        IWorkflowBroker storage,
        CancellationToken cancellationToken)
    {
        List<PlatformEntities.Lead> candidates = await storage.Leads
            .Include(lead => lead.Company)
            .Where(lead => lead.Status == LeadStatus.Rejected
                && lead.Company != null
                && lead.Company.IsProspectingSuppressed
                && lead.Company.ProspectingSuppressedReason == "Lead rejected: Reject lead")
            .ToListAsync(cancellationToken);

        foreach (PlatformEntities.Lead lead in candidates)
        {
            bool coherentIdentity = Regex.IsMatch(
                lead.QualificationNotes ?? string.Empty,
                @"(?im)^Identity coherent:\s*yes\b|^Identity result:\s*(matched|partially matched)\b",
                RegexOptions.CultureInvariant);
            bool inactive = lead.Company.DissolvedOn.HasValue || Regex.IsMatch(
                lead.Company.CompanyStatus ?? string.Empty,
                "dissolved|liquidation|removed|closed|inactive|converted-closed",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            bool wasScored = Regex.IsMatch(
                lead.QualificationNotes ?? string.Empty,
                @"(?im)^Fit score:\s*\d+\b",
                RegexOptions.CultureInvariant);
            if (!coherentIdentity || inactive || !wasScored)
                continue;

            lead.Status = LeadStatus.Deferred;
            lead.LastUpdatedBy = CurrentUserId;
            lead.LastUpdated = DateTimeOffset.UtcNow;
            lead.Company.IsProspectingSuppressed = false;
            lead.Company.ProspectingSuppressedReason = null;
            lead.Company.ProspectingSuppressedOn = null;
            lead.Company.LastUpdatedBy = CurrentUserId;
            lead.Company.LastUpdated = DateTimeOffset.UtcNow;
        }
    }

    static ProcessStepContract GetSeedContract(ProcessScopeType scopeType, string stepKey) =>
        (scopeType, stepKey) switch
        {
            (ProcessScopeType.Lead, "lead-research") => new(
                "Confirm that the imported record identifies one coherent legal entity.",
                "company.identity, company.status, company.registered-address",
                "company.identity-verification",
                "Gate: false or irreconcilable identity stops progression; uncertainty is retained for remediation."),
            (ProcessScopeType.Lead, "company-activity") => new(
                "Establish a concise, evidence-backed description of the company's primary activity.",
                "company.identity, company.sic",
                "company.primary-activity",
                "Evidence: the activity description informs fit without deciding it."),
            (ProcessScopeType.Lead, "company-scale") => new(
                "Establish the strongest evidence available for organisational scale without guessing.",
                "company.identity, company.primary-activity",
                "company.scale",
                "Evidence: scale supports prioritisation and fit; unknown evidence holds only decisions that require a size threshold."),
            (ProcessScopeType.Lead, "verify-company") => new(
                "Assess whether the record has enough reliable evidence for commercial scoring.",
                "company.identity-verification, company.primary-activity, company.status",
                "company.data-quality",
                "Gate: missing critical evidence is held for remediation; missing optional evidence is recorded."),
            (ProcessScopeType.Lead, "commercial-fit") => new(
                "Score whether this organisation is commercially worth pursuing using verified evidence only.",
                "company.identity-verification, company.primary-activity, company.data-quality, company.scale",
                "company.fit-score, company.opening-angle",
                "Score: increases or decreases qualification viability and supplies the first outreach angle."),
            (ProcessScopeType.Lead, "qualify-lead") => new(
                "Apply the declared qualification rule without doing additional research.",
                "company.identity-verification, company.status, company.scale, company.fit-score",
                "company.qualification-decision",
                "Gate: qualified creates an opportunity; inactive or proven-invalid records stop; current low-fit or insufficient-evidence records are deferred for later reassessment."),
            (ProcessScopeType.Opportunity, "confirm-route") => new(
                "Identify a usable decision-maker or contact route for the qualified opportunity.",
                "company.qualification-decision, company.opening-angle",
                "company.primary-contact, opportunity.outreach-route",
                "Gate: outreach cannot begin until a credible recipient and route are known."),
            (ProcessScopeType.Opportunity, "intro-email") => new(
                "Send a relevant first message through the confirmed outreach route.",
                "company.primary-contact, opportunity.outreach-route, company.opening-angle",
                "opportunity.outreach-sent",
                "Progress: starts a measurable commercial conversation; external contact requires the configured approval policy."),
            (ProcessScopeType.Opportunity, "review-response") => new(
                "Classify reply evidence, or the absence of it, and choose the next bounded action.",
                "opportunity.outreach-sent",
                "opportunity.latest-response, opportunity.response-classification, opportunity.demo-interest",
                "Gate: positive demo interest advances; no reply schedules follow-up; rejection closes the opportunity."),
            (ProcessScopeType.Opportunity, "follow-up-call") => new(
                "Make one focused follow-up and record whether a live opportunity exists.",
                "company.primary-contact, opportunity.outreach-sent",
                "opportunity.follow-up-outcome, opportunity.demo-interest",
                "Gate: demonstrated interest advances; no route forward closes; uncertainty returns to response review."),
            (ProcessScopeType.Opportunity, "opportunity-summary") => new(
                "Turn the confirmed conversation into a concise, evidence-backed opportunity brief.",
                "opportunity.latest-response, opportunity.demo-interest",
                "opportunity.summary, opportunity.pain, opportunity.value-hypothesis",
                "Evidence: supplies the account owner with what was asked, the need, the potential, and explicit unknowns."),
            (ProcessScopeType.Opportunity, "handoff-account-owner") => new(
                "Give the account owner a complete, evidence-backed opportunity brief once demo interest is confirmed.",
                "opportunity.latest-response, opportunity.demo-interest, opportunity.summary, opportunity.pain, opportunity.value-hypothesis",
                "opportunity.handoff-pack",
                "Handoff: transfers responsibility to a human without losing the evidence gathered by the AI workflow."),
            (ProcessScopeType.Opportunity, "account-owner-decision") => new(
                "Record the accountable human decision after demo, commercials, and contract negotiation.",
                "opportunity.handoff-pack",
                "opportunity.demo-outcome, opportunity.commercial-terms, opportunity.contract-status, opportunity.client-decision",
                "Gate: a positive contracted decision creates a client; otherwise negotiation continues or the opportunity closes."),
            (ProcessScopeType.ClientAccount, "client-baseline") => new(
                "Create the factual baseline needed to manage the new client consistently.",
                "opportunity.client-decision, opportunity.handoff-pack",
                "client.baseline, client.service-scope, client.key-contacts",
                "Foundation: missing contractual or ownership facts are assigned for human follow-up before routine maintenance."),
            (ProcessScopeType.ClientAccount, "client-status-review") => new(
                "Keep the client relationship current through a regular, evidence-backed housekeeping review.",
                "client.baseline, client.service-scope, client.key-contacts",
                "client.health, client.risks, client.opportunities, client.next-action",
                "Health: identifies retention risk, service changes, and expansion potential before scheduling the next review."),
            _ => null
        };

    sealed record ProcessStepContract(
        string Objective,
        string RequiredFacts,
        string ProducedFacts,
        string ViabilityImpact);

    public async ValueTask EnsureCoverageAsync(
        Guid? leadId = null,
        Guid? opportunityId = null,
        Guid? clientAccountId = null,
        bool forceCreate = false,
        CancellationToken cancellationToken = default)
    {
        await EnsureSeedProcessesAsync(cancellationToken);


        await NormaliseImportedCompanyNamesAsync(storage, cancellationToken);
        await NormaliseImportedEmailStatesAsync(storage, opportunityId, clientAccountId, cancellationToken);

        List<PlatformEntities.Lead> leads = await storage.Leads
            .Where(leadId.HasValue ? lead => lead.Id == leadId.Value : _ => true)
            .OrderBy(lead => lead.CreatedOn)
            .ToListAsync(cancellationToken);

        List<PlatformEntities.Opportunity> opportunities = await storage.Opportunities
            .Where(opportunityId.HasValue ? opportunity => opportunity.Id == opportunityId.Value : _ => true)
            .OrderBy(opportunity => opportunity.CreatedOn)
            .ToListAsync(cancellationToken);

        List<PlatformEntities.ClientAccount> clientAccounts = await storage.ClientAccounts
            .Where(clientAccountId.HasValue ? clientAccount => clientAccount.Id == clientAccountId.Value : _ => true)
            .OrderBy(clientAccount => clientAccount.CreatedOn)
            .ToListAsync(cancellationToken);

        foreach (PlatformEntities.Lead lead in leads)
            await EnsureLeadCoverageAsync(storage, lead, forceCreate, cancellationToken);

        foreach (PlatformEntities.Opportunity opportunity in opportunities)
            await EnsureOpportunityCoverageAsync(storage, opportunity, forceCreate, cancellationToken);

        foreach (PlatformEntities.ClientAccount clientAccount in clientAccounts)
            await EnsureClientCoverageAsync(storage, clientAccount, forceCreate, cancellationToken);

        await storage.SaveAsync(cancellationToken);
        await SynchroniseCurrentTasksAsync(storage, cancellationToken);
    }

    public async ValueTask<int> ReevaluateDeferredLeadsAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        List<PlatformEntities.Lead> leads = await storage.Leads
            .Where(lead => lead.TenantId == tenantId && lead.Status == LeadStatus.Deferred)
            .ToListAsync(cancellationToken);
        foreach (PlatformEntities.Lead lead in leads)
        {
            lead.Status = LeadStatus.Imported;
            lead.LastUpdatedBy = CurrentUserId;
            lead.LastUpdated = DateTimeOffset.UtcNow;
            await EnsureLeadCoverageAsync(storage, lead, true, cancellationToken);
        }

        await storage.SaveAsync(cancellationToken);
        return leads.Count;
    }

    public async ValueTask<PlatformEntities.ProcessTask> CompleteTaskAsync(
        ProcessTaskCompletionCommand command,
        CancellationToken cancellationToken = default)
    {

        PlatformEntities.ProcessTask task = await storage.ProcessTasks
            .Include(item => item.ProcessInstance)
            .Include(item => item.ProcessStep)
            .FirstOrDefaultAsync(
                item => item.Id == command.ProcessTaskId && item.State == ProcessTaskState.Pending,
                cancellationToken);

        if (task is null)
            return null;

        await CompleteTaskAsync(storage, task, command.OutcomeKey, command.CompletionNote, cancellationToken);
        await storage.SaveAsync(cancellationToken);
        return task;
    }

    public async ValueTask<bool> CompleteEmailTaskAsync(Guid emailId, CancellationToken cancellationToken = default)
    {

        PlatformEntities.ProcessTask task = await storage.ProcessTasks
            .Include(item => item.ProcessInstance)
            .Include(item => item.ProcessStep)
            .FirstOrDefaultAsync(
                item => item.EmailId == emailId && item.State == ProcessTaskState.Pending,
                cancellationToken);

        if (task is null)
            return false;

        await CompleteTaskAsync(storage, task, "sent", "Email sent.", cancellationToken);
        await storage.SaveAsync(cancellationToken);
        return true;
    }

    async ValueTask EnsureLeadCoverageAsync(
        IWorkflowBroker storage,
        PlatformEntities.Lead lead,
        bool forceCreate,
        CancellationToken cancellationToken)
    {
        PlatformEntities.ProcessInstance activeInstance = await storage.ProcessInstances
            .FirstOrDefaultAsync(
                item => item.LeadId == lead.Id && item.State == ProcessInstanceState.Active,
                cancellationToken);

        if (lead.Status is LeadStatus.Deferred or LeadStatus.Rejected or LeadStatus.Converted)
        {
            await CloseActiveInstanceAsync(storage, activeInstance, lead.Status.ToString(), cancellationToken);
            return;
        }

        if (activeInstance is null)
        {
            PlatformEntities.ProcessDefinition definition = await GetDefaultDefinitionAsync(storage, lead.TenantId, ProcessScopeType.Lead, cancellationToken);
            PlatformEntities.ProcessStep entryStep = await GetEntryStepAsync(storage, definition.Id, cancellationToken);
            if (entryStep is null)
                return;

            activeInstance = new PlatformEntities.ProcessInstance
            {
                Id = Guid.NewGuid(),
                ProcessDefinitionId = definition.Id,
                LeadId = lead.Id,
                CurrentProcessStepId = entryStep.Id,
                State = ProcessInstanceState.Active,
                StartedOn = DateTimeOffset.UtcNow,
                CreatedBy = CurrentUserId,
                LastUpdatedBy = CurrentUserId,
                CreatedOn = DateTimeOffset.UtcNow,
                LastUpdated = DateTimeOffset.UtcNow
            };

            storage.Add(activeInstance);
        }

        if (!forceCreate && await storage.ProcessTasks.AnyAsync(
                item => item.ProcessInstanceId == activeInstance.Id && item.State == ProcessTaskState.Pending,
                cancellationToken))
        {
            return;
        }

        await EnsurePendingTaskAsync(storage, activeInstance, cancellationToken);
    }

    async ValueTask EnsureOpportunityCoverageAsync(
        IWorkflowBroker storage,
        PlatformEntities.Opportunity opportunity,
        bool forceCreate,
        CancellationToken cancellationToken)
    {
        PlatformEntities.ProcessInstance activeInstance = await storage.ProcessInstances
            .FirstOrDefaultAsync(
                item => item.OpportunityId == opportunity.Id && item.State == ProcessInstanceState.Active,
                cancellationToken);

        if (opportunity.Stage is SalesPipelineStage.Won or SalesPipelineStage.Lost)
        {
            await CloseActiveInstanceAsync(storage, activeInstance, opportunity.Stage.ToString(), cancellationToken);
            return;
        }

        if (activeInstance is null)
        {
            string tenantId = await storage.TenantCompanyRelationships
                .Where(relationship => relationship.Id == opportunity.TenantCompanyRelationshipId)
                .Select(relationship => relationship.TenantId)
                .FirstOrDefaultAsync(cancellationToken);

            PlatformEntities.ProcessDefinition definition = await GetDefaultDefinitionAsync(storage, tenantId, ProcessScopeType.Opportunity, cancellationToken);
            PlatformEntities.ProcessStep inferredStep = await ResolveOpportunityStepAsync(storage, definition.Id, opportunity, cancellationToken);
            if (inferredStep is null)
                return;

            activeInstance = new PlatformEntities.ProcessInstance
            {
                Id = Guid.NewGuid(),
                ProcessDefinitionId = definition.Id,
                OpportunityId = opportunity.Id,
                TenantCompanyRelationshipId = opportunity.TenantCompanyRelationshipId,
                CurrentProcessStepId = inferredStep.Id,
                State = ProcessInstanceState.Active,
                StartedOn = DateTimeOffset.UtcNow,
                CreatedBy = CurrentUserId,
                LastUpdatedBy = CurrentUserId,
                CreatedOn = DateTimeOffset.UtcNow,
                LastUpdated = DateTimeOffset.UtcNow
            };

            storage.Add(activeInstance);
        }

        bool reconciled = await ReconcileOpportunityWorkflowAsync(storage, activeInstance, opportunity, cancellationToken);

        if (reconciled)
            await storage.SaveAsync(cancellationToken);

        if (!forceCreate && await storage.ProcessTasks.AnyAsync(
                item => item.ProcessInstanceId == activeInstance.Id && item.State == ProcessTaskState.Pending,
                cancellationToken))
        {
            return;
        }

        await EnsurePendingTaskAsync(storage, activeInstance, cancellationToken);
    }

    async ValueTask EnsureClientCoverageAsync(
        IWorkflowBroker storage,
        PlatformEntities.ClientAccount clientAccount,
        bool forceCreate,
        CancellationToken cancellationToken)
    {
        PlatformEntities.ProcessInstance activeInstance = await storage.ProcessInstances
            .FirstOrDefaultAsync(
                item => item.ClientAccountId == clientAccount.Id && item.State == ProcessInstanceState.Active,
                cancellationToken);

        if (clientAccount.Status == ClientAccountStatus.Closed)
        {
            await CloseActiveInstanceAsync(storage, activeInstance, clientAccount.Status.ToString(), cancellationToken);
            return;
        }

        if (activeInstance is null)
        {
            string tenantId = await storage.TenantCompanyRelationships
                .Where(relationship => relationship.Id == clientAccount.TenantCompanyRelationshipId)
                .Select(relationship => relationship.TenantId)
                .FirstOrDefaultAsync(cancellationToken);

            PlatformEntities.ProcessDefinition definition = await GetDefaultDefinitionAsync(storage, tenantId, ProcessScopeType.ClientAccount, cancellationToken);
            PlatformEntities.ProcessStep entryStep = await GetEntryStepAsync(storage, definition.Id, cancellationToken);
            if (entryStep is null)
                return;

            activeInstance = new PlatformEntities.ProcessInstance
            {
                Id = Guid.NewGuid(),
                ProcessDefinitionId = definition.Id,
                ClientAccountId = clientAccount.Id,
                TenantCompanyRelationshipId = clientAccount.TenantCompanyRelationshipId,
                CurrentProcessStepId = entryStep.Id,
                State = ProcessInstanceState.Active,
                StartedOn = DateTimeOffset.UtcNow,
                CreatedBy = CurrentUserId,
                LastUpdatedBy = CurrentUserId,
                CreatedOn = DateTimeOffset.UtcNow,
                LastUpdated = DateTimeOffset.UtcNow
            };

            storage.Add(activeInstance);
        }

        if (!forceCreate && await storage.ProcessTasks.AnyAsync(
                item => item.ProcessInstanceId == activeInstance.Id && item.State == ProcessTaskState.Pending,
                cancellationToken))
        {
            return;
        }

        await EnsurePendingTaskAsync(storage, activeInstance, cancellationToken);
    }

    async ValueTask EnsurePendingTaskAsync(
        IWorkflowBroker storage,
        PlatformEntities.ProcessInstance instance,
        CancellationToken cancellationToken)
    {
        PlatformEntities.ProcessTask existingTask = await storage.ProcessTasks
            .FirstOrDefaultAsync(
                item => item.ProcessInstanceId == instance.Id && item.State == ProcessTaskState.Pending,
                cancellationToken);

        if (existingTask is not null)
        {
            await RefreshPendingTaskAsync(storage, existingTask, cancellationToken);
            instance.CurrentProcessTaskId = existingTask.Id;
            instance.LastUpdatedBy = CurrentUserId;
            instance.LastUpdated = DateTimeOffset.UtcNow;
            return;
        }

        PlatformEntities.ProcessStep step = await storage.ProcessSteps
            .FirstOrDefaultAsync(item => item.Id == instance.CurrentProcessStepId, cancellationToken);

        if (step is null)
            return;

        PlatformEntities.ProcessTask task = await CreateTaskForStepAsync(storage, instance, step, cancellationToken);
        // A brand-new instance and its first task form a database FK cycle if both sides are
        // populated in one batch. Persist that pair with a null current-task pointer first,
        // then establish the pointer in the caller's normal save. Existing instances can link
        // the new task immediately.
        if (storage.IsAdded(instance))
            await storage.SaveAsync(cancellationToken);
        instance.CurrentProcessTaskId = task.Id;
        instance.LastUpdatedBy = CurrentUserId;
        instance.LastUpdated = DateTimeOffset.UtcNow;
    }

    async ValueTask ArchiveDuplicateActiveDefinitionsAsync(
        IWorkflowBroker storage,
        IEnumerable<string> tenantIds,
        CancellationToken cancellationToken)
    {
        string[] tenantIdArray =
        [
            .. tenantIds
                .Where(tenantId => !string.IsNullOrWhiteSpace(tenantId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
        ];

        if (tenantIdArray.Length == 0)
            return;

        HashSet<Guid> activeDefinitionIds =
        [
            .. await storage.ProcessInstances
                .AsNoTracking()
                .Where(item => item.State == ProcessInstanceState.Active)
                .Select(item => item.ProcessDefinitionId)
                .Distinct()
                .ToListAsync(cancellationToken)
        ];

        List<PlatformEntities.ProcessDefinition> activeDefinitions = await storage.ProcessDefinitions
            .Where(item =>
                tenantIdArray.Contains(item.TenantId)
                && (item.LifecycleState == ProcessDefinitionLifecycleState.Active || item.IsActive))
            .OrderBy(item => item.CreatedOn)
            .ToListAsync(cancellationToken);

        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach (IGrouping<object, PlatformEntities.ProcessDefinition> definitionGroup in activeDefinitions.GroupBy(item => new
                 {
                     item.TenantId,
                     item.ScopeType
                 }))
        {
            List<PlatformEntities.ProcessDefinition> definitions = definitionGroup.ToList();
            if (definitions.Count <= 1)
                continue;

            PlatformEntities.ProcessDefinition canonical = definitions
                .OrderByDescending(item => activeDefinitionIds.Contains(item.Id))
                .ThenByDescending(item => item.IsDefault)
                .ThenBy(item => item.CreatedOn)
                .First();

            foreach (PlatformEntities.ProcessDefinition duplicate in definitions.Where(item => item.Id != canonical.Id))
            {
                duplicate.LifecycleState = ProcessDefinitionLifecycleState.Archived;
                duplicate.IsActive = false;
                duplicate.IsDefault = false;
                duplicate.LastUpdatedBy = "system";
                duplicate.LastUpdated = now;
            }
        }
    }

    async ValueTask SynchroniseCurrentTasksAsync(
        IWorkflowBroker storage,
        CancellationToken cancellationToken)
    {
        List<PlatformEntities.ProcessInstance> instancesNeedingCurrentTask = await storage.ProcessInstances
            .Where(item => item.State == ProcessInstanceState.Active && item.CurrentProcessTaskId == null)
            .ToListAsync(cancellationToken);

        if (instancesNeedingCurrentTask.Count == 0)
            return;

        foreach (PlatformEntities.ProcessInstance instance in instancesNeedingCurrentTask)
        {
            PlatformEntities.ProcessTask pendingTask = await storage.ProcessTasks
                .Where(item => item.ProcessInstanceId == instance.Id && item.State == ProcessTaskState.Pending)
                .OrderBy(item => item.DueOn)
                .FirstOrDefaultAsync(cancellationToken);

            if (pendingTask is null)
                continue;

            instance.CurrentProcessTaskId = pendingTask.Id;
            instance.LastUpdatedBy = CurrentUserId;
            instance.LastUpdated = DateTimeOffset.UtcNow;
        }

        await storage.SaveAsync(cancellationToken);
    }

    async ValueTask NormaliseImportedEmailStatesAsync(
        IWorkflowBroker storage,
        Guid? opportunityId,
        Guid? clientAccountId,
        CancellationToken cancellationToken)
    {
        if (!await EmailTableExistsAsync(storage, cancellationToken))
            return;

        IQueryable<PlatformEntities.Email> query = storage.Emails;

        if (opportunityId.HasValue)
            query = query.Where(item => item.OpportunityId == opportunityId.Value);
        else if (clientAccountId.HasValue)
            query = query.Where(item => item.ClientAccountId == clientAccountId.Value);

        List<PlatformEntities.Email> invalidEmails = await query
            .Where(item => !ValidEmailStates.Contains(item.State))
            .ToListAsync(cancellationToken);

        if (invalidEmails.Count == 0)
            return;

        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<Guid> materialIds =
        [
            .. invalidEmails
                .Where(item => item.MaterialId.HasValue)
                .Select(item => item.MaterialId!.Value)
                .Distinct()
        ];

        Dictionary<Guid, PlatformEntities.Material> materialsById = materialIds.Count == 0
            ? []
            : await storage.Materials
                .Where(item => materialIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);

        foreach (PlatformEntities.Email email in invalidEmails)
        {
            EmailState normalisedState = NormalizeLegacyEmailState(email.State);
            if ((int)email.State == (int)normalisedState)
                continue;

            email.State = normalisedState;
            email.LastUpdatedBy = "system";
            email.LastUpdated = now;

            if (normalisedState == EmailState.Sent)
                email.SentOn ??= email.LastSendAttemptOn ?? email.ApprovedOn ?? email.LastUpdated;

            if (!email.MaterialId.HasValue || !materialsById.TryGetValue(email.MaterialId.Value, out PlatformEntities.Material material))
                continue;

            material.Status = normalisedState switch
            {
                EmailState.Sent => MaterialStatus.Sent,
                EmailState.Approved => MaterialStatus.Approved,
                _ => MaterialStatus.Draft
            };
            material.SentOn ??= email.SentOn;
            material.LastUpdatedBy = "system";
            material.LastUpdated = now;
        }
    }

    async ValueTask NormaliseImportedCompanyNamesAsync(
        IWorkflowBroker storage,
        CancellationToken cancellationToken)
    {
        List<PlatformEntities.Company> companies = await storage.Companies
            .Where(item => item.TradingName != null && EF.Functions.Like(item.SourceSystem, "legacy%"))
            .ToListAsync(cancellationToken);

        if (companies.Count == 0)
            return;

        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach (PlatformEntities.Company company in companies)
        {
            if (!CompanyNames.IsPlaceholderName(company.OfficialName))
                continue;

            string preferredName = CompanyNames.ResolvePreferredName(company);
            if (string.IsNullOrWhiteSpace(preferredName)
                || string.Equals(company.OfficialName, preferredName, StringComparison.Ordinal))
            {
                continue;
            }

            company.OfficialName = preferredName;
            company.LastUpdatedBy = "system";
            company.LastUpdated = now;
        }
    }

    static async ValueTask<bool> EmailTableExistsAsync(
        IWorkflowBroker storage,
        CancellationToken cancellationToken)
    {
        DbConnection connection = storage.GetDbConnection();
        bool shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM INFORMATION_SCHEMA.TABLES
                        WHERE LOWER(TABLE_SCHEMA) = 'crm'
                          AND LOWER(TABLE_NAME) = 'emails')
                    THEN 1
                    ELSE 0
                END
                """;

            object result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result) == 1;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    async ValueTask<bool> ReconcileOpportunityWorkflowAsync(
        IWorkflowBroker storage,
        PlatformEntities.ProcessInstance activeInstance,
        PlatformEntities.Opportunity opportunity,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        PlatformEntities.TenantCompanyRelationship relationship = await storage.TenantCompanyRelationships
            .FirstOrDefaultAsync(item => item.Id == opportunity.TenantCompanyRelationshipId, cancellationToken);

        bool hasSentEmail = await HasSentOpportunityEmailAsync(storage, opportunity, cancellationToken);
        bool relationshipChanged = false;
        bool opportunityChanged = false;
        bool workflowChanged = false;

        if (hasSentEmail && opportunity.Stage < SalesPipelineStage.OutreachSent)
        {
            opportunity.Stage = SalesPipelineStage.OutreachSent;
            opportunityChanged = true;
            workflowChanged = true;
        }

        if (relationship is not null)
        {
            if (relationship.Status < RelationshipStatus.ActiveOpportunity)
            {
                relationship.Status = RelationshipStatus.ActiveOpportunity;
                relationshipChanged = true;
                workflowChanged = true;
            }

            SalesPipelineStage targetStage = opportunity.Stage;
            if (hasSentEmail && targetStage < SalesPipelineStage.OutreachSent)
                targetStage = SalesPipelineStage.OutreachSent;

            if (relationship.CurrentStage < targetStage)
            {
                relationship.CurrentStage = targetStage;
                relationshipChanged = true;
                workflowChanged = true;
            }
        }

        if (opportunityChanged)
            Touch(opportunity, now);

        if (relationshipChanged)
            Touch(relationship, now);

        PlatformEntities.ProcessStep inferredStep = await ResolveOpportunityStepAsync(
            storage,
            activeInstance.ProcessDefinitionId,
            opportunity,
            cancellationToken);

        if (inferredStep is null)
            return workflowChanged;

        List<PlatformEntities.ProcessTask> pendingTasks = await storage.ProcessTasks
            .Where(item => item.ProcessInstanceId == activeInstance.Id && item.State == ProcessTaskState.Pending)
            .ToListAsync(cancellationToken);

        bool stepMismatch =
            activeInstance.CurrentProcessStepId != inferredStep.Id
            || pendingTasks.Any(item => item.ProcessStepId != inferredStep.Id);

        if (!stepMismatch)
            return workflowChanged;

        foreach (PlatformEntities.ProcessTask pendingTask in pendingTasks)
        {
            pendingTask.State = ProcessTaskState.Cancelled;
            pendingTask.CompletionOutcomeKey = "reconciled";
            pendingTask.CompletionNotes = "Superseded by workflow reconciliation against imported communication history.";
            pendingTask.CompletedBy = "system";
            pendingTask.CompletedOn = now;
            pendingTask.LastUpdatedBy = "system";
            pendingTask.LastUpdated = now;

            loggingBroker.LogInformation(
                "Cancelled scheduled task for {RecordName} because imported communication history advanced the workflow past {TaskTitle}.",
                ResolveWorkflowRecordName(lead: null, company: null, relationship, opportunity, clientAccount: null),
                pendingTask.RenderedTitle);
        }

        activeInstance.CurrentProcessStepId = inferredStep.Id;
        activeInstance.CurrentProcessTaskId = null;
        activeInstance.LastUpdatedBy = CurrentUserId;
        activeInstance.LastUpdated = now;
        workflowChanged = true;

        return workflowChanged;
    }

    async ValueTask<PlatformEntities.ProcessTask> CreateTaskForStepAsync(
        IWorkflowBroker storage,
        PlatformEntities.ProcessInstance instance,
        PlatformEntities.ProcessStep step,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        TaskRenderContext renderContext = await BuildTaskRenderContextAsync(
            storage,
            instance.LeadId,
            instance.TenantCompanyRelationshipId,
            instance.OpportunityId,
            instance.ClientAccountId,
            cancellationToken);
        MailSenderProfile senderProfile = await currentUserMailProfileProvider.GetCurrentAsync(cancellationToken);

        ApplyStepActivationState(
            renderContext.Lead,
            renderContext.Relationship,
            renderContext.Opportunity,
            renderContext.ClientAccount,
            step,
            now);

        TaskRenderValues renderValues = RenderTaskValues(step, renderContext, now);

        PlatformEntities.ProcessTask task = new()
        {
            Id = Guid.NewGuid(),
            ProcessInstanceId = instance.Id,
            ProcessStepId = step.Id,
            LeadId = instance.LeadId,
            TenantCompanyRelationshipId = instance.TenantCompanyRelationshipId,
            OpportunityId = instance.OpportunityId,
            ClientAccountId = instance.ClientAccountId,
            ActionType = step.ActionType,
            State = ProcessTaskState.Pending,
            DueOn = now.AddDays(step.DueAfterDays).AddHours(step.DueAfterHours),
            RenderedTitle = string.IsNullOrWhiteSpace(renderValues.Title) ? step.Name : renderValues.Title,
            RenderedInstructions = renderValues.Instructions,
            RenderedEmailSubject = renderValues.EmailSubject,
            RenderedEmailBody = renderValues.EmailBody,
            RenderedCallScript = renderValues.CallScript,
            RenderedQuestionSet = renderValues.QuestionSet,
            CreatedBy = CurrentUserId,
            LastUpdatedBy = CurrentUserId,
            CreatedOn = now,
            LastUpdated = now
        };

        storage.Add(task);

        if (step.ActionType == ProcessActionType.Email && renderContext.Relationship is not null)
        {
            bool isAccountOwnerHandoff = step.EmailRecipientTarget == ProcessEmailRecipientTarget.AccountOwner;
            MailSenderProfile accountOwnerProfile = isAccountOwnerHandoff
                ? await currentUserMailProfileProvider.GetByUserIdAsync(
                    renderContext.Relationship.AccountOwnerUserId,
                    cancellationToken)
                : null;
            string recipientAddress = isAccountOwnerHandoff
                ? accountOwnerProfile?.EmailAddress
                    ?? (renderContext.Relationship.AccountOwnerUserId?.Contains('@') == true
                        ? renderContext.Relationship.AccountOwnerUserId
                        : null)
                : renderContext.Contact?.EmailAddress ?? renderContext.Company?.ContactEmailAddress;

            PlatformEntities.Material material = new()
            {
                Id = Guid.NewGuid(),
                TenantCompanyRelationshipId = renderContext.Relationship.Id,
                OpportunityId = renderContext.Opportunity?.Id,
                ClientAccountId = renderContext.ClientAccount?.Id,
                CompanyContactId = isAccountOwnerHandoff ? null : renderContext.Contact?.Id,
                Name = task.RenderedEmailSubject ?? task.RenderedTitle,
                Type = MaterialType.Email,
                Status = MaterialStatus.Draft,
                Notes = task.RenderedEmailBody,
                CreatedBy = CurrentUserId,
                LastUpdatedBy = CurrentUserId,
                CreatedOn = now,
                LastUpdated = now
            };

            storage.Add(material);

            PlatformEntities.Email email = new()
            {
                Id = Guid.NewGuid(),
                TenantCompanyRelationshipId = renderContext.Relationship.Id,
                OpportunityId = renderContext.Opportunity?.Id,
                ClientAccountId = renderContext.ClientAccount?.Id,
                MaterialId = material.Id,
                CompanyContactId = isAccountOwnerHandoff ? null : renderContext.Contact?.Id,
                SenderUserId = senderProfile?.UserId ?? CurrentUserId,
                FromDisplayName = senderProfile?.DisplayName ?? renderContext.Relationship.AccountOwnerDisplayName,
                FromEmailAddress = senderProfile?.EmailAddress,
                ReplyToAddresses = senderProfile?.EmailAddress,
                ToAddresses = recipientAddress,
                Subject = task.RenderedEmailSubject ?? task.RenderedTitle,
                BodyHtml = task.RenderedEmailBody,
                BodyText = task.RenderedEmailBody,
                IsBodyHtml = false,
                State = EmailState.Draft,
                CreatedBy = CurrentUserId,
                LastUpdatedBy = CurrentUserId,
                CreatedOn = now,
                LastUpdated = now
            };

            storage.Add(email);
            task.EmailId = email.Id;

            if (!string.IsNullOrWhiteSpace(email.ToAddresses))
            {
                foreach (string address in SplitAddresses(email.ToAddresses))
                {
                    storage.Add(new PlatformEntities.EmailRecipient
                    {
                        Id = Guid.NewGuid(),
                        EmailId = email.Id,
                        CompanyContactId = !isAccountOwnerHandoff
                            && string.Equals(renderContext.Contact?.EmailAddress, address, StringComparison.OrdinalIgnoreCase)
                            ? renderContext.Contact?.Id
                            : null,
                        Address = address,
                        RecipientType = EmailRecipientType.To,
                        CreatedBy = CurrentUserId,
                        LastUpdatedBy = CurrentUserId,
                        CreatedOn = now,
                        LastUpdated = now
                    });
                }
            }
        }

        loggingBroker.LogInformation(
            "Added scheduled task for {RecordName} to {TaskTitle}. Due {DueOn}.",
            ResolveWorkflowRecordName(
                renderContext.Lead,
                renderContext.Company,
                renderContext.Relationship,
                renderContext.Opportunity,
                renderContext.ClientAccount),
            task.RenderedTitle,
            task.DueOn);

        return task;
    }

    async ValueTask RefreshPendingTaskAsync(
        IWorkflowBroker storage,
        PlatformEntities.ProcessTask task,
        CancellationToken cancellationToken)
    {
        PlatformEntities.ProcessStep step = await storage.ProcessSteps
            .FirstOrDefaultAsync(item => item.Id == task.ProcessStepId, cancellationToken);

        if (step is null)
            return;

        DateTimeOffset now = DateTimeOffset.UtcNow;
        TaskRenderContext renderContext = await BuildTaskRenderContextAsync(
            storage,
            task.LeadId,
            task.TenantCompanyRelationshipId,
            task.OpportunityId,
            task.ClientAccountId,
            cancellationToken);
        TaskRenderValues renderValues = RenderTaskValues(step, renderContext, now);

        string renderedTitle = string.IsNullOrWhiteSpace(renderValues.Title) ? step.Name : renderValues.Title;

        if (string.Equals(task.RenderedTitle, renderedTitle, StringComparison.Ordinal)
            && string.Equals(task.RenderedInstructions, renderValues.Instructions, StringComparison.Ordinal)
            && string.Equals(task.RenderedEmailSubject, renderValues.EmailSubject, StringComparison.Ordinal)
            && string.Equals(task.RenderedEmailBody, renderValues.EmailBody, StringComparison.Ordinal)
            && string.Equals(task.RenderedCallScript, renderValues.CallScript, StringComparison.Ordinal)
            && string.Equals(task.RenderedQuestionSet, renderValues.QuestionSet, StringComparison.Ordinal))
        {
            return;
        }

        task.RenderedTitle = renderedTitle;
        task.RenderedInstructions = renderValues.Instructions;
        task.RenderedEmailSubject = renderValues.EmailSubject;
        task.RenderedEmailBody = renderValues.EmailBody;
        task.RenderedCallScript = renderValues.CallScript;
        task.RenderedQuestionSet = renderValues.QuestionSet;
        task.LastUpdatedBy = CurrentUserId;
        task.LastUpdated = now;
    }

    async ValueTask<TaskRenderContext> BuildTaskRenderContextAsync(
        IWorkflowBroker storage,
        Guid? leadId,
        Guid? tenantCompanyRelationshipId,
        Guid? opportunityId,
        Guid? clientAccountId,
        CancellationToken cancellationToken)
    {
        PlatformEntities.Lead lead = leadId.HasValue
            ? await storage.Leads.FirstOrDefaultAsync(item => item.Id == leadId.Value, cancellationToken)
            : null;
        PlatformEntities.TenantCompanyRelationship relationship = tenantCompanyRelationshipId.HasValue
            ? await storage.TenantCompanyRelationships.FirstOrDefaultAsync(item => item.Id == tenantCompanyRelationshipId.Value, cancellationToken)
            : null;
        PlatformEntities.Opportunity opportunity = opportunityId.HasValue
            ? await storage.Opportunities.FirstOrDefaultAsync(item => item.Id == opportunityId.Value, cancellationToken)
            : null;
        PlatformEntities.ClientAccount clientAccount = clientAccountId.HasValue
            ? await storage.ClientAccounts.FirstOrDefaultAsync(item => item.Id == clientAccountId.Value, cancellationToken)
            : null;

        Guid? companyId = lead?.CompanyId ?? relationship?.CompanyId;

        PlatformEntities.Company company = companyId.HasValue
            ? await storage.Companies
                .Include(item => item.RegisteredAddress)
                .FirstOrDefaultAsync(item => item.Id == companyId.Value, cancellationToken)
            : null;
        PlatformEntities.CompanyContact contact = await ResolvePreferredContactAsync(storage, lead, relationship, opportunity, cancellationToken);
        PlatformEntities.Activity latestInboundActivity = tenantCompanyRelationshipId.HasValue
            ? await storage.Activities
                .AsNoTracking()
                .Where(item => item.TenantCompanyRelationshipId == tenantCompanyRelationshipId.Value
                    && item.Direction == ActivityDirection.Inbound)
                .OrderByDescending(item => item.ActivityOn)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        return new TaskRenderContext(lead, relationship, opportunity, clientAccount, company, contact, latestInboundActivity);
    }

    static TaskRenderValues RenderTaskValues(
        PlatformEntities.ProcessStep step,
        TaskRenderContext renderContext,
        DateTimeOffset now)
        => new(
            WorkflowTemplateRenderer.Render(step.TaskTitleTemplate, renderContext.Lead, renderContext.Company, renderContext.Contact, renderContext.Relationship, renderContext.Opportunity, renderContext.ClientAccount, now, renderContext.LatestInboundActivity),
            WorkflowTemplateRenderer.Render(step.TaskInstructionsTemplate, renderContext.Lead, renderContext.Company, renderContext.Contact, renderContext.Relationship, renderContext.Opportunity, renderContext.ClientAccount, now, renderContext.LatestInboundActivity),
            WorkflowTemplateRenderer.Render(step.EmailSubjectTemplate, renderContext.Lead, renderContext.Company, renderContext.Contact, renderContext.Relationship, renderContext.Opportunity, renderContext.ClientAccount, now, renderContext.LatestInboundActivity),
            WorkflowTemplateRenderer.Render(step.EmailBodyTemplate, renderContext.Lead, renderContext.Company, renderContext.Contact, renderContext.Relationship, renderContext.Opportunity, renderContext.ClientAccount, now, renderContext.LatestInboundActivity),
            WorkflowTemplateRenderer.Render(step.CallScriptTemplate, renderContext.Lead, renderContext.Company, renderContext.Contact, renderContext.Relationship, renderContext.Opportunity, renderContext.ClientAccount, now, renderContext.LatestInboundActivity),
            WorkflowTemplateRenderer.Render(step.QuestionSetTemplate, renderContext.Lead, renderContext.Company, renderContext.Contact, renderContext.Relationship, renderContext.Opportunity, renderContext.ClientAccount, now, renderContext.LatestInboundActivity));

    async ValueTask CompleteTaskAsync(
        IWorkflowBroker storage,
        PlatformEntities.ProcessTask task,
        string outcomeKey,
        string completionNote,
        CancellationToken cancellationToken)
    {
        PlatformEntities.ProcessInstance instance = task.ProcessInstance
            ?? await storage.ProcessInstances.FirstAsync(item => item.Id == task.ProcessInstanceId, cancellationToken);
        PlatformEntities.ProcessStep step = task.ProcessStep
            ?? await storage.ProcessSteps.FirstAsync(item => item.Id == task.ProcessStepId, cancellationToken);

        if (instance.State == ProcessInstanceState.Active
            && instance.CurrentProcessTaskId is null
            && instance.CurrentProcessStepId == step.Id)
        {
            int pendingTaskCount = await storage.ProcessTasks.CountAsync(
                item => item.ProcessInstanceId == instance.Id && item.State == ProcessTaskState.Pending,
                cancellationToken);
            if (pendingTaskCount == 1)
            {
                instance.CurrentProcessTaskId = task.Id;
                instance.LastUpdatedBy = CurrentUserId;
                instance.LastUpdated = DateTimeOffset.UtcNow;
            }
        }

        if (instance.State != ProcessInstanceState.Active
            || instance.CurrentProcessTaskId != task.Id
            || instance.CurrentProcessStepId != step.Id)
        {
            throw new WorkflowRuleViolationException(
                "The task is not the active step for this process instance and cannot advance the workflow.");
        }

        PlatformEntities.Lead lead = task.LeadId.HasValue
            ? await storage.Leads.FirstOrDefaultAsync(item => item.Id == task.LeadId.Value, cancellationToken)
            : null;
        PlatformEntities.TenantCompanyRelationship relationship = task.TenantCompanyRelationshipId.HasValue
            ? await storage.TenantCompanyRelationships.FirstOrDefaultAsync(item => item.Id == task.TenantCompanyRelationshipId.Value, cancellationToken)
            : null;
        PlatformEntities.Opportunity opportunity = task.OpportunityId.HasValue
            ? await storage.Opportunities.FirstOrDefaultAsync(item => item.Id == task.OpportunityId.Value, cancellationToken)
            : null;
        PlatformEntities.ClientAccount clientAccount = task.ClientAccountId.HasValue
            ? await storage.ClientAccounts.FirstOrDefaultAsync(item => item.Id == task.ClientAccountId.Value, cancellationToken)
            : null;

        List<PlatformEntities.ProcessTransition> transitions = await storage.ProcessTransitions
            .Where(item => item.ProcessStepId == task.ProcessStepId)
            .OrderByDescending(item => item.IsDefaultOutcome)
            .ThenBy(item => item.OutcomeLabel)
            .ToListAsync(cancellationToken);

        PlatformEntities.ProcessTransition transition = ResolveTransition(transitions, outcomeKey);
        PlatformEntities.ProcessStep nextStep = null;
        if (!transition.IsTerminal && transition.NextProcessStepId.HasValue)
        {
            nextStep = await storage.ProcessSteps.FirstOrDefaultAsync(
                item => item.Id == transition.NextProcessStepId.Value
                    && item.ProcessDefinitionId == step.ProcessDefinitionId
                    && item.IsActive,
                cancellationToken);

            if (nextStep is null)
            {
                throw new WorkflowRuleViolationException(
                    $"Outcome '{transition.OutcomeKey}' does not target an active step in the current process definition.");
            }
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        task.State = ProcessTaskState.Completed;
        task.CompletionOutcomeKey = transition.OutcomeKey;
        task.CompletionNotes = Normalize(completionNote);
        task.CompletedBy = CurrentUserId;
        task.CompletedOn = now;
        task.LastUpdatedBy = CurrentUserId;
        task.LastUpdated = now;

        await ApplyCompletionNoteUpdatesAsync(storage, task, step, task.CompletionNotes, relationship, opportunity, now, cancellationToken);
        await RecordCompletionActivityAsync(storage, task, relationship, opportunity, clientAccount, now, cancellationToken);
        await ApplyTransitionEffectAsync(storage, transition, lead, relationship, opportunity, clientAccount, now, cancellationToken);
        await RecordCompanyHistoryAsync(storage, task, step, instance, transition, lead, relationship, clientAccount, now, cancellationToken);

        loggingBroker.LogInformation(
            "Completed scheduled task for {RecordName} to {TaskTitle} with outcome {OutcomeKey}.",
            ResolveWorkflowRecordName(
                lead,
                await ResolveCompanyAsync(storage, lead, relationship, cancellationToken),
                relationship,
                opportunity,
                clientAccount),
            task.RenderedTitle,
            transition.OutcomeKey);

        if (transition.IsTerminal || !transition.NextProcessStepId.HasValue)
        {
            instance.State = ProcessInstanceState.Completed;
            instance.CompletionOutcomeKey = transition.OutcomeKey;
            instance.CompletedOn = now;
            instance.CurrentProcessTaskId = null;
            instance.CurrentProcessStepId = null;
            instance.LastUpdatedBy = CurrentUserId;
            instance.LastUpdated = now;

            await storage.SaveAsync(cancellationToken);

            if (transition.Effect == ProcessTransitionEffect.QualifyLeadAndCreateOpportunity && lead?.OpportunityId is Guid opportunityId)
                await EnsureCoverageAsync(opportunityId: opportunityId, forceCreate: true, cancellationToken: cancellationToken);

            if (transition.Effect == ProcessTransitionEffect.CreateClientAccount && opportunity?.TenantCompanyRelationshipId is Guid relationshipId)
            {
                PlatformEntities.ClientAccount newClientAccount = await storage.ClientAccounts
                    .OrderByDescending(account => account.CreatedOn)
                    .FirstOrDefaultAsync(
                        account => account.TenantCompanyRelationshipId == relationshipId
                            && account.WonOpportunityId == opportunity.Id,
                        cancellationToken);

                if (newClientAccount is not null)
                    await EnsureCoverageAsync(clientAccountId: newClientAccount.Id, forceCreate: true, cancellationToken: cancellationToken);
            }

            return;
        }

        instance.CurrentProcessTaskId = null;
        instance.CurrentProcessStepId = nextStep.Id;
        instance.LastUpdatedBy = CurrentUserId;
        instance.LastUpdated = now;

        PlatformEntities.ProcessTask nextTask = await CreateTaskForStepAsync(
            storage,
            instance,
            nextStep,
            cancellationToken);
        instance.CurrentProcessTaskId = nextTask.Id;
    }

    async ValueTask RecordCompletionActivityAsync(
        IWorkflowBroker storage,
        PlatformEntities.ProcessTask task,
        PlatformEntities.TenantCompanyRelationship relationship,
        PlatformEntities.Opportunity opportunity,
        PlatformEntities.ClientAccount clientAccount,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (relationship is null)
            return;

        PlatformEntities.Activity activity = new()
        {
            Id = Guid.NewGuid(),
            TenantCompanyRelationshipId = relationship.Id,
            OpportunityId = opportunity?.Id,
            ClientAccountId = clientAccount?.Id,
            MaterialId = task.EmailId.HasValue
                ? await storage.Emails
                    .Where(email => email.Id == task.EmailId.Value)
                    .Select(email => email.MaterialId)
                    .FirstOrDefaultAsync(cancellationToken)
                : null,
            ActivityOn = now,
            Type = task.ActionType switch
            {
                ProcessActionType.Call => ActivityType.PhoneCall,
                ProcessActionType.Email => ActivityType.Email,
                ProcessActionType.Meeting => ActivityType.Meeting,
                ProcessActionType.Research => ActivityType.Qualification,
                _ => ActivityType.Process
            },
            Direction = task.ActionType == ProcessActionType.Email || task.ActionType == ProcessActionType.Call
                ? ActivityDirection.Outbound
                : ActivityDirection.Internal,
            Summary = $"Completed workflow task: {task.RenderedTitle}",
            Outcome = task.CompletionNotes,
            CreatedBy = CurrentUserId,
            LastUpdatedBy = CurrentUserId,
            CreatedOn = now,
            LastUpdated = now
        };

        storage.Add(activity);
    }

    static async ValueTask RecordCompanyHistoryAsync(
        IWorkflowBroker storage,
        PlatformEntities.ProcessTask task,
        PlatformEntities.ProcessStep step,
        PlatformEntities.ProcessInstance instance,
        PlatformEntities.ProcessTransition transition,
        PlatformEntities.Lead lead,
        PlatformEntities.TenantCompanyRelationship relationship,
        PlatformEntities.ClientAccount clientAccount,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        Guid? companyId = lead?.CompanyId ?? relationship?.CompanyId;
        if (!companyId.HasValue && clientAccount is not null)
        {
            companyId = await storage.TenantCompanyRelationships
                .Where(item => item.Id == clientAccount.TenantCompanyRelationshipId)
                .Select(item => (Guid?)item.CompanyId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (!companyId.HasValue)
            return;

        string tenantId = lead?.TenantId ?? relationship?.TenantId;
        string lane = lead is not null
            ? "Lead"
            : clientAccount is not null
                ? "Client"
                : "Opportunity";
        string[] producedFacts = SplitFactKeys(step.ProducedFacts);
        string[] facts = producedFacts.Length == 0 ? [null] : producedFacts;

        foreach (string factKey in facts)
        {
            storage.Add(new PlatformEntities.CompanyHistoryItem
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId.Value,
                TenantId = tenantId,
                OccurredOn = now,
                Lane = lane,
                EventType = "process-step-completed",
                Summary = $"{step.Name}: {transition.OutcomeLabel}",
                Details = task.CompletionNotes,
                FactKey = factKey,
                FactValue = factKey is null ? null : task.CompletionNotes,
                Confidence = ExtractConfidence(task.CompletionNotes),
                SourceType = "ProcessTask",
                SourceId = task.Id,
                ProcessDefinitionId = step.ProcessDefinitionId,
                ProcessInstanceId = instance.Id,
                ProcessStepId = step.Id,
                ProcessTaskId = task.Id,
                IsPrivate = !string.IsNullOrWhiteSpace(tenantId),
                CreatedBy = task.CompletedBy,
                LastUpdatedBy = task.CompletedBy,
                CreatedOn = now,
                LastUpdated = now
            });
        }
    }

    static string[] SplitFactKeys(string value) =>
        (value ?? string.Empty)
            .Split([',', '\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    static string ExtractConfidence(string value)
    {
        Match match = Regex.Match(
            value ?? string.Empty,
            @"(?im)^Confidence:\s*(?<value>high|medium|low)\b",
            RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["value"].Value.ToLowerInvariant() : null;
    }

    async ValueTask ApplyCompletionNoteUpdatesAsync(
        IWorkflowBroker storage,
        PlatformEntities.ProcessTask task,
        PlatformEntities.ProcessStep step,
        string completionNote,
        PlatformEntities.TenantCompanyRelationship relationship,
        PlatformEntities.Opportunity opportunity,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (relationship is null
            || opportunity is null
            || string.IsNullOrWhiteSpace(completionNote)
            || task.ActionType != ProcessActionType.Review
            || !string.Equals(step.Key, "confirm-route", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        RouteConfirmationDetails routeDetails = ParseRouteConfirmationDetails(completionNote);
        bool hasChanges = false;

        if (!string.IsNullOrWhiteSpace(routeDetails.OpeningAngle))
        {
            relationship.PreferredOpeningAngle = routeDetails.OpeningAngle;
            if (string.IsNullOrWhiteSpace(opportunity.ValueHypothesis))
                opportunity.ValueHypothesis = routeDetails.OpeningAngle;

            hasChanges = true;
        }

        if (string.IsNullOrWhiteSpace(routeDetails.EmailAddress)
            && string.IsNullOrWhiteSpace(routeDetails.Name)
            && string.IsNullOrWhiteSpace(routeDetails.PhoneNumber))
        {
            if (hasChanges)
                await storage.SaveAsync(cancellationToken);

            return;
        }

        PlatformEntities.Company company = await storage.Companies
            .FirstOrDefaultAsync(item => item.Id == relationship.CompanyId, cancellationToken);

        if (company is null)
        {
            if (hasChanges)
                await storage.SaveAsync(cancellationToken);

            return;
        }

        PlatformEntities.CompanyContact companyContact = await ResolveOrCreateCompanyContactFromRouteAsync(
            storage,
            company,
            relationship.Id,
            routeDetails,
            completionNote,
            now,
            cancellationToken);

        PlatformEntities.RelationshipContact relationshipContact = await UpsertPrimaryRelationshipContactAsync(
            storage,
            relationship.Id,
            companyContact,
            routeDetails.OpeningAngle,
            completionNote,
            now,
            cancellationToken);

        opportunity.PrimaryRelationshipContactId = relationshipContact.Id;

        if (!string.IsNullOrWhiteSpace(routeDetails.EmailAddress))
            company.ContactEmailAddress = routeDetails.EmailAddress;

        if (!string.IsNullOrWhiteSpace(routeDetails.PhoneNumber))
            company.ContactPhoneNumber = routeDetails.PhoneNumber;

        await storage.SaveAsync(cancellationToken);
    }

    async ValueTask ApplyTransitionEffectAsync(
        IWorkflowBroker storage,
        PlatformEntities.ProcessTransition transition,
        PlatformEntities.Lead lead,
        PlatformEntities.TenantCompanyRelationship relationship,
        PlatformEntities.Opportunity opportunity,
        PlatformEntities.ClientAccount clientAccount,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (transition.ResultingRelationshipStatus.HasValue && relationship is not null)
            relationship.Status = transition.ResultingRelationshipStatus.Value;

        if (transition.ResultingSalesStage.HasValue && opportunity is not null)
            opportunity.Stage = transition.ResultingSalesStage.Value;

        if (transition.ResultingSalesStage.HasValue && relationship is not null)
            relationship.CurrentStage = transition.ResultingSalesStage.Value;

        if (transition.ResultingClientAccountStatus.HasValue && clientAccount is not null)
            clientAccount.Status = transition.ResultingClientAccountStatus.Value;

        switch (transition.Effect)
        {
            case ProcessTransitionEffect.QualifyLeadAndCreateOpportunity:
                await QualifyLeadAsync(storage, lead, now, cancellationToken);
                break;

            case ProcessTransitionEffect.RejectLead:
                if (lead is not null)
                {
                    lead.Status = LeadStatus.Rejected;
                    await SuppressCompanyAsync(storage, lead.CompanyId, $"Lead rejected: {transition.OutcomeLabel}", now, cancellationToken);
                }
                break;

            case ProcessTransitionEffect.DeferLead:
                if (lead is not null)
                    lead.Status = LeadStatus.Deferred;
                break;

            case ProcessTransitionEffect.CreateClientAccount:
                await CreateClientAccountAsync(storage, relationship, opportunity, now, cancellationToken);
                break;

            case ProcessTransitionEffect.CloseOpportunityAsWon:
                if (opportunity is not null)
                    opportunity.Stage = SalesPipelineStage.Won;
                if (relationship is not null)
                    relationship.Status = RelationshipStatus.Contracted;
                break;

            case ProcessTransitionEffect.CloseOpportunityAsLost:
                if (opportunity is not null)
                    opportunity.Stage = SalesPipelineStage.Lost;
                if (relationship is not null)
                {
                    relationship.Status = RelationshipStatus.Disqualified;
                    await SuppressCompanyAsync(storage, relationship.CompanyId, $"Opportunity closed: {transition.OutcomeLabel}", now, cancellationToken);
                }
                break;

            case ProcessTransitionEffect.CloseClientAccount:
                if (clientAccount is not null)
                    clientAccount.Status = ClientAccountStatus.Closed;
                if (relationship is not null)
                    relationship.Status = RelationshipStatus.Dormant;
                break;
        }

        Touch(lead, now);
        Touch(relationship, now);
        Touch(opportunity, now);
        Touch(clientAccount, now);
    }

    static async ValueTask SuppressCompanyAsync(
        IWorkflowBroker storage,
        Guid? companyId,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!companyId.HasValue)
            return;

        PlatformEntities.Company company = await storage.Companies
            .FirstOrDefaultAsync(item => item.Id == companyId.Value, cancellationToken);
        if (company is null)
            return;

        company.IsProspectingSuppressed = true;
        company.ProspectingSuppressedReason = reason;
        company.ProspectingSuppressedOn = now;
        Touch(company, now);
    }

    async ValueTask QualifyLeadAsync(
        IWorkflowBroker storage,
        PlatformEntities.Lead lead,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (lead is null)
            return;

        PlatformEntities.Company company = await storage.Companies
            .FirstOrDefaultAsync(item => item.Id == lead.CompanyId, cancellationToken)
            ?? throw new InvalidOperationException($"Lead '{lead.Id}' does not reference a valid company.");

        PlatformEntities.TenantCompanyRelationship relationship = lead.TenantCompanyRelationshipId.HasValue
            ? await storage.TenantCompanyRelationships.FirstOrDefaultAsync(item => item.Id == lead.TenantCompanyRelationshipId.Value, cancellationToken)
            : null;

        if (relationship is null)
        {
            relationship = new PlatformEntities.TenantCompanyRelationship
            {
                Id = Guid.NewGuid(),
                TenantId = string.IsNullOrWhiteSpace(lead.TenantId) ? DefaultTenantId : lead.TenantId,
                CompanyId = company.Id,
                AccountOwnerUserId = CurrentUserId,
                AccountOwnerDisplayName = CurrentUserId,
                Status = RelationshipStatus.Prospect,
                CurrentStage = SalesPipelineStage.Researched,
                Priority = RelationshipPriority.Medium,
                LeadSource = FirstNonEmpty(lead.SourceSystem, "Lead import"),
                ResearchSummary = lead.QualificationNotes,
                CreatedBy = CurrentUserId,
                LastUpdatedBy = CurrentUserId,
                CreatedOn = now,
                LastUpdated = now
            };

            storage.Add(relationship);
            await storage.SaveAsync(cancellationToken);
            lead.TenantCompanyRelationshipId = relationship.Id;
        }

        List<PlatformEntities.LeadContact> leadContacts = await storage.LeadContacts
            .Where(item => item.LeadId == lead.Id)
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.CreatedOn)
            .ToListAsync(cancellationToken);

        foreach (PlatformEntities.LeadContact leadContact in leadContacts)
        {
            PlatformEntities.CompanyContact companyContact = await ResolveOrCreateCompanyContactAsync(storage, company.Id, leadContact, now, cancellationToken);
            await ResolveOrCreateRelationshipContactAsync(storage, relationship.Id, companyContact.Id, leadContact, now, cancellationToken);
        }

        PlatformEntities.Opportunity opportunity = lead.OpportunityId.HasValue
            ? await storage.Opportunities.FirstOrDefaultAsync(item => item.Id == lead.OpportunityId.Value, cancellationToken)
            : null;

        if (opportunity is null)
        {
            PlatformEntities.RelationshipContact primaryContact = await storage.RelationshipContacts
                .Where(item => item.TenantCompanyRelationshipId == relationship.Id)
                .OrderByDescending(item => item.IsPrimary)
                .ThenBy(item => item.CreatedOn)
                .FirstOrDefaultAsync(cancellationToken);

            opportunity = new PlatformEntities.Opportunity
            {
                Id = Guid.NewGuid(),
                TenantCompanyRelationshipId = relationship.Id,
                PrimaryRelationshipContactId = primaryContact?.Id,
                Type = OpportunityType.General,
                Stage = SalesPipelineStage.Researched,
                PainSummary = lead.QualificationNotes,
                ValueHypothesis = relationship.PreferredOpeningAngle,
                CreatedBy = CurrentUserId,
                LastUpdatedBy = CurrentUserId,
                CreatedOn = now,
                LastUpdated = now
            };

            storage.Add(opportunity);
            await storage.SaveAsync(cancellationToken);
            lead.OpportunityId = opportunity.Id;
        }

        lead.Status = LeadStatus.Converted;
        lead.LastUpdatedBy = CurrentUserId;
        lead.LastUpdated = now;
    }

    async ValueTask CreateClientAccountAsync(
        IWorkflowBroker storage,
        PlatformEntities.TenantCompanyRelationship relationship,
        PlatformEntities.Opportunity opportunity,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (relationship is null || opportunity is null)
            return;

        PlatformEntities.ClientAccount existing = await storage.ClientAccounts
            .FirstOrDefaultAsync(
                item => item.TenantCompanyRelationshipId == relationship.Id
                    && item.WonOpportunityId == opportunity.Id,
                cancellationToken);

        if (existing is not null)
            return;

        PlatformEntities.ClientAccount clientAccount = new()
        {
            Id = Guid.NewGuid(),
            TenantCompanyRelationshipId = relationship.Id,
            WonOpportunityId = opportunity.Id,
            Status = ClientAccountStatus.Onboarding,
            ContractSignedOn = now,
            CreatedBy = CurrentUserId,
            LastUpdatedBy = CurrentUserId,
            CreatedOn = now,
            LastUpdated = now
        };

        storage.Add(clientAccount);
        relationship.Status = RelationshipStatus.Onboarding;
        opportunity.Stage = SalesPipelineStage.Won;

        loggingBroker.LogInformation(
            "Created client account for {RecordName} from opportunity {OpportunityId} and raised clientaccount_created event.",
            ResolveWorkflowRecordName(lead: null, company: relationship.Company, relationship, opportunity, clientAccount),
            opportunity.Id);

        await eventHub.RaiseEventAsync(
            "clientaccount_created",
            new EventMessage<PlatformEntities.ClientAccount>
            {
                AuthInfo = new EventAuthInfo
                {
                    SSOUserId = authInfo.SSOUserId
                },
                Data = clientAccount
            });
    }

    async ValueTask<PlatformEntities.CompanyContact> ResolvePreferredContactAsync(
        IWorkflowBroker storage,
        PlatformEntities.Lead lead,
        PlatformEntities.TenantCompanyRelationship relationship,
        PlatformEntities.Opportunity opportunity,
        CancellationToken cancellationToken)
    {
        if (opportunity?.PrimaryRelationshipContactId is Guid primaryRelationshipContactId)
        {
            return await storage.RelationshipContacts
                .Where(item => item.Id == primaryRelationshipContactId)
                .Select(item => item.CompanyContact)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (relationship is not null)
        {
            PlatformEntities.RelationshipContact relationshipContact = await storage.RelationshipContacts
                .Include(item => item.CompanyContact)
                .Where(item => item.TenantCompanyRelationshipId == relationship.Id)
                .OrderByDescending(item => item.IsPrimary)
                .ThenBy(item => item.CreatedOn)
                .FirstOrDefaultAsync(cancellationToken);

            if (relationshipContact?.CompanyContact is not null)
                return relationshipContact.CompanyContact;
        }

        if (lead is not null)
        {
            PlatformEntities.LeadContact leadContact = await storage.LeadContacts
                .Where(item => item.LeadId == lead.Id)
                .OrderByDescending(item => item.IsPrimary)
                .ThenBy(item => item.CreatedOn)
                .FirstOrDefaultAsync(cancellationToken);

            if (leadContact is not null)
                return await ResolveOrCreateCompanyContactAsync(storage, lead.CompanyId, leadContact, DateTimeOffset.UtcNow, cancellationToken);
        }

        return null;
    }

    async ValueTask<PlatformEntities.CompanyContact> ResolveOrCreateCompanyContactAsync(
        IWorkflowBroker storage,
        Guid companyId,
        PlatformEntities.LeadContact leadContact,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        PlatformEntities.CompanyContact existing = await storage.CompanyContacts
            .FirstOrDefaultAsync(
                item => item.CompanyId == companyId
                    && item.EmailAddress == leadContact.EmailAddress
                    && item.Name == leadContact.Name,
                cancellationToken);

        if (existing is not null)
            return existing;

        PlatformEntities.CompanyContact companyContact = new()
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            SourceSystem = "lead-process",
            IsPrimary = leadContact.IsPrimary,
            Name = string.IsNullOrWhiteSpace(leadContact.Name) ? "Imported contact" : leadContact.Name,
            Position = leadContact.Position,
            EmailAddress = leadContact.EmailAddress,
            PhoneNumber = leadContact.PhoneNumber,
            LinkedInUrl = leadContact.LinkedInUrl,
            Notes = leadContact.Notes,
            CreatedBy = CurrentUserId,
            LastUpdatedBy = CurrentUserId,
            CreatedOn = now,
            LastUpdated = now
        };

        storage.Add(companyContact);
        await storage.SaveAsync(cancellationToken);
        return companyContact;
    }

    async ValueTask<PlatformEntities.CompanyContact> ResolveOrCreateCompanyContactFromRouteAsync(
        IWorkflowBroker storage,
        PlatformEntities.Company company,
        Guid relationshipId,
        RouteConfirmationDetails routeDetails,
        string completionNote,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        IQueryable<PlatformEntities.CompanyContact> query = storage.CompanyContacts.Where(item => item.CompanyId == company.Id);

        PlatformEntities.CompanyContact existing = null;

        if (!string.IsNullOrWhiteSpace(routeDetails.EmailAddress))
        {
            existing = await query.FirstOrDefaultAsync(
                item => item.EmailAddress == routeDetails.EmailAddress,
                cancellationToken);
        }

        if (existing is null && !string.IsNullOrWhiteSpace(routeDetails.Name))
        {
            existing = await query.FirstOrDefaultAsync(
                item => item.Name == routeDetails.Name,
                cancellationToken);
        }

        if (existing is not null)
        {
            existing.Name = FirstNonEmpty(routeDetails.Name, existing.Name, "Primary contact");
            existing.EmailAddress = FirstNonEmpty(routeDetails.EmailAddress, existing.EmailAddress);
            existing.PhoneNumber = FirstNonEmpty(routeDetails.PhoneNumber, existing.PhoneNumber);
            existing.Position = FirstNonEmpty(existing.Position, "Primary contact");
            existing.Notes = FirstNonEmpty(existing.Notes, completionNote);
            existing.IsPrimary = true;
            existing.LastUpdatedBy = CurrentUserId;
            existing.LastUpdated = now;
            return existing;
        }

        PlatformEntities.CompanyContact companyContact = new()
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            SourceSystem = "workflow-route-confirmation",
            IsVerified = false,
            IsPrimary = true,
            Name = FirstNonEmpty(routeDetails.Name, DeriveNameFromEmail(routeDetails.EmailAddress), "Primary contact"),
            Position = "Primary contact",
            EmailAddress = routeDetails.EmailAddress,
            PhoneNumber = routeDetails.PhoneNumber,
            Notes = completionNote,
            CreatedBy = CurrentUserId,
            LastUpdatedBy = CurrentUserId,
            CreatedOn = now,
            LastUpdated = now
        };

        storage.Add(companyContact);
        return companyContact;
    }

    async ValueTask ResolveOrCreateRelationshipContactAsync(
        IWorkflowBroker storage,
        Guid relationshipId,
        Guid companyContactId,
        PlatformEntities.LeadContact leadContact,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        bool exists = await storage.RelationshipContacts.AnyAsync(
            item => item.TenantCompanyRelationshipId == relationshipId && item.CompanyContactId == companyContactId,
            cancellationToken);

        if (exists)
            return;

        storage.Add(new PlatformEntities.RelationshipContact
        {
            Id = Guid.NewGuid(),
            TenantCompanyRelationshipId = relationshipId,
            CompanyContactId = companyContactId,
            Status = RelationshipContactStatus.Active,
            IsPrimary = leadContact.IsPrimary,
            RelationshipRoute = leadContact.Position,
            Notes = leadContact.Notes,
            CreatedBy = CurrentUserId,
            LastUpdatedBy = CurrentUserId,
            CreatedOn = now,
            LastUpdated = now
        });

        await storage.SaveAsync(cancellationToken);
    }

    async ValueTask<PlatformEntities.RelationshipContact> UpsertPrimaryRelationshipContactAsync(
        IWorkflowBroker storage,
        Guid relationshipId,
        PlatformEntities.CompanyContact companyContact,
        string openingAngle,
        string completionNote,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        List<PlatformEntities.RelationshipContact> existingContacts = await storage.RelationshipContacts
            .Where(item => item.TenantCompanyRelationshipId == relationshipId)
            .ToListAsync(cancellationToken);

        foreach (PlatformEntities.RelationshipContact existingContact in existingContacts)
        {
            existingContact.IsPrimary = existingContact.CompanyContactId == companyContact.Id;
            if (existingContact.CompanyContactId == companyContact.Id)
            {
                existingContact.Status = RelationshipContactStatus.Active;
                existingContact.RelationshipRoute = FirstNonEmpty(openingAngle, existingContact.RelationshipRoute);
                existingContact.Notes = FirstNonEmpty(completionNote, existingContact.Notes);
                existingContact.LastUpdatedBy = CurrentUserId;
                existingContact.LastUpdated = now;
                return existingContact;
            }

            existingContact.LastUpdatedBy = CurrentUserId;
            existingContact.LastUpdated = now;
        }

        PlatformEntities.RelationshipContact relationshipContact = new()
        {
            Id = Guid.NewGuid(),
            TenantCompanyRelationshipId = relationshipId,
            CompanyContactId = companyContact.Id,
            Status = RelationshipContactStatus.Active,
            IsPrimary = true,
            RelationshipRoute = openingAngle,
            Source = "workflow-route-confirmation",
            Notes = completionNote,
            CreatedBy = CurrentUserId,
            LastUpdatedBy = CurrentUserId,
            CreatedOn = now,
            LastUpdated = now
        };

        storage.Add(relationshipContact);
        return relationshipContact;
    }

    static RouteConfirmationDetails ParseRouteConfirmationDetails(string completionNote)
    {
        if (string.IsNullOrWhiteSpace(completionNote))
            return new(null, null, null, null);

        string emailAddress = Regex.Match(
                completionNote,
                @"(?<email>[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,})",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Groups["email"]
            .Value;

        string name = string.IsNullOrWhiteSpace(emailAddress)
            ? null
            : Regex.Match(
                    completionNote,
                    @"(?:use|contact|reach out to)\s+(?<name>.+?)\s+at\s+" + Regex.Escape(emailAddress),
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                .Groups["name"]
                .Value
                .Trim(' ', '.', ',', ';', ':');

        string phoneNumber = Regex.Match(
                completionNote,
                @"(?<phone>\+?[0-9][0-9\s()\-]{7,}[0-9])",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Groups["phone"]
            .Value;

        string openingAngle = Regex.Match(
                completionNote,
                @"opening angle\s*:\s*(?<value>.+?)(?:$|\r?\n)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline)
            .Groups["value"]
            .Value
            .Trim();

        return new(
            Normalize(name),
            Normalize(emailAddress),
            Normalize(phoneNumber),
            Normalize(openingAngle));
    }

    static string DeriveNameFromEmail(string emailAddress)
    {
        if (string.IsNullOrWhiteSpace(emailAddress) || !emailAddress.Contains('@'))
            return null;

        string localPart = emailAddress.Split('@')[0].Replace('.', ' ').Replace('_', ' ').Replace('-', ' ');
        if (string.IsNullOrWhiteSpace(localPart))
            return null;

        string[] parts = localPart
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant())
            .ToArray();

        return parts.Length == 0 ? null : string.Join(' ', parts);
    }

    sealed record RouteConfirmationDetails(
        string Name,
        string EmailAddress,
        string PhoneNumber,
        string OpeningAngle);

    static void ApplyStepActivationState(
        PlatformEntities.Lead lead,
        PlatformEntities.TenantCompanyRelationship relationship,
        PlatformEntities.Opportunity opportunity,
        PlatformEntities.ClientAccount clientAccount,
        PlatformEntities.ProcessStep step,
        DateTimeOffset now)
    {
        if (relationship is not null && step.RelationshipStatusOnActivate.HasValue)
            relationship.Status = Max(relationship.Status, step.RelationshipStatusOnActivate.Value);

        if (opportunity is not null && step.SalesStageOnActivate.HasValue)
            opportunity.Stage = Max(opportunity.Stage, step.SalesStageOnActivate.Value);

        if (relationship is not null && step.SalesStageOnActivate.HasValue)
            relationship.CurrentStage = Max(relationship.CurrentStage, step.SalesStageOnActivate.Value);

        if (clientAccount is not null && step.ClientAccountStatusOnActivate.HasValue)
            clientAccount.Status = Max(clientAccount.Status, step.ClientAccountStatusOnActivate.Value);

        Touch(lead, now);
        Touch(relationship, now);
        Touch(opportunity, now);
        Touch(clientAccount, now);
    }

    async ValueTask<PlatformEntities.ProcessDefinition> GetDefaultDefinitionAsync(
        IWorkflowBroker storage,
        string tenantId,
        ProcessScopeType scopeType,
        CancellationToken cancellationToken)
    {
        string resolvedTenantId = string.IsNullOrWhiteSpace(tenantId) ? DefaultTenantId : tenantId;

        return await storage.ProcessDefinitions
            .Where(item => item.TenantId == resolvedTenantId && item.ScopeType == scopeType && item.IsActive)
            .OrderByDescending(item => item.IsDefault)
            .ThenBy(item => item.Name)
            .FirstAsync(cancellationToken);
    }

    async ValueTask<PlatformEntities.ProcessStep> GetEntryStepAsync(
        IWorkflowBroker storage,
        Guid definitionId,
        CancellationToken cancellationToken) =>
        await storage.ProcessSteps
            .Where(item => item.ProcessDefinitionId == definitionId && item.IsActive)
            .OrderByDescending(item => item.IsEntryPoint)
            .ThenBy(item => item.Sequence)
            .FirstOrDefaultAsync(cancellationToken);

    async ValueTask<PlatformEntities.ProcessStep> ResolveOpportunityStepAsync(
        IWorkflowBroker storage,
        Guid definitionId,
        PlatformEntities.Opportunity opportunity,
        CancellationToken cancellationToken)
    {
        List<PlatformEntities.ProcessStep> steps = await storage.ProcessSteps
            .Where(item => item.ProcessDefinitionId == definitionId && item.IsActive)
            .OrderBy(item => item.Sequence)
            .ToListAsync(cancellationToken);

        if (steps.Count == 0)
            return null;

        Dictionary<string, PlatformEntities.ProcessStep> stepsByKey = steps
            .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        PlatformEntities.ProcessStep entryStep = steps
            .OrderByDescending(item => item.IsEntryPoint)
            .ThenBy(item => item.Sequence)
            .First();

        bool hasSentEmail = await HasSentOpportunityEmailAsync(storage, opportunity, cancellationToken);
        bool hasPendingProposalTask = await HasPendingOpportunityTaskForStepAsync(
            storage,
            opportunity.Id,
            definitionId,
            "send-proposal",
            cancellationToken);
        bool hasPendingContractTask = await HasPendingOpportunityTaskForStepAsync(
            storage,
            opportunity.Id,
            definitionId,
            "send-contract",
            cancellationToken);

        return opportunity.Stage switch
        {
            <= SalesPipelineStage.ContactIdentified
                => stepsByKey.GetValueOrDefault("confirm-route") ?? entryStep,

            SalesPipelineStage.OutreachReady
                => hasSentEmail
                    ? stepsByKey.GetValueOrDefault("review-response") ?? stepsByKey.GetValueOrDefault("intro-email") ?? entryStep
                    : stepsByKey.GetValueOrDefault("intro-email") ?? entryStep,

            SalesPipelineStage.OutreachSent or SalesPipelineStage.Responded or SalesPipelineStage.DiscoveryBooked or SalesPipelineStage.DiscoveryCompleted or SalesPipelineStage.Nurture
                => stepsByKey.GetValueOrDefault("review-response") ?? entryStep,

            SalesPipelineStage.ProposalSent
                => hasPendingProposalTask
                    ? stepsByKey.GetValueOrDefault("send-proposal") ?? entryStep
                    : stepsByKey.GetValueOrDefault("negotiate") ?? stepsByKey.GetValueOrDefault("send-proposal") ?? entryStep,

            SalesPipelineStage.Negotiation
                => stepsByKey.GetValueOrDefault("negotiate") ?? entryStep,

            SalesPipelineStage.ContractSent
                => hasPendingContractTask
                    ? stepsByKey.GetValueOrDefault("send-contract") ?? entryStep
                    : stepsByKey.GetValueOrDefault("confirm-signature") ?? stepsByKey.GetValueOrDefault("send-contract") ?? entryStep,

            _ => entryStep
        };
    }

    static Task<bool> HasPendingOpportunityTaskForStepAsync(
        IWorkflowBroker storage,
        Guid opportunityId,
        Guid processDefinitionId,
        string stepKey,
        CancellationToken cancellationToken)
        => storage.ProcessTasks
            .AnyAsync(
                item => item.OpportunityId == opportunityId
                    && item.State == ProcessTaskState.Pending
                    && item.ProcessStep.ProcessDefinitionId == processDefinitionId
                    && item.ProcessStep.Key == stepKey,
                cancellationToken);

    async ValueTask<bool> HasSentOpportunityEmailAsync(
        IWorkflowBroker storage,
        PlatformEntities.Opportunity opportunity,
        CancellationToken cancellationToken)
    {
        if (await storage.Emails.AnyAsync(
                item => item.OpportunityId == opportunity.Id && item.State == EmailState.Sent,
                cancellationToken))
        {
            return true;
        }

        bool hasSiblingOpportunities = await storage.Opportunities.AnyAsync(
            item => item.TenantCompanyRelationshipId == opportunity.TenantCompanyRelationshipId
                && item.Id != opportunity.Id,
            cancellationToken);

        if (hasSiblingOpportunities)
            return false;

        return await storage.Emails.AnyAsync(
            item => item.TenantCompanyRelationshipId == opportunity.TenantCompanyRelationshipId
                && item.OpportunityId == null
                && item.ClientAccountId == null
                && item.State == EmailState.Sent,
            cancellationToken);
    }

    static PlatformEntities.ProcessTransition ResolveTransition(
        IReadOnlyList<PlatformEntities.ProcessTransition> transitions,
        string outcomeKey)
    {
        if (transitions.Count == 0)
            return null;

        string normalizedOutcome = Normalize(outcomeKey);
        if (!string.IsNullOrWhiteSpace(normalizedOutcome))
        {
            PlatformEntities.ProcessTransition explicitTransition = transitions.FirstOrDefault(transition =>
                transition.OutcomeKey.Equals(normalizedOutcome, StringComparison.OrdinalIgnoreCase));

            if (explicitTransition is not null)
                return explicitTransition;

            throw new WorkflowRuleViolationException(
                $"Outcome '{normalizedOutcome}' is not valid for the current workflow step.");
        }

        return transitions.FirstOrDefault(transition => transition.IsDefaultOutcome) ?? transitions[0];
    }

    static async ValueTask CloseActiveInstanceAsync(
        IWorkflowBroker storage,
        PlatformEntities.ProcessInstance activeInstance,
        string outcomeKey,
        CancellationToken cancellationToken)
    {
        if (activeInstance is null)
            return;

        List<PlatformEntities.ProcessTask> pendingTasks = await storage.ProcessTasks
            .Where(item => item.ProcessInstanceId == activeInstance.Id && item.State == ProcessTaskState.Pending)
            .ToListAsync(cancellationToken);

        foreach (PlatformEntities.ProcessTask task in pendingTasks)
        {
            task.State = ProcessTaskState.Cancelled;
            task.CompletionOutcomeKey = outcomeKey;
            task.CompletedOn = DateTimeOffset.UtcNow;
            task.CompletedBy = "system";
            task.LastUpdatedBy = "system";
            task.LastUpdated = DateTimeOffset.UtcNow;
        }

        activeInstance.State = ProcessInstanceState.Completed;
        activeInstance.CompletionOutcomeKey = outcomeKey;
        activeInstance.CompletedOn = DateTimeOffset.UtcNow;
        activeInstance.CurrentProcessTaskId = null;
        activeInstance.CurrentProcessStepId = null;
        activeInstance.LastUpdatedBy = "system";
        activeInstance.LastUpdated = DateTimeOffset.UtcNow;
    }

    async ValueTask EnsureLeadProcessAsync(
        IWorkflowBroker storage,
        string tenantId,
        CancellationToken cancellationToken)
    {
        PlatformEntities.ProcessDefinition existingDefinition = await storage.ProcessDefinitions.FirstOrDefaultAsync(
                item => item.TenantId == tenantId
                    && item.ScopeType == ProcessScopeType.Lead
                    && item.Name == "Lead Generation"
                    && (item.LifecycleState == ProcessDefinitionLifecycleState.Active || item.IsActive),
                cancellationToken);

        if (existingDefinition is not null)
        {
            NormalizeSeedDefinitionMetadata(
                existingDefinition,
                "Lead Generation",
                "Move a raw lead through bounded identity, activity, data-quality, and commercial-fit checks before creating an opportunity or rejecting it cleanly.");

            bool hasBoundedStages = await storage.ProcessSteps.AnyAsync(
                item => item.ProcessDefinitionId == existingDefinition.Id
                    && item.Key == "company-activity"
                    && item.IsActive,
                cancellationToken);

            if (!hasBoundedStages)
                await UpgradeLeadProcessToBoundedStagesAsync(storage, existingDefinition, cancellationToken);

            return;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        PlatformEntities.ProcessDefinition definition = new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ScopeType = ProcessScopeType.Lead,
            FamilyId = null,
            VersionNumber = 1,
            LifecycleState = ProcessDefinitionLifecycleState.Active,
            Name = "Lead Generation",
            Description = "Move a raw lead through bounded identity, activity, data-quality, and commercial-fit checks before creating an opportunity or rejecting it cleanly.",
            IsDefault = true,
            IsActive = true,
            CreatedBy = CurrentUserId,
            LastUpdatedBy = CurrentUserId,
            CreatedOn = now,
            LastUpdated = now
        };

        PlatformEntities.ProcessStep research = NewStep(definition, "lead-research", "Confirm Registry Identity", 10, true, ProcessActionType.Review, null, null, null, 0, 0,
            "Confirm the registry identity of {{Lead.RawCompanyName}}",
            "Use only the supplied company number, legal name, status, registered office, and registry link. Decide whether they describe one coherent legal entity. Record mismatches or uncertainty; do not assess commercial fit and do not search for contacts.",
            null,
            null,
            null,
            "Identity result: matched, partially matched, or unresolved.\nEvidence: list the exact matching fields.\nUncertainty: list only unresolved identity fields.");

        PlatformEntities.ProcessStep describeActivity = NewStep(definition, "company-activity", "Describe Company Activity", 20, false, ProcessActionType.Research, null, null, null, 0, 0,
            "Describe what {{Lead.RawCompanyName}} does",
            "Write a factual two-sentence maximum description of the company's primary activity. Use the supplied SIC codes and existing CRM data first; if a website or registry link is supplied, check at most those two sources. Do not score the lead, find contacts, or propose outreach.",
            null,
            null,
            null,
            "Primary activity: one concise statement.\nEvidence used: SIC, registry, website, or existing CRM data.\nConfidence: high, medium, or low.");

        PlatformEntities.ProcessStep verify = NewStep(definition, "verify-company", "Assess Record Quality", 30, false, ProcessActionType.Review, null, null, null, 0, 0,
            "Assess the data quality for {{Lead.RawCompanyName}}",
            "Check only whether the current record contains a usable legal identity, company status, registered office, activity description, website, and contact route. List present and missing fields. Missing optional fields are a recorded fact, not a reason to keep researching. Do not assess commercial fit.",
            null,
            null,
            null,
            "Present fields: exact list.\nMissing fields: exact list.\nUsable for scoring: yes or no, with one reason.");

        PlatformEntities.ProcessStep assessFit = NewStep(definition, "commercial-fit", "Assess Commercial Fit", 40, false, ProcessActionType.Review, null, null, null, 0, 0,
            "Score the commercial fit of {{Lead.RawCompanyName}}",
            "Using only the verified identity, company activity, status, and existing research, assign a 0-100 fit score. Consider evidence of organisational scale, credible B2B need, and whether Corporate Linx could plausibly add value. Give one opening angle. Do not perform more research and do not find contacts.",
            null,
            null,
            null,
            "Fit score: integer 0-100.\nFit reason: one sentence tied to known evidence.\nOpening angle: one sentence, or none.\nConfidence: high, medium, or low.");

        PlatformEntities.ProcessStep qualify = NewStep(definition, "qualify-lead", "Apply Qualification Rule", 50, false, ProcessActionType.Review, null, null, null, 0, 0,
            "Decide whether {{Lead.RawCompanyName}} becomes an opportunity",
            "Apply this rule without further research: qualify when the legal identity is coherent, the company is not known to be inactive, and the recorded commercial-fit score is 60 or higher. Reject only when the company is known inactive or identity evidence proves the record false. Otherwise defer the lead so a later qualification-rule change can reassess it. State which conditions determined the result.",
            null,
            null,
            null,
            "Identity coherent: yes or no.\nKnown active: yes, no, or uncertain.\nRecorded fit score: integer.\nDecision: qualified, deferred, or rejected.\nRule explanation: one sentence.");

        storage.Add(definition);
        definition.FamilyId = definition.Id;
        storage.AddRange(research, describeActivity, verify, assessFit, qualify);
        storage.AddRange(
            NewTransition(research, describeActivity, "identity-checked", "Registry identity checked", true, ProcessTransitionEffect.None),
            NewTransition(describeActivity, verify, "activity-described", "Company activity described", true, ProcessTransitionEffect.None),
            NewTransition(verify, assessFit, "quality-assessed", "Record quality assessed", true, ProcessTransitionEffect.None),
            NewTransition(assessFit, qualify, "fit-assessed", "Commercial fit assessed", true, ProcessTransitionEffect.None),
            NewTransition(qualify, null, "qualified", "Qualified and create opportunity", false, ProcessTransitionEffect.QualifyLeadAndCreateOpportunity, true),
            NewTransition(qualify, null, "deferred", "Defer until qualification criteria change", true, ProcessTransitionEffect.DeferLead, true),
            NewTransition(qualify, null, "rejected", "Reject lead", false, ProcessTransitionEffect.RejectLead, true));
    }

    async ValueTask UpgradeLeadProcessToBoundedStagesAsync(
        IWorkflowBroker storage,
        PlatformEntities.ProcessDefinition definition,
        CancellationToken cancellationToken)
    {
        List<PlatformEntities.ProcessStep> existingSteps = await storage.ProcessSteps
            .Where(item => item.ProcessDefinitionId == definition.Id)
            .ToListAsync(cancellationToken);

        PlatformEntities.ProcessStep research = existingSteps.First(item => item.Key == "lead-research");
        PlatformEntities.ProcessStep verify = existingSteps.First(item => item.Key == "verify-company");
        PlatformEntities.ProcessStep qualify = existingSteps.First(item => item.Key == "qualify-lead");
        DateTimeOffset now = DateTimeOffset.UtcNow;

        ConfigureBoundedLeadStep(
            research, "Confirm Registry Identity", 10, ProcessActionType.Review,
            "Confirm the registry identity of {{Lead.RawCompanyName}}",
            "Use only the supplied company number, legal name, status, registered office, and registry link. Decide whether they describe one coherent legal entity. Record mismatches or uncertainty; do not assess commercial fit and do not search for contacts.",
            "Identity result: matched, partially matched, or unresolved.\nEvidence: list the exact matching fields.\nUncertainty: list only unresolved identity fields.", now);

        PlatformEntities.ProcessStep describeActivity = NewStep(definition, "company-activity", "Describe Company Activity", 20, false, ProcessActionType.Research, null, null, null, 0, 0,
            "Describe what {{Lead.RawCompanyName}} does",
            "Write a factual two-sentence maximum description of the company's primary activity. Use the supplied SIC codes and existing CRM data first; if a website or registry link is supplied, check at most those two sources. Do not score the lead, find contacts, or propose outreach.",
            null, null, null,
            "Primary activity: one concise statement.\nEvidence used: SIC, registry, website, or existing CRM data.\nConfidence: high, medium, or low.");

        ConfigureBoundedLeadStep(
            verify, "Assess Record Quality", 30, ProcessActionType.Review,
            "Assess the data quality for {{Lead.RawCompanyName}}",
            "Check only whether the current record contains a usable legal identity, company status, registered office, activity description, website, and contact route. List present and missing fields. Missing optional fields are a recorded fact, not a reason to keep researching. Do not assess commercial fit.",
            "Present fields: exact list.\nMissing fields: exact list.\nUsable for scoring: yes or no, with one reason.", now);

        PlatformEntities.ProcessStep assessFit = NewStep(definition, "commercial-fit", "Assess Commercial Fit", 40, false, ProcessActionType.Review, null, null, null, 0, 0,
            "Score the commercial fit of {{Lead.RawCompanyName}}",
            "Using only the verified identity, company activity, status, and existing research, assign a 0-100 fit score. Consider evidence of organisational scale, credible B2B need, and whether Corporate Linx could plausibly add value. Give one opening angle. Do not perform more research and do not find contacts.",
            null, null, null,
            "Fit score: integer 0-100.\nFit reason: one sentence tied to known evidence.\nOpening angle: one sentence, or none.\nConfidence: high, medium, or low.");

        ConfigureBoundedLeadStep(
            qualify, "Apply Qualification Rule", 50, ProcessActionType.Review,
            "Decide whether {{Lead.RawCompanyName}} becomes an opportunity",
            "Apply this rule without further research: qualify when the legal identity is coherent, the company is not known to be inactive, and the recorded commercial-fit score is 60 or higher. Reject only when the company is known inactive or identity evidence proves the record false. Otherwise defer the lead so a later qualification-rule change can reassess it. State which conditions determined the result.",
            "Identity coherent: yes or no.\nKnown active: yes, no, or uncertain.\nRecorded fit score: integer.\nDecision: qualified, deferred, or rejected.\nRule explanation: one sentence.", now);

        List<Guid> upgradedStepIds = [research.Id, verify.Id, qualify.Id];
        List<PlatformEntities.ProcessTransition> oldTransitions = await storage.ProcessTransitions
            .Where(item => upgradedStepIds.Contains(item.ProcessStepId))
            .ToListAsync(cancellationToken);
        storage.RemoveRange(oldTransitions);

        storage.AddRange(describeActivity, assessFit);
        storage.AddRange(
            NewTransition(research, describeActivity, "identity-checked", "Registry identity checked", true, ProcessTransitionEffect.None),
            NewTransition(describeActivity, verify, "activity-described", "Company activity described", true, ProcessTransitionEffect.None),
            NewTransition(verify, assessFit, "quality-assessed", "Record quality assessed", true, ProcessTransitionEffect.None),
            NewTransition(assessFit, qualify, "fit-assessed", "Commercial fit assessed", true, ProcessTransitionEffect.None),
            NewTransition(qualify, null, "qualified", "Qualified and create opportunity", false, ProcessTransitionEffect.QualifyLeadAndCreateOpportunity, true),
            NewTransition(qualify, null, "deferred", "Defer until qualification criteria change", true, ProcessTransitionEffect.DeferLead, true),
            NewTransition(qualify, null, "rejected", "Reject lead", false, ProcessTransitionEffect.RejectLead, true));

        List<PlatformEntities.ProcessTask> pendingTasks = await storage.ProcessTasks
            .Where(item => item.State == ProcessTaskState.Pending && upgradedStepIds.Contains(item.ProcessStepId))
            .ToListAsync(cancellationToken);

        foreach (PlatformEntities.ProcessTask task in pendingTasks)
        {
            PlatformEntities.ProcessStep step = task.ProcessStepId == research.Id ? research
                : task.ProcessStepId == verify.Id ? verify
                : qualify;
            TaskRenderContext renderContext = await BuildTaskRenderContextAsync(
                storage, task.LeadId, task.TenantCompanyRelationshipId, task.OpportunityId, task.ClientAccountId, cancellationToken);
            TaskRenderValues rendered = RenderTaskValues(step, renderContext, now);
            task.ActionType = step.ActionType;
            task.RenderedTitle = rendered.Title;
            task.RenderedInstructions = rendered.Instructions;
            task.RenderedQuestionSet = rendered.QuestionSet;
            task.LastUpdatedBy = CurrentUserId;
            task.LastUpdated = now;
        }
    }

    void ConfigureBoundedLeadStep(
        PlatformEntities.ProcessStep step,
        string name,
        int sequence,
        ProcessActionType actionType,
        string title,
        string instructions,
        string questions,
        DateTimeOffset now)
    {
        step.Name = name;
        step.Sequence = sequence;
        step.ActionType = actionType;
        step.TaskTitleTemplate = title;
        step.TaskInstructionsTemplate = instructions;
        step.QuestionSetTemplate = questions;
        step.DueAfterDays = 0;
        step.DueAfterHours = 0;
        step.LastUpdatedBy = CurrentUserId;
        step.LastUpdated = now;
    }

    async ValueTask EnsureOpportunityProcessAsync(
        IWorkflowBroker storage,
        string tenantId,
        CancellationToken cancellationToken)
    {
        PlatformEntities.ProcessDefinition existingDefinition = await storage.ProcessDefinitions.FirstOrDefaultAsync(
                item => item.TenantId == tenantId
                    && item.ScopeType == ProcessScopeType.Opportunity
                    && (item.LifecycleState == ProcessDefinitionLifecycleState.Active || item.IsActive),
                cancellationToken);

        if (existingDefinition is not null)
        {
            bool isCurrentHandoffProcess = await storage.ProcessSteps.AnyAsync(
                item => item.ProcessDefinitionId == existingDefinition.Id
                    && item.Key == "handoff-account-owner"
                    && item.IsActive,
                cancellationToken);

            if (isCurrentHandoffProcess)
            {
                NormalizeSeedDefinitionMetadata(
                    existingDefinition,
                    "Opportunity Conversion",
                    "Qualify a positive response through demo interest, hand the opportunity to its account owner, and wait for a human commercial decision.");
                return;
            }

            existingDefinition.IsDefault = false;
            existingDefinition.IsActive = false;
            existingDefinition.LifecycleState = ProcessDefinitionLifecycleState.Archived;
            existingDefinition.LastUpdatedBy = CurrentUserId;
            existingDefinition.LastUpdated = DateTimeOffset.UtcNow;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        int versionNumber = await storage.ProcessDefinitions
            .Where(item => item.TenantId == tenantId && item.ScopeType == ProcessScopeType.Opportunity)
            .Select(item => (int?)item.VersionNumber)
            .MaxAsync(cancellationToken) ?? 0;
        PlatformEntities.ProcessDefinition definition = new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ScopeType = ProcessScopeType.Opportunity,
            FamilyId = existingDefinition?.FamilyId ?? existingDefinition?.Id,
            VersionNumber = versionNumber + 1,
            LifecycleState = ProcessDefinitionLifecycleState.Active,
            Name = "Opportunity Conversion",
            Description = "Qualify a positive response through demo interest, hand the opportunity to its account owner, and wait for a human commercial decision.",
            IsDefault = true,
            IsActive = true,
            CreatedBy = CurrentUserId,
            LastUpdatedBy = CurrentUserId,
            CreatedOn = now,
            LastUpdated = now
        };

        PlatformEntities.ProcessStep confirmRoute = NewStep(definition, "confirm-route", "Confirm Outreach Route", 10, true, ProcessActionType.Review, RelationshipStatus.Prospect, SalesPipelineStage.ContactIdentified, null, 0, 0,
            "Confirm the opening route into {{Company.OfficialName}}",
            "Choose the best contact and sharpen the opening angle before sending the first message.",
            null,
            null,
            null,
            "Who should we contact?\nWhat opening route is strongest?");

        PlatformEntities.ProcessStep introEmail = NewStep(definition, "intro-email", "Send Intro Email", 20, false, ProcessActionType.Email, RelationshipStatus.ActiveOpportunity, SalesPipelineStage.OutreachReady, null, 0, 0,
            "Send the first outreach email to {{Company.OfficialName}}",
            "Review the generated draft, make any necessary edits, approve it, then send it.",
            "Potential value for {{Company.OfficialName}}",
            "Hello {{Contact.Name}},\n\nI am reaching out because there appears to be a credible opportunity to discuss {{Relationship.PreferredOpeningAngle}} with {{Company.OfficialName}}.\n\nIf helpful, I can share a concise outline and suggest a short call to test whether there is mutual fit.\n\nKind regards,\n{{Relationship.AccountOwnerDisplayName}}",
            null,
            null);

        PlatformEntities.ProcessStep reviewResponse = NewStep(definition, "review-response", "Review Response", 30, false, ProcessActionType.ManualTask, RelationshipStatus.ActiveOpportunity, SalesPipelineStage.OutreachSent, null, 3, 0,
            "Review the response from {{Company.OfficialName}}",
            "Check whether the recipient replied and decide whether to follow up, advance, or close out the opportunity.",
            null,
            null,
            null,
            "Was there a reply?\nWas it positive, neutral, or negative?");

        PlatformEntities.ProcessStep followUpCall = NewStep(definition, "follow-up-call", "Make Follow-Up Call", 40, false, ProcessActionType.Call, RelationshipStatus.ActiveOpportunity, SalesPipelineStage.OutreachSent, null, 0, 0,
            "Call {{Company.OfficialName}} to follow up",
            "Use the script to confirm whether the opportunity is live and whether discovery can be booked.",
            null,
            null,
            "Questions:\n1. Are you the right person?\n2. Is this issue live?\n3. Is a short discovery call worthwhile?",
            "Was the contact reached?\nWas there interest?");

        PlatformEntities.ProcessStep handoffToAccountOwner = NewStep(definition, "handoff-account-owner", "Hand Off to Account Owner", 50, false, ProcessActionType.Email, RelationshipStatus.ActiveOpportunity, SalesPipelineStage.DiscoveryBooked, null, 0, 0,
            "Hand off {{Company.OfficialName}} to {{Relationship.AccountOwnerDisplayName}}",
            "Review the generated internal handoff, correct anything that is unclear, and send it to the account owner. This is the final AI-owned action for the opportunity.",
            "Demo-ready opportunity: {{Company.OfficialName}}",
            "Hello {{Relationship.AccountOwnerDisplayName}},\n\nA potential client is ready for human ownership. The contact has responded positively and is interested in at least a demo.\n\nPOTENTIAL CLIENT\nCompany: {{Company.OfficialName}}\nTrading name: {{Company.TradingName}}\nWebsite: {{Company.WebsiteUrl}}\nContact: {{Contact.Name}}\nPosition: {{Contact.Position}}\nEmail: {{Contact.EmailAddress}}\nPhone: {{Contact.PhoneNumber}}\n\nWHAT THEY HAVE ASKED / LATEST RESPONSE\nSubject: {{Opportunity.LatestResponseSubject}}\n{{Opportunity.LatestResponseBody}}\n\nOPPORTUNITY POTENTIAL\nSummary: {{Relationship.OpportunitySummary}}\nNeed or pain: {{Opportunity.PainSummary}}\nValue hypothesis: {{Opportunity.ValueHypothesis}}\nEstimated annual value: {{Opportunity.EstimatedAnnualValue}}\nProbability: {{Opportunity.Probability}}\nDecision process: {{Opportunity.DecisionProcess}}\n\nRecommended next step: arrange the demo, take ownership of commercial discussions, and negotiate any contract terms. Once those discussions are complete, record the final go/no-go decision in CRM.\n\nThe AI-led qualification process is now complete.",
            null,
            null,
            ProcessEmailRecipientTarget.AccountOwner);

        PlatformEntities.ProcessStep accountOwnerDecision = NewStep(definition, "account-owner-decision", "Account Owner Decision", 60, false, ProcessActionType.Approval, RelationshipStatus.ActiveOpportunity, SalesPipelineStage.Negotiation, null, 0, 0,
            "Decide whether {{Company.OfficialName}} moves forward",
            "Human decision only. After the demo, commercial discussion, and any contract negotiations, decide whether this opportunity becomes a client, remains in negotiation, or closes without proceeding.",
            null,
            null,
            null,
            "Has the demo taken place?\nAre commercials and contract terms agreed?\nShould this organisation become a client?");

        storage.Add(definition);
        definition.FamilyId ??= definition.Id;
        storage.AddRange(confirmRoute, introEmail, reviewResponse, followUpCall, handoffToAccountOwner, accountOwnerDecision);
        storage.AddRange(
            NewTransition(confirmRoute, introEmail, "ready", "Route confirmed", true, ProcessTransitionEffect.None),
            NewTransition(introEmail, reviewResponse, "sent", "Email sent", true, ProcessTransitionEffect.None),
            NewTransition(reviewResponse, handoffToAccountOwner, "demo-interest", "Positive response and demo interest", false, ProcessTransitionEffect.None),
            NewTransition(reviewResponse, followUpCall, "no-reply", "No reply", true, ProcessTransitionEffect.None),
            NewTransition(reviewResponse, null, "not-interested", "Not interested or no fit", false, ProcessTransitionEffect.CloseOpportunityAsLost, true, resultingRelationshipStatus: RelationshipStatus.Disqualified, resultingSalesStage: SalesPipelineStage.Lost),
            NewTransition(followUpCall, handoffToAccountOwner, "demo-interest", "Positive response and demo interest", true, ProcessTransitionEffect.None),
            NewTransition(followUpCall, reviewResponse, "await-response", "Awaiting response", false, ProcessTransitionEffect.None),
            NewTransition(followUpCall, null, "not-interested", "No route forward", false, ProcessTransitionEffect.CloseOpportunityAsLost, true, resultingRelationshipStatus: RelationshipStatus.Disqualified, resultingSalesStage: SalesPipelineStage.Lost),
            NewTransition(handoffToAccountOwner, accountOwnerDecision, "sent", "Handoff sent", true, ProcessTransitionEffect.None),
            NewTransition(accountOwnerDecision, null, "move-forward", "Move forward as client", false, ProcessTransitionEffect.CreateClientAccount, true, resultingRelationshipStatus: RelationshipStatus.Onboarding, resultingSalesStage: SalesPipelineStage.Won),
            NewTransition(accountOwnerDecision, accountOwnerDecision, "continue-negotiations", "Continue negotiations", true, ProcessTransitionEffect.None),
            NewTransition(accountOwnerDecision, null, "do-not-proceed", "Do not proceed", false, ProcessTransitionEffect.CloseOpportunityAsLost, true, resultingRelationshipStatus: RelationshipStatus.Disqualified, resultingSalesStage: SalesPipelineStage.Lost));
    }

    async ValueTask EnsureClientProcessAsync(
        IWorkflowBroker storage,
        string tenantId,
        CancellationToken cancellationToken)
    {
        PlatformEntities.ProcessDefinition existingDefinition = await storage.ProcessDefinitions.FirstOrDefaultAsync(
                item => item.TenantId == tenantId
                    && item.ScopeType == ProcessScopeType.ClientAccount
                    && (item.LifecycleState == ProcessDefinitionLifecycleState.Active || item.IsActive),
                cancellationToken);

        if (existingDefinition is not null)
        {
            bool hasInstances = await storage.ProcessInstances.AnyAsync(
                item => item.ProcessDefinitionId == existingDefinition.Id,
                cancellationToken);

            if (!hasInstances)
            {
                List<PlatformEntities.ProcessStep> oldSteps = await storage.ProcessSteps
                    .Where(item => item.ProcessDefinitionId == existingDefinition.Id)
                    .ToListAsync(cancellationToken);
                List<Guid> oldStepIds = [.. oldSteps.Select(item => item.Id)];
                List<PlatformEntities.ProcessTransition> oldTransitions = oldStepIds.Count == 0
                    ? []
                    : await storage.ProcessTransitions
                        .Where(item => oldStepIds.Contains(item.ProcessStepId))
                        .ToListAsync(cancellationToken);

                storage.RemoveRange(oldTransitions);
                storage.RemoveRange(oldSteps);
                NormalizeSeedDefinitionMetadata(
                    existingDefinition,
                    "Client Maintenance",
                    "Schedule regular client status reviews so relationship health, risks, and next actions stay current.");
                AddClientMaintenanceSteps(storage, existingDefinition);
                return;
            }

            if (string.Equals(existingDefinition.Name, "Client Maintenance", StringComparison.OrdinalIgnoreCase))
            {
                NormalizeSeedDefinitionMetadata(
                    existingDefinition,
                    "Client Maintenance",
                    "Schedule regular client status reviews so relationship health, risks, and next actions stay current.");
                return;
            }

            existingDefinition.LifecycleState = ProcessDefinitionLifecycleState.Archived;
            existingDefinition.IsActive = false;
            existingDefinition.IsDefault = false;
            existingDefinition.LastUpdatedBy = CurrentUserId;
            existingDefinition.LastUpdated = DateTimeOffset.UtcNow;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        PlatformEntities.ProcessDefinition definition = new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ScopeType = ProcessScopeType.ClientAccount,
            FamilyId = null,
            VersionNumber = 1,
            LifecycleState = ProcessDefinitionLifecycleState.Active,
            Name = "Client Maintenance",
            Description = "Schedule regular client status reviews so relationship health, risks, and next actions stay current.",
            IsDefault = true,
            IsActive = true,
            CreatedBy = CurrentUserId,
            LastUpdatedBy = CurrentUserId,
            CreatedOn = now,
            LastUpdated = now
        };

        storage.Add(definition);
        definition.FamilyId = definition.Id;
        AddClientMaintenanceSteps(storage, definition);
    }

    void NormalizeSeedDefinitionMetadata(
        PlatformEntities.ProcessDefinition definition,
        string name,
        string description)
    {
        definition.FamilyId ??= definition.Id;
        definition.VersionNumber = Math.Max(1, definition.VersionNumber);
        definition.LifecycleState = ProcessDefinitionLifecycleState.Active;
        definition.Name = name;
        definition.Description = description;
        definition.IsDefault = true;
        definition.IsActive = true;
        definition.LastUpdatedBy = CurrentUserId;
        definition.LastUpdated = DateTimeOffset.UtcNow;
    }

    static void AddClientMaintenanceSteps(
        IWorkflowBroker storage,
        PlatformEntities.ProcessDefinition definition)
    {
        PlatformEntities.ProcessStep review = NewStep(
            definition,
            "client-status-review",
            "Review Client Status",
            10,
            true,
            ProcessActionType.ManualTask,
            null,
            null,
            null,
            90,
            0,
            "Complete the regular status review for {{Company.OfficialName}}",
            "Check relationship health, delivery status, stakeholder changes, risks, opportunities, and agree the next check-in.",
            null,
            null,
            null,
            "Is the client satisfied?\nHas delivery or scope changed?\nAre there risks or expansion opportunities?\nWho owns the next action?");

        storage.Add(review);
        storage.Add(
            NewTransition(
                review,
                review,
                "reviewed",
                "Review complete; schedule the next check-in",
                true,
                ProcessTransitionEffect.None));
    }

    static PlatformEntities.ProcessStep NewStep(
        PlatformEntities.ProcessDefinition definition,
        string key,
        string name,
        int sequence,
        bool isEntryPoint,
        ProcessActionType actionType,
        RelationshipStatus? relationshipStatusOnActivate,
        SalesPipelineStage? salesStageOnActivate,
        ClientAccountStatus? clientAccountStatusOnActivate,
        int dueAfterDays,
        int dueAfterHours,
        string titleTemplate,
        string instructionsTemplate,
        string emailSubjectTemplate,
        string emailBodyTemplate,
        string callScriptTemplate,
        string questionSetTemplate,
        ProcessEmailRecipientTarget emailRecipientTarget = ProcessEmailRecipientTarget.PrimaryContact) =>
        new()
        {
            Id = Guid.NewGuid(),
            ProcessDefinitionId = definition.Id,
            Key = key,
            Name = name,
            Sequence = sequence,
            IsEntryPoint = isEntryPoint,
            IsActive = true,
            ActionType = actionType,
            RelationshipStatusOnActivate = relationshipStatusOnActivate,
            SalesStageOnActivate = salesStageOnActivate,
            ClientAccountStatusOnActivate = clientAccountStatusOnActivate,
            DueAfterDays = dueAfterDays,
            DueAfterHours = dueAfterHours,
            TaskTitleTemplate = titleTemplate,
            TaskInstructionsTemplate = instructionsTemplate,
            EmailRecipientTarget = emailRecipientTarget,
            EmailSubjectTemplate = emailSubjectTemplate,
            EmailBodyTemplate = emailBodyTemplate,
            CallScriptTemplate = callScriptTemplate,
            QuestionSetTemplate = questionSetTemplate,
            CreatedBy = definition.CreatedBy,
            LastUpdatedBy = definition.LastUpdatedBy,
            CreatedOn = definition.CreatedOn,
            LastUpdated = definition.LastUpdated
        };

    static PlatformEntities.ProcessTransition NewTransition(
        PlatformEntities.ProcessStep fromStep,
        PlatformEntities.ProcessStep toStep,
        string outcomeKey,
        string outcomeLabel,
        bool isDefaultOutcome,
        ProcessTransitionEffect effect,
        bool isTerminal = false,
        RelationshipStatus? resultingRelationshipStatus = null,
        SalesPipelineStage? resultingSalesStage = null,
        ClientAccountStatus? resultingClientAccountStatus = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            ProcessStepId = fromStep.Id,
            NextProcessStepId = isTerminal ? null : toStep?.Id,
            OutcomeKey = outcomeKey,
            OutcomeLabel = outcomeLabel,
            IsDefaultOutcome = isDefaultOutcome,
            IsTerminal = isTerminal,
            Effect = effect,
            ResultingRelationshipStatus = resultingRelationshipStatus,
            ResultingSalesStage = resultingSalesStage,
            ResultingClientAccountStatus = resultingClientAccountStatus,
            CreatedBy = fromStep.CreatedBy,
            LastUpdatedBy = fromStep.LastUpdatedBy,
            CreatedOn = fromStep.CreatedOn,
            LastUpdated = fromStep.LastUpdated
        };

    static IEnumerable<string> SplitAddresses(string addresses) =>
        string.IsNullOrWhiteSpace(addresses)
            ? []
            : addresses
                .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(address => !string.IsNullOrWhiteSpace(address))
                .Distinct(StringComparer.OrdinalIgnoreCase);

    static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    static string Normalize(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

    static EmailState NormalizeLegacyEmailState(EmailState state) =>
        (int)state switch
        {
            1 => EmailState.Approved,
            2 => EmailState.Sending,
            3 => EmailState.Sent,
            4 => EmailState.Failed,
            5 => EmailState.Cancelled,
            _ => state
        };

    static RelationshipStatus Max(RelationshipStatus current, RelationshipStatus proposed) =>
        current >= proposed ? current : proposed;

    static SalesPipelineStage Max(SalesPipelineStage current, SalesPipelineStage proposed) =>
        current >= proposed ? current : proposed;

    static ClientAccountStatus Max(ClientAccountStatus current, ClientAccountStatus proposed) =>
        current >= proposed ? current : proposed;

    async ValueTask<PlatformEntities.Company> ResolveCompanyAsync(
        IWorkflowBroker storage,
        PlatformEntities.Lead lead,
        PlatformEntities.TenantCompanyRelationship relationship,
        CancellationToken cancellationToken)
    {
        Guid? companyId = lead?.CompanyId ?? relationship?.CompanyId;
        if (!companyId.HasValue)
            return null;

        if (relationship?.Company is not null && relationship.Company.Id == companyId.Value)
            return relationship.Company;

        return await storage.Companies.FirstOrDefaultAsync(item => item.Id == companyId.Value, cancellationToken);
    }

    static string ResolveWorkflowRecordName(
        PlatformEntities.Lead lead,
        PlatformEntities.Company company,
        PlatformEntities.TenantCompanyRelationship relationship,
        PlatformEntities.Opportunity opportunity,
        PlatformEntities.ClientAccount clientAccount) =>
        FirstNonEmpty(
            CompanyNames.ResolvePreferredName(company),
            CompanyNames.ResolvePreferredName(relationship?.Company),
            lead?.RawCompanyName,
            opportunity?.Id.ToString(),
            clientAccount?.Id.ToString(),
            relationship?.Id.ToString(),
            "unknown record");

    sealed record TaskRenderContext(
        PlatformEntities.Lead Lead,
        PlatformEntities.TenantCompanyRelationship Relationship,
        PlatformEntities.Opportunity Opportunity,
        PlatformEntities.ClientAccount ClientAccount,
        PlatformEntities.Company Company,
        PlatformEntities.CompanyContact Contact,
        PlatformEntities.Activity LatestInboundActivity);

    sealed record TaskRenderValues(
        string Title,
        string Instructions,
        string EmailSubject,
        string EmailBody,
        string CallScript,
        string QuestionSet);

    sealed record RelatedEmailDraftSource(
        PlatformEntities.AgentMessage Message,
        PlatformEntities.ProcessDefinition Definition,
        PlatformEntities.ProcessStep Step,
        List<PlatformEntities.ProcessTask> Tasks,
        PlatformEntities.Email ApprovedCorrection,
        string LiveTemplateRenderedSubject,
        string LiveTemplateRenderedBody,
        bool LiveTemplateMatchesApprovedCorrection);

    string CurrentUserId =>
        !string.IsNullOrWhiteSpace(currentExecutionUserAccessor?.UserId)
            ? currentExecutionUserAccessor.UserId
            : string.IsNullOrWhiteSpace(authInfo?.SSOUserId) || string.Equals(authInfo.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase)
            ? "system"
            : authInfo.SSOUserId;

    static void Touch(PlatformEntities.ICrmEntity entity, DateTimeOffset now)
    {
        if (entity is null)
            return;

        entity.LastUpdated = now;
        entity.LastUpdatedBy = "system";
    }
}
