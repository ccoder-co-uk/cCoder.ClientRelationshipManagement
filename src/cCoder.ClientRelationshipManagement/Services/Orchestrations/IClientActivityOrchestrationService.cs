using cCoder.ClientRelationshipManagement.Models.Entities;

namespace cCoder.ClientRelationshipManagement.Services.Orchestrations;

public interface IClientActivityOrchestrationService
{
    ClientActivity Get(Guid id, bool ignoreFilters = false);
    IQueryable<ClientActivity> GetAll(bool ignoreFilters = false);
    ValueTask<ClientActivity> AddAsync(ClientActivity clientActivity);
    ValueTask<ClientActivity> UpdateAsync(ClientActivity clientActivity);
    ValueTask<ClientActivity> UpsertAsync(ClientActivity clientActivity);
    ValueTask DeleteAsync(Guid id);
}
