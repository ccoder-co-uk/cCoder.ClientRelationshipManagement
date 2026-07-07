using cCoder.ClientRelationshipManagement.Brokers.Events;
using cCoder.ClientRelationshipManagement.Brokers.Storages;
using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.Eventing.Models;

namespace cCoder.ClientRelationshipManagement.Services.Foundations;

internal partial class ClientContactService(
    IClientContactBroker clientContactBroker,
    IClientContactEventBroker clientContactEventBroker,
    ICRMAuthInfo authInfo)
    : IClientContactService
{
    public ClientContact Get(Guid id, bool ignoreFilters = false) =>
        clientContactBroker.GetAllClientContacts(ignoreFilters)
            .FirstOrDefault(contact => contact.Id == id);

    public IQueryable<ClientContact> GetAll(bool ignoreFilters = false) =>
        clientContactBroker.GetAllClientContacts(ignoreFilters);

    public async ValueTask<ClientContact> AddAsync(ClientContact clientContact)
    {
        ValidateClientContact(clientContact, nameof(clientContact));

        if (clientContact.Id == Guid.Empty)
            clientContact.Id = Guid.NewGuid();

        if (Get(clientContact.Id, ignoreFilters: true) is not null)
            throw new InvalidOperationException($"Client contact '{clientContact.Id}' already exists.");

        DateTimeOffset now = DateTimeOffset.UtcNow;
        ClientContact storageClientContact = new()
        {
            Id = clientContact.Id,
            ClientId = clientContact.ClientId,
            Name = clientContact.Name,
            Position = clientContact.Position,
            EmailAddress = clientContact.EmailAddress,
            PhoneNumber = clientContact.PhoneNumber,
            LinkedInUrl = clientContact.LinkedInUrl,
            Source = clientContact.Source,
            RelationshipRoute = clientContact.RelationshipRoute,
            Status = clientContact.Status,
            IsPrimary = clientContact.IsPrimary,
            Notes = clientContact.Notes,
            CreatedBy = clientContact.CreatedBy ?? authInfo.SSOUserId,
            LastUpdatedBy = authInfo.SSOUserId,
            CreatedOn = clientContact.CreatedOn == default ? now : clientContact.CreatedOn,
            LastUpdated = now,
        };

        ClientContact result = await clientContactBroker.AddClientContactAsync(storageClientContact);
        result.Client = clientContact.Client;
        result.Activities = clientContact.Activities;
        await clientContactEventBroker.RaiseClientContactAddEventAsync(CreateEventMessage(result));
        return result;
    }

    public async ValueTask<ClientContact> UpdateAsync(ClientContact clientContact)
    {
        ValidateClientContact(clientContact, nameof(clientContact));

        ClientContact storageClientContact = new()
        {
            Id = clientContact.Id,
            ClientId = clientContact.ClientId,
            Name = clientContact.Name,
            Position = clientContact.Position,
            EmailAddress = clientContact.EmailAddress,
            PhoneNumber = clientContact.PhoneNumber,
            LinkedInUrl = clientContact.LinkedInUrl,
            Source = clientContact.Source,
            RelationshipRoute = clientContact.RelationshipRoute,
            Status = clientContact.Status,
            IsPrimary = clientContact.IsPrimary,
            Notes = clientContact.Notes,
            CreatedBy = clientContact.CreatedBy,
            LastUpdatedBy = authInfo.SSOUserId,
            CreatedOn = clientContact.CreatedOn,
            LastUpdated = DateTimeOffset.UtcNow,
        };

        ClientContact result = await clientContactBroker.UpdateClientContactAsync(storageClientContact);
        result.Client = clientContact.Client;
        result.Activities = clientContact.Activities;
        await clientContactEventBroker.RaiseClientContactUpdateEventAsync(CreateEventMessage(result));
        return result;
    }

    public async ValueTask DeleteAsync(Guid id)
    {
        ClientContact clientContact = Get(id, ignoreFilters: true);

        if (clientContact is null)
            return;

        await clientContactEventBroker.RaiseClientContactDeleteEventAsync(CreateEventMessage(clientContact));
        await clientContactBroker.DeleteClientContactAsync(clientContact);
    }

    EventMessage<ClientContact> CreateEventMessage(ClientContact clientContact) =>
        new()
        {
            AuthInfo = new EventAuthInfo
            {
                SSOUserId = authInfo.SSOUserId,
            },
            Data = clientContact,
        };

    static void ValidateClientContact(ClientContact clientContact, string parameterName) =>
        ArgumentNullException.ThrowIfNull(clientContact, parameterName);
}
