using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using cCoder.Security.Objects;
using ClientRelationshipManagement.Web.Brokers.Loggings;
using ClientRelationshipManagement.Web.Services.Execution;
using ClientRelationshipManagement.Web.Utilities;
using Microsoft.EntityFrameworkCore;
using PlatformEntities = cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using ClientRelationshipManagement.Web.Services.Agents;
using ClientRelationshipManagement.Web.Brokers.Storages;

namespace ClientRelationshipManagement.Web.Services.Mail;

public sealed class EmailDraftWorkflowService(
    IEmailWorkflowBroker storage,
    ICurrentUserMailProfileProvider currentUserMailProfileProvider,
    ISSOAuthInfo authInfo,
    ICurrentExecutionUserAccessor currentExecutionUserAccessor,
    ILoggingBroker<EmailDraftWorkflowService> loggingBroker)
    : IEmailDraftWorkflowService
{
    public async ValueTask<PlatformEntities.Email> RejectAsync(
        Guid clientId, Guid emailId, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) return null;
        PlatformEntities.Email email = await storage.Emails
            .Include(item => item.Material)
            .Include(item => item.TenantCompanyRelationship).ThenInclude(item => item.Company)
            .FirstOrDefaultAsync(item => item.Id == emailId && item.TenantCompanyRelationshipId == clientId, cancellationToken);
        if (email is null) return null;

        DateTimeOffset now = DateTimeOffset.UtcNow;
        email.State = EmailState.Rejected;
        email.LastError = $"Rejected by {CurrentUserId}: {reason.Trim()}";
        email.LastUpdatedBy = CurrentUserId;
        email.LastUpdated = now;
        if (email.Material is not null)
        {
            email.Material.Status = MaterialStatus.Archived;
            email.Material.LastUpdatedBy = CurrentUserId;
            email.Material.LastUpdated = now;
        }
        await storage.SaveAsync(cancellationToken);
        return email;
    }
    public async ValueTask<PlatformEntities.Email> SaveDraftAsync(
        EmailDraftUpsertCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        string subject = command.Subject?.Trim() ?? string.Empty;
        string body = command.Body?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(subject)
            || string.IsNullOrWhiteSpace(body)
            || RecipientEmailContentValidator.ContainsInternalDraftingGuidance(body))
            return null;

        PlatformEntities.TenantCompanyRelationship relationship = await storage.Relationships
            .FirstOrDefaultAsync(item => item.Id == command.ClientId, cancellationToken);

        if (relationship is null)
            return null;

        DateTimeOffset now = DateTimeOffset.UtcNow;
        string currentUser = CurrentUserId;
        MailSenderProfile senderProfile = await currentUserMailProfileProvider.GetCurrentAsync(cancellationToken);

        PlatformEntities.Material material = await GetOrCreateMaterialAsync(
            storage,
            relationship,
            command,
            subject,
            body,
            now,
            currentUser,
            cancellationToken);

        PlatformEntities.Email email = await ResolveExistingEmailAsync(storage, command, material?.Id, cancellationToken);
        PlatformEntities.CompanyContact contact = await ResolvePreferredContactAsync(storage, relationship.Id, email?.CompanyContactId, material?.CompanyContactId, cancellationToken);
        Guid? resolvedClientAccountId = command.ClientAccountId ?? material?.ClientAccountId ?? email?.ClientAccountId;

        if (email is null)
        {
            email = new PlatformEntities.Email
            {
                Id = Guid.NewGuid(),
                TenantCompanyRelationshipId = relationship.Id,
                OpportunityId = command.ClientOpportunityId,
                ClientAccountId = resolvedClientAccountId,
                MaterialId = material?.Id,
                CompanyContactId = contact?.Id,
                SenderUserId = senderProfile?.UserId ?? currentUser,
                FromDisplayName = senderProfile?.DisplayName ?? relationship.AccountOwnerDisplayName ?? currentUser,
                FromEmailAddress = senderProfile?.EmailAddress,
                ReplyToAddresses = senderProfile?.EmailAddress,
                ToAddresses = ResolveRecipientAddresses(command.ToAddresses, contact, relationship),
                CcAddresses = Normalize(command.CcAddresses),
                BccAddresses = Normalize(command.BccAddresses),
                Subject = subject,
                BodyHtml = body,
                BodyText = body,
                IsBodyHtml = false,
                State = EmailState.Draft,
                ScheduledSendTimeUtc = command.ScheduledSendTimeUtc,
                CreatedBy = currentUser,
                LastUpdatedBy = currentUser,
                CreatedOn = now,
                LastUpdated = now
            };

            storage.Add(email);
        }
        else
        {
            email.TenantCompanyRelationshipId = relationship.Id;
            email.OpportunityId = command.ClientOpportunityId ?? email.OpportunityId;
            email.ClientAccountId = resolvedClientAccountId ?? email.ClientAccountId;
            email.MaterialId = material?.Id;
            email.CompanyContactId = contact?.Id ?? email.CompanyContactId;
            email.SenderUserId = senderProfile?.UserId ?? email.SenderUserId ?? currentUser;
            email.FromDisplayName = senderProfile?.DisplayName ?? email.FromDisplayName ?? relationship.AccountOwnerDisplayName ?? currentUser;
            email.FromEmailAddress = senderProfile?.EmailAddress ?? email.FromEmailAddress;
            email.ReplyToAddresses = Normalize(email.ReplyToAddresses) ?? senderProfile?.EmailAddress;
            email.ToAddresses = ResolveRecipientAddresses(command.ToAddresses, contact, relationship, email.ToAddresses);
            email.CcAddresses = Normalize(command.CcAddresses);
            email.BccAddresses = Normalize(command.BccAddresses);
            email.Subject = subject;
            email.BodyHtml = body;
            email.BodyText = body;
            email.IsBodyHtml = false;
            email.State = EmailState.Draft;
            email.ApprovedBy = null;
            email.ApprovedOn = null;
            email.LastError = null;
            email.ScheduledSendTimeUtc = command.ScheduledSendTimeUtc;
            email.LastUpdatedBy = currentUser;
            email.LastUpdated = now;
        }

        if (material is not null)
        {
            material.CompanyContactId = contact?.Id ?? material.CompanyContactId;
            material.OpportunityId = command.ClientOpportunityId ?? material.OpportunityId;
            material.ClientAccountId = resolvedClientAccountId ?? material.ClientAccountId;
        }

        await UpsertRecipientsAsync(storage, email, contact, cancellationToken);
        await UpsertLinkedActivityAsync(
            storage,
            relationship.Id,
            command,
            material?.Id,
            contact?.Id,
            resolvedClientAccountId,
            subject,
            body,
            now,
            currentUser,
            cancellationToken);

        await storage.SaveAsync(cancellationToken);
        loggingBroker.LogInformation(
            "Saved draft email for {RelationshipName} with subject {Subject}.",
            ResolveRelationshipName(relationship),
            email.Subject);
        return email;
    }

    public async ValueTask<PlatformEntities.Email> ApproveAsync(
        Guid clientId,
        Guid emailId,
        DateTimeOffset? scheduledSendTimeUtc,
        CancellationToken cancellationToken = default)
    {
        PlatformEntities.Email email = await storage.Emails
            .Include(item => item.Material)
            .Include(item => item.TenantCompanyRelationship)
            .Include(item => item.CompanyContact)
            .FirstOrDefaultAsync(item => item.Id == emailId && item.TenantCompanyRelationshipId == clientId, cancellationToken);

        if (email is null)
            return null;

        string recipientBody = FirstNonEmpty(email.BodyText, email.BodyHtml);
        if (RecipientEmailContentValidator.ContainsInternalDraftingGuidance(recipientBody))
        {
            loggingBroker.LogWarning(
                "Refused to approve email {EmailId} because its recipient content contains internal drafting guidance.",
                email.Id);
            return null;
        }

        string recipientAddresses = ResolveRecipientAddresses(
            email.ToAddresses,
            email.CompanyContact,
            email.TenantCompanyRelationship,
            email.ToAddresses);
        if (string.IsNullOrWhiteSpace(recipientAddresses))
        {
            loggingBroker.LogWarning(
                "Refused to approve email {EmailId} because it has no recipient.",
                email.Id);
            return null;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        string currentUser = CurrentUserId;
        MailSenderProfile senderProfile = await currentUserMailProfileProvider.GetCurrentAsync(cancellationToken);

        email.FromDisplayName = senderProfile?.DisplayName
            ?? email.FromDisplayName
            ?? email.TenantCompanyRelationship?.AccountOwnerDisplayName
            ?? currentUser;
        email.FromEmailAddress = senderProfile?.EmailAddress ?? email.FromEmailAddress;
        email.ReplyToAddresses = Normalize(email.ReplyToAddresses) ?? email.FromEmailAddress;
        email.ToAddresses = recipientAddresses;
        email.State = EmailState.Approved;
        email.ApprovedOn = now;
        email.ApprovedBy = currentUser;
        email.ScheduledSendTimeUtc = scheduledSendTimeUtc ?? email.ScheduledSendTimeUtc ?? now;
        email.LastUpdatedBy = currentUser;
        email.LastUpdated = now;
        email.LastError = null;

        if (email.Material is not null)
        {
            email.Material.Name = email.Subject;
            email.Material.Notes = FirstNonEmpty(email.BodyText, email.BodyHtml);
            email.Material.Type = MaterialType.Email;
            email.Material.Status = MaterialStatus.Approved;
            email.Material.LastUpdatedBy = currentUser;
            email.Material.LastUpdated = now;
        }

        await UpsertRecipientsAsync(storage, email, email.CompanyContact, cancellationToken);
        await storage.SaveAsync(cancellationToken);
        loggingBroker.LogInformation(
            "Approved draft email for {RelationshipName} with subject {Subject}. Scheduled for {ScheduledSendTimeUtc}.",
            ResolveRelationshipName(email.TenantCompanyRelationship),
            email.Subject,
            email.ScheduledSendTimeUtc);
        return email;
    }

    public async ValueTask<PlatformEntities.Email> MarkSentAsync(
        Guid clientId,
        Guid emailId,
        CancellationToken cancellationToken = default)
    {
        PlatformEntities.Email email = await storage.Emails
            .Include(item => item.Material)
            .FirstOrDefaultAsync(item => item.Id == emailId && item.TenantCompanyRelationshipId == clientId, cancellationToken);

        if (email is null)
            return null;

        if (string.IsNullOrWhiteSpace(email.ToAddresses))
        {
            loggingBroker.LogWarning(
                "Refused to mark email {EmailId} as sent because it has no recipient.",
                email.Id);
            return null;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        string currentUser = CurrentUserId;

        email.State = EmailState.Sent;
        email.SentOn = now;
        email.LastSendAttemptOn = now;
        email.LastError = null;
        email.LastUpdatedBy = currentUser;
        email.LastUpdated = now;

        if (email.Material is not null)
        {
            email.Material.Status = MaterialStatus.Sent;
            email.Material.SentOn = now;
            email.Material.LastUpdatedBy = currentUser;
            email.Material.LastUpdated = now;
        }

        await storage.SaveAsync(cancellationToken);
        loggingBroker.LogInformation(
            "Marked email as sent for {ClientId} with subject {Subject}.",
            clientId,
            email.Subject);
        return email;
    }

    async ValueTask<PlatformEntities.Email> ResolveExistingEmailAsync(
        IEmailWorkflowBroker storage,
        EmailDraftUpsertCommand command,
        Guid? materialId,
        CancellationToken cancellationToken)
    {
        if (command.EmailId.HasValue && command.EmailId.Value != Guid.Empty)
            return await storage.Emails.FirstOrDefaultAsync(item => item.Id == command.EmailId.Value, cancellationToken);

        Guid resolvedMaterialId = command.ClientMaterialId ?? materialId ?? Guid.Empty;
        if (resolvedMaterialId == Guid.Empty)
            return null;

        return await storage.Emails.FirstOrDefaultAsync(item => item.MaterialId == resolvedMaterialId, cancellationToken);
    }

    async ValueTask<PlatformEntities.Material> GetOrCreateMaterialAsync(
        IEmailWorkflowBroker storage,
        PlatformEntities.TenantCompanyRelationship relationship,
        EmailDraftUpsertCommand command,
        string subject,
        string body,
        DateTimeOffset now,
        string currentUser,
        CancellationToken cancellationToken)
    {
        PlatformEntities.Material material = null;

        if (command.ClientMaterialId.HasValue && command.ClientMaterialId.Value != Guid.Empty)
        {
            material = await storage.Materials.FirstOrDefaultAsync(item => item.Id == command.ClientMaterialId.Value, cancellationToken);
        }

        if (material is null)
        {
            material = new PlatformEntities.Material
            {
                Id = command.ClientMaterialId.GetValueOrDefault(Guid.NewGuid()),
                TenantCompanyRelationshipId = relationship.Id,
                OpportunityId = command.ClientOpportunityId,
                ClientAccountId = command.ClientAccountId,
                Name = subject,
                Type = MaterialType.Email,
                Status = MaterialStatus.Draft,
                Notes = body,
                CreatedBy = currentUser,
                LastUpdatedBy = currentUser,
                CreatedOn = now,
                LastUpdated = now
            };

            storage.Add(material);
            return material;
        }

        material.TenantCompanyRelationshipId = relationship.Id;
        material.OpportunityId = command.ClientOpportunityId ?? material.OpportunityId;
        material.ClientAccountId = command.ClientAccountId ?? material.ClientAccountId;
        material.Name = subject;
        material.Type = MaterialType.Email;
        material.Status = MaterialStatus.Draft;
        material.Notes = body;
        material.LastUpdatedBy = currentUser;
        material.LastUpdated = now;
        return material;
    }

    async ValueTask<PlatformEntities.CompanyContact> ResolvePreferredContactAsync(
        IEmailWorkflowBroker storage,
        Guid relationshipId,
        Guid? emailContactId,
        Guid? materialContactId,
        CancellationToken cancellationToken)
    {
        Guid? preferredId = emailContactId ?? materialContactId;
        if (preferredId.HasValue)
            return await storage.CompanyContacts.FirstOrDefaultAsync(item => item.Id == preferredId.Value, cancellationToken);

        Guid? relationshipContactId = await storage.RelationshipContacts
            .Where(item => item.TenantCompanyRelationshipId == relationshipId)
            .OrderByDescending(item => item.IsPrimary)
            .Select(item => (Guid?)item.CompanyContactId)
            .FirstOrDefaultAsync(cancellationToken);

        if (relationshipContactId.HasValue)
        return await storage.CompanyContacts.FirstOrDefaultAsync(item => item.Id == relationshipContactId.Value, cancellationToken);

        return null;
    }

    async ValueTask UpsertRecipientsAsync(
        IEmailWorkflowBroker storage,
        PlatformEntities.Email email,
        PlatformEntities.CompanyContact contact,
        CancellationToken cancellationToken)
    {
        List<PlatformEntities.EmailRecipient> existingRecipients = await storage.EmailRecipients
            .Where(item => item.EmailId == email.Id)
            .ToListAsync(cancellationToken);

        if (existingRecipients.Count > 0)
        storage.RemoveEmailRecipients(existingRecipients);

        foreach (string address in SplitAddresses(email.ToAddresses))
        {
            storage.Add(new PlatformEntities.EmailRecipient
            {
                Id = Guid.NewGuid(),
                EmailId = email.Id,
                CompanyContactId = contact is not null && string.Equals(contact.EmailAddress, address, StringComparison.OrdinalIgnoreCase)
                    ? contact.Id
                    : null,
                Address = address,
                RecipientType = EmailRecipientType.To,
                CreatedBy = email.LastUpdatedBy,
                LastUpdatedBy = email.LastUpdatedBy,
                CreatedOn = email.LastUpdated,
                LastUpdated = email.LastUpdated
            });
        }

        foreach (string address in SplitAddresses(email.CcAddresses))
        {
            storage.Add(new PlatformEntities.EmailRecipient
            {
                Id = Guid.NewGuid(),
                EmailId = email.Id,
                Address = address,
                RecipientType = EmailRecipientType.Cc,
                CreatedBy = email.LastUpdatedBy,
                LastUpdatedBy = email.LastUpdatedBy,
                CreatedOn = email.LastUpdated,
                LastUpdated = email.LastUpdated
            });
        }

        foreach (string address in SplitAddresses(email.BccAddresses))
        {
            storage.Add(new PlatformEntities.EmailRecipient
            {
                Id = Guid.NewGuid(),
                EmailId = email.Id,
                Address = address,
                RecipientType = EmailRecipientType.Bcc,
                CreatedBy = email.LastUpdatedBy,
                LastUpdatedBy = email.LastUpdatedBy,
                CreatedOn = email.LastUpdated,
                LastUpdated = email.LastUpdated
            });
        }
    }

    async ValueTask UpsertLinkedActivityAsync(
        IEmailWorkflowBroker storage,
        Guid relationshipId,
        EmailDraftUpsertCommand command,
        Guid? materialId,
        Guid? companyContactId,
        Guid? clientAccountId,
        string subject,
        string body,
        DateTimeOffset now,
        string currentUser,
        CancellationToken cancellationToken)
    {
        if (!materialId.HasValue || materialId.Value == Guid.Empty)
            return;

        PlatformEntities.Activity linkedActivity = await storage.Activities
            .FirstOrDefaultAsync(
                item => item.TenantCompanyRelationshipId == relationshipId && item.MaterialId == materialId.Value,
                cancellationToken);

        DateTimeOffset activityOn = command.ActivityOn ?? now;

        if (linkedActivity is null)
        {
            storage.Add(new PlatformEntities.Activity
            {
                Id = Guid.NewGuid(),
                TenantCompanyRelationshipId = relationshipId,
                OpportunityId = command.ClientOpportunityId,
                ClientAccountId = clientAccountId,
                CompanyContactId = companyContactId,
                MaterialId = materialId,
                ActivityOn = activityOn,
                Type = ActivityType.Email,
                Direction = command.Direction,
                Summary = subject,
                Outcome = body,
                NextAction = Normalize(command.NextAction),
                NextActionDueOn = command.NextActionDueOn,
                CreatedBy = currentUser,
                LastUpdatedBy = currentUser,
                CreatedOn = now,
                LastUpdated = now
            });

            return;
        }

        linkedActivity.OpportunityId = command.ClientOpportunityId;
        linkedActivity.ClientAccountId = clientAccountId ?? linkedActivity.ClientAccountId;
        linkedActivity.CompanyContactId = companyContactId ?? linkedActivity.CompanyContactId;
        linkedActivity.ActivityOn = activityOn;
        linkedActivity.Type = ActivityType.Email;
        linkedActivity.Direction = command.Direction;
        linkedActivity.Summary = subject;
        linkedActivity.Outcome = body;
        linkedActivity.NextAction = Normalize(command.NextAction);
        linkedActivity.NextActionDueOn = command.NextActionDueOn;
        linkedActivity.LastUpdatedBy = currentUser;
        linkedActivity.LastUpdated = now;
    }

    static IEnumerable<string> SplitAddresses(string addresses) =>
        string.IsNullOrWhiteSpace(addresses)
            ? []
            : addresses
                .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(address => !string.IsNullOrWhiteSpace(address))
                .Distinct(StringComparer.OrdinalIgnoreCase);

    static string ResolveRecipientAddresses(
        string requestedAddresses,
        PlatformEntities.CompanyContact contact,
        PlatformEntities.TenantCompanyRelationship relationship,
        string fallback = null)
    {
        string normalizedRequested = Normalize(requestedAddresses);
        if (!string.IsNullOrWhiteSpace(normalizedRequested))
            return normalizedRequested;

        if (!string.IsNullOrWhiteSpace(contact?.EmailAddress))
            return contact.EmailAddress;

        if (!string.IsNullOrWhiteSpace(relationship?.Company?.ContactEmailAddress))
            return relationship.Company.ContactEmailAddress;

        return Normalize(fallback);
    }

    static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    static string Normalize(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

    string CurrentUserId =>
        !string.IsNullOrWhiteSpace(currentExecutionUserAccessor?.UserId)
            ? currentExecutionUserAccessor.UserId
            : string.IsNullOrWhiteSpace(authInfo?.SSOUserId) || string.Equals(authInfo.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase)
            ? "system"
            : authInfo.SSOUserId;

    static string ResolveRelationshipName(PlatformEntities.TenantCompanyRelationship relationship) =>
        FirstNonEmpty(
            CompanyNames.ResolvePreferredName(relationship?.Company),
            relationship?.AccountOwnerDisplayName,
            relationship?.Id.ToString(),
            "unknown relationship");
}
