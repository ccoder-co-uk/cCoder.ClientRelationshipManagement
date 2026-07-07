using cCoder.ClientRelationshipManagement.Models.Entities;

namespace cCoder.ClientRelationshipManagement.Services.Foundations;

public interface IClientMaterialService
{
    ClientMaterial Get(Guid id, bool ignoreFilters = false);
    IQueryable<ClientMaterial> GetAll(bool ignoreFilters = false);
    ValueTask<ClientMaterial> AddAsync(ClientMaterial clientMaterial);
    ValueTask<ClientMaterial> UpdateAsync(ClientMaterial clientMaterial);
    ValueTask DeleteAsync(Guid id);
}
