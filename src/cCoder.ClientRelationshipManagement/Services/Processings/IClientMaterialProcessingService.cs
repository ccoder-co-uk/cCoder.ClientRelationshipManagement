using cCoder.ClientRelationshipManagement.Models.Entities;

namespace cCoder.ClientRelationshipManagement.Services.Processings;

public interface IClientMaterialProcessingService
{
    ClientMaterial Get(Guid id, bool ignoreFilters = false);
    IQueryable<ClientMaterial> GetAll(bool ignoreFilters = false);
    ValueTask<ClientMaterial> AddAsync(ClientMaterial clientMaterial);
    ValueTask<ClientMaterial> UpdateAsync(ClientMaterial clientMaterial);
    ValueTask<ClientMaterial> UpsertAsync(ClientMaterial clientMaterial);
    ValueTask DeleteAsync(Guid id);
}
