using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Coordinations;
using cCoder.ClientRelationshipManagement.Services.Processings;

namespace cCoder.ClientRelationshipManagement.Services.Orchestrations;

internal class ClientOpportunityOrchestrationService(
    IClientOpportunityProcessingService clientOpportunityService,
    IClientProcessingService clientService,
    IDeletionCoordinationService deletionCoordinationService,
    IAuthorizationProcessingService authorizationService)
    : IClientOpportunityOrchestrationService
{
    public ClientOpportunity Get(Guid id, bool ignoreFilters = false)
    {
        ClientOpportunity clientOpportunity = clientOpportunityService.Get(id, ignoreFilters: true);
        if (clientOpportunity is null)
            return null;

        AuthorizeRead(clientOpportunity.ClientId);
        return ignoreFilters ? clientOpportunity : clientOpportunityService.Get(id);
    }

    public IQueryable<ClientOpportunity> GetAll(bool ignoreFilters = false)
    {
        authorizationService.AuthorizeAny("client_read");
        return clientOpportunityService.GetAll(ignoreFilters);
    }

    public async ValueTask<ClientOpportunity> AddAsync(ClientOpportunity clientOpportunity)
    {
        AuthorizeWrite(clientOpportunity?.ClientId);
        return await clientOpportunityService.AddAsync(clientOpportunity);
    }

    public async ValueTask<ClientOpportunity> UpdateAsync(ClientOpportunity clientOpportunity)
    {
        Guid clientId = clientOpportunity?.ClientId ?? clientOpportunityService.Get(clientOpportunity.Id, ignoreFilters: true)?.ClientId ?? Guid.Empty;
        AuthorizeWrite(clientId);
        return await clientOpportunityService.UpdateAsync(clientOpportunity);
    }

    public async ValueTask<ClientOpportunity> UpsertAsync(ClientOpportunity clientOpportunity)
    {
        ClientOpportunity existingClientOpportunity = clientOpportunity is not null && clientOpportunity.Id != Guid.Empty
            ? clientOpportunityService.Get(clientOpportunity.Id, ignoreFilters: true)
            : null;

        Guid clientId = clientOpportunity?.ClientId ?? existingClientOpportunity?.ClientId ?? Guid.Empty;
        AuthorizeWrite(clientId);
        return await clientOpportunityService.UpsertAsync(clientOpportunity);
    }

    public async ValueTask DeleteAsync(Guid id)
    {
        ClientOpportunity clientOpportunity = clientOpportunityService.Get(id, ignoreFilters: true);
        if (clientOpportunity is null)
            return;

        AuthorizeWrite(clientOpportunity.ClientId);
        await deletionCoordinationService.HandleClientOpportunityDeleteAsync(clientOpportunity);
        await clientOpportunityService.DeleteAsync(id);
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
