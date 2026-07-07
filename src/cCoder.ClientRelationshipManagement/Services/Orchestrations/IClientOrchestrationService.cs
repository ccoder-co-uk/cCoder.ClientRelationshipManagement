using cCoder.ClientRelationshipManagement.Models.Entities;

namespace cCoder.ClientRelationshipManagement.Services.Orchestrations;

public interface IClientOrchestrationService
{
    Client Get(Guid id, bool ignoreFilters = false);
    IQueryable<Client> GetAll(bool ignoreFilters = false);
    ValueTask<Client> AddAsync(Client client);
    ValueTask<Client> UpdateAsync(Client client);
    ValueTask<Client> UpsertAsync(Client client);
    ValueTask DeleteAsync(Guid id);
}
