using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Coordinations;
using cCoder.ClientRelationshipManagement.Services.Processings;

namespace cCoder.ClientRelationshipManagement.Services.Orchestrations;

internal class ClientOrchestrationService(
    IClientProcessingService clientService,
    IDeletionCoordinationService deletionCoordinationService,
    IAuthorizationProcessingService authorizationService)
    : IClientOrchestrationService
{
    public Client Get(Guid id, bool ignoreFilters = false)
    {
        Client client = clientService.Get(id, ignoreFilters: true);
        if (client is null)
            return null;

        authorizationService.Authorize(client.TenantId, "client_read");
        return ignoreFilters ? client : clientService.Get(id);
    }

    public IQueryable<Client> GetAll(bool ignoreFilters = false)
    {
        authorizationService.AuthorizeAny("client_read");
        return clientService.GetAll(ignoreFilters);
    }

    public async ValueTask<Client> AddAsync(Client client)
    {
        authorizationService.Authorize(client?.TenantId, "client_write");
        return await clientService.AddAsync(client);
    }

    public async ValueTask<Client> UpdateAsync(Client client)
    {
        string tenantId = client?.TenantId ?? clientService.Get(client.Id, ignoreFilters: true)?.TenantId;
        authorizationService.Authorize(tenantId, "client_write");
        return await clientService.UpdateAsync(client);
    }

    public async ValueTask<Client> UpsertAsync(Client client)
    {
        Client existingClient = client is not null && client.Id != Guid.Empty
            ? clientService.Get(client.Id, ignoreFilters: true)
            : null;

        string tenantId = client?.TenantId ?? existingClient?.TenantId;
        authorizationService.Authorize(tenantId, "client_write");
        return await clientService.UpsertAsync(client);
    }

    public async ValueTask DeleteAsync(Guid id)
    {
        Client client = clientService.Get(id, ignoreFilters: true);
        if (client is null)
            return;

        authorizationService.Authorize(client.TenantId, "client_write");
        await deletionCoordinationService.HandleClientDeleteAsync(client);
        await clientService.DeleteAsync(id);
    }
}
