using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Coordinations;
using cCoder.ClientRelationshipManagement.Services.Processings;

namespace cCoder.ClientRelationshipManagement.Services.Orchestrations;

internal class ClientMaterialOrchestrationService(
    IClientMaterialProcessingService clientMaterialService,
    IClientProcessingService clientService,
    IDeletionCoordinationService deletionCoordinationService,
    IAuthorizationProcessingService authorizationService)
    : IClientMaterialOrchestrationService
{
    public ClientMaterial Get(Guid id, bool ignoreFilters = false)
    {
        ClientMaterial clientMaterial = clientMaterialService.Get(id, ignoreFilters: true);
        if (clientMaterial is null)
            return null;

        AuthorizeRead(clientMaterial.ClientId);
        return ignoreFilters ? clientMaterial : clientMaterialService.Get(id);
    }

    public IQueryable<ClientMaterial> GetAll(bool ignoreFilters = false)
    {
        authorizationService.AuthorizeAny("client_read");
        return clientMaterialService.GetAll(ignoreFilters);
    }

    public async ValueTask<ClientMaterial> AddAsync(ClientMaterial clientMaterial)
    {
        AuthorizeWrite(clientMaterial?.ClientId);
        return await clientMaterialService.AddAsync(clientMaterial);
    }

    public async ValueTask<ClientMaterial> UpdateAsync(ClientMaterial clientMaterial)
    {
        Guid clientId = clientMaterial?.ClientId ?? clientMaterialService.Get(clientMaterial.Id, ignoreFilters: true)?.ClientId ?? Guid.Empty;
        AuthorizeWrite(clientId);
        return await clientMaterialService.UpdateAsync(clientMaterial);
    }

    public async ValueTask<ClientMaterial> UpsertAsync(ClientMaterial clientMaterial)
    {
        ClientMaterial existingClientMaterial = clientMaterial is not null && clientMaterial.Id != Guid.Empty
            ? clientMaterialService.Get(clientMaterial.Id, ignoreFilters: true)
            : null;

        Guid clientId = clientMaterial?.ClientId ?? existingClientMaterial?.ClientId ?? Guid.Empty;
        AuthorizeWrite(clientId);
        return await clientMaterialService.UpsertAsync(clientMaterial);
    }

    public async ValueTask DeleteAsync(Guid id)
    {
        ClientMaterial clientMaterial = clientMaterialService.Get(id, ignoreFilters: true);
        if (clientMaterial is null)
            return;

        AuthorizeWrite(clientMaterial.ClientId);
        await deletionCoordinationService.HandleClientMaterialDeleteAsync(clientMaterial);
        await clientMaterialService.DeleteAsync(id);
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
