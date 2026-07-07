using cCoder.ClientRelationshipManagement.Models.Entities;

namespace cCoder.ClientRelationshipManagement.Brokers.Storages;

public interface IClientHandoffPackBroker
{
    IQueryable<ClientHandoffPack> GetAllClientHandoffPacks(bool ignoreFilters);

    ValueTask<ClientHandoffPack> AddClientHandoffPackAsync(ClientHandoffPack entity);

    ValueTask<ClientHandoffPack> UpdateClientHandoffPackAsync(ClientHandoffPack entity);

    ValueTask<int> DeleteClientHandoffPackAsync(ClientHandoffPack entity);
}
