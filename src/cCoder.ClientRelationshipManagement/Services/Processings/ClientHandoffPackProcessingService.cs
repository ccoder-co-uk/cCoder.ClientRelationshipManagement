using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Foundations;

namespace cCoder.ClientRelationshipManagement.Services.Processings;

internal class ClientHandoffPackProcessingService(IClientHandoffPackService clientHandoffPackService) : IClientHandoffPackProcessingService
{
    public ClientHandoffPack Get(Guid id, bool ignoreFilters = false) => clientHandoffPackService.Get(id, ignoreFilters);
    public IQueryable<ClientHandoffPack> GetAll(bool ignoreFilters = false) => clientHandoffPackService.GetAll(ignoreFilters);
    public ValueTask<ClientHandoffPack> AddAsync(ClientHandoffPack clientHandoffPack) => clientHandoffPackService.AddAsync(clientHandoffPack);
    public ValueTask<ClientHandoffPack> UpdateAsync(ClientHandoffPack clientHandoffPack) => clientHandoffPackService.UpdateAsync(clientHandoffPack);
    public ValueTask DeleteAsync(Guid id) => clientHandoffPackService.DeleteAsync(id);

    public async ValueTask<ClientHandoffPack> UpsertAsync(ClientHandoffPack clientHandoffPack)
    {
        ArgumentNullException.ThrowIfNull(clientHandoffPack, nameof(clientHandoffPack));

        return clientHandoffPack.Id != Guid.Empty && Get(clientHandoffPack.Id, ignoreFilters: true) is not null
            ? await UpdateAsync(clientHandoffPack)
            : await AddAsync(clientHandoffPack);
    }
}
