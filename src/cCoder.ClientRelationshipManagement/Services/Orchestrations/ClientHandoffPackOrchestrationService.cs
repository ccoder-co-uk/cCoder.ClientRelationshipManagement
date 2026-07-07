using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Processings;

namespace cCoder.ClientRelationshipManagement.Services.Orchestrations;

internal class ClientHandoffPackOrchestrationService(
    IClientHandoffPackProcessingService clientHandoffPackService,
    IClientProcessingService clientService,
    IAuthorizationProcessingService authorizationService)
    : IClientHandoffPackOrchestrationService
{
    public ClientHandoffPack Get(Guid id, bool ignoreFilters = false)
    {
        ClientHandoffPack clientHandoffPack = clientHandoffPackService.Get(id, ignoreFilters: true);
        if (clientHandoffPack is null)
            return null;

        AuthorizeRead(clientHandoffPack.ClientId);
        return ignoreFilters ? clientHandoffPack : clientHandoffPackService.Get(id);
    }

    public IQueryable<ClientHandoffPack> GetAll(bool ignoreFilters = false)
    {
        authorizationService.AuthorizeAny("client_read");
        return clientHandoffPackService.GetAll(ignoreFilters);
    }

    public async ValueTask<ClientHandoffPack> AddAsync(ClientHandoffPack clientHandoffPack)
    {
        AuthorizeWrite(clientHandoffPack?.ClientId);
        return await clientHandoffPackService.AddAsync(clientHandoffPack);
    }

    public async ValueTask<ClientHandoffPack> UpdateAsync(ClientHandoffPack clientHandoffPack)
    {
        Guid clientId = clientHandoffPack?.ClientId ?? clientHandoffPackService.Get(clientHandoffPack.Id, ignoreFilters: true)?.ClientId ?? Guid.Empty;
        AuthorizeWrite(clientId);
        return await clientHandoffPackService.UpdateAsync(clientHandoffPack);
    }

    public async ValueTask<ClientHandoffPack> UpsertAsync(ClientHandoffPack clientHandoffPack)
    {
        ClientHandoffPack existingClientHandoffPack = clientHandoffPack is not null && clientHandoffPack.Id != Guid.Empty
            ? clientHandoffPackService.Get(clientHandoffPack.Id, ignoreFilters: true)
            : null;

        Guid clientId = clientHandoffPack?.ClientId ?? existingClientHandoffPack?.ClientId ?? Guid.Empty;
        AuthorizeWrite(clientId);
        return await clientHandoffPackService.UpsertAsync(clientHandoffPack);
    }

    public async ValueTask DeleteAsync(Guid id)
    {
        ClientHandoffPack clientHandoffPack = clientHandoffPackService.Get(id, ignoreFilters: true);
        if (clientHandoffPack is null)
            return;

        AuthorizeWrite(clientHandoffPack.ClientId);
        await clientHandoffPackService.DeleteAsync(id);
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
