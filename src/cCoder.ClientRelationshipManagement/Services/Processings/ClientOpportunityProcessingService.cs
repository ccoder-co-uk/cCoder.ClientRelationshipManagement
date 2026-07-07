using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Foundations;

namespace cCoder.ClientRelationshipManagement.Services.Processings;

internal class ClientOpportunityProcessingService(IClientOpportunityService clientOpportunityService) : IClientOpportunityProcessingService
{
    public ClientOpportunity Get(Guid id, bool ignoreFilters = false) => clientOpportunityService.Get(id, ignoreFilters);
    public IQueryable<ClientOpportunity> GetAll(bool ignoreFilters = false) => clientOpportunityService.GetAll(ignoreFilters);
    public ValueTask<ClientOpportunity> AddAsync(ClientOpportunity clientOpportunity) => clientOpportunityService.AddAsync(clientOpportunity);
    public ValueTask<ClientOpportunity> UpdateAsync(ClientOpportunity clientOpportunity) => clientOpportunityService.UpdateAsync(clientOpportunity);
    public ValueTask DeleteAsync(Guid id) => clientOpportunityService.DeleteAsync(id);

    public async ValueTask<ClientOpportunity> UpsertAsync(ClientOpportunity clientOpportunity)
    {
        ArgumentNullException.ThrowIfNull(clientOpportunity, nameof(clientOpportunity));

        return clientOpportunity.Id != Guid.Empty && Get(clientOpportunity.Id, ignoreFilters: true) is not null
            ? await UpdateAsync(clientOpportunity)
            : await AddAsync(clientOpportunity);
    }
}
