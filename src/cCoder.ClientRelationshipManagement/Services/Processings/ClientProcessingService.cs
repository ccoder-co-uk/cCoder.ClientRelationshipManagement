using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Foundations;

namespace cCoder.ClientRelationshipManagement.Services.Processings;

internal class ClientProcessingService(IClientService clientService) : IClientProcessingService
{
    public Client Get(Guid id, bool ignoreFilters = false) => clientService.Get(id, ignoreFilters);
    public IQueryable<Client> GetAll(bool ignoreFilters = false) => clientService.GetAll(ignoreFilters);
    public ValueTask<Client> AddAsync(Client client) => clientService.AddAsync(client);
    public ValueTask<Client> UpdateAsync(Client client) => clientService.UpdateAsync(client);
    public ValueTask DeleteAsync(Guid id) => clientService.DeleteAsync(id);

    public async ValueTask<Client> UpsertAsync(Client client)
    {
        ArgumentNullException.ThrowIfNull(client, nameof(client));

        return client.Id != Guid.Empty && Get(client.Id, ignoreFilters: true) is not null
            ? await UpdateAsync(client)
            : await AddAsync(client);
    }
}
