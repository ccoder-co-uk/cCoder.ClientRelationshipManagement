using cCoder.ClientRelationshipManagement.Models.Entities;

namespace cCoder.ClientRelationshipManagement.Services.Foundations;

public interface IClientService
{
    Client Get(Guid id, bool ignoreFilters = false);

    IQueryable<Client> GetAll(bool ignoreFilters = false);

    ValueTask<Client> AddAsync(Client client);

    ValueTask<Client> UpdateAsync(Client client);

    ValueTask DeleteAsync(Guid id);
}
