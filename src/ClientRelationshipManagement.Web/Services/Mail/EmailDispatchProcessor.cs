using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using ClientRelationshipManagement.Web.Brokers.Loggings;
using ClientRelationshipManagement.Web.Configuration;
using ClientRelationshipManagement.Web.Services.Processes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PlatformEntities = cCoder.ClientRelationshipManagement.Platform.Models.Entities;

namespace ClientRelationshipManagement.Web.Services.Mail;

public sealed class EmailDispatchProcessor(
    IPlatformDbContextFactory dbContextFactory,
    IMailClientFactory mailClientFactory,
    ICurrentUserMailProfileProvider currentUserMailProfileProvider,
    IWorkflowAutomationService workflowAutomationService,
    IOptions<MailOptions> options,
    ILoggingBroker<EmailDispatchProcessor> loggingBroker)
    : IEmailDispatchProcessor
{
    public async ValueTask<int> DispatchDueEmailsAsync(CancellationToken cancellationToken = default)
    {
        MailOptions mailOptions = options.Value;
        if (!mailOptions.EmailSendingEnabled)
        {
            loggingBroker.LogInformation("Email dispatch skipped because email sending is disabled.");
            return 0;
        }

        bool isSendGrid = string.Equals(mailOptions.Provider, "SendGrid", StringComparison.OrdinalIgnoreCase);
        bool isMicrosoftGraph = string.Equals(mailOptions.Provider, "MicrosoftGraph", StringComparison.OrdinalIgnoreCase);
        bool isConfigured = isSendGrid
            ? !string.IsNullOrWhiteSpace(mailOptions.ApiKey)
            : isMicrosoftGraph
                ? !string.IsNullOrWhiteSpace(mailOptions.MicrosoftGraphTenantId)
                    && !string.IsNullOrWhiteSpace(mailOptions.MicrosoftGraphClientId)
                    && !string.IsNullOrWhiteSpace(mailOptions.MicrosoftGraphClientSecret)
                    && !string.IsNullOrWhiteSpace(mailOptions.MicrosoftGraphMailboxUser)
                : !string.IsNullOrWhiteSpace(mailOptions.Host);

        if (!isConfigured)
        {
            loggingBroker.LogWarning(
                "Email dispatch skipped because provider {Provider} is not fully configured.",
                mailOptions.Provider);
            return 0;
        }

        using PlatformDbContext dbContext = dbContextFactory.CreateDbContext(useAdminConnection: true);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<PlatformEntities.Email> dueEmails = await dbContext.Emails
            .Include(email => email.CompanyContact)
            .Include(email => email.Material)
                .ThenInclude(material => material.CompanyContact)
            .Where(email =>
                email.State == EmailState.Approved
                && email.SendFailureCount < Math.Max(1, mailOptions.RetryLimit)
                && (email.ScheduledSendTimeUtc == null || email.ScheduledSendTimeUtc <= now))
            .OrderBy(email => email.ScheduledSendTimeUtc ?? email.CreatedOn)
            .Take(Math.Max(1, mailOptions.BatchSize))
            .ToListAsync(cancellationToken);

        if (dueEmails.Count == 0)
        {
            loggingBroker.LogInformation("Email dispatch found no approved email(s) due for sending.");
            return 0;
        }

        loggingBroker.LogInformation(
            "Email dispatch found {DueEmailCount} approved email(s) due for sending.",
            dueEmails.Count);

        IMailClient mailClient = mailClientFactory.CreateClient();
        int dispatchedCount = 0;

        foreach (PlatformEntities.Email email in dueEmails)
        {
            cancellationToken.ThrowIfCancellationRequested();
            dispatchedCount += await DispatchSingleEmailAsync(dbContext, mailClient, email, now, mailOptions, cancellationToken);
        }

        return dispatchedCount;
    }

    async Task<int> DispatchSingleEmailAsync(
        PlatformDbContext dbContext,
        IMailClient mailClient,
        PlatformEntities.Email email,
        DateTimeOffset now,
        MailOptions mailOptions,
        CancellationToken cancellationToken)
    {
        MailSenderProfile senderProfile = await ResolveSenderProfileAsync(email, cancellationToken);
        string toAddresses = ResolveToAddresses(email);

        if (string.IsNullOrWhiteSpace(senderProfile?.EmailAddress))
        {
            await MarkFailedAsync(dbContext, email, now, "No sender email address is available for this draft.", cancellationToken);
            return 0;
        }

        if (string.IsNullOrWhiteSpace(toAddresses))
        {
            await MarkFailedAsync(dbContext, email, now, "No recipient email address is available for this draft.", cancellationToken);
            return 0;
        }

        email.State = EmailState.Sending;
        email.LastSendAttemptOn = now;
        email.LastUpdated = now;
        email.LastUpdatedBy = email.LastUpdatedBy ?? email.CreatedBy;
        email.FromDisplayName ??= senderProfile.DisplayName;
        email.FromEmailAddress ??= senderProfile.EmailAddress ?? mailOptions.FallbackFromAddress;
        email.ToAddresses = toAddresses;
        await dbContext.SaveChangesAsync(cancellationToken);

        MailSendResult result = await mailClient.SendAsync(
            new MailSendRequest
            {
                FromDisplayName = email.FromDisplayName,
                FromEmailAddress = email.FromEmailAddress,
                ReplyToAddresses = string.IsNullOrWhiteSpace(email.ReplyToAddresses)
                    ? email.FromEmailAddress
                    : email.ReplyToAddresses,
                ToAddresses = ResolveSafeToAddresses(mailOptions, email.ToAddresses),
                CcAddresses = ResolveSafeSecondaryAddresses(mailOptions, email.CcAddresses),
                BccAddresses = ResolveSafeSecondaryAddresses(mailOptions, email.BccAddresses),
                Subject = email.Subject,
                BodyHtml = email.BodyHtml,
                BodyText = email.BodyText,
                IsBodyHtml = email.IsBodyHtml,
            },
            cancellationToken);

        if (result.Success)
        {
            email.State = EmailState.Sent;
            email.SentOn = DateTimeOffset.UtcNow;
            email.LastError = null;
            email.ExternalMessageId = result.ExternalMessageId;
            email.LastUpdated = DateTimeOffset.UtcNow;

            if (email.Material is not null)
            {
                email.Material.Status = MaterialStatus.Sent;
                email.Material.SentOn = email.SentOn;
                email.Material.LastUpdated = email.LastUpdated;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            bool workflowAdvanced = await workflowAutomationService.CompleteEmailTaskAsync(email.Id, cancellationToken);
            loggingBroker.LogInformation(
                "Sent approved email for {RelationshipName} with subject {Subject}. Workflow advanced: {WorkflowAdvanced}.",
                ResolveRelationshipName(email),
                email.Subject,
                workflowAdvanced);
            return 1;
        }

        await MarkFailedAsync(dbContext, email, DateTimeOffset.UtcNow, result.ErrorMessage, cancellationToken);
        return 0;
    }

    async Task<MailSenderProfile> ResolveSenderProfileAsync(PlatformEntities.Email email, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(email.FromEmailAddress) && !string.IsNullOrWhiteSpace(email.FromDisplayName))
        {
            return new MailSenderProfile
            {
                UserId = email.SenderUserId,
                DisplayName = email.FromDisplayName,
                EmailAddress = email.FromEmailAddress,
            };
        }

        string userId = !string.IsNullOrWhiteSpace(email.SenderUserId)
            ? email.SenderUserId
            : email.CreatedBy;

        return await currentUserMailProfileProvider.GetByUserIdAsync(userId, cancellationToken);
    }

    static string ResolveToAddresses(PlatformEntities.Email email) =>
        !string.IsNullOrWhiteSpace(email.ToAddresses)
            ? email.ToAddresses
            : email.CompanyContact?.EmailAddress
                ?? email.Material?.CompanyContact?.EmailAddress;

    static string ResolveSafeToAddresses(MailOptions mailOptions, string addresses) =>
        string.IsNullOrWhiteSpace(mailOptions.SafeRecipientOverrideAddress)
            ? addresses
            : mailOptions.SafeRecipientOverrideAddress.Trim();

    static string ResolveSafeSecondaryAddresses(MailOptions mailOptions, string addresses) =>
        string.IsNullOrWhiteSpace(mailOptions.SafeRecipientOverrideAddress)
            ? addresses
            : string.Empty;

    async Task MarkFailedAsync(
        PlatformDbContext dbContext,
        PlatformEntities.Email email,
        DateTimeOffset failedOn,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        email.State = EmailState.Failed;
        email.LastSendAttemptOn = failedOn;
        email.LastError = errorMessage;
        email.SendFailureCount += 1;
        email.LastUpdated = failedOn;

        await dbContext.SaveChangesAsync(cancellationToken);

        loggingBroker.LogWarning(
            "Email {EmailId} failed dispatch: {ErrorMessage}",
            email.Id,
            errorMessage);
    }

    static string ResolveRelationshipName(PlatformEntities.Email email) =>
        email.CompanyContact?.Name
        ?? email.Material?.CompanyContact?.Name
        ?? email.ToAddresses
        ?? email.TenantCompanyRelationshipId.ToString();
}
