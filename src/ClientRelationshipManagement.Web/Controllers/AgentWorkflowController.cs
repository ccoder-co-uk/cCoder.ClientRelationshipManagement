using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using cCoder.Security.Objects;
using ClientRelationshipManagement.Web.Models.AgentWorkflow;
using ClientRelationshipManagement.Web.Services.Agents;
using ClientRelationshipManagement.Web.Services.Execution;
using ClientRelationshipManagement.Web.Services.Mail;
using ClientRelationshipManagement.Web.Services.Processes;
using ClientRelationshipManagement.Web.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.Web.Controllers;

[ApiController]
[Route("Api/[controller]")]
public sealed class AgentWorkflowController(
    IPlatformDbContextFactory dbContextFactory,
    IEmailDraftWorkflowService emailDraftWorkflowService,
    IAgentMessageService agentMessageService,
    IProcessDraftService processDraftService,
    IWorkflowAutomationService workflowAutomationService,
    ICurrentExecutionUserAccessor currentExecutionUserAccessor,
    ISSOAuthInfo authInfo)
    : ControllerBase
{
    [HttpGet("Tasks/Due")]
    public async Task<IActionResult> GetDueTasks([FromQuery] int limit = 25, CancellationToken cancellationToken = default)
    {
        if (TryAuthenticate(out IActionResult failure))
            return failure!;

        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<ProcessTask> tasks = await context.ProcessTasks
            .AsNoTracking()
            .Include(task => task.ProcessStep)
            .Include(task => task.Email)
            .Include(task => task.Lead)
            .Include(task => task.TenantCompanyRelationship)
                .ThenInclude(relationship => relationship.Company)
            .Include(task => task.Opportunity)
            .Include(task => task.ClientAccount)
            .Where(task => task.State == ProcessTaskState.Pending && task.DueOn <= now)
            .OrderBy(task =>
                task.OpportunityId.HasValue || task.ClientAccountId.HasValue
                    ? 0
                    : task.TenantCompanyRelationshipId.HasValue
                        ? 1
                        : 2)
            .ThenBy(task => task.DueOn)
            .Take(Math.Max(1, limit))
            .ToListAsync(cancellationToken);

        List<Guid> stepIds =
        [
            .. tasks.Select(item => item.ProcessStepId).Distinct()
        ];

        Dictionary<Guid, List<AgentTaskOutcomeViewModel>> outcomeLookup = stepIds.Count == 0
            ? []
            : await context.ProcessTransitions
                .AsNoTracking()
                .Where(item => stepIds.Contains(item.ProcessStepId))
                .GroupBy(item => item.ProcessStepId)
                .ToDictionaryAsync(
                    group => group.Key,
                    group => group
                        .OrderByDescending(item => item.IsDefaultOutcome)
                        .ThenBy(item => item.OutcomeLabel)
                        .Select(item => new AgentTaskOutcomeViewModel
                        {
                            Key = item.OutcomeKey,
                            Label = item.OutcomeLabel,
                            IsDefault = item.IsDefaultOutcome
                        })
                        .ToList(),
                    cancellationToken);

        List<Guid> relationshipIds =
        [
            .. tasks.Where(item => item.TenantCompanyRelationshipId.HasValue)
                .Select(item => item.TenantCompanyRelationshipId!.Value)
                .Distinct()
        ];

        Dictionary<Guid, RelationshipContact> primaryContactsByRelationship = relationshipIds.Count == 0
            ? []
            : await context.RelationshipContacts
                .AsNoTracking()
                .Include(item => item.CompanyContact)
                .Where(item => relationshipIds.Contains(item.TenantCompanyRelationshipId))
                .OrderByDescending(item => item.IsPrimary)
                .ThenBy(item => item.CreatedOn)
                .GroupBy(item => item.TenantCompanyRelationshipId)
                .ToDictionaryAsync(group => group.Key, group => group.First(), cancellationToken);

        return Ok(tasks.Select(task =>
        {
            RelationshipContact primaryContact = task.TenantCompanyRelationshipId.HasValue
                ? primaryContactsByRelationship.GetValueOrDefault(task.TenantCompanyRelationshipId.Value)
                : null;

            return new AgentDueTaskViewModel
            {
                ProcessTaskId = task.Id,
                LeadId = task.LeadId,
                ClientId = task.TenantCompanyRelationshipId,
                OpportunityId = task.OpportunityId,
                ClientAccountId = task.ClientAccountId,
                EmailId = task.EmailId,
                MaterialId = task.Email?.MaterialId,
                ScopeType = task.LeadId.HasValue
                    ? "Lead"
                    : task.ClientAccountId.HasValue
                        ? "Client"
                        : task.OpportunityId.HasValue
                            ? "Opportunity"
                            : "Relationship",
                CompanyName = FirstNonEmpty(
                    CompanyNames.ResolvePreferredName(task.TenantCompanyRelationship?.Company),
                    task.Lead?.RawCompanyName,
                    string.Empty),
                ContactName = primaryContact?.CompanyContact?.Name ?? string.Empty,
                ContactEmailAddress = primaryContact?.CompanyContact?.EmailAddress ?? string.Empty,
                OwnerUserId = task.TenantCompanyRelationship?.AccountOwnerUserId ?? task.CreatedBy,
                OwnerDisplayName = task.TenantCompanyRelationship?.AccountOwnerDisplayName ?? task.CreatedBy,
                DueOn = task.DueOn,
                ActionType = task.ActionType,
                Title = task.RenderedTitle,
                Instructions = task.RenderedInstructions ?? string.Empty,
                EmailSubjectTemplate = task.RenderedEmailSubject ?? string.Empty,
                EmailBodyTemplate = task.RenderedEmailBody ?? string.Empty,
                CallScriptTemplate = task.RenderedCallScript ?? string.Empty,
                QuestionSetTemplate = task.RenderedQuestionSet ?? string.Empty,
                ExistingEmailState = task.Email?.State.ToString() ?? string.Empty,
                ExistingEmailSubject = task.Email?.Subject ?? string.Empty,
                ExistingEmailBody = task.Email?.BodyText ?? task.Email?.BodyHtml ?? string.Empty,
                AvailableOutcomes = outcomeLookup.GetValueOrDefault(task.ProcessStepId) ?? []
            };
        }));
    }

    [HttpPost("Tasks/{processTaskId:guid}/Complete")]
    public async Task<IActionResult> CompleteTask(
        Guid processTaskId,
        [FromBody] CompleteAgentTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        if (TryAuthenticate(out IActionResult failure))
            return failure!;

        if (string.IsNullOrWhiteSpace(request?.OutcomeKey))
            return BadRequest("An outcome key is required.");

        ProcessTask task = await workflowAutomationService.CompleteTaskAsync(
            new ProcessTaskCompletionCommand
            {
                ProcessTaskId = processTaskId,
                OutcomeKey = request.OutcomeKey,
                CompletionNote = request.CompletionNote
            },
            cancellationToken);

        if (task is null)
            return NotFound();

        return Ok(new
        {
            task.Id,
            task.State,
            task.CompletionOutcomeKey,
            task.CompletedOn
        });
    }

    [HttpPost("Tasks/{processTaskId:guid}/DraftEmail")]
    public async Task<IActionResult> SaveTaskDraftEmail(
        Guid processTaskId,
        [FromBody] AgentTaskDraftEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        if (TryAuthenticate(out IActionResult failure))
            return failure!;

        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        ProcessTask task = await context.ProcessTasks
            .Include(item => item.Email)
            .FirstOrDefaultAsync(item => item.Id == processTaskId, cancellationToken);

        if (task is null || !task.TenantCompanyRelationshipId.HasValue)
            return NotFound();

        Email email = await emailDraftWorkflowService.SaveDraftAsync(
            new EmailDraftUpsertCommand
            {
                ClientId = task.TenantCompanyRelationshipId.Value,
                EmailId = task.EmailId,
                ClientMaterialId = task.Email?.MaterialId,
                ClientOpportunityId = task.OpportunityId,
                ClientAccountId = task.ClientAccountId,
                Direction = request.Direction,
                Subject = request.Subject,
                Body = request.Body,
                ToAddresses = request.ToAddresses,
                CcAddresses = request.CcAddresses,
                BccAddresses = request.BccAddresses,
                ScheduledSendTimeUtc = request.ScheduledSendTimeUtc
            },
            cancellationToken);

        if (email is null)
            return BadRequest("Unable to save the draft email.");

        if (task.EmailId != email.Id)
        {
            task.EmailId = email.Id;
            task.LastUpdatedBy = CurrentExecutionUserId;
            task.LastUpdated = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }

        await agentMessageService.UpsertAsync(
            new AgentMessage
            {
                Id = Guid.NewGuid(),
                AgentRunId = null,
                TenantCompanyRelationshipId = task.TenantCompanyRelationshipId,
                OpportunityId = task.OpportunityId,
                ClientAccountId = task.ClientAccountId,
                ProcessTaskId = task.Id,
                EmailId = email.Id,
                Kind = AgentMessageKind.ApprovalRequest,
                State = AgentMessageState.Pending,
                CorrelationKey = string.IsNullOrWhiteSpace(request.CorrelationKey)
                    ? $"approval:email:{task.Id}"
                    : request.CorrelationKey,
                Title = string.IsNullOrWhiteSpace(request.ApprovalTitle)
                    ? $"Approve draft email for {task.RenderedTitle}"
                    : request.ApprovalTitle,
                Body = string.IsNullOrWhiteSpace(request.ApprovalBody)
                    ? "A draft email has been prepared and is ready for review."
                    : request.ApprovalBody,
                AgentName = "Task Agent",
                CreatedBy = CurrentExecutionUserId,
                LastUpdatedBy = CurrentExecutionUserId
            },
            cancellationToken);

        return Ok(new
        {
            EmailId = email.Id,
            EmailState = email.State,
            ProcessTaskId = task.Id
        });
    }

    [HttpPost("Messages")]
    public async Task<IActionResult> CreateMessage(
        [FromBody] CreateAgentMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (TryAuthenticate(out IActionResult failure))
            return failure!;

        AgentMessage message = await agentMessageService.UpsertAsync(
            new AgentMessage
            {
                Id = Guid.NewGuid(),
                AgentRunId = request.AgentRunId,
                LeadId = request.LeadId,
                TenantCompanyRelationshipId = request.ClientId,
                OpportunityId = request.OpportunityId,
                ClientAccountId = request.ClientAccountId,
                ProcessTaskId = request.ProcessTaskId,
                EmailId = request.EmailId,
                ProcessDefinitionId = request.ProcessDefinitionId,
                ProposedProcessDefinitionId = request.ProposedProcessDefinitionId,
                Kind = request.Kind,
                State = request.State,
                CorrelationKey = request.CorrelationKey,
                Title = request.Title,
                Body = request.Body,
                AgentName = request.AgentName,
                CreatedBy = CurrentExecutionUserId,
                LastUpdatedBy = CurrentExecutionUserId
            },
            cancellationToken);

        return Ok(new { message.Id, message.State });
    }

    [HttpGet("Processes/Metrics")]
    public async Task<IActionResult> GetProcessMetrics(CancellationToken cancellationToken = default)
    {
        if (TryAuthenticate(out IActionResult failure))
            return failure!;

        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        List<ProcessDefinition> definitions = await context.ProcessDefinitions
            .AsNoTracking()
            .Where(item => item.LifecycleState == ProcessDefinitionLifecycleState.Active || item.IsActive)
            .OrderBy(item => item.ScopeType)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);

        List<ProcessPerformanceMetricsViewModel> metrics = [];

        foreach (ProcessDefinition definition in definitions)
        {
            int activeInstanceCount = await context.ProcessInstances.CountAsync(
                item => item.ProcessDefinitionId == definition.Id && item.State == ProcessInstanceState.Active,
                cancellationToken);

            int pendingTaskCount = await context.ProcessTasks.CountAsync(
                item => item.ProcessInstance.ProcessDefinitionId == definition.Id && item.State == ProcessTaskState.Pending,
                cancellationToken);

            int sentEmailCount = await context.Emails.CountAsync(
                item => item.State == EmailState.Sent
                    && ((definition.ScopeType == ProcessScopeType.Opportunity && item.Opportunity!.ProcessInstances.Any(instance => instance.ProcessDefinitionId == definition.Id))
                        || (definition.ScopeType == ProcessScopeType.ClientAccount && item.ClientAccount!.ProcessInstances.Any(instance => instance.ProcessDefinitionId == definition.Id))),
                cancellationToken);

            int replyActivityCount = await context.Activities.CountAsync(
                item => item.Direction == ActivityDirection.Inbound
                    && ((definition.ScopeType == ProcessScopeType.Opportunity && item.Opportunity!.ProcessInstances.Any(instance => instance.ProcessDefinitionId == definition.Id))
                        || (definition.ScopeType == ProcessScopeType.ClientAccount && item.ClientAccount!.ProcessInstances.Any(instance => instance.ProcessDefinitionId == definition.Id))),
                cancellationToken);

            int wonCount = definition.ScopeType == ProcessScopeType.Opportunity
                ? await context.Opportunities.CountAsync(item =>
                    item.ProcessInstances.Any(instance => instance.ProcessDefinitionId == definition.Id)
                    && item.Stage == SalesPipelineStage.Won, cancellationToken)
                : 0;

            int lostCount = definition.ScopeType == ProcessScopeType.Opportunity
                ? await context.Opportunities.CountAsync(item =>
                    item.ProcessInstances.Any(instance => instance.ProcessDefinitionId == definition.Id)
                    && item.Stage == SalesPipelineStage.Lost, cancellationToken)
                : 0;

            metrics.Add(new ProcessPerformanceMetricsViewModel
            {
                ProcessDefinitionId = definition.Id,
                TenantId = definition.TenantId,
                ScopeType = definition.ScopeType.ToString(),
                Name = definition.Name,
                ActiveInstanceCount = activeInstanceCount,
                PendingTaskCount = pendingTaskCount,
                SentEmailCount = sentEmailCount,
                ReplyActivityCount = replyActivityCount,
                WonCount = wonCount,
                LostCount = lostCount
            });
        }

        return Ok(metrics);
    }

    [HttpPost("Processes/{processDefinitionId:guid}/DraftProposal")]
    public async Task<IActionResult> CreateProcessDraftProposal(
        Guid processDefinitionId,
        [FromBody] CreateProcessDraftProposalRequest request,
        CancellationToken cancellationToken = default)
    {
        if (TryAuthenticate(out IActionResult failure))
            return failure!;

        ProcessDefinition draft = await processDraftService.CreateDraftAsync(
            processDefinitionId,
            CurrentExecutionUserId,
            request.ProposedByAgent,
            request.ChangeSummary,
            request.Name,
            request.Description,
            request.StepUpdates.Select(item => new ProcessStepDraftUpdate
            {
                Key = item.Key,
                Name = item.Name,
                TaskInstructionsTemplate = item.TaskInstructionsTemplate,
                EmailSubjectTemplate = item.EmailSubjectTemplate,
                EmailBodyTemplate = item.EmailBodyTemplate,
                CallScriptTemplate = item.CallScriptTemplate,
                QuestionSetTemplate = item.QuestionSetTemplate
            }).ToList(),
            cancellationToken);

        if (draft is null)
            return NotFound();

        await agentMessageService.UpsertAsync(
            new AgentMessage
            {
                Id = Guid.NewGuid(),
                ProcessDefinitionId = processDefinitionId,
                ProposedProcessDefinitionId = draft.Id,
                Kind = AgentMessageKind.ProcessProposal,
                State = AgentMessageState.Pending,
                CorrelationKey = string.IsNullOrWhiteSpace(request.CorrelationKey)
                    ? $"process-proposal:{draft.Id}"
                    : request.CorrelationKey,
                Title = string.IsNullOrWhiteSpace(request.ApprovalTitle)
                    ? $"Review process draft for {draft.Name}"
                    : request.ApprovalTitle,
                Body = string.IsNullOrWhiteSpace(request.ApprovalBody)
                    ? request.ChangeSummary ?? "A new process improvement draft is ready for review."
                    : request.ApprovalBody,
                AgentName = request.ProposedByAgent ?? "Process Optimiser",
                CreatedBy = CurrentExecutionUserId,
                LastUpdatedBy = CurrentExecutionUserId
            },
            cancellationToken);

        return Ok(new { draft.Id, draft.Name, draft.VersionNumber });
    }

    [HttpPost("Leads/{leadId:guid}/Research")]
    public async Task<IActionResult> UpdateLeadResearch(
        Guid leadId,
        [FromBody] UpdateLeadResearchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (TryAuthenticate(out IActionResult failure))
            return failure!;

        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        Lead lead = await context.Leads
            .Include(item => item.Contacts)
            .FirstOrDefaultAsync(item => item.Id == leadId, cancellationToken);

        if (lead is null)
            return NotFound();

        DateTimeOffset now = DateTimeOffset.UtcNow;
        lead.RawCompanyName = FirstNonEmpty(request.RawCompanyName, lead.RawCompanyName);
        lead.RawTradingName = Normalize(request.RawTradingName) ?? lead.RawTradingName;
        lead.RawCompanyNumber = Normalize(request.RawCompanyNumber) ?? lead.RawCompanyNumber;
        lead.RawVatNumber = Normalize(request.RawVatNumber) ?? lead.RawVatNumber;
        lead.RawWebsiteUrl = Normalize(request.RawWebsiteUrl) ?? lead.RawWebsiteUrl;
        lead.RawContactEmailAddress = Normalize(request.RawContactEmailAddress) ?? lead.RawContactEmailAddress;
        lead.RawContactPhoneNumber = Normalize(request.RawContactPhoneNumber) ?? lead.RawContactPhoneNumber;
        lead.RawAddressText = Normalize(request.RawAddressText) ?? lead.RawAddressText;
        lead.QualificationNotes = Normalize(request.QualificationNotes) ?? lead.QualificationNotes;
        lead.LastUpdatedBy = CurrentExecutionUserId;
        lead.LastUpdated = now;

        if (!string.IsNullOrWhiteSpace(request.ContactName)
            || !string.IsNullOrWhiteSpace(request.ContactEmailAddress)
            || !string.IsNullOrWhiteSpace(request.ContactPhoneNumber))
        {
            LeadContact primaryContact = lead.Contacts
                .OrderByDescending(item => item.IsPrimary)
                .ThenBy(item => item.CreatedOn)
                .FirstOrDefault(item =>
                    !string.IsNullOrWhiteSpace(request.ContactEmailAddress)
                    && string.Equals(item.EmailAddress, request.ContactEmailAddress, StringComparison.OrdinalIgnoreCase))
                ?? lead.Contacts
                    .OrderByDescending(item => item.IsPrimary)
                    .ThenBy(item => item.CreatedOn)
                    .FirstOrDefault();

            if (primaryContact is null)
            {
                primaryContact = new LeadContact
                {
                    Id = Guid.NewGuid(),
                    LeadId = lead.Id,
                    IsPrimary = true,
                    Name = FirstNonEmpty(request.ContactName, "Researched contact"),
                    Position = Normalize(request.ContactPosition),
                    EmailAddress = Normalize(request.ContactEmailAddress),
                    PhoneNumber = Normalize(request.ContactPhoneNumber),
                    LinkedInUrl = Normalize(request.ContactLinkedInUrl),
                    CreatedBy = CurrentExecutionUserId,
                    LastUpdatedBy = CurrentExecutionUserId,
                    CreatedOn = now,
                    LastUpdated = now
                };

                context.LeadContacts.Add(primaryContact);
            }
            else
            {
                primaryContact.IsPrimary = true;
                primaryContact.Name = FirstNonEmpty(request.ContactName, primaryContact.Name);
                primaryContact.Position = Normalize(request.ContactPosition) ?? primaryContact.Position;
                primaryContact.EmailAddress = Normalize(request.ContactEmailAddress) ?? primaryContact.EmailAddress;
                primaryContact.PhoneNumber = Normalize(request.ContactPhoneNumber) ?? primaryContact.PhoneNumber;
                primaryContact.LinkedInUrl = Normalize(request.ContactLinkedInUrl) ?? primaryContact.LinkedInUrl;
                primaryContact.LastUpdatedBy = CurrentExecutionUserId;
                primaryContact.LastUpdated = now;
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            lead.Id,
            lead.RawCompanyName,
            lead.RawCompanyNumber,
            lead.RawWebsiteUrl
        });
    }

    bool TryAuthenticate(out IActionResult failure)
    {
        string authorizationHeader = Request.Headers.Authorization.FirstOrDefault() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(authorizationHeader)
            || !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            failure = Unauthorized();
            return true;
        }

        if (string.IsNullOrWhiteSpace(authInfo?.SSOUserId)
            || string.Equals(authInfo.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
        {
            failure = Unauthorized();
            return true;
        }

        currentExecutionUserAccessor.UserId = authInfo.SSOUserId;

        failure = null;
        return false;
    }

    string CurrentExecutionUserId =>
        currentExecutionUserAccessor.UserId ?? "system";

    static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    static string Normalize(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
}
