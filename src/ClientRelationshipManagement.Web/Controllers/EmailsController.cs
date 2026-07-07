using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using cCoder.Security.Objects;
using ClientRelationshipManagement.Web.Models.Emails;
using ClientRelationshipManagement.Web.Services.Mail;
using ClientRelationshipManagement.Web.Services.Processes;
using ClientRelationshipManagement.Web.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.Web.Controllers;

public sealed class EmailsController(
    IPlatformDbContextFactory dbContextFactory,
    IEmailDraftWorkflowService emailDraftWorkflowService,
    IWorkflowAutomationService workflowAutomationService,
    ICRMAuthInfo authInfo,
    ISSOAuthInfo ssoAuthInfo)
    : Controller
{
    public async Task<IActionResult> Index(string search = null, string state = null)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        using PlatformDbContext context = dbContextFactory.CreateDbContext();
        string[] readableTenantIds = GetReadableTenantIds();

        EmailState? parsedState = Enum.TryParse<EmailState>(state, true, out EmailState stateValue)
            ? stateValue
            : null;

        IQueryable<EmailRowProjection> query = context.Emails
            .AsNoTracking()
            .Include(email => email.TenantCompanyRelationship)
                .ThenInclude(relationship => relationship.Company)
            .Where(email => readableTenantIds.Contains(email.TenantCompanyRelationship.TenantId))
            .Select(email => new EmailRowProjection
            {
                Id = email.Id,
                ClientId = email.TenantCompanyRelationshipId,
                ClientMaterialId = email.MaterialId,
                ClientName = CompanyNames.ResolvePreferredName(email.TenantCompanyRelationship.Company),
                ToAddresses = email.ToAddresses ?? string.Empty,
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
                    CanMarkSent = item.State != EmailState.Sent
                })
            ]
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(ApproveEmailRequest request)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        if (request.ClientId == Guid.Empty || request.EmailId == Guid.Empty)
            return RedirectToAction(nameof(Index));

        var email = await emailDraftWorkflowService.ApproveAsync(
            request.ClientId,
            request.EmailId,
            ToDateTimeOffset(request.ScheduledSendOn));

        TempData["EmailsNotice"] = email is null
            ? "That email could not be approved."
            : "Email approved for sending.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
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
        public string Subject { get; init; }
        public string Preview { get; init; }
        public EmailState State { get; init; }
        public DateTimeOffset? ScheduledSendTimeUtc { get; init; }
        public DateTimeOffset? SentOn { get; init; }
        public DateTimeOffset CreatedOn { get; init; }
        public string LastError { get; init; }
    }
}
