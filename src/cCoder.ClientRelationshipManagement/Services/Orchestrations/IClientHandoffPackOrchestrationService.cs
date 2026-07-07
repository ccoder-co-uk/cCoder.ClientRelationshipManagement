using cCoder.ClientRelationshipManagement.Models.Entities;

namespace cCoder.ClientRelationshipManagement.Services.Orchestrations;

public interface IClientHandoffPackOrchestrationService
{
    ClientHandoffPack Get(Guid id, bool ignoreFilters = false);
    IQueryable<ClientHandoffPack> GetAll(bool ignoreFilters = false);
    ValueTask<ClientHandoffPack> AddAsync(ClientHandoffPack clientHandoffPack);
    ValueTask<ClientHandoffPack> UpdateAsync(ClientHandoffPack clientHandoffPack);
    ValueTask<ClientHandoffPack> UpsertAsync(ClientHandoffPack clientHandoffPack);
    ValueTask DeleteAsync(Guid id);
}
