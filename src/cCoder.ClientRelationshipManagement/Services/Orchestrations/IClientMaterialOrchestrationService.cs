using cCoder.ClientRelationshipManagement.Models.Entities;

namespace cCoder.ClientRelationshipManagement.Services.Orchestrations;

public interface IClientMaterialOrchestrationService
{
    ClientMaterial Get(Guid id, bool ignoreFilters = false);
    IQueryable<ClientMaterial> GetAll(bool ignoreFilters = false);
    ValueTask<ClientMaterial> AddAsync(ClientMaterial clientMaterial);
    ValueTask<ClientMaterial> UpdateAsync(ClientMaterial clientMaterial);
    ValueTask<ClientMaterial> UpsertAsync(ClientMaterial clientMaterial);
    ValueTask DeleteAsync(Guid id);
}
