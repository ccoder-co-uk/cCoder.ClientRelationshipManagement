using cCoder.ClientRelationshipManagement.Models.Entities;

namespace cCoder.ClientRelationshipManagement.Brokers.Storages;

public interface IClientContactBroker
{
    IQueryable<ClientContact> GetAllClientContacts(bool ignoreFilters);

    ValueTask<ClientContact> AddClientContactAsync(ClientContact entity);

    ValueTask<ClientContact> UpdateClientContactAsync(ClientContact entity);

    ValueTask<int> DeleteClientContactAsync(ClientContact entity);
}
