using cCoder.ClientRelationshipManagement.Models.Entities;

namespace cCoder.ClientRelationshipManagement.Brokers.Storages;

public interface IClientActivityBroker
{
    IQueryable<ClientActivity> GetAllClientActivities(bool ignoreFilters);

    ValueTask<ClientActivity> AddClientActivityAsync(ClientActivity entity);

    ValueTask<ClientActivity> UpdateClientActivityAsync(ClientActivity entity);

    ValueTask<int> DeleteClientActivityAsync(ClientActivity entity);
}
