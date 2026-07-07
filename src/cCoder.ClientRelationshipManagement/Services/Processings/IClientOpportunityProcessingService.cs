using cCoder.ClientRelationshipManagement.Models.Entities;

namespace cCoder.ClientRelationshipManagement.Services.Processings;

public interface IClientOpportunityProcessingService
{
    ClientOpportunity Get(Guid id, bool ignoreFilters = false);
    IQueryable<ClientOpportunity> GetAll(bool ignoreFilters = false);
    ValueTask<ClientOpportunity> AddAsync(ClientOpportunity clientOpportunity);
    ValueTask<ClientOpportunity> UpdateAsync(ClientOpportunity clientOpportunity);
    ValueTask<ClientOpportunity> UpsertAsync(ClientOpportunity clientOpportunity);
    ValueTask DeleteAsync(Guid id);
}
