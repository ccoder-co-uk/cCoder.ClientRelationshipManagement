using cCoder.ClientRelationshipManagement.Models.Entities;

namespace cCoder.ClientRelationshipManagement.Brokers.Storages;

public interface IClientBroker
{
    IQueryable<Client> GetAllClients(bool ignoreFilters);

    ValueTask<Client> AddClientAsync(Client entity);

    ValueTask<Client> UpdateClientAsync(Client entity);

    ValueTask<int> DeleteClientAsync(Client entity);
}
