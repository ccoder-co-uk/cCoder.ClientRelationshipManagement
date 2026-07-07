using cCoder.ClientRelationshipManagement.Brokers.Events;
using cCoder.ClientRelationshipManagement.Brokers.Storages;
using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.Eventing.Models;

namespace cCoder.ClientRelationshipManagement.Services.Foundations;

internal class EmailService(
    IEmailBroker emailBroker,
    IEmailEventBroker emailEventBroker,
    ICRMAuthInfo authInfo)
    : IEmailService
{
    public Email Get(Guid id, bool ignoreFilters = false) =>
        emailBroker.GetAllEmails(ignoreFilters)
            .FirstOrDefault(email => email.Id == id);

    public IQueryable<Email> GetAll(bool ignoreFilters = false) =>
        emailBroker.GetAllEmails(ignoreFilters);

    public async ValueTask<Email> AddAsync(Email email)
    {
        ValidateEmail(email, nameof(email));

        if (email.Id == Guid.Empty)
            email.Id = Guid.NewGuid();

        if (Get(email.Id, ignoreFilters: true) is not null)
            throw new InvalidOperationException($"Email '{email.Id}' already exists.");

        DateTimeOffset now = DateTimeOffset.UtcNow;
        Email storageEmail = new()
        {
            Id = email.Id,
            ClientId = email.ClientId,
            ClientMaterialId = email.ClientMaterialId,
            SentToContactId = email.SentToContactId,
            SenderUserId = email.SenderUserId ?? authInfo.SSOUserId,
            FromDisplayName = email.FromDisplayName,
            FromEmailAddress = email.FromEmailAddress,
            ReplyToAddresses = email.ReplyToAddresses,
            ToAddresses = email.ToAddresses,
            CcAddresses = email.CcAddresses,
            BccAddresses = email.BccAddresses,
            Subject = email.Subject,
            BodyHtml = email.BodyHtml,
            BodyText = email.BodyText,
            IsBodyHtml = email.IsBodyHtml,
            State = email.State,
            ApprovedOn = email.ApprovedOn,
            ApprovedBy = email.ApprovedBy,
            ScheduledSendTimeUtc = email.ScheduledSendTimeUtc,
            LastSendAttemptOn = email.LastSendAttemptOn,
            SentOn = email.SentOn,
            ExternalMessageId = email.ExternalMessageId,
            LastError = email.LastError,
            SendFailureCount = email.SendFailureCount,
            CreatedBy = email.CreatedBy ?? authInfo.SSOUserId,
            LastUpdatedBy = authInfo.SSOUserId,
            CreatedOn = email.CreatedOn == default ? now : email.CreatedOn,
            LastUpdated = now,
        };

        Email result = await emailBroker.AddEmailAsync(storageEmail);
        result.Client = email.Client;
        result.ClientMaterial = email.ClientMaterial;
        result.SentToContact = email.SentToContact;
        await emailEventBroker.RaiseEmailAddEventAsync(CreateEventMessage(result));
        return result;
    }

    public async ValueTask<Email> UpdateAsync(Email email)
    {
        ValidateEmail(email, nameof(email));

        Email storageEmail = new()
        {
            Id = email.Id,
            ClientId = email.ClientId,
            ClientMaterialId = email.ClientMaterialId,
            SentToContactId = email.SentToContactId,
            SenderUserId = email.SenderUserId,
            FromDisplayName = email.FromDisplayName,
            FromEmailAddress = email.FromEmailAddress,
            ReplyToAddresses = email.ReplyToAddresses,
            ToAddresses = email.ToAddresses,
            CcAddresses = email.CcAddresses,
            BccAddresses = email.BccAddresses,
            Subject = email.Subject,
            BodyHtml = email.BodyHtml,
            BodyText = email.BodyText,
            IsBodyHtml = email.IsBodyHtml,
            State = email.State,
            ApprovedOn = email.ApprovedOn,
            ApprovedBy = email.ApprovedBy,
            ScheduledSendTimeUtc = email.ScheduledSendTimeUtc,
            LastSendAttemptOn = email.LastSendAttemptOn,
            SentOn = email.SentOn,
            ExternalMessageId = email.ExternalMessageId,
            LastError = email.LastError,
            SendFailureCount = email.SendFailureCount,
            CreatedBy = email.CreatedBy,
            LastUpdatedBy = authInfo.SSOUserId,
            CreatedOn = email.CreatedOn,
            LastUpdated = DateTimeOffset.UtcNow,
        };

        Email result = await emailBroker.UpdateEmailAsync(storageEmail);
        result.Client = email.Client;
        result.ClientMaterial = email.ClientMaterial;
        result.SentToContact = email.SentToContact;
        await emailEventBroker.RaiseEmailUpdateEventAsync(CreateEventMessage(result));
        return result;
    }

    public async ValueTask DeleteAsync(Guid id)
    {
        Email email = Get(id, ignoreFilters: true);

        if (email is null)
            return;

        await emailEventBroker.RaiseEmailDeleteEventAsync(CreateEventMessage(email));
        await emailBroker.DeleteEmailAsync(email);
    }

    EventMessage<Email> CreateEventMessage(Email email) =>
        new()
        {
            AuthInfo = new EventAuthInfo
            {
                SSOUserId = authInfo.SSOUserId,
            },
            Data = email,
        };

    static void ValidateEmail(Email email, string parameterName) =>
        ArgumentNullException.ThrowIfNull(email, parameterName);
}
