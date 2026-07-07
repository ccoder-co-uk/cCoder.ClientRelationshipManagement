using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Coordinations;
using cCoder.ClientRelationshipManagement.Services.Processings;

namespace cCoder.ClientRelationshipManagement.Services.Orchestrations;

internal class ClientContactOrchestrationService(
    IClientContactProcessingService clientContactService,
    IClientProcessingService clientService,
    IDeletionCoordinationService deletionCoordinationService,
    IAuthorizationProcessingService authorizationService)
    : IClientContactOrchestrationService
{
    public ClientContact Get(Guid id, bool ignoreFilters = false)
    {
        ClientContact clientContact = clientContactService.Get(id, ignoreFilters: true);
        if (clientContact is null)
            return null;

        AuthorizeRead(clientContact.ClientId);
        return ignoreFilters ? clientContact : clientContactService.Get(id);
    }

    public IQueryable<ClientContact> GetAll(bool ignoreFilters = false)
    {
        authorizationService.AuthorizeAny("client_read");
        return clientContactService.GetAll(ignoreFilters);
    }

    public async ValueTask<ClientContact> AddAsync(ClientContact clientContact)
    {
        AuthorizeWrite(clientContact?.ClientId);
        return await clientContactService.AddAsync(clientContact);
    }

    public async ValueTask<ClientContact> UpdateAsync(ClientContact clientContact)
    {
        Guid clientId = clientContact?.ClientId ?? clientContactService.Get(clientContact.Id, ignoreFilters: true)?.ClientId ?? Guid.Empty;
        AuthorizeWrite(clientId);
        return await clientContactService.UpdateAsync(clientContact);
    }

    public async ValueTask<ClientContact> UpsertAsync(ClientContact clientContact)
    {
        ClientContact existingClientContact = clientContact is not null && clientContact.Id != Guid.Empty
            ? clientContactService.Get(clientContact.Id, ignoreFilters: true)
            : null;

        Guid clientId = clientContact?.ClientId ?? existingClientContact?.ClientId ?? Guid.Empty;
        AuthorizeWrite(clientId);
        return await clientContactService.UpsertAsync(clientContact);
    }

    public async ValueTask DeleteAsync(Guid id)
    {
        ClientContact clientContact = clientContactService.Get(id, ignoreFilters: true);
        if (clientContact is null)
            return;

        AuthorizeWrite(clientContact.ClientId);
        await deletionCoordinationService.HandleClientContactDeleteAsync(clientContact);
        await clientContactService.DeleteAsync(id);
    }

    void AuthorizeRead(Guid? clientId) =>
        authorizationService.Authorize(ResolveTenantId(clientId), "client_read");

    void AuthorizeWrite(Guid? clientId) =>
        authorizationService.Authorize(ResolveTenantId(clientId), "client_write");

    string ResolveTenantId(Guid? clientId) =>
        clientId is Guid id && id != Guid.Empty
            ? clientService.Get(id, ignoreFilters: true)?.TenantId
            : null;
}
