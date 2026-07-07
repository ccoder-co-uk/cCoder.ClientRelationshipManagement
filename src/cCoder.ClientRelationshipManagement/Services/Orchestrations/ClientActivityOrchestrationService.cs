using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Processings;

namespace cCoder.ClientRelationshipManagement.Services.Orchestrations;

internal class ClientActivityOrchestrationService(
    IClientActivityProcessingService clientActivityService,
    IClientProcessingService clientService,
    IAuthorizationProcessingService authorizationService)
    : IClientActivityOrchestrationService
{
    public ClientActivity Get(Guid id, bool ignoreFilters = false)
    {
        ClientActivity clientActivity = clientActivityService.Get(id, ignoreFilters: true);
        if (clientActivity is null)
            return null;

        AuthorizeRead(clientActivity.ClientId);
        return ignoreFilters ? clientActivity : clientActivityService.Get(id);
    }

    public IQueryable<ClientActivity> GetAll(bool ignoreFilters = false)
    {
        authorizationService.AuthorizeAny("client_read");
        return clientActivityService.GetAll(ignoreFilters);
    }

    public async ValueTask<ClientActivity> AddAsync(ClientActivity clientActivity)
    {
        AuthorizeWrite(clientActivity?.ClientId);
        return await clientActivityService.AddAsync(clientActivity);
    }

    public async ValueTask<ClientActivity> UpdateAsync(ClientActivity clientActivity)
    {
        Guid clientId = clientActivity?.ClientId ?? clientActivityService.Get(clientActivity.Id, ignoreFilters: true)?.ClientId ?? Guid.Empty;
        AuthorizeWrite(clientId);
        return await clientActivityService.UpdateAsync(clientActivity);
    }

    public async ValueTask<ClientActivity> UpsertAsync(ClientActivity clientActivity)
    {
        ClientActivity existingClientActivity = clientActivity is not null && clientActivity.Id != Guid.Empty
            ? clientActivityService.Get(clientActivity.Id, ignoreFilters: true)
            : null;

        Guid clientId = clientActivity?.ClientId ?? existingClientActivity?.ClientId ?? Guid.Empty;
        AuthorizeWrite(clientId);
        return await clientActivityService.UpsertAsync(clientActivity);
    }

    public async ValueTask DeleteAsync(Guid id)
    {
        ClientActivity clientActivity = clientActivityService.Get(id, ignoreFilters: true);
        if (clientActivity is null)
            return;

        AuthorizeWrite(clientActivity.ClientId);
        await clientActivityService.DeleteAsync(id);
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
