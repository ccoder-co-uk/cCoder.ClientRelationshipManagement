using cCoder.ClientRelationshipManagement.Platform.Data;
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
using PlatformEntities = cCoder.ClientRelationshipManagement.Platform.Models.Entities;

namespace ClientRelationshipManagement.Web.Services.Processes;

public sealed class WorkflowAutomationService(
    IPlatformDbContextFactory dbContextFactory,
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
        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
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
            .. await context.Leads.AsNoTracking().Select(lead => lead.TenantId).Distinct().ToListAsync(cancellationToken),
            .. await context.TenantCompanyRelationships.AsNoTracking().Select(relationship => relationship.TenantId).Distinct().ToListAsync(cancellationToken)
        ];

        await ArchiveDuplicateActiveDefinitionsAsync(context, tenantIds, cancellationToken);

        foreach (string tenantId in tenantIds.Where(tenantId => !string.IsNullOrWhiteSpace(tenantId)))
        {
            await EnsureLeadProcessAsync(context, tenantId, cancellationToken);
            await EnsureOpportunityProcessAsync(context, tenantId, cancellationToken);
            await EnsureClientProcessAsync(context, tenantId, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async ValueTask EnsureCoverageAsync(
        Guid? leadId = null,
        Guid? opportunityId = null,
        Guid? clientAccountId = null,
        bool forceCreate = false,
        CancellationToken cancellationToken = default)
    {
        await EnsureSeedProcessesAsync(cancellationToken);

        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);

        await NormaliseImportedCompanyNamesAsync(context, cancellationToken);
        await NormaliseImportedEmailStatesAsync(context, opportunityId, clientAccountId, cancellationToken);

        List<PlatformEntities.Lead> leads = await context.Leads
            .Where(leadId.HasValue ? lead => lead.Id == leadId.Value : _ => true)
            .OrderBy(lead => lead.CreatedOn)
            .ToListAsync(cancellationToken);

        List<PlatformEntities.Opportunity> opportunities = await context.Opportunities
            .Where(opportunityId.HasValue ? opportunity => opportunity.Id == opportunityId.Value : _ => true)
            .OrderBy(opportunity => opportunity.CreatedOn)
            .ToListAsync(cancellationToken);

        List<PlatformEntities.ClientAccount> clientAccounts = await context.ClientAccounts
            .Where(clientAccountId.HasValue ? clientAccount => clientAccount.Id == clientAccountId.Value : _ => true)
            .OrderBy(clientAccount => clientAccount.CreatedOn)
            .ToListAsync(cancellationToken);

        foreach (PlatformEntities.Lead lead in leads)
            await EnsureLeadCoverageAsync(context, lead, forceCreate, cancellationToken);

        foreach (PlatformEntities.Opportunity opportunity in opportunities)
            await EnsureOpportunityCoverageAsync(context, opportunity, forceCreate, cancellationToken);

        foreach (PlatformEntities.ClientAccount clientAccount in clientAccounts)
            await EnsureClientCoverageAsync(context, clientAccount, forceCreate, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        await SynchroniseCurrentTasksAsync(context, cancellationToken);
    }

    public async ValueTask<PlatformEntities.ProcessTask> CompleteTaskAsync(
        ProcessTaskCompletionCommand command,
        CancellationToken cancellationToken = default)
    {
        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);

        PlatformEntities.ProcessTask task = await context.ProcessTasks
            .Include(item => item.ProcessInstance)
            .Include(item => item.ProcessStep)
            .FirstOrDefaultAsync(
                item => item.Id == command.ProcessTaskId && item.State == ProcessTaskState.Pending,
                cancellationToken);

        if (task is null)
            return null;

        await CompleteTaskAsync(context, task, command.OutcomeKey, command.CompletionNote, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return task;
    }

    public async ValueTask<bool> CompleteEmailTaskAsync(Guid emailId, CancellationToken cancellationToken = default)
    {
        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);

        PlatformEntities.ProcessTask task = await context.ProcessTasks
            .Include(item => item.ProcessInstance)
            .Include(item => item.ProcessStep)
            .FirstOrDefaultAsync(
                item => item.EmailId == emailId && item.State == ProcessTaskState.Pending,
                cancellationToken);

        if (task is null)
            return false;

        await CompleteTaskAsync(context, task, "sent", "Email sent.", cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    async ValueTask EnsureLeadCoverageAsync(
        PlatformDbContext context,
        PlatformEntities.Lead lead,
        bool forceCreate,
        CancellationToken cancellationToken)
    {
        PlatformEntities.ProcessInstance activeInstance = await context.ProcessInstances
            .FirstOrDefaultAsync(
                item => item.LeadId == lead.Id && item.State == ProcessInstanceState.Active,
                cancellationToken);

        if (lead.Status is LeadStatus.Rejected or LeadStatus.Converted)
        {
            await CloseActiveInstanceAsync(context, activeInstance, lead.Status.ToString(), cancellationToken);
            return;
        }

        if (activeInstance is null)
        {
            PlatformEntities.ProcessDefinition definition = await GetDefaultDefinitionAsync(context, lead.TenantId, ProcessScopeType.Lead, cancellationToken);
            PlatformEntities.ProcessStep entryStep = await GetEntryStepAsync(context, definition.Id, cancellationToken);
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

            context.ProcessInstances.Add(activeInstance);
        }

        if (!forceCreate && await context.ProcessTasks.AnyAsync(
                item => item.ProcessInstanceId == activeInstance.Id && item.State == ProcessTaskState.Pending,
                cancellationToken))
        {
            return;
        }

        await EnsurePendingTaskAsync(context, activeInstance, cancellationToken);
    }

    async ValueTask EnsureOpportunityCoverageAsync(
        PlatformDbContext context,
        PlatformEntities.Opportunity opportunity,
        bool forceCreate,
        CancellationToken cancellationToken)
    {
        PlatformEntities.ProcessInstance activeInstance = await context.ProcessInstances
            .FirstOrDefaultAsync(
                item => item.OpportunityId == opportunity.Id && item.State == ProcessInstanceState.Active,
                cancellationToken);

        if (opportunity.Stage is SalesPipelineStage.Won or SalesPipelineStage.Lost)
        {
            await CloseActiveInstanceAsync(context, activeInstance, opportunity.Stage.ToString(), cancellationToken);
            return;
        }

        if (activeInstance is null)
        {
            string tenantId = await context.TenantCompanyRelationships
                .Where(relationship => relationship.Id == opportunity.TenantCompanyRelationshipId)
                .Select(relationship => relationship.TenantId)
                .FirstOrDefaultAsync(cancellationToken);

            PlatformEntities.ProcessDefinition definition = await GetDefaultDefinitionAsync(context, tenantId, ProcessScopeType.Opportunity, cancellationToken);
            PlatformEntities.ProcessStep inferredStep = await ResolveOpportunityStepAsync(context, definition.Id, opportunity, cancellationToken);
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

            context.ProcessInstances.Add(activeInstance);
        }

        bool reconciled = await ReconcileOpportunityWorkflowAsync(context, activeInstance, opportunity, cancellationToken);

        if (reconciled)
            await context.SaveChangesAsync(cancellationToken);

        if (!forceCreate && await context.ProcessTasks.AnyAsync(
                item => item.ProcessInstanceId == activeInstance.Id && item.State == ProcessTaskState.Pending,
                cancellationToken))
        {
            return;
        }

        await EnsurePendingTaskAsync(context, activeInstance, cancellationToken);
    }

    async ValueTask EnsureClientCoverageAsync(
        PlatformDbContext context,
        PlatformEntities.ClientAccount clientAccount,
        bool forceCreate,
        CancellationToken cancellationToken)
    {
        PlatformEntities.ProcessInstance activeInstance = await context.ProcessInstances
            .FirstOrDefaultAsync(
                item => item.ClientAccountId == clientAccount.Id && item.State == ProcessInstanceState.Active,
                cancellationToken);

        if (clientAccount.Status == ClientAccountStatus.Closed)
        {
            await CloseActiveInstanceAsync(context, activeInstance, clientAccount.Status.ToString(), cancellationToken);
            return;
        }

        if (activeInstance is null)
        {
            string tenantId = await context.TenantCompanyRelationships
                .Where(relationship => relationship.Id == clientAccount.TenantCompanyRelationshipId)
                .Select(relationship => relationship.TenantId)
                .FirstOrDefaultAsync(cancellationToken);

            PlatformEntities.ProcessDefinition definition = await GetDefaultDefinitionAsync(context, tenantId, ProcessScopeType.ClientAccount, cancellationToken);
            PlatformEntities.ProcessStep entryStep = await GetEntryStepAsync(context, definition.Id, cancellationToken);
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

            context.ProcessInstances.Add(activeInstance);
        }

        if (!forceCreate && await context.ProcessTasks.AnyAsync(
                item => item.ProcessInstanceId == activeInstance.Id && item.State == ProcessTaskState.Pending,
                cancellationToken))
        {
            return;
        }

        await EnsurePendingTaskAsync(context, activeInstance, cancellationToken);
    }

    async ValueTask EnsurePendingTaskAsync(
        PlatformDbContext context,
        PlatformEntities.ProcessInstance instance,
        CancellationToken cancellationToken)
    {
        PlatformEntities.ProcessTask existingTask = await context.ProcessTasks
            .FirstOrDefaultAsync(
                item => item.ProcessInstanceId == instance.Id && item.State == ProcessTaskState.Pending,
                cancellationToken);

        if (existingTask is not null)
        {
            await RefreshPendingTaskAsync(context, existingTask, cancellationToken);
            instance.CurrentProcessTaskId = existingTask.Id;
            instance.LastUpdatedBy = CurrentUserId;
            instance.LastUpdated = DateTimeOffset.UtcNow;
            return;
        }

        PlatformEntities.ProcessStep step = await context.ProcessSteps
            .FirstOrDefaultAsync(item => item.Id == instance.CurrentProcessStepId, cancellationToken);

        if (step is null)
            return;

        PlatformEntities.ProcessTask task = await CreateTaskForStepAsync(context, instance, step, cancellationToken);
        instance.LastUpdatedBy = CurrentUserId;
        instance.LastUpdated = DateTimeOffset.UtcNow;
    }

    async ValueTask ArchiveDuplicateActiveDefinitionsAsync(
        PlatformDbContext context,
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
            .. await context.ProcessInstances
                .AsNoTracking()
                .Where(item => item.State == ProcessInstanceState.Active)
                .Select(item => item.ProcessDefinitionId)
                .Distinct()
                .ToListAsync(cancellationToken)
        ];

        List<PlatformEntities.ProcessDefinition> activeDefinitions = await context.ProcessDefinitions
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
        PlatformDbContext context,
        CancellationToken cancellationToken)
    {
        List<PlatformEntities.ProcessInstance> instancesNeedingCurrentTask = await context.ProcessInstances
            .Where(item => item.State == ProcessInstanceState.Active && item.CurrentProcessTaskId == null)
            .ToListAsync(cancellationToken);

        if (instancesNeedingCurrentTask.Count == 0)
            return;

        foreach (PlatformEntities.ProcessInstance instance in instancesNeedingCurrentTask)
        {
            PlatformEntities.ProcessTask pendingTask = await context.ProcessTasks
                .Where(item => item.ProcessInstanceId == instance.Id && item.State == ProcessTaskState.Pending)
                .OrderBy(item => item.DueOn)
                .FirstOrDefaultAsync(cancellationToken);

            if (pendingTask is null)
                continue;

            instance.CurrentProcessTaskId = pendingTask.Id;
            instance.LastUpdatedBy = CurrentUserId;
            instance.LastUpdated = DateTimeOffset.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    async ValueTask NormaliseImportedEmailStatesAsync(
        PlatformDbContext context,
        Guid? opportunityId,
        Guid? clientAccountId,
        CancellationToken cancellationToken)
    {
        if (!await EmailTableExistsAsync(context, cancellationToken))
            return;

        IQueryable<PlatformEntities.Email> query = context.Emails;

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
            : await context.Materials
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
        PlatformDbContext context,
        CancellationToken cancellationToken)
    {
        List<PlatformEntities.Company> companies = await context.Companies
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
        PlatformDbContext context,
        CancellationToken cancellationToken)
    {
        DbConnection connection = context.Database.GetDbConnection();
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
        PlatformDbContext context,
        PlatformEntities.ProcessInstance activeInstance,
        PlatformEntities.Opportunity opportunity,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        PlatformEntities.TenantCompanyRelationship relationship = await context.TenantCompanyRelationships
            .FirstOrDefaultAsync(item => item.Id == opportunity.TenantCompanyRelationshipId, cancellationToken);

        bool hasSentEmail = await HasSentOpportunityEmailAsync(context, opportunity, cancellationToken);
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
            context,
            activeInstance.ProcessDefinitionId,
            opportunity,
            cancellationToken);

        if (inferredStep is null)
            return workflowChanged;

        List<PlatformEntities.ProcessTask> pendingTasks = await context.ProcessTasks
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
        PlatformDbContext context,
        PlatformEntities.ProcessInstance instance,
        PlatformEntities.ProcessStep step,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        TaskRenderContext renderContext = await BuildTaskRenderContextAsync(
            context,
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

        context.ProcessTasks.Add(task);

        if (step.ActionType == ProcessActionType.Email && renderContext.Relationship is not null)
        {
            PlatformEntities.Material material = new()
            {
                Id = Guid.NewGuid(),
                TenantCompanyRelationshipId = renderContext.Relationship.Id,
                OpportunityId = renderContext.Opportunity?.Id,
                ClientAccountId = renderContext.ClientAccount?.Id,
                CompanyContactId = renderContext.Contact?.Id,
                Name = task.RenderedEmailSubject ?? task.RenderedTitle,
                Type = MaterialType.Email,
                Status = MaterialStatus.Draft,
                Notes = task.RenderedEmailBody,
                CreatedBy = CurrentUserId,
                LastUpdatedBy = CurrentUserId,
                CreatedOn = now,
                LastUpdated = now
            };

            context.Materials.Add(material);

            PlatformEntities.Email email = new()
            {
                Id = Guid.NewGuid(),
                TenantCompanyRelationshipId = renderContext.Relationship.Id,
                OpportunityId = renderContext.Opportunity?.Id,
                ClientAccountId = renderContext.ClientAccount?.Id,
                MaterialId = material.Id,
                CompanyContactId = renderContext.Contact?.Id,
                SenderUserId = senderProfile?.UserId ?? CurrentUserId,
                FromDisplayName = senderProfile?.DisplayName ?? renderContext.Relationship.AccountOwnerDisplayName,
                FromEmailAddress = senderProfile?.EmailAddress,
                ReplyToAddresses = senderProfile?.EmailAddress,
                ToAddresses = renderContext.Contact?.EmailAddress ?? renderContext.Company?.ContactEmailAddress,
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

            context.Emails.Add(email);
            task.EmailId = email.Id;

            if (!string.IsNullOrWhiteSpace(email.ToAddresses))
            {
                foreach (string address in SplitAddresses(email.ToAddresses))
                {
                    context.EmailRecipients.Add(new PlatformEntities.EmailRecipient
                    {
                        Id = Guid.NewGuid(),
                        EmailId = email.Id,
                        CompanyContactId = string.Equals(renderContext.Contact?.EmailAddress, address, StringComparison.OrdinalIgnoreCase)
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
        PlatformDbContext context,
        PlatformEntities.ProcessTask task,
        CancellationToken cancellationToken)
    {
        PlatformEntities.ProcessStep step = await context.ProcessSteps
            .FirstOrDefaultAsync(item => item.Id == task.ProcessStepId, cancellationToken);

        if (step is null)
            return;

        DateTimeOffset now = DateTimeOffset.UtcNow;
        TaskRenderContext renderContext = await BuildTaskRenderContextAsync(
            context,
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
        PlatformDbContext context,
        Guid? leadId,
        Guid? tenantCompanyRelationshipId,
        Guid? opportunityId,
        Guid? clientAccountId,
        CancellationToken cancellationToken)
    {
        PlatformEntities.Lead lead = leadId.HasValue
            ? await context.Leads.FirstOrDefaultAsync(item => item.Id == leadId.Value, cancellationToken)
            : null;
        PlatformEntities.TenantCompanyRelationship relationship = tenantCompanyRelationshipId.HasValue
            ? await context.TenantCompanyRelationships.FirstOrDefaultAsync(item => item.Id == tenantCompanyRelationshipId.Value, cancellationToken)
            : null;
        PlatformEntities.Opportunity opportunity = opportunityId.HasValue
            ? await context.Opportunities.FirstOrDefaultAsync(item => item.Id == opportunityId.Value, cancellationToken)
            : null;
        PlatformEntities.ClientAccount clientAccount = clientAccountId.HasValue
            ? await context.ClientAccounts.FirstOrDefaultAsync(item => item.Id == clientAccountId.Value, cancellationToken)
            : null;

        Guid? companyId = lead?.CompanyId ?? relationship?.CompanyId;

        PlatformEntities.Company company = companyId.HasValue
            ? await context.Companies.FirstOrDefaultAsync(item => item.Id == companyId.Value, cancellationToken)
            : null;
        PlatformEntities.CompanyContact contact = await ResolvePreferredContactAsync(context, lead, relationship, opportunity, cancellationToken);

        return new TaskRenderContext(lead, relationship, opportunity, clientAccount, company, contact);
    }

    static TaskRenderValues RenderTaskValues(
        PlatformEntities.ProcessStep step,
        TaskRenderContext renderContext,
        DateTimeOffset now)
        => new(
            WorkflowTemplateRenderer.Render(step.TaskTitleTemplate, renderContext.Lead, renderContext.Company, renderContext.Contact, renderContext.Relationship, renderContext.Opportunity, renderContext.ClientAccount, now),
            WorkflowTemplateRenderer.Render(step.TaskInstructionsTemplate, renderContext.Lead, renderContext.Company, renderContext.Contact, renderContext.Relationship, renderContext.Opportunity, renderContext.ClientAccount, now),
            WorkflowTemplateRenderer.Render(step.EmailSubjectTemplate, renderContext.Lead, renderContext.Company, renderContext.Contact, renderContext.Relationship, renderContext.Opportunity, renderContext.ClientAccount, now),
            WorkflowTemplateRenderer.Render(step.EmailBodyTemplate, renderContext.Lead, renderContext.Company, renderContext.Contact, renderContext.Relationship, renderContext.Opportunity, renderContext.ClientAccount, now),
            WorkflowTemplateRenderer.Render(step.CallScriptTemplate, renderContext.Lead, renderContext.Company, renderContext.Contact, renderContext.Relationship, renderContext.Opportunity, renderContext.ClientAccount, now),
            WorkflowTemplateRenderer.Render(step.QuestionSetTemplate, renderContext.Lead, renderContext.Company, renderContext.Contact, renderContext.Relationship, renderContext.Opportunity, renderContext.ClientAccount, now));

    async ValueTask CompleteTaskAsync(
        PlatformDbContext context,
        PlatformEntities.ProcessTask task,
        string outcomeKey,
        string completionNote,
        CancellationToken cancellationToken)
    {
        PlatformEntities.ProcessInstance instance = task.ProcessInstance
            ?? await context.ProcessInstances.FirstAsync(item => item.Id == task.ProcessInstanceId, cancellationToken);
        PlatformEntities.ProcessStep step = task.ProcessStep
            ?? await context.ProcessSteps.FirstAsync(item => item.Id == task.ProcessStepId, cancellationToken);

        PlatformEntities.Lead lead = task.LeadId.HasValue
            ? await context.Leads.FirstOrDefaultAsync(item => item.Id == task.LeadId.Value, cancellationToken)
            : null;
        PlatformEntities.TenantCompanyRelationship relationship = task.TenantCompanyRelationshipId.HasValue
            ? await context.TenantCompanyRelationships.FirstOrDefaultAsync(item => item.Id == task.TenantCompanyRelationshipId.Value, cancellationToken)
            : null;
        PlatformEntities.Opportunity opportunity = task.OpportunityId.HasValue
            ? await context.Opportunities.FirstOrDefaultAsync(item => item.Id == task.OpportunityId.Value, cancellationToken)
            : null;
        PlatformEntities.ClientAccount clientAccount = task.ClientAccountId.HasValue
            ? await context.ClientAccounts.FirstOrDefaultAsync(item => item.Id == task.ClientAccountId.Value, cancellationToken)
            : null;

        List<PlatformEntities.ProcessTransition> transitions = await context.ProcessTransitions
            .Where(item => item.ProcessStepId == task.ProcessStepId)
            .OrderByDescending(item => item.IsDefaultOutcome)
            .ThenBy(item => item.OutcomeLabel)
            .ToListAsync(cancellationToken);

        PlatformEntities.ProcessTransition transition = ResolveTransition(transitions, outcomeKey);
        if (transition is null)
            return;

        DateTimeOffset now = DateTimeOffset.UtcNow;
        task.State = ProcessTaskState.Completed;
        task.CompletionOutcomeKey = transition.OutcomeKey;
        task.CompletionNotes = Normalize(completionNote);
        task.CompletedBy = CurrentUserId;
        task.CompletedOn = now;
        task.LastUpdatedBy = CurrentUserId;
        task.LastUpdated = now;

        await ApplyCompletionNoteUpdatesAsync(context, task, step, task.CompletionNotes, relationship, opportunity, now, cancellationToken);
        await RecordCompletionActivityAsync(context, task, relationship, opportunity, clientAccount, now, cancellationToken);
        await ApplyTransitionEffectAsync(context, transition, lead, relationship, opportunity, clientAccount, now, cancellationToken);

        loggingBroker.LogInformation(
            "Completed scheduled task for {RecordName} to {TaskTitle} with outcome {OutcomeKey}.",
            ResolveWorkflowRecordName(
                lead,
                await ResolveCompanyAsync(context, lead, relationship, cancellationToken),
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

            await context.SaveChangesAsync(cancellationToken);

            if (transition.Effect == ProcessTransitionEffect.QualifyLeadAndCreateOpportunity && lead?.OpportunityId is Guid opportunityId)
                await EnsureCoverageAsync(opportunityId: opportunityId, forceCreate: true, cancellationToken: cancellationToken);

            if (transition.Effect == ProcessTransitionEffect.CreateClientAccount && opportunity?.TenantCompanyRelationshipId is Guid relationshipId)
            {
                PlatformEntities.ClientAccount newClientAccount = await context.ClientAccounts
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

        PlatformEntities.ProcessStep nextStep = await context.ProcessSteps
            .FirstOrDefaultAsync(item => item.Id == transition.NextProcessStepId.Value, cancellationToken);

        if (nextStep is null)
            return;

        instance.CurrentProcessTaskId = null;
        instance.CurrentProcessStepId = nextStep.Id;
        instance.LastUpdatedBy = CurrentUserId;
        instance.LastUpdated = now;

        await CreateTaskForStepAsync(context, instance, nextStep, cancellationToken);
    }

    async ValueTask RecordCompletionActivityAsync(
        PlatformDbContext context,
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
                ? await context.Emails
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

        context.Activities.Add(activity);
    }

    async ValueTask ApplyCompletionNoteUpdatesAsync(
        PlatformDbContext context,
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
                await context.SaveChangesAsync(cancellationToken);

            return;
        }

        PlatformEntities.Company company = await context.Companies
            .FirstOrDefaultAsync(item => item.Id == relationship.CompanyId, cancellationToken);

        if (company is null)
        {
            if (hasChanges)
                await context.SaveChangesAsync(cancellationToken);

            return;
        }

        PlatformEntities.CompanyContact companyContact = await ResolveOrCreateCompanyContactFromRouteAsync(
            context,
            company,
            relationship.Id,
            routeDetails,
            completionNote,
            now,
            cancellationToken);

        PlatformEntities.RelationshipContact relationshipContact = await UpsertPrimaryRelationshipContactAsync(
            context,
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

        await context.SaveChangesAsync(cancellationToken);
    }

    async ValueTask ApplyTransitionEffectAsync(
        PlatformDbContext context,
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
                await QualifyLeadAsync(context, lead, now, cancellationToken);
                break;

            case ProcessTransitionEffect.RejectLead:
                if (lead is not null)
                    lead.Status = LeadStatus.Rejected;
                break;

            case ProcessTransitionEffect.CreateClientAccount:
                await CreateClientAccountAsync(context, relationship, opportunity, now, cancellationToken);
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
                    relationship.Status = RelationshipStatus.Disqualified;
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

    async ValueTask QualifyLeadAsync(
        PlatformDbContext context,
        PlatformEntities.Lead lead,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (lead is null)
            return;

        PlatformEntities.Company company = lead.CompanyId.HasValue
            ? await context.Companies.FirstOrDefaultAsync(item => item.Id == lead.CompanyId.Value, cancellationToken)
            : null;

        if (company is null)
        {
            PlatformEntities.Address address = CreateAddressFromLead(lead, now);
            if (address is not null)
            {
                context.Addresses.Add(address);
                await context.SaveChangesAsync(cancellationToken);
            }

            company = new PlatformEntities.Company
            {
                Id = Guid.NewGuid(),
                SourceSystem = lead.SourceSystem,
                SourceRecordId = lead.SourceRecordId,
                IsVerified = false,
                OfficialName = FirstNonEmpty(lead.RawCompanyName, "Imported company"),
                TradingName = lead.RawTradingName,
                CompanyNumber = lead.RawCompanyNumber,
                VatNumber = lead.RawVatNumber,
                WebsiteUrl = lead.RawWebsiteUrl,
                ContactEmailAddress = lead.RawContactEmailAddress,
                ContactPhoneNumber = lead.RawContactPhoneNumber,
                RegisteredOfficeText = lead.RawAddressText,
                ResearchSummary = lead.QualificationNotes,
                RegisteredAddressId = address?.Id,
                CreatedBy = CurrentUserId,
                LastUpdatedBy = CurrentUserId,
                CreatedOn = now,
                LastUpdated = now
            };

            context.Companies.Add(company);
            await context.SaveChangesAsync(cancellationToken);
            lead.CompanyId = company.Id;
        }

        PlatformEntities.TenantCompanyRelationship relationship = lead.TenantCompanyRelationshipId.HasValue
            ? await context.TenantCompanyRelationships.FirstOrDefaultAsync(item => item.Id == lead.TenantCompanyRelationshipId.Value, cancellationToken)
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

            context.TenantCompanyRelationships.Add(relationship);
            await context.SaveChangesAsync(cancellationToken);
            lead.TenantCompanyRelationshipId = relationship.Id;
        }

        List<PlatformEntities.LeadContact> leadContacts = await context.LeadContacts
            .Where(item => item.LeadId == lead.Id)
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.CreatedOn)
            .ToListAsync(cancellationToken);

        foreach (PlatformEntities.LeadContact leadContact in leadContacts)
        {
            PlatformEntities.CompanyContact companyContact = await ResolveOrCreateCompanyContactAsync(context, company.Id, leadContact, now, cancellationToken);
            await ResolveOrCreateRelationshipContactAsync(context, relationship.Id, companyContact.Id, leadContact, now, cancellationToken);
        }

        PlatformEntities.Opportunity opportunity = lead.OpportunityId.HasValue
            ? await context.Opportunities.FirstOrDefaultAsync(item => item.Id == lead.OpportunityId.Value, cancellationToken)
            : null;

        if (opportunity is null)
        {
            PlatformEntities.RelationshipContact primaryContact = await context.RelationshipContacts
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

            context.Opportunities.Add(opportunity);
            await context.SaveChangesAsync(cancellationToken);
            lead.OpportunityId = opportunity.Id;
        }

        lead.Status = LeadStatus.Converted;
        lead.LastUpdatedBy = CurrentUserId;
        lead.LastUpdated = now;
    }

    async ValueTask CreateClientAccountAsync(
        PlatformDbContext context,
        PlatformEntities.TenantCompanyRelationship relationship,
        PlatformEntities.Opportunity opportunity,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (relationship is null || opportunity is null)
            return;

        PlatformEntities.ClientAccount existing = await context.ClientAccounts
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

        context.ClientAccounts.Add(clientAccount);
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
        PlatformDbContext context,
        PlatformEntities.Lead lead,
        PlatformEntities.TenantCompanyRelationship relationship,
        PlatformEntities.Opportunity opportunity,
        CancellationToken cancellationToken)
    {
        if (opportunity?.PrimaryRelationshipContactId is Guid primaryRelationshipContactId)
        {
            return await context.RelationshipContacts
                .Where(item => item.Id == primaryRelationshipContactId)
                .Select(item => item.CompanyContact)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (relationship is not null)
        {
            PlatformEntities.RelationshipContact relationshipContact = await context.RelationshipContacts
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
            PlatformEntities.LeadContact leadContact = await context.LeadContacts
                .Where(item => item.LeadId == lead.Id)
                .OrderByDescending(item => item.IsPrimary)
                .ThenBy(item => item.CreatedOn)
                .FirstOrDefaultAsync(cancellationToken);

            if (leadContact is not null && lead.CompanyId.HasValue)
                return await ResolveOrCreateCompanyContactAsync(context, lead.CompanyId.Value, leadContact, DateTimeOffset.UtcNow, cancellationToken);
        }

        return null;
    }

    async ValueTask<PlatformEntities.CompanyContact> ResolveOrCreateCompanyContactAsync(
        PlatformDbContext context,
        Guid companyId,
        PlatformEntities.LeadContact leadContact,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        PlatformEntities.CompanyContact existing = await context.CompanyContacts
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

        context.CompanyContacts.Add(companyContact);
        await context.SaveChangesAsync(cancellationToken);
        return companyContact;
    }

    async ValueTask<PlatformEntities.CompanyContact> ResolveOrCreateCompanyContactFromRouteAsync(
        PlatformDbContext context,
        PlatformEntities.Company company,
        Guid relationshipId,
        RouteConfirmationDetails routeDetails,
        string completionNote,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        IQueryable<PlatformEntities.CompanyContact> query = context.CompanyContacts.Where(item => item.CompanyId == company.Id);

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

        context.CompanyContacts.Add(companyContact);
        return companyContact;
    }

    async ValueTask ResolveOrCreateRelationshipContactAsync(
        PlatformDbContext context,
        Guid relationshipId,
        Guid companyContactId,
        PlatformEntities.LeadContact leadContact,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        bool exists = await context.RelationshipContacts.AnyAsync(
            item => item.TenantCompanyRelationshipId == relationshipId && item.CompanyContactId == companyContactId,
            cancellationToken);

        if (exists)
            return;

        context.RelationshipContacts.Add(new PlatformEntities.RelationshipContact
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

        await context.SaveChangesAsync(cancellationToken);
    }

    async ValueTask<PlatformEntities.RelationshipContact> UpsertPrimaryRelationshipContactAsync(
        PlatformDbContext context,
        Guid relationshipId,
        PlatformEntities.CompanyContact companyContact,
        string openingAngle,
        string completionNote,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        List<PlatformEntities.RelationshipContact> existingContacts = await context.RelationshipContacts
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

        context.RelationshipContacts.Add(relationshipContact);
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

    static PlatformEntities.Address CreateAddressFromLead(PlatformEntities.Lead lead, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(lead?.RawAddressText))
            return null;

        string[] lines = lead.RawAddressText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new PlatformEntities.Address
        {
            Id = Guid.NewGuid(),
            SourceSystem = lead.SourceSystem,
            Line1 = lines.Length > 0 ? lines[0] : null,
            Line2 = lines.Length > 1 ? lines[1] : null,
            TownOrCity = lines.Length > 2 ? lines[2] : null,
            VerificationNotes = lead.RawAddressText,
            CreatedBy = "system",
            LastUpdatedBy = "system",
            CreatedOn = now,
            LastUpdated = now
        };
    }

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
        PlatformDbContext context,
        string tenantId,
        ProcessScopeType scopeType,
        CancellationToken cancellationToken)
    {
        string resolvedTenantId = string.IsNullOrWhiteSpace(tenantId) ? DefaultTenantId : tenantId;

        return await context.ProcessDefinitions
            .Where(item => item.TenantId == resolvedTenantId && item.ScopeType == scopeType && item.IsActive)
            .OrderByDescending(item => item.IsDefault)
            .ThenBy(item => item.Name)
            .FirstAsync(cancellationToken);
    }

    async ValueTask<PlatformEntities.ProcessStep> GetEntryStepAsync(
        PlatformDbContext context,
        Guid definitionId,
        CancellationToken cancellationToken) =>
        await context.ProcessSteps
            .Where(item => item.ProcessDefinitionId == definitionId && item.IsActive)
            .OrderByDescending(item => item.IsEntryPoint)
            .ThenBy(item => item.Sequence)
            .FirstOrDefaultAsync(cancellationToken);

    async ValueTask<PlatformEntities.ProcessStep> ResolveOpportunityStepAsync(
        PlatformDbContext context,
        Guid definitionId,
        PlatformEntities.Opportunity opportunity,
        CancellationToken cancellationToken)
    {
        List<PlatformEntities.ProcessStep> steps = await context.ProcessSteps
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

        bool hasSentEmail = await HasSentOpportunityEmailAsync(context, opportunity, cancellationToken);
        bool hasPendingProposalTask = await HasPendingOpportunityTaskForStepAsync(
            context,
            opportunity.Id,
            definitionId,
            "send-proposal",
            cancellationToken);
        bool hasPendingContractTask = await HasPendingOpportunityTaskForStepAsync(
            context,
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
        PlatformDbContext context,
        Guid opportunityId,
        Guid processDefinitionId,
        string stepKey,
        CancellationToken cancellationToken)
        => context.ProcessTasks
            .AnyAsync(
                item => item.OpportunityId == opportunityId
                    && item.State == ProcessTaskState.Pending
                    && item.ProcessStep.ProcessDefinitionId == processDefinitionId
                    && item.ProcessStep.Key == stepKey,
                cancellationToken);

    async ValueTask<bool> HasSentOpportunityEmailAsync(
        PlatformDbContext context,
        PlatformEntities.Opportunity opportunity,
        CancellationToken cancellationToken)
    {
        if (await context.Emails.AnyAsync(
                item => item.OpportunityId == opportunity.Id && item.State == EmailState.Sent,
                cancellationToken))
        {
            return true;
        }

        bool hasSiblingOpportunities = await context.Opportunities.AnyAsync(
            item => item.TenantCompanyRelationshipId == opportunity.TenantCompanyRelationshipId
                && item.Id != opportunity.Id,
            cancellationToken);

        if (hasSiblingOpportunities)
            return false;

        return await context.Emails.AnyAsync(
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
        }

        return transitions.FirstOrDefault(transition => transition.IsDefaultOutcome) ?? transitions[0];
    }

    static async ValueTask CloseActiveInstanceAsync(
        PlatformDbContext context,
        PlatformEntities.ProcessInstance activeInstance,
        string outcomeKey,
        CancellationToken cancellationToken)
    {
        if (activeInstance is null)
            return;

        List<PlatformEntities.ProcessTask> pendingTasks = await context.ProcessTasks
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
        PlatformDbContext context,
        string tenantId,
        CancellationToken cancellationToken)
    {
        if (await context.ProcessDefinitions.AnyAsync(
                item => item.TenantId == tenantId
                    && item.ScopeType == ProcessScopeType.Lead
                    && (item.LifecycleState == ProcessDefinitionLifecycleState.Active || item.IsActive),
                cancellationToken))
        {
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
            Name = "Lead Qualification",
            Description = "Research a raw lead, verify enough of the company data to trust it, then convert it into an opportunity or reject it cleanly.",
            IsDefault = true,
            IsActive = true,
            CreatedBy = CurrentUserId,
            LastUpdatedBy = CurrentUserId,
            CreatedOn = now,
            LastUpdated = now
        };

        PlatformEntities.ProcessStep research = NewStep(definition, "lead-research", "Research Lead", 10, true, ProcessActionType.Research, null, null, null, 0, 0,
            "Research {{Lead.RawCompanyName}}",
            "Review the imported lead data, correct obvious errors, and capture any initial qualification notes.",
            null,
            null,
            null,
            "What does this company do?\nIs the raw data credible?\nWhat needs correcting?");

        PlatformEntities.ProcessStep verify = NewStep(definition, "verify-company", "Verify Company", 20, false, ProcessActionType.Review, null, null, null, 1, 0,
            "Verify the company record for {{Lead.RawCompanyName}}",
            "Confirm name, address, VAT number, and any key contact information before qualification.",
            null,
            null,
            null,
            "What fields were verified?\nWhat fields remain uncertain?");

        PlatformEntities.ProcessStep qualify = NewStep(definition, "qualify-lead", "Qualify Lead", 30, false, ProcessActionType.Approval, null, null, null, 0, 0,
            "Decide whether {{Lead.RawCompanyName}} becomes an opportunity",
            "Approve conversion only when the company record is good enough to drive outreach with confidence.",
            null,
            null,
            null,
            "Is this lead credible?\nIs there enough data to begin outreach?\nWhat commercial angle should be pursued?");

        context.ProcessDefinitions.Add(definition);
        definition.FamilyId = definition.Id;
        context.ProcessSteps.AddRange(research, verify, qualify);
        context.ProcessTransitions.AddRange(
            NewTransition(research, verify, "researched", "Research complete", true, ProcessTransitionEffect.None),
            NewTransition(verify, qualify, "verified", "Company verified", true, ProcessTransitionEffect.None),
            NewTransition(qualify, null, "qualified", "Qualified and create opportunity", true, ProcessTransitionEffect.QualifyLeadAndCreateOpportunity, true),
            NewTransition(qualify, null, "rejected", "Reject lead", false, ProcessTransitionEffect.RejectLead, true));
    }

    async ValueTask EnsureOpportunityProcessAsync(
        PlatformDbContext context,
        string tenantId,
        CancellationToken cancellationToken)
    {
        if (await context.ProcessDefinitions.AnyAsync(
                item => item.TenantId == tenantId
                    && item.ScopeType == ProcessScopeType.Opportunity
                    && (item.LifecycleState == ProcessDefinitionLifecycleState.Active || item.IsActive),
                cancellationToken))
        {
            return;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        PlatformEntities.ProcessDefinition definition = new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ScopeType = ProcessScopeType.Opportunity,
            FamilyId = null,
            VersionNumber = 1,
            LifecycleState = ProcessDefinitionLifecycleState.Active,
            Name = "Opportunity Progression",
            Description = "Own the outreach, response handling, proposal, and contract path until the opportunity is won or lost.",
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

        PlatformEntities.ProcessStep followUpCall = NewStep(definition, "follow-up-call", "Make Follow-Up Call", 40, false, ProcessActionType.Call, RelationshipStatus.ActiveOpportunity, SalesPipelineStage.OutreachSent, null, 1, 0,
            "Call {{Company.OfficialName}} to follow up",
            "Use the script to confirm whether the opportunity is live and whether discovery can be booked.",
            null,
            null,
            "Questions:\n1. Are you the right person?\n2. Is this issue live?\n3. Is a short discovery call worthwhile?",
            "Was the contact reached?\nWas there interest?");

        PlatformEntities.ProcessStep sendProposal = NewStep(definition, "send-proposal", "Send Proposal", 50, false, ProcessActionType.Email, RelationshipStatus.ActiveOpportunity, SalesPipelineStage.ProposalSent, null, 2, 0,
            "Send a proposal to {{Company.OfficialName}}",
            "Package the commercial position clearly and send it for review.",
            "Proposal for {{Company.OfficialName}}",
            "Hello {{Contact.Name}},\n\nThank you for the conversation. Based on what we discussed, I have attached the recommended next step and the commercial outline.\n\nKind regards,\n{{Relationship.AccountOwnerDisplayName}}",
            null,
            null);

        PlatformEntities.ProcessStep negotiate = NewStep(definition, "negotiate", "Negotiate Terms", 60, false, ProcessActionType.ManualTask, RelationshipStatus.ActiveOpportunity, SalesPipelineStage.Negotiation, null, 2, 0,
            "Negotiate terms with {{Company.OfficialName}}",
            "Resolve open commercials, delivery expectations, and legal positions.",
            null,
            null,
            null,
            "What terms remain open?\nWho needs to approve them?");

        PlatformEntities.ProcessStep sendContract = NewStep(definition, "send-contract", "Send Contract", 70, false, ProcessActionType.Email, RelationshipStatus.Contracted, SalesPipelineStage.ContractSent, null, 1, 0,
            "Send the contract to {{Company.OfficialName}}",
            "Send the contract once commercials are settled and the approval path is clear.",
            "Contract for {{Company.OfficialName}}",
            "Hello {{Contact.Name}},\n\nPlease find the contract attached reflecting the agreed commercials and scope.\n\nKind regards,\n{{Relationship.AccountOwnerDisplayName}}",
            null,
            null);

        PlatformEntities.ProcessStep confirmSignature = NewStep(definition, "confirm-signature", "Confirm Signature", 80, false, ProcessActionType.ManualTask, RelationshipStatus.Contracted, SalesPipelineStage.ContractSent, null, 5, 0,
            "Confirm whether {{Company.OfficialName}} has signed",
            "Check contract status and decide whether the opportunity is won, still open, or lost.",
            null,
            null,
            null,
            "Is the contract signed?\nWhat blocker remains?");

        context.ProcessDefinitions.Add(definition);
        definition.FamilyId = definition.Id;
        context.ProcessSteps.AddRange(confirmRoute, introEmail, reviewResponse, followUpCall, sendProposal, negotiate, sendContract, confirmSignature);
        context.ProcessTransitions.AddRange(
            NewTransition(confirmRoute, introEmail, "ready", "Route confirmed", true, ProcessTransitionEffect.None),
            NewTransition(introEmail, reviewResponse, "sent", "Email sent", true, ProcessTransitionEffect.None),
            NewTransition(reviewResponse, sendProposal, "positive-reply", "Positive reply", false, ProcessTransitionEffect.None),
            NewTransition(reviewResponse, followUpCall, "no-reply", "No reply", true, ProcessTransitionEffect.None),
            NewTransition(reviewResponse, null, "lost", "No fit", false, ProcessTransitionEffect.CloseOpportunityAsLost, true, resultingRelationshipStatus: RelationshipStatus.Disqualified, resultingSalesStage: SalesPipelineStage.Lost),
            NewTransition(followUpCall, sendProposal, "interested", "Interested", true, ProcessTransitionEffect.None),
            NewTransition(followUpCall, null, "lost", "No route forward", false, ProcessTransitionEffect.CloseOpportunityAsLost, true, resultingRelationshipStatus: RelationshipStatus.Disqualified, resultingSalesStage: SalesPipelineStage.Lost),
            NewTransition(sendProposal, negotiate, "sent", "Proposal sent", true, ProcessTransitionEffect.None),
            NewTransition(negotiate, sendContract, "contract-ready", "Contract ready", true, ProcessTransitionEffect.None),
            NewTransition(negotiate, null, "lost", "Negotiation failed", false, ProcessTransitionEffect.CloseOpportunityAsLost, true, resultingRelationshipStatus: RelationshipStatus.Disqualified, resultingSalesStage: SalesPipelineStage.Lost),
            NewTransition(sendContract, confirmSignature, "sent", "Contract sent", true, ProcessTransitionEffect.None),
            NewTransition(confirmSignature, null, "won", "Signed", true, ProcessTransitionEffect.CreateClientAccount, true, resultingRelationshipStatus: RelationshipStatus.Onboarding, resultingSalesStage: SalesPipelineStage.Won),
            NewTransition(confirmSignature, negotiate, "still-open", "Still negotiating", false, ProcessTransitionEffect.None),
            NewTransition(confirmSignature, null, "lost", "Contract lost", false, ProcessTransitionEffect.CloseOpportunityAsLost, true, resultingRelationshipStatus: RelationshipStatus.Disqualified, resultingSalesStage: SalesPipelineStage.Lost));
    }

    async ValueTask EnsureClientProcessAsync(
        PlatformDbContext context,
        string tenantId,
        CancellationToken cancellationToken)
    {
        if (await context.ProcessDefinitions.AnyAsync(
                item => item.TenantId == tenantId
                    && item.ScopeType == ProcessScopeType.ClientAccount
                    && (item.LifecycleState == ProcessDefinitionLifecycleState.Active || item.IsActive),
                cancellationToken))
        {
            return;
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
            Name = "Client Onboarding",
            Description = "Capture the handoff, confirm onboarding readiness, and close out into a live client state.",
            IsDefault = true,
            IsActive = true,
            CreatedBy = CurrentUserId,
            LastUpdatedBy = CurrentUserId,
            CreatedOn = now,
            LastUpdated = now
        };

        PlatformEntities.ProcessStep buildHandoff = NewStep(definition, "build-handoff", "Build Handoff Pack", 10, true, ProcessActionType.ManualTask, RelationshipStatus.Onboarding, SalesPipelineStage.Won, ClientAccountStatus.Onboarding, 0, 0,
            "Build the onboarding pack for {{Company.OfficialName}}",
            "Capture scope, commercials, promised outcomes, and operational handoff details.",
            null,
            null,
            null,
            "What has been sold?\nWho owns onboarding?\nWhat risks exist?");

        PlatformEntities.ProcessStep goLiveReview = NewStep(definition, "go-live-review", "Confirm Go-Live Readiness", 20, false, ProcessActionType.ManualTask, RelationshipStatus.Client, SalesPipelineStage.Won, ClientAccountStatus.Active, 2, 0,
            "Confirm go-live readiness for {{Company.OfficialName}}",
            "Validate that the client can now be treated as live and hand off any remaining operational notes.",
            null,
            null,
            null,
            "Is onboarding complete?\nIs the client now live?");

        context.ProcessDefinitions.Add(definition);
        definition.FamilyId = definition.Id;
        context.ProcessSteps.AddRange(buildHandoff, goLiveReview);
        context.ProcessTransitions.AddRange(
            NewTransition(buildHandoff, goLiveReview, "complete", "Handoff complete", true, ProcessTransitionEffect.None),
            NewTransition(goLiveReview, null, "live", "Client live", true, ProcessTransitionEffect.None, true, resultingRelationshipStatus: RelationshipStatus.Client, resultingClientAccountStatus: ClientAccountStatus.Active));
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
        string questionSetTemplate) =>
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
        PlatformDbContext context,
        PlatformEntities.Lead lead,
        PlatformEntities.TenantCompanyRelationship relationship,
        CancellationToken cancellationToken)
    {
        Guid? companyId = lead?.CompanyId ?? relationship?.CompanyId;
        if (!companyId.HasValue)
            return null;

        if (relationship?.Company is not null && relationship.Company.Id == companyId.Value)
            return relationship.Company;

        return await context.Companies.FirstOrDefaultAsync(item => item.Id == companyId.Value, cancellationToken);
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
        PlatformEntities.CompanyContact Contact);

    sealed record TaskRenderValues(
        string Title,
        string Instructions,
        string EmailSubject,
        string EmailBody,
        string CallScript,
        string QuestionSet);

    string CurrentUserId =>
        !string.IsNullOrWhiteSpace(currentExecutionUserAccessor?.UserId)
            ? currentExecutionUserAccessor.UserId
            : string.IsNullOrWhiteSpace(authInfo?.SSOUserId) || string.Equals(authInfo.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase)
            ? "system"
            : authInfo.SSOUserId;

    static void Touch(PlatformEntities.AuditableEntity entity, DateTimeOffset now)
    {
        if (entity is null)
            return;

        entity.LastUpdated = now;
        entity.LastUpdatedBy = "system";
    }
}
