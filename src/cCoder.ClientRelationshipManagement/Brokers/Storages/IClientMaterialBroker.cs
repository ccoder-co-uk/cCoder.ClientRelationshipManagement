using cCoder.ClientRelationshipManagement.Models.Entities;

namespace cCoder.ClientRelationshipManagement.Brokers.Storages;

public interface IClientMaterialBroker
{
    IQueryable<ClientMaterial> GetAllClientMaterials(bool ignoreFilters);

    ValueTask<ClientMaterial> AddClientMaterialAsync(ClientMaterial entity);

    ValueTask<ClientMaterial> UpdateClientMaterialAsync(ClientMaterial entity);

    ValueTask<int> DeleteClientMaterialAsync(ClientMaterial entity);
}
