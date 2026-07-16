using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using cCoder.Security.Objects;
using ClientRelationshipManagement.Web.Configuration;
using ClientRelationshipManagement.Web.Models.AgentWorkflow;
using ClientRelationshipManagement.Web.Services.Agents;
using ClientRelationshipManagement.Web.Services.Execution;
using ClientRelationshipManagement.Web.Services.Leads;
using ClientRelationshipManagement.Web.Services.Mail;
using ClientRelationshipManagement.Web.Services.Processes;
using ClientRelationshipManagement.Web.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using cCoder.ClientRelationshipManagement.Services.Foundations.Platform;
using IPlatformAgentMessageService = cCoder.ClientRelationshipManagement.Services.Entities.IAgentMessageOrchestrationService;
using IWebAgentMessageService = ClientRelationshipManagement.Web.Services.Agents.IAgentMessageService;

namespace ClientRelationshipManagement.Web.Controllers;

[ApiController]
[Route("Api/[controller]")]
public sealed class AgentWorkflowController(
    IOperationsCoordinationService operations,
    ISalesCoordinationService salesWorkspace,
    IProcessCoordinationService processWorkspace,
    IPlatformAgentMessageService messageWorkspace,
    IEmailDraftWorkflowService emailDraftWorkflowService,
    IWebAgentMessageService agentMessageService,
    IProcessDraftService processDraftService,
    IWorkflowAutomationService workflowAutomationService,
    IEmailTaskEvidenceService emailTaskEvidenceService,
    IMicrosoftGraphMailboxClient mailboxClient,
    ICurrentExecutionUserAccessor currentExecutionUserAccessor,
    ICRMAuthInfo crmAuthInfo,
    ISSOAuthInfo authInfo)
    : ControllerBase
{
    [HttpGet("Mailbox/Sent/Reconciliation")]
    public async Task<IActionResult> GetSentEmailReconciliation(
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] int limit = 250,
        CancellationToken cancellationToken = default)
    {
        if (TryAuthenticate(out IActionResult failure))
            return failure!;

        DateTimeOffset fromDate = from ?? DateTimeOffset.UtcNow.AddDays(-90);
        string[] readableTenantIds = crmAuthInfo.ReadableTenants.Length > 0
            ? crmAuthInfo.ReadableTenants : crmAuthInfo.WriteableTenants;
        if (readableTenantIds.Length == 0) readableTenantIds = ["default"];
        List<Email> crmEmails = await operations.RetrieveAllEmails().AsNoTracking()
            .Include(item => item.TenantCompanyRelationship).ThenInclude(item => item.Company)
            .Where(item => readableTenantIds.Contains(item.TenantCompanyRelationship.TenantId)
                && item.State == EmailState.Sent
                && (item.SentOn ?? item.LastUpdated) >= fromDate)
            .OrderByDescending(item => item.SentOn ?? item.LastUpdated)
            .Take(Math.Clamp(limit, 1, 1000))
            .ToListAsync(cancellationToken);

        DateTimeOffset mailboxFrom = crmEmails.Count == 0
            ? fromDate
            : crmEmails.Min(item => item.SentOn ?? item.LastUpdated).AddDays(-1);
        IReadOnlyList<MailboxMessage> sentItems = await mailboxClient.RetrieveSentAsync(
            mailboxFrom,
            Math.Clamp(Math.Max(crmEmails.Count * 5, 100), 1, 500),
            cancellationToken);

        Guid[] relationshipIds = crmEmails.Select(item => item.TenantCompanyRelationshipId).Distinct().ToArray();
        var opportunities = await salesWorkspace.RetrieveOpportunities().AsNoTracking()
            .Where(item => relationshipIds.Contains(item.TenantCompanyRelationshipId))
            .OrderByDescending(item => item.LastUpdated)
            .Select(item => new { item.Id, item.TenantCompanyRelationshipId, Stage = item.Stage.ToString(), item.LastUpdated })
            .ToListAsync(cancellationToken);

        return Ok(crmEmails.Select(email =>
        {
            var candidates = sentItems.Select(message => ScoreSentCandidate(email, message))
                .Where(candidate => candidate.Score >= 50)
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => Math.Abs(((email.SentOn ?? email.LastUpdated) - candidate.Message.ReceivedOn).TotalMinutes))
                .Take(5)
                .Select(candidate => new
                {
                    candidate.Message.ExternalId,
                    candidate.Message.InternetMessageId,
                    candidate.Message.ConversationId,
                    candidate.Message.FromAddress,
                    candidate.Message.ToAddresses,
                    candidate.Message.Subject,
                    SentOn = candidate.Message.ReceivedOn,
                    candidate.Score,
                    candidate.Reasons
                });
            return new
            {
                email.Id,
                Company = CompanyNames.ResolvePreferredName(email.TenantCompanyRelationship.Company),
                email.OpportunityId,
                email.ExternalMessageId,
                email.FromEmailAddress,
                email.ToAddresses,
                email.Subject,
                email.SentOn,
                Candidates = candidates,
                Opportunities = opportunities.Where(item => item.TenantCompanyRelationshipId == email.TenantCompanyRelationshipId)
                    .Select(item => new { item.Id, item.Stage, item.LastUpdated })
            };
        }));
    }

    [HttpPost("Emails/{emailId:guid}/ReconcileSent")]
    public async Task<IActionResult> ReconcileSentEmail(
        Guid emailId,
        [FromBody] ReconcileSentEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        if (TryAuthenticate(out IActionResult failure))
            return failure!;
        if (string.IsNullOrWhiteSpace(request.MailboxExternalId))
            return BadRequest("A Sent Items mailbox message id is required.");

        string[] writeableTenantIds = crmAuthInfo.WriteableTenants.Length > 0 ? crmAuthInfo.WriteableTenants : ["default"];
        Email email = await operations.RetrieveAllEmails().Include(item => item.TenantCompanyRelationship)
            .FirstOrDefaultAsync(item => item.Id == emailId
                && writeableTenantIds.Contains(item.TenantCompanyRelationship.TenantId), cancellationToken);
        if (email is null) return NotFound();

        DateTimeOffset referenceOn = email.SentOn ?? email.LastUpdated;
        IReadOnlyList<MailboxMessage> sentItems = await mailboxClient.RetrieveSentAsync(
            referenceOn.AddDays(-7), referenceOn.AddDays(7), 250, cancellationToken);
        MailboxMessage match = sentItems.FirstOrDefault(item => item.ExternalId == request.MailboxExternalId);
        if (match is null) return NotFound("The specified message was not found in Sent Items.");
        SentCandidate candidate = ScoreSentCandidate(email, match);
        if (candidate.Score < 80)
            return BadRequest($"Mailbox evidence is not strong enough to reconcile this email. Score {candidate.Score}: {string.Join(", ", candidate.Reasons)}.");

        if (!string.IsNullOrWhiteSpace(match.InternetMessageId))
        {
            bool alreadyLinked = await operations.RetrieveAllEmails().AsNoTracking().AnyAsync(item =>
                item.Id != email.Id && item.ExternalMessageId == match.InternetMessageId,
                cancellationToken);
            if (alreadyLinked)
                return Conflict("This Sent Items message is already linked to another CRM email record.");
        }

        if (request.OpportunityId.HasValue)
        {
            bool validOpportunity = await salesWorkspace.RetrieveOpportunities().AnyAsync(item =>
                item.Id == request.OpportunityId.Value
                && item.TenantCompanyRelationshipId == email.TenantCompanyRelationshipId,
                cancellationToken);
            if (!validOpportunity) return BadRequest("The selected opportunity does not belong to this company relationship.");
            email.OpportunityId = request.OpportunityId;
        }

        email.State = EmailState.Sent;
        email.ExternalMessageId = match.InternetMessageId ?? match.ExternalId;
        email.SentOn = match.ReceivedOn;
        email.LastError = null;
        email.LastUpdatedBy = CurrentExecutionUserId;
        email.LastUpdated = DateTimeOffset.UtcNow;
        await operations.SaveAsync(cancellationToken);
        return Ok(new { email.Id, email.State, email.OpportunityId, email.ExternalMessageId, email.SentOn, candidate.Score, candidate.Reasons });
    }

    [HttpPost("Emails/{emailId:guid}/CancelUnverified")]
    public async Task<IActionResult> CancelUnverifiedEmail(
        Guid emailId,
        [FromBody] CancelUnverifiedEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        if (TryAuthenticate(out IActionResult failure))
            return failure!;

        string[] writeableTenantIds = crmAuthInfo.WriteableTenants.Length > 0 ? crmAuthInfo.WriteableTenants : ["default"];
        Email email = await operations.RetrieveAllEmails().Include(item => item.TenantCompanyRelationship)
            .FirstOrDefaultAsync(item => item.Id == emailId
                && item.State == EmailState.Sent
                && writeableTenantIds.Contains(item.TenantCompanyRelationship.TenantId), cancellationToken);
        if (email is null) return NotFound();

        DateTimeOffset referenceOn = email.SentOn ?? email.LastUpdated;
        IReadOnlyList<MailboxMessage> sentItems = await mailboxClient.RetrieveSentAsync(
            referenceOn.AddDays(-7), referenceOn.AddDays(7), 250, cancellationToken);
        SentCandidate credibleMatch = sentItems.Select(message => ScoreSentCandidate(email, message))
            .OrderByDescending(candidate => candidate.Score).FirstOrDefault(candidate => candidate.Score >= 80);
        if (credibleMatch is not null)
            return Conflict(new
            {
                message = "A credible Sent Items match exists; reconcile it instead of cancelling the CRM record.",
                credibleMatch.Message.ExternalId,
                credibleMatch.Score,
                credibleMatch.Reasons
            });

        string reason = string.IsNullOrWhiteSpace(request?.Reason)
            ? "No matching message was found in Sent Items during mailbox reconciliation."
            : request.Reason.Trim();
        email.State = EmailState.Cancelled;
        email.SentOn = null;
        email.ExternalMessageId = null;
        email.LastError = $"Mailbox reconciliation: {reason}";
        email.LastUpdatedBy = CurrentExecutionUserId;
        email.LastUpdated = DateTimeOffset.UtcNow;
        await operations.SaveAsync(cancellationToken);
        return Ok(new { email.Id, email.State, Reason = reason });
    }

    [HttpGet("Messages")]
    public async Task<IActionResult> GetMessages(
        [FromQuery] int limit = 25,
        [FromQuery] Guid? conversationId = null,
        CancellationToken cancellationToken = default)
    {
        if (TryAuthenticate(out IActionResult failure))
            return failure!;

        string[] readableTenantIds = crmAuthInfo.ReadableTenants.Length > 0 ? crmAuthInfo.ReadableTenants : crmAuthInfo.WriteableTenants;
        if (readableTenantIds.Length == 0) readableTenantIds = ["default"];
        var query = messageWorkspace.RetrieveAll()
            .AsNoTracking()
            .Include(item => item.Entries)
            .Include(item => item.ProcessDefinition)
            .Include(item => item.ProcessStep)
            .Where(item => readableTenantIds.Contains(item.TenantId)
                && item.State == AgentMessageState.Pending);

        query = conversationId.HasValue
            ? query.Where(item => item.Id == conversationId.Value)
            : query.Where(item => item.Kind != AgentMessageKind.ApprovalRequest);

        var messages = await query
            .OrderBy(item => item.CreatedOn)
            .Take(Math.Clamp(limit, 1, 100))
            .Select(item => new
            {
                item.Id,
                item.Kind,
                item.Title,
                item.Body,
                item.ProcessDefinitionId,
                ProcessName = item.ProcessDefinition == null ? null : item.ProcessDefinition.Name,
                item.ProcessTaskId,
                item.ProcessStepId,
                ProcessStepKey = item.ProcessStep == null ? null : item.ProcessStep.Key,
                ProcessStepName = item.ProcessStep == null ? null : item.ProcessStep.Name,
                item.EmailId,
                item.ProposedProcessDefinitionId,
                Entries = item.Entries.OrderBy(entry => entry.CreatedOn).Select(entry => new
                {
                    entry.Role,
                    entry.Body,
                    entry.CreatedOn
                })
            })
            .ToListAsync(cancellationToken);
        return Ok(messages);
    }

    [HttpPost("Messages/{messageId:guid}/Entries")]
    public async Task<IActionResult> AppendMessageEntry(
        Guid messageId,
        [FromBody] AppendAgentMessageEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (TryAuthenticate(out IActionResult failure))
            return failure!;
        if (string.IsNullOrWhiteSpace(request.Body))
            return BadRequest("A conversation response is required.");

        AgentMessageEntry entry = await agentMessageService.AppendEntryAsync(
            messageId,
            "Agent",
            request.Body,
            CurrentExecutionUserId,
            cancellationToken);
        return entry is null ? NotFound() : Ok(new { entry.Id, entry.AgentMessageId, entry.CreatedOn });
    }

    [HttpGet("Tasks/Due")]
    public async Task<IActionResult> GetDueTasks(
        [FromQuery] int limit = 25,
        [FromQuery] Guid? processTaskId = null,
        CancellationToken cancellationToken = default)
    {
        if (TryAuthenticate(out IActionResult failure))
            return failure!;

        DateTimeOffset now = DateTimeOffset.UtcNow;
        IQueryable<ProcessTask> runnableTasks;
        if (processTaskId.HasValue)
        {
            runnableTasks = processWorkspace.RetrieveTasks().Where(task =>
                task.Id == processTaskId.Value
                && task.State == ProcessTaskState.Pending
                && (!task.AgentClaimId.HasValue || task.AgentClaimedBy == CurrentExecutionUserId));
        }
        else
        {
            runnableTasks = salesWorkspace.RetrieveRunnableProcessTasks(now);
        }

        List<ProcessTask> tasks = await WorkflowTaskQueue.OrderByCommercialProgress(runnableTasks)
            .AsNoTracking()
            .Include(task => task.ProcessStep)
            .Include(task => task.Email)
            .Include(task => task.Lead)
                .ThenInclude(lead => lead.Company)
                    .ThenInclude(company => company.RegisteredAddress)
            .Include(task => task.Lead)
                .ThenInclude(lead => lead.Contacts)
            .Include(task => task.TenantCompanyRelationship)
                .ThenInclude(relationship => relationship.Company)
                    .ThenInclude(company => company.RegisteredAddress)
            .Include(task => task.Opportunity)
            .Include(task => task.ClientAccount)
            .Take(Math.Max(1, limit))
            .ToListAsync(cancellationToken);

        List<Guid> stepIds =
        [
            .. tasks.Select(item => item.ProcessStepId).Distinct()
        ];

        Dictionary<Guid, List<AgentTaskOutcomeViewModel>> outcomeLookup = stepIds.Count == 0
            ? []
            : await processWorkspace.RetrieveTransitions()
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
            : await salesWorkspace.RetrieveRelationshipContacts()
                .AsNoTracking()
                .Include(item => item.CompanyContact)
                .Where(item => relationshipIds.Contains(item.TenantCompanyRelationshipId))
                .OrderByDescending(item => item.IsPrimary)
                .ThenBy(item => item.CreatedOn)
                .GroupBy(item => item.TenantCompanyRelationshipId)
                .ToDictionaryAsync(group => group.Key, group => group.First(), cancellationToken);

        HashSet<(Guid CompanyId, string TenantId)> taskCompanies = tasks
            .Select(task => (
                CompanyId: task.TenantCompanyRelationship?.CompanyId ?? task.Lead?.CompanyId ?? Guid.Empty,
                TenantId: task.TenantCompanyRelationship?.TenantId ?? task.Lead?.TenantId ?? string.Empty))
            .Where(item => item.CompanyId != Guid.Empty && !string.IsNullOrWhiteSpace(item.TenantId))
            .ToHashSet();
        List<Guid> companyIds = [.. taskCompanies.Select(item => item.CompanyId).Distinct()];
        List<string> tenantIds = [.. taskCompanies.Select(item => item.TenantId).Distinct()];
        List<CompanyHistoryItem> recentHistory = companyIds.Count == 0
            ? []
            : await salesWorkspace.RetrieveCompanyHistory()
                .AsNoTracking()
                .Where(item => companyIds.Contains(item.CompanyId) && tenantIds.Contains(item.TenantId))
                .OrderByDescending(item => item.OccurredOn)
                .Take(Math.Max(20, companyIds.Count * 20))
                .ToListAsync(cancellationToken);
        Dictionary<(Guid CompanyId, string TenantId), List<AgentCompanyHistoryViewModel>> historyLookup = recentHistory
            .Where(item => taskCompanies.Contains((item.CompanyId, item.TenantId)))
            .GroupBy(item => (item.CompanyId, item.TenantId))
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.OccurredOn)
                    .Take(20)
                    .Select(item => new AgentCompanyHistoryViewModel
                    {
                        OccurredOn = item.OccurredOn,
                        Lane = item.Lane,
                        EventType = item.EventType,
                        Summary = item.Summary,
                        FactKey = item.FactKey ?? string.Empty,
                        FactValue = item.FactValue ?? string.Empty,
                        Confidence = item.Confidence ?? string.Empty,
                        SourceType = item.SourceType ?? string.Empty
                    })
                    .ToList());

        return Ok(tasks.Select(task =>
        {
            RelationshipContact primaryContact = task.TenantCompanyRelationshipId.HasValue
                ? primaryContactsByRelationship.GetValueOrDefault(task.TenantCompanyRelationshipId.Value)
                : null;
            Company company = task.TenantCompanyRelationship?.Company ?? task.Lead?.Company;
            string tenantId = task.TenantCompanyRelationship?.TenantId ?? task.Lead?.TenantId ?? string.Empty;
            IReadOnlyList<AgentCompanyHistoryViewModel> companyHistory = company is null
                ? []
                : historyLookup.GetValueOrDefault((company.Id, tenantId)) ?? [];

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
                    CompanyNames.ResolvePreferredName(company),
                    task.Lead?.RawCompanyName,
                    string.Empty),
                CompanyNumber = company?.CompanyNumber ?? task.Lead?.RawCompanyNumber ?? string.Empty,
                CompanyStatus = company?.CompanyStatus ?? string.Empty,
                CompanyWebsiteUrl = company?.WebsiteUrl ?? task.Lead?.RawWebsiteUrl ?? string.Empty,
                CompanyRegistryUrl = company?.RegistryUri ?? string.Empty,
                CompanySicCodes = company?.PrimarySicCodes ?? string.Empty,
                CompanyRegisteredOffice = AddressRecordMapper.Format(company?.RegisteredAddress),
                ExistingResearchSummary = company?.ResearchSummary ?? string.Empty,
                ExistingQualificationNotes = task.Lead?.QualificationNotes ?? string.Empty,
                ContactName = primaryContact?.CompanyContact?.Name ?? task.Lead?.Contacts.FirstOrDefault()?.Name ?? string.Empty,
                ContactEmailAddress = primaryContact?.CompanyContact?.EmailAddress ?? task.Lead?.RawContactEmailAddress ?? string.Empty,
                OwnerUserId = task.TenantCompanyRelationship?.AccountOwnerUserId ?? task.CreatedBy,
                OwnerDisplayName = task.TenantCompanyRelationship?.AccountOwnerDisplayName ?? task.CreatedBy,
                DueOn = task.DueOn,
                ActionType = task.ActionType,
                StepKey = task.ProcessStep.Key,
                StepObjective = task.ProcessStep.Objective ?? string.Empty,
                RequiredFacts = task.ProcessStep.RequiredFacts ?? string.Empty,
                ProducedFacts = task.ProcessStep.ProducedFacts ?? string.Empty,
                ViabilityImpact = task.ProcessStep.ViabilityImpact ?? string.Empty,
                Title = task.RenderedTitle,
                Instructions = task.RenderedInstructions ?? string.Empty,
                EmailSubjectTemplate = task.RenderedEmailSubject ?? string.Empty,
                EmailBodyTemplate = task.RenderedEmailBody ?? string.Empty,
                CallScriptTemplate = task.RenderedCallScript ?? string.Empty,
                QuestionSetTemplate = task.RenderedQuestionSet ?? string.Empty,
                ExistingEmailState = task.Email?.State.ToString() ?? string.Empty,
                ExistingEmailSubject = task.Email?.Subject ?? string.Empty,
                ExistingEmailBody = task.Email?.BodyText ?? task.Email?.BodyHtml ?? string.Empty,
                CompanyHistory = companyHistory,
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

        {
            var taskContext = await processWorkspace.RetrieveTasks()
                .AsNoTracking()
                .Where(item => item.Id == processTaskId)
                .Select(item => new { item.ActionType, StepKey = item.ProcessStep.Key })
                .FirstOrDefaultAsync(cancellationToken);

            if (taskContext is null)
                return NotFound();

            if (taskContext.ActionType == ProcessActionType.Approval)
                return Conflict("This workflow decision requires a human account owner.");

            if (string.Equals(taskContext.StepKey, "review-response", StringComparison.OrdinalIgnoreCase))
            {
                EmailTaskEvidenceResult evidence = await emailTaskEvidenceService.GetAsync(
                    processTaskId,
                    CurrentExecutionUserId,
                    cancellationToken);
                string outcome = request.OutcomeKey.Trim().ToLowerInvariant();

                if (outcome == "no-reply" && evidence?.NoEvidenceConfirmed != true)
                    return Conflict(evidence?.Status ?? "A fresh mailbox check is required before recording no reply.");

                if (outcome is "positive-reply" or "demo-interest" or "interested" or "not-interested" or "lost"
                    && evidence?.HasMatchingEvidence != true)
                {
                    return Conflict("Matching inbound email evidence is required for this response outcome.");
                }
            }
        }

        ProcessTask task;
        try
        {
            task = await workflowAutomationService.CompleteTaskAsync(
                new ProcessTaskCompletionCommand
                {
                    ProcessTaskId = processTaskId,
                    OutcomeKey = request.OutcomeKey,
                    CompletionNote = request.CompletionNote
                },
                cancellationToken);
        }
        catch (WorkflowRuleViolationException exception)
        {
            return Conflict(exception.Message);
        }

        if (task is null)
            return NotFound();

        if (string.Equals(request.OutcomeKey, "await-response", StringComparison.OrdinalIgnoreCase))
        {
            List<AgentMessage> obsoleteApprovals = await messageWorkspace.RetrieveAll()
                .Where(item => item.ProcessTaskId == processTaskId
                    && item.State == AgentMessageState.Pending
                    && item.Kind == AgentMessageKind.ApprovalRequest)
                .ToListAsync(cancellationToken);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            foreach (AgentMessage message in obsoleteApprovals)
            {
                message.State = AgentMessageState.Completed;
                message.ResponseNotes = "Closed automatically because the overdue task progressed as awaiting response; no contact was claimed.";
                message.RespondedBy = CurrentExecutionUserId;
                message.RespondedOn = now;
                message.LastUpdatedBy = CurrentExecutionUserId;
                message.LastUpdated = now;
            }

            foreach (AgentMessage message in obsoleteApprovals)
                await messageWorkspace.ModifyAsync(message, cancellationToken);
        }

        return Ok(new
        {
            task.Id,
            task.State,
            task.CompletionOutcomeKey,
            task.CompletedOn
        });
    }

    [HttpGet("Tasks/{processTaskId:guid}/EmailEvidence")]
    public async Task<IActionResult> GetTaskEmailEvidence(
        Guid processTaskId,
        CancellationToken cancellationToken = default)
    {
        if (TryAuthenticate(out IActionResult failure))
            return failure!;

        EmailTaskEvidenceResult result = await emailTaskEvidenceService.GetAsync(
            processTaskId,
            CurrentExecutionUserId,
            cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("Tasks/{processTaskId:guid}/DraftEmail")]
    public async Task<IActionResult> SaveTaskDraftEmail(
        Guid processTaskId,
        [FromBody] AgentTaskDraftEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        if (TryAuthenticate(out IActionResult failure))
            return failure!;

        ProcessTask task = await processWorkspace.RetrieveTasks()
            .Include(item => item.Email)
            .FirstOrDefaultAsync(item => item.Id == processTaskId, cancellationToken);

        if (task is null || !task.TenantCompanyRelationshipId.HasValue)
            return NotFound();
        if (RecipientEmailContentValidator.ContainsInternalDraftingGuidance(request.Body))
            return BadRequest("Recipient-facing email content contains internal drafting guidance.");

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
            await processWorkspace.SaveAsync(cancellationToken);
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

    [HttpPost("Messages/{messageId:guid}/ReplacementEmailDraft")]
    public async Task<IActionResult> CreateReplacementEmailDraft(
        Guid messageId,
        [FromBody] AgentReplacementEmailDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        if (TryAuthenticate(out IActionResult failure))
            return failure!;
        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Body))
            return BadRequest("A subject and recipient-ready body are required.");
        if (RecipientEmailContentValidator.ContainsInternalDraftingGuidance(request.Body))
            return BadRequest("Recipient-facing email content contains internal drafting guidance.");

        Guid relationshipId;
        Guid opportunityId;
        Guid processDefinitionId;
        Guid? processStepId;
        Guid? processTaskId;
        Email rejectedEmail;
        {
            AgentMessage conversation = await messageWorkspace.RetrieveAll()
                .FirstOrDefaultAsync(item => item.Id == messageId, cancellationToken);
            if (conversation is null)
                return NotFound("The conversation was not found.");
            if (!conversation.EmailId.HasValue)
                return Conflict("The conversation does not identify a source email.");

            rejectedEmail = await operations.RetrieveAllEmails()
                .Include(item => item.Material)
                .Include(item => item.TenantCompanyRelationship)
                .FirstOrDefaultAsync(item => item.Id == conversation.EmailId.Value, cancellationToken);
            if (rejectedEmail is null)
                return Conflict("The conversation's source email no longer exists.");
            if (rejectedEmail.State != EmailState.Rejected)
                return Conflict("Only a rejected email can be replaced through this operation.");

            relationshipId = rejectedEmail.TenantCompanyRelationshipId;
            string tenantId = rejectedEmail.TenantCompanyRelationship.TenantId;
            if (!crmAuthInfo.WriteableTenants.Contains(tenantId))
                return Forbid();

            Guid? inferredOpportunityId = rejectedEmail.OpportunityId ?? conversation.OpportunityId;
            if (!inferredOpportunityId.HasValue)
            {
                List<Guid> candidates = await salesWorkspace.RetrieveOpportunities()
                    .Where(item => item.TenantCompanyRelationshipId == relationshipId
                        && item.Stage != SalesPipelineStage.Won
                        && item.Stage != SalesPipelineStage.Lost)
                    .OrderByDescending(item => item.LastUpdated)
                    .Select(item => item.Id)
                    .Take(2)
                    .ToListAsync(cancellationToken);
                if (candidates.Count != 1)
                    return Conflict("CRM could not infer one unambiguous active opportunity for the rejected email.");
                inferredOpportunityId = candidates[0];
            }
            opportunityId = inferredOpportunityId.Value;

            Guid? inferredDefinitionId = conversation.ProcessDefinitionId
                ?? await processWorkspace.RetrieveDefinitions()
                    .Where(item => item.TenantId == tenantId
                        && item.ScopeType == ProcessScopeType.Opportunity
                        && item.IsActive)
                    .OrderByDescending(item => item.VersionNumber)
                    .Select(item => (Guid?)item.Id)
                    .FirstOrDefaultAsync(cancellationToken);
            if (!inferredDefinitionId.HasValue)
                return Conflict("CRM could not identify the active Opportunity Conversion process.");
            processDefinitionId = inferredDefinitionId.Value;
            processStepId = conversation.ProcessStepId;
            processTaskId = conversation.ProcessTaskId;

            DateTimeOffset now = DateTimeOffset.UtcNow;
            rejectedEmail.OpportunityId = opportunityId;
            rejectedEmail.LastUpdatedBy = CurrentExecutionUserId;
            rejectedEmail.LastUpdated = now;
            if (rejectedEmail.Material is not null)
            {
                rejectedEmail.Material.OpportunityId = opportunityId;
                rejectedEmail.Material.LastUpdatedBy = CurrentExecutionUserId;
                rejectedEmail.Material.LastUpdated = now;
            }
            conversation.OpportunityId = opportunityId;
            conversation.ProcessDefinitionId = processDefinitionId;
            conversation.LastUpdatedBy = CurrentExecutionUserId;
            conversation.LastUpdated = now;
            await operations.SaveAsync(cancellationToken);
            await messageWorkspace.ModifyAsync(conversation, cancellationToken);
        }

        Email replacement = await emailDraftWorkflowService.SaveDraftAsync(
            new EmailDraftUpsertCommand
            {
                ClientId = relationshipId,
                ClientOpportunityId = opportunityId,
                Subject = request.Subject,
                Body = request.Body,
                ToAddresses = string.IsNullOrWhiteSpace(request.ToAddresses) ? rejectedEmail.ToAddresses : request.ToAddresses,
                CcAddresses = request.CcAddresses,
                BccAddresses = request.BccAddresses
            },
            cancellationToken);
        if (replacement is null)
            return BadRequest("CRM could not create the replacement draft.");

        await agentMessageService.UpsertAsync(new AgentMessage
        {
            Id = Guid.NewGuid(),
            TenantCompanyRelationshipId = relationshipId,
            OpportunityId = opportunityId,
            EmailId = replacement.Id,
            ProcessDefinitionId = processDefinitionId,
            ProcessStepId = processStepId,
            ProcessTaskId = processTaskId,
            Kind = AgentMessageKind.ApprovalRequest,
            State = AgentMessageState.Pending,
            CorrelationKey = $"approval:replacement-email:{messageId}",
            Title = string.IsNullOrWhiteSpace(request.ApprovalTitle)
                ? "Approve corrected replacement email"
                : request.ApprovalTitle.Trim(),
            Body = string.IsNullOrWhiteSpace(request.ApprovalBody)
                ? "The rejected source email was preserved and a corrected recipient-ready replacement is awaiting human approval."
                : request.ApprovalBody.Trim(),
            AgentName = "Approval Agent",
            CreatedBy = CurrentExecutionUserId,
            LastUpdatedBy = CurrentExecutionUserId
        }, cancellationToken);

        return Ok(new
        {
            SourceEmailId = rejectedEmail.Id,
            ReplacementEmailId = replacement.Id,
            replacement.State,
            OpportunityId = opportunityId,
            ProcessDefinitionId = processDefinitionId
        });
    }

    [HttpGet("Messages/{messageId:guid}/RelatedDraftEmails")]
    public async Task<IActionResult> GetRelatedDraftEmails(
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        if (TryAuthenticate(out IActionResult failure))
            return failure!;

        try
        {
            RelatedEmailDraftContext context = await workflowAutomationService
                .GetRelatedEmailDraftContextAsync(messageId, cancellationToken);
            return context is null ? NotFound("The conversation or its source email was not found.") : Ok(context);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
    }

    [HttpPost("Messages/{messageId:guid}/RefreshRelatedDraftEmails")]
    public async Task<IActionResult> RefreshRelatedDraftEmails(
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        if (TryAuthenticate(out IActionResult failure))
            return failure!;

        try
        {
            RelatedEmailDraftRefreshResult result = await workflowAutomationService
                .RefreshRelatedEmailDraftsAsync(messageId, cancellationToken);
            return result is null ? NotFound("The conversation or its source email was not found.") : Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
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
                ProcessStepId = request.ProcessStepId,
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

        List<ProcessDefinition> definitions = await processWorkspace.RetrieveDefinitions()
            .AsNoTracking()
            .Where(item => item.LifecycleState == ProcessDefinitionLifecycleState.Active || item.IsActive)
            .OrderBy(item => item.ScopeType)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);

        List<ProcessPerformanceMetricsViewModel> metrics = [];
        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach (ProcessDefinition definition in definitions)
        {
        int activeInstanceCount = await processWorkspace.RetrieveInstances().CountAsync(
                item => item.ProcessDefinitionId == definition.Id && item.State == ProcessInstanceState.Active,
                cancellationToken);

        int pendingTaskCount = await processWorkspace.RetrieveTasks().CountAsync(
                item => item.ProcessInstance.ProcessDefinitionId == definition.Id && item.State == ProcessTaskState.Pending,
                cancellationToken);

        int sentEmailCount = await operations.RetrieveAllEmails().CountAsync(
                item => item.State == EmailState.Sent
                    && ((definition.ScopeType == ProcessScopeType.Opportunity && item.Opportunity!.ProcessInstances.Any(instance => instance.ProcessDefinitionId == definition.Id))
                        || (definition.ScopeType == ProcessScopeType.ClientAccount && item.ClientAccount!.ProcessInstances.Any(instance => instance.ProcessDefinitionId == definition.Id))),
                cancellationToken);

        int replyActivityCount = await salesWorkspace.RetrieveActivities().CountAsync(
                item => item.Direction == ActivityDirection.Inbound
                    && ((definition.ScopeType == ProcessScopeType.Opportunity && item.Opportunity!.ProcessInstances.Any(instance => instance.ProcessDefinitionId == definition.Id))
                        || (definition.ScopeType == ProcessScopeType.ClientAccount && item.ClientAccount!.ProcessInstances.Any(instance => instance.ProcessDefinitionId == definition.Id))),
                cancellationToken);

            int wonCount = definition.ScopeType == ProcessScopeType.Opportunity
            ? await salesWorkspace.RetrieveOpportunities().CountAsync(item =>
                    item.ProcessInstances.Any(instance => instance.ProcessDefinitionId == definition.Id)
                    && item.Stage == SalesPipelineStage.Won, cancellationToken)
                : 0;

            int lostCount = definition.ScopeType == ProcessScopeType.Opportunity
            ? await salesWorkspace.RetrieveOpportunities().CountAsync(item =>
                    item.ProcessInstances.Any(instance => instance.ProcessDefinitionId == definition.Id)
                    && item.Stage == SalesPipelineStage.Lost, cancellationToken)
                : 0;

        List<ProcessStepPerformanceMetricsViewModel> stepMetrics = await processWorkspace.RetrieveSteps()
                .AsNoTracking()
                .Where(step => step.ProcessDefinitionId == definition.Id && step.IsActive)
                .OrderBy(step => step.Sequence)
                .Select(step => new ProcessStepPerformanceMetricsViewModel
                {
                    ProcessStepId = step.Id,
                    Key = step.Key,
                    Name = step.Name,
                    Sequence = step.Sequence,
                    PendingCount = step.Tasks.Count(task => task.State == ProcessTaskState.Pending),
                    OverdueCount = step.Tasks.Count(task => task.State == ProcessTaskState.Pending && task.DueOn <= now),
                    CompletedCount = step.Tasks.Count(task => task.State == ProcessTaskState.Completed),
                    CancelledCount = step.Tasks.Count(task => task.State == ProcessTaskState.Cancelled),
                    CompletedWithoutEvidenceCount = step.Tasks.Count(task =>
                        task.State == ProcessTaskState.Completed && (task.CompletionNotes == null || task.CompletionNotes == string.Empty)),
                    AverageTurnaroundMinutes = step.Tasks
                        .Where(task => task.CompletedOn.HasValue)
                        .Average(task => (double?)EF.Functions.DateDiffSecond(task.CreatedOn, task.CompletedOn!.Value) / 60.0),
                    OldestPendingSince = step.Tasks
                        .Where(task => task.State == ProcessTaskState.Pending)
                        .Min(task => (DateTimeOffset?)task.CreatedOn)
                })
                .ToListAsync(cancellationToken);

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
                LostCount = lostCount,
                Steps = stepMetrics
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

        List<ProcessStepDraftUpdateRequest> requestedStepUpdates = request.StepUpdates ?? [];
        if (requestedStepUpdates.Count == 0 &&
            (request.ProcessStepId.HasValue || !string.IsNullOrWhiteSpace(request.ProcessStepKey)) &&
            (!string.IsNullOrWhiteSpace(request.EmailSubjectTemplate) || !string.IsNullOrWhiteSpace(request.EmailBodyTemplate)))
        {
            requestedStepUpdates =
            [
                new ProcessStepDraftUpdateRequest
                {
                    Id = request.ProcessStepId,
                    Key = request.ProcessStepKey,
                    EmailSubjectTemplate = request.EmailSubjectTemplate,
                    EmailBodyTemplate = request.EmailBodyTemplate
                }
            ];
        }

        ProcessDefinition draft;
        try
        {
            draft = await processDraftService.CreateDraftAsync(
            processDefinitionId,
            CurrentExecutionUserId,
            request.ProposedByAgent,
            request.ChangeSummary,
            request.Name,
            request.Description,
            requestedStepUpdates.Select(item => new ProcessStepDraftUpdate
            {
                Id = item.Id,
                Key = item.Key,
                Name = item.Name,
                Objective = item.Objective,
                RequiredFacts = item.RequiredFacts,
                ProducedFacts = item.ProducedFacts,
                ViabilityImpact = item.ViabilityImpact,
                TaskInstructionsTemplate = item.TaskInstructionsTemplate,
                EmailSubjectTemplate = item.EmailSubjectTemplate,
                EmailBodyTemplate = item.EmailBodyTemplate,
                CallScriptTemplate = item.CallScriptTemplate,
                QuestionSetTemplate = item.QuestionSetTemplate
            }).ToList(),
            cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }

        if (draft is null)
            return NotFound();

        if (request.AgentMessageId.HasValue)
        {
            AgentMessage conversation = await messageWorkspace.RetrieveAll().FirstOrDefaultAsync(
                item => item.Id == request.AgentMessageId.Value,
                cancellationToken);
            if (conversation is null)
                return NotFound("The originating Approval Agent conversation was not found.");

            conversation.ProcessDefinitionId = processDefinitionId;
            conversation.ProposedProcessDefinitionId = draft.Id;
            conversation.Kind = AgentMessageKind.ProcessProposal;
            conversation.State = AgentMessageState.Pending;
            conversation.Title = string.IsNullOrWhiteSpace(request.ApprovalTitle)
                ? $"Review proposed change to {draft.Name}"
                : request.ApprovalTitle;
            conversation.Body = string.IsNullOrWhiteSpace(request.ApprovalBody)
                ? request.ChangeSummary ?? "A process change is ready for final human approval."
                : request.ApprovalBody;
            conversation.LastUpdatedBy = CurrentExecutionUserId;
            conversation.LastUpdated = DateTimeOffset.UtcNow;
            await messageWorkspace.ModifyAsync(conversation, cancellationToken);
            await agentMessageService.AppendEntryAsync(
                conversation.Id,
                "Agent",
                $"Proposed process version {draft.VersionNumber}: {draft.ChangeSummary ?? request.ChangeSummary}. Review the exact draft and use Approve and activate change only when satisfied.",
                CurrentExecutionUserId,
                cancellationToken);
            return Ok(new { draft.Id, draft.Name, draft.VersionNumber, ConversationId = conversation.Id });
        }

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

        Lead lead = await salesWorkspace.RetrieveLeads()
            .Include(item => item.Contacts)
            .Include(item => item.Company)
                .ThenInclude(company => company.RegisteredAddress)
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
        string addressText = Normalize(request.RawAddressText);
        if (addressText is not null)
        {
            if (lead.Company.RegisteredAddress is null)
            {
                Address address = AddressRecordMapper.CreateFromText(
                    addressText,
                    lead.SourceSystem,
                    CurrentExecutionUserId,
                    now);
                salesWorkspace.Add(address);
                lead.Company.RegisteredAddressId = address.Id;
            }
            else
            {
                AddressRecordMapper.ApplyText(lead.Company.RegisteredAddress, addressText, CurrentExecutionUserId, now);
            }
            lead.Company.LastUpdatedBy = CurrentExecutionUserId;
            lead.Company.LastUpdated = now;
        }
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

                salesWorkspace.Add(primaryContact);
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

        await salesWorkspace.SaveAsync(cancellationToken);

        return Ok(new
        {
            lead.Id,
            lead.RawCompanyName,
            lead.RawCompanyNumber,
            lead.RawWebsiteUrl
        });
    }

    [HttpPost("Leads/{leadId:guid}/ResearchFindings")]
    public async Task<IActionResult> AppendLeadResearchFinding(
        Guid leadId,
        [FromBody] AppendLeadResearchFindingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (TryAuthenticate(out IActionResult failure))
            return failure!;

        string sectionKey = Normalize(request?.SectionKey)?.ToLowerInvariant();
        string finding = Normalize(request?.Finding);
        if (string.IsNullOrWhiteSpace(sectionKey) || string.IsNullOrWhiteSpace(finding))
            return BadRequest("A section key and finding are required.");

        if (!Regex.IsMatch(sectionKey, "^[a-z0-9-]{1,64}$", RegexOptions.CultureInvariant))
            return BadRequest("The section key may contain only lower-case letters, numbers, and hyphens.");

        Lead lead = await salesWorkspace.RetrieveLeads().FirstOrDefaultAsync(item => item.Id == leadId, cancellationToken);
        if (lead is null)
            return NotFound();

        string heading = $"## {sectionKey}";
        string section = $"{heading}\n{finding}";
        string notes = lead.QualificationNotes ?? string.Empty;
        string pattern = $@"(?ms)^## {Regex.Escape(sectionKey)}\s*\r?\n.*?(?=^## |\z)";
        lead.QualificationNotes = Regex.IsMatch(notes, pattern, RegexOptions.CultureInvariant)
            ? Regex.Replace(notes, pattern, section + "\n", RegexOptions.CultureInvariant).Trim()
            : string.Join("\n\n", new[] { notes.Trim(), section }.Where(value => !string.IsNullOrWhiteSpace(value)));
        lead.LastUpdatedBy = CurrentExecutionUserId;
        lead.LastUpdated = DateTimeOffset.UtcNow;

        await salesWorkspace.SaveAsync(cancellationToken);
        return Ok(new { lead.Id, SectionKey = sectionKey });
    }

    static SentCandidate ScoreSentCandidate(Email email, MailboxMessage message)
    {
        int score = 0;
        List<string> reasons = [];
        if (!string.IsNullOrWhiteSpace(email.ExternalMessageId)
            && (string.Equals(email.ExternalMessageId, message.ExternalId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(email.ExternalMessageId, message.InternetMessageId, StringComparison.OrdinalIgnoreCase)))
        {
            score += 100;
            reasons.Add("provider message id matches");
        }
        if (string.Equals(NormalizeSubject(email.Subject), NormalizeSubject(message.Subject), StringComparison.OrdinalIgnoreCase))
        {
            score += 40;
            reasons.Add("subject matches");
        }
        if (SplitAddresses(email.ToAddresses).Intersect(SplitAddresses(message.ToAddresses), StringComparer.OrdinalIgnoreCase).Any())
        {
            score += 30;
            reasons.Add("recipient matches");
        }
        if (!string.IsNullOrWhiteSpace(email.FromEmailAddress)
            && string.Equals(email.FromEmailAddress.Trim(), message.FromAddress?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
            reasons.Add("sender matches");
        }
        double minutes = Math.Abs(((email.SentOn ?? email.LastUpdated) - message.ReceivedOn).TotalMinutes);
        if (minutes <= 1)
        {
            score += 50;
            reasons.Add("sent time is within 1 minute");
        }
        else if (minutes <= 15)
        {
            score += 30;
            reasons.Add("sent time is within 15 minutes");
        }
        else if (minutes <= 24 * 60)
        {
            score += 15;
            reasons.Add("sent time is within 24 hours");
        }
        return new SentCandidate(message, score, reasons);
    }

    static IEnumerable<string> SplitAddresses(string addresses) =>
        string.IsNullOrWhiteSpace(addresses)
            ? []
            : addresses.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(address => address.ToLowerInvariant());

    static string NormalizeSubject(string subject)
    {
        string value = subject?.Trim() ?? string.Empty;
        while (Regex.IsMatch(value, "^(re|fw|fwd):\\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            value = Regex.Replace(value, "^(re|fw|fwd):\\s*", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return value.Trim();
    }

    sealed record SentCandidate(MailboxMessage Message, int Score, IReadOnlyList<string> Reasons);

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
