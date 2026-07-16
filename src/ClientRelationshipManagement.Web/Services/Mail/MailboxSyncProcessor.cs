using System.Security.Cryptography;
using System.Text;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using cCoder.ClientRelationshipManagement.Services.Foundations.Platform;
using ClientRelationshipManagement.Web.Brokers.Loggings;
using ClientRelationshipManagement.Web.Configuration;
using ClientRelationshipManagement.Web.Services.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClientRelationshipManagement.Web.Services.Mail;

public sealed class MailboxSyncProcessor(
    IMicrosoftGraphMailboxClient mailboxClient,
    IOperationsCoordinationService operations,
    ISalesCoordinationService sales,
    IAgentAutomationSettingsService automationSettingsService,
    IOptions<MailOptions> mailOptions,
    IOptions<AgentWorkflowOptions> agentWorkflowOptions,
    ILoggingBroker<MailboxSyncProcessor> loggingBroker)
    : IMailboxSyncProcessor
{
    public async ValueTask<int> SyncAsync(CancellationToken cancellationToken = default)
    {
        MailOptions options = mailOptions.Value;
        string executionUserId = agentWorkflowOptions.Value.ExecutionUserId?.Trim();

        if (!options.MailboxSyncEnabled
            || !string.Equals(options.Provider, "MicrosoftGraph", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(executionUserId))
        {
            return 0;
        }

        AgentAutomationSetting setting = await automationSettingsService.GetAsync(executionUserId, cancellationToken);
        DateTimeOffset syncStartedOn = DateTimeOffset.UtcNow;
        bool backfillComplete = setting?.MailboxEvidenceBackfillCompletedOn.HasValue == true;
        DateTimeOffset receivedSince = backfillComplete
            ? setting?.LastMailboxSyncOn?.AddMinutes(-5) ?? syncStartedOn.AddDays(-7)
            : syncStartedOn.AddDays(-90);
        IReadOnlyList<MailboxMessage> messages = await mailboxClient.ReceiveAsync(
            receivedSince,
            Math.Max(1, options.MailboxSyncBatchSize),
            cancellationToken);

        int importedCount = 0;
        foreach (MailboxMessage message in messages.OrderBy(item => item.ReceivedOn))
        {
            cancellationToken.ThrowIfCancellationRequested();
            importedCount += await ImportMessageAsync(message, executionUserId, cancellationToken);
        }

        await automationSettingsService.SetLastMailboxSyncOnAsync(executionUserId, syncStartedOn, cancellationToken);
        if (!backfillComplete)
        {
            await automationSettingsService.SetMailboxEvidenceBackfillCompletedOnAsync(
                executionUserId,
                syncStartedOn,
                cancellationToken);
        }
        loggingBroker.LogInformation(
            "Mailbox sync inspected {MessageCount} message(s) and imported {ImportedCount} CRM activity item(s).",
            messages.Count,
            importedCount);
        return importedCount;
    }

    async ValueTask<int> ImportMessageAsync(
        MailboxMessage message,
        string executionUserId,
        CancellationToken cancellationToken)
    {
        string legacyId = CreateLegacyId(message);
        string externalId = string.IsNullOrWhiteSpace(message.ExternalId) ? legacyId : message.ExternalId;
        if (await operations.RetrieveMailboxMessages().AnyAsync(
                item => item.ExternalId == externalId
                    || (!string.IsNullOrWhiteSpace(message.InternetMessageId)
                        && item.InternetMessageId == message.InternetMessageId),
                cancellationToken))
        {
            return 0;
        }

        var relationship = string.IsNullOrWhiteSpace(message.FromAddress)
            ? null
            : await sales.RetrieveRelationshipContacts()
                .AsNoTracking()
                .Where(item =>
                    !item.TenantCompanyRelationship.IsArchived
                    && item.CompanyContact.EmailAddress == message.FromAddress)
                .OrderByDescending(item => item.IsPrimary)
                .ThenByDescending(item => item.LastUpdated)
                .Select(item => new
                {
                    item.TenantCompanyRelationshipId,
                    item.CompanyContactId
                })
                .FirstOrDefaultAsync(cancellationToken);

        Guid? opportunityId = relationship is null
            ? null
            : await sales.RetrieveOpportunities()
                .Where(item => item.TenantCompanyRelationshipId == relationship.TenantCompanyRelationshipId)
                .OrderByDescending(item => item.LastUpdated)
                .Select(item => (Guid?)item.Id)
                .FirstOrDefaultAsync(cancellationToken);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        operations.Add(new MailboxMessageRecord
        {
            Id = Guid.NewGuid(),
            ExternalId = externalId,
            InternetMessageId = message.InternetMessageId,
            ConversationId = message.ConversationId,
            InReplyTo = message.InReplyTo,
            References = message.References,
            FromAddress = message.FromAddress,
            ToAddresses = message.ToAddresses,
            CcAddresses = message.CcAddresses,
            Subject = message.Subject,
            Body = message.Body,
            IsBodyHtml = message.IsBodyHtml,
            ReceivedOn = message.ReceivedOn,
            TenantCompanyRelationshipId = relationship?.TenantCompanyRelationshipId,
            OpportunityId = opportunityId,
            CompanyContactId = relationship?.CompanyContactId,
            CreatedBy = executionUserId,
            LastUpdatedBy = executionUserId,
            CreatedOn = now,
            LastUpdated = now
        });

        int importedActivityCount = 0;
        if (relationship is not null
            && !await sales.RetrieveActivities().AnyAsync(item => item.LegacyId == legacyId, cancellationToken))
        {
            Guid? clientAccountId = await sales.RetrieveClientAccounts()
                .Where(item => item.TenantCompanyRelationshipId == relationship.TenantCompanyRelationshipId)
                .OrderByDescending(item => item.LastUpdated)
                .Select(item => (Guid?)item.Id)
                .FirstOrDefaultAsync(cancellationToken);

            sales.Add(new Activity
            {
                Id = Guid.NewGuid(),
                LegacyId = legacyId,
                TenantCompanyRelationshipId = relationship.TenantCompanyRelationshipId,
                OpportunityId = opportunityId,
                ClientAccountId = clientAccountId,
                CompanyContactId = relationship.CompanyContactId,
                ActivityOn = message.ReceivedOn,
                Type = ActivityType.Email,
                Direction = ActivityDirection.Inbound,
                Summary = string.IsNullOrWhiteSpace(message.Subject) ? "Inbound email" : message.Subject.Trim(),
                Outcome = message.Body,
                CreatedBy = executionUserId,
                LastUpdatedBy = executionUserId,
                CreatedOn = now,
                LastUpdated = now
            });
            importedActivityCount = 1;
        }

        await operations.SaveAsync(cancellationToken);
        await sales.SaveAsync(cancellationToken);
        return importedActivityCount;
    }

    static string CreateLegacyId(MailboxMessage message)
    {
        string source = message.InternetMessageId ?? message.ExternalId
            ?? $"{message.FromAddress}|{message.ReceivedOn:O}|{message.Subject}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        return $"graph:{Convert.ToHexString(hash)}";
    }
}
