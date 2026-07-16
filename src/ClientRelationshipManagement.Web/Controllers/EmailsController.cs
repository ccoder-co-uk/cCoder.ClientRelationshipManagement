using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Services.Foundations.Platform;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using cCoder.Security.Objects;
using ClientRelationshipManagement.Web.Models.Emails;
using ClientRelationshipManagement.Web.Services.Agents;
using ClientRelationshipManagement.Web.Services.Mail;
using ClientRelationshipManagement.Web.Services.Processes;
using ClientRelationshipManagement.Web.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebAgentMessageService = ClientRelationshipManagement.Web.Services.Agents.IAgentMessageService;

namespace ClientRelationshipManagement.Web.Controllers;

[Route("Admin/Emails")]
public sealed class EmailsController(
    IOperationsCoordinationService operationsService,
    IProcessCoordinationService processService,
    ISalesCoordinationService salesWorkspaceService,
    IEmailDraftWorkflowService emailDraftWorkflowService,
    WebAgentMessageService agentMessageService,
    IWorkflowAutomationService workflowAutomationService,
    ICRMAuthInfo authInfo,
    ISSOAuthInfo ssoAuthInfo)
    : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string search = null, string state = null, Guid? id = null)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        string[] readableTenantIds = GetReadableTenantIds();

        EmailState? parsedState = Enum.TryParse<EmailState>(state, true, out EmailState stateValue)
            ? stateValue
            : null;

        IQueryable<cCoder.ClientRelationshipManagement.Platform.Models.Entities.ProcessTask> processTasks =
            processService.RetrieveTasks();
        IQueryable<EmailRowProjection> query = operationsService.RetrieveAllEmails()
            .AsNoTracking()
            .Include(email => email.TenantCompanyRelationship)
                .ThenInclude(relationship => relationship.Company)
            .Where(email => readableTenantIds.Contains(email.TenantCompanyRelationship.TenantId))
            .Where(email =>
                (email.State != EmailState.Draft
                    && email.State != EmailState.Approved
                    && email.State != EmailState.Sending
                    && email.State != EmailState.Failed)
                || !processTasks.Any(task => task.EmailId == email.Id)
                || processTasks.Any(task => task.EmailId == email.Id && task.State == ProcessTaskState.Pending))
            .Select(email => new EmailRowProjection
            {
                Id = email.Id,
                ClientId = email.TenantCompanyRelationshipId,
                ClientMaterialId = email.MaterialId,
                ClientName = CompanyNames.ResolvePreferredName(email.TenantCompanyRelationship.Company),
                ToAddresses = email.ToAddresses ?? string.Empty,
                FromDisplayName = email.FromDisplayName ?? string.Empty,
                FromEmailAddress = email.FromEmailAddress ?? string.Empty,
                Subject = email.Subject,
                Preview = !string.IsNullOrWhiteSpace(email.BodyText)
                    ? email.BodyText
                    : email.BodyHtml ?? string.Empty,
                State = email.State,
                ScheduledSendTimeUtc = email.ScheduledSendTimeUtc,
                SentOn = email.SentOn,
                CreatedOn = email.CreatedOn,
                LastError = email.LastError ?? string.Empty,
            });

        if (parsedState.HasValue)
            query = query.Where(item => item.State == parsedState.Value);

        if (id.HasValue)
            query = query.Where(item => item.Id == id.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            string trimmedSearch = search.Trim();
            query = query.Where(item =>
                item.ClientName.Contains(trimmedSearch)
                || item.Subject.Contains(trimmedSearch)
                || item.ToAddresses.Contains(trimmedSearch));
        }

        List<EmailRowProjection> rows = await query
            .OrderBy(item => item.State == EmailState.Sent)
            .ThenBy(item => item.ScheduledSendTimeUtc == null)
            .ThenBy(item => item.ScheduledSendTimeUtc)
            .ThenByDescending(item => item.CreatedOn)
            .ToListAsync();

        return View(new EmailsPageViewModel
        {
            Notice = TempData["EmailsNotice"]?.ToString() ?? string.Empty,
            Search = search ?? string.Empty,
            StateFilter = parsedState?.ToString() ?? string.Empty,
            StateOptions = BuildStateOptions(parsedState?.ToString()),
            TotalEmails = rows.Count,
            DraftEmails = rows.Count(item => item.State is EmailState.Draft or EmailState.Failed),
            ApprovedEmails = rows.Count(item => item.State is EmailState.Approved or EmailState.Sending),
            SentEmails = rows.Count(item => item.State == EmailState.Sent),
            Emails =
            [
                .. rows.Select(item => new EmailListItemViewModel
                {
                    Id = item.Id,
                    ClientId = item.ClientId,
                    ClientMaterialId = item.ClientMaterialId,
                    ClientName = item.ClientName,
                    StateLabel = DisplayText.Humanize(item.State),
                    ToAddresses = item.ToAddresses,
                    FromDisplayName = item.FromDisplayName,
                    FromEmailAddress = item.FromEmailAddress,
                    Subject = item.Subject,
                    Preview = item.Preview,
                    ScheduledSendLabel = item.ScheduledSendTimeUtc == null
                        ? "Not scheduled"
                        : item.ScheduledSendTimeUtc.Value.LocalDateTime.ToString("dd MMM yyyy HH:mm"),
                    ScheduledSendValue = item.ScheduledSendTimeUtc == null
                        ? string.Empty
                        : item.ScheduledSendTimeUtc.Value.LocalDateTime.ToString("yyyy-MM-ddTHH:mm"),
                    SentOnLabel = item.SentOn == null
                        ? string.Empty
                        : item.SentOn.Value.LocalDateTime.ToString("dd MMM yyyy HH:mm"),
                    CreatedOnLabel = item.CreatedOn.LocalDateTime.ToString("dd MMM yyyy HH:mm"),
                    LastError = item.LastError,
                    CanApprove = item.State is EmailState.Draft or EmailState.Failed,
                    CanReject = item.State is EmailState.Draft or EmailState.Failed,
                    CanMarkSent = item.State != EmailState.Sent
                })
            ]
        });
    }

    [HttpPost("ReviewAndApprove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewAndApprove(ReviewEmailRequest request, CancellationToken cancellationToken)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        if (!ModelState.IsValid || request.ClientId == Guid.Empty || request.EmailId == Guid.Empty)
        {
            TempData["EmailsNotice"] = "Add both a subject and email content before approving.";
            return RedirectToAction(nameof(Index));
        }

        cCoder.ClientRelationshipManagement.Platform.Models.Entities.Email current =
            await operationsService.RetrieveAllEmails().AsNoTracking().FirstOrDefaultAsync(item =>
                item.Id == request.EmailId
                && item.TenantCompanyRelationshipId == request.ClientId, cancellationToken);

        if (current is null)
            return NotFound();

        if (!await IsEmailActionableAsync(request.EmailId, cancellationToken))
        {
            TempData["EmailsNotice"] = "That email belongs to completed or cancelled workflow work and cannot be approved.";
            return RedirectToAction(nameof(Index));
        }

        var saved = await emailDraftWorkflowService.SaveDraftAsync(new EmailDraftUpsertCommand
        {
            ClientId = request.ClientId,
            EmailId = request.EmailId,
            ClientMaterialId = current.MaterialId,
            ClientOpportunityId = current.OpportunityId,
            ClientAccountId = current.ClientAccountId,
            Subject = request.Subject,
            Body = request.Body,
            ToAddresses = current.ToAddresses,
            CcAddresses = current.CcAddresses,
            BccAddresses = current.BccAddresses,
            ScheduledSendTimeUtc = ToDateTimeOffset(request.ScheduledSendOn)
        }, cancellationToken);

        var approved = saved is null
            ? null
            : await emailDraftWorkflowService.ApproveAsync(request.ClientId, request.EmailId, ToDateTimeOffset(request.ScheduledSendOn), cancellationToken);
        TempData["EmailsNotice"] = approved is null
            ? "That email could not be saved and approved."
            : "Your reviewed changes were saved and the email was approved for sending.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(RejectEmailRequest request, CancellationToken cancellationToken)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        if (!ModelState.IsValid || request.ClientId == Guid.Empty || request.EmailId == Guid.Empty)
        {
            TempData["EmailsNotice"] = "A clear rejection reason is required so the approval agent can investigate.";
            return RedirectToAction(nameof(Index));
        }

        var email = await emailDraftWorkflowService.RejectAsync(
            request.ClientId, request.EmailId, request.Reason, cancellationToken);
        if (email is null)
            return NotFound();

        var task = await salesWorkspaceService.RetrieveProcessTasks()
            .AsNoTracking()
            .Include(item => item.ProcessStep)
            .Include(item => item.ProcessInstance).ThenInclude(item => item.ProcessDefinition)
            .FirstOrDefaultAsync(item => item.EmailId == email.Id, cancellationToken);
        string companyName = CompanyNames.ResolvePreferredName(email.TenantCompanyRelationship.Company);
        string source = task is null
            ? "No process-task provenance was found."
            : $"Process: {task.ProcessInstance.ProcessDefinition.Name}; step: {task.ProcessStep.Name} ({task.ProcessStep.Key}); task: {task.RenderedTitle}.";
        var conversation = await agentMessageService.UpsertAsync(new cCoder.ClientRelationshipManagement.Platform.Models.Entities.AgentMessage
        {
            Id = Guid.NewGuid(), TenantId = email.TenantCompanyRelationship.TenantId,
            TenantCompanyRelationshipId = email.TenantCompanyRelationshipId, OpportunityId = email.OpportunityId,
            ClientAccountId = email.ClientAccountId, ProcessTaskId = task?.Id, ProcessStepId = task?.ProcessStepId,
            EmailId = email.Id, ProcessDefinitionId = task?.ProcessInstance.ProcessDefinitionId,
            Kind = AgentMessageKind.FeedbackRequest, State = AgentMessageState.Pending,
            CorrelationKey = $"email-rejection:{email.Id}", Title = $"Review rejected email to {companyName}",
            Body = $"A human rejected this email and the source process may need refinement. {source}",
            AgentName = "Approval Agent", CreatedBy = CurrentUserId, LastUpdatedBy = CurrentUserId
        }, cancellationToken);
        await agentMessageService.AppendEntryAsync(conversation.Id, "System", $"Rejected email evidence\nFrom: {email.FromDisplayName} <{email.FromEmailAddress}>\nTo: {email.ToAddresses}\nSubject: {email.Subject}\n\n{email.BodyText ?? email.BodyHtml}\n\n{source}", CurrentUserId, cancellationToken);
        await agentMessageService.AppendEntryAsync(conversation.Id, "User", request.Reason, CurrentUserId, cancellationToken);

        TempData["EmailsNotice"] = "Email rejected. An Approval Agent conversation has been opened with the reason and source details attached.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(ApproveEmailRequest request, CancellationToken cancellationToken)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        if (request.ClientId == Guid.Empty || request.EmailId == Guid.Empty)
            return RedirectToAction(nameof(Index));

        if (!await IsEmailActionableAsync(request.EmailId, cancellationToken))
        {
            TempData["EmailsNotice"] = "That email belongs to completed or cancelled workflow work and cannot be approved.";
            return RedirectToAction(nameof(Index));
        }

        var email = await emailDraftWorkflowService.ApproveAsync(
            request.ClientId,
            request.EmailId,
            ToDateTimeOffset(request.ScheduledSendOn));

        TempData["EmailsNotice"] = email is null
            ? "That email could not be approved."
            : "Email approved for sending.";

        return RedirectToAction(nameof(Index));
    }

    async ValueTask<bool> IsEmailActionableAsync(Guid emailId, CancellationToken cancellationToken)
    {
        bool hasWorkflowTask = await processService.RetrieveTasks()
            .AnyAsync(task => task.EmailId == emailId, cancellationToken);
        return !hasWorkflowTask || await processService.RetrieveTasks()
            .AnyAsync(task => task.EmailId == emailId && task.State == ProcessTaskState.Pending, cancellationToken);
    }

    [HttpPost("MarkSent")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkSent(MarkEmailSentRequest request)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        if (request.ClientId == Guid.Empty || request.EmailId == Guid.Empty)
            return RedirectToAction(nameof(Index));

        var email = await emailDraftWorkflowService.MarkSentAsync(request.ClientId, request.EmailId);
        if (email is not null)
            await workflowAutomationService.CompleteEmailTaskAsync(request.EmailId);

        TempData["EmailsNotice"] = email is null
            ? "That email could not be marked as sent."
            : "Email marked as sent.";

        return RedirectToAction(nameof(Index));
    }

    IActionResult RedirectIfUnauthenticated()
    {
        if (!string.IsNullOrWhiteSpace(ssoAuthInfo?.SSOUserId)
            && !string.Equals(ssoAuthInfo.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            return null;

        string returnUrl = $"{Request.Path}{Request.QueryString}";
        return RedirectToAction("Login", "Account", new { returnUrl });
    }

    string[] GetReadableTenantIds()
    {
        if (authInfo.WriteableTenants.Length > 0)
            return authInfo.WriteableTenants;

        if (authInfo.ReadableTenants.Length > 0)
            return authInfo.ReadableTenants;

        return ["default"];
    }

    string CurrentUserId => string.IsNullOrWhiteSpace(ssoAuthInfo?.SSOUserId) ? "system" : ssoAuthInfo.SSOUserId;

    static IReadOnlyList<SelectListItem> BuildStateOptions(string selectedValue) =>
    [
        new("All states", string.Empty, string.IsNullOrWhiteSpace(selectedValue)),
        .. Enum.GetValues<EmailState>()
            .Select(stateItem => new SelectListItem(
                DisplayText.Humanize(stateItem),
                stateItem.ToString(),
                string.Equals(selectedValue, stateItem.ToString(), StringComparison.OrdinalIgnoreCase)))
    ];

    static DateTimeOffset? ToDateTimeOffset(DateTime? value)
    {
        if (value is null)
            return null;

        DateTime localTime = DateTime.SpecifyKind(value.Value, DateTimeKind.Local);
        return new DateTimeOffset(localTime).ToUniversalTime();
    }

    sealed class EmailRowProjection
    {
        public Guid Id { get; init; }
        public Guid ClientId { get; init; }
        public Guid? ClientMaterialId { get; init; }
        public string ClientName { get; init; }
        public string ToAddresses { get; init; }
        public string FromDisplayName { get; init; }
        public string FromEmailAddress { get; init; }
        public string Subject { get; init; }
        public string Preview { get; init; }
        public EmailState State { get; init; }
        public DateTimeOffset? ScheduledSendTimeUtc { get; init; }
        public DateTimeOffset? SentOn { get; init; }
        public DateTimeOffset CreatedOn { get; init; }
        public string LastError { get; init; }
    }
}
