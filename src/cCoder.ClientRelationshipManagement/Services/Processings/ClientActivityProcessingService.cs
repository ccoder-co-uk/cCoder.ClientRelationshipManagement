using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Foundations;

namespace cCoder.ClientRelationshipManagement.Services.Processings;

internal class ClientActivityProcessingService(IClientActivityService clientActivityService) : IClientActivityProcessingService
{
    public ClientActivity Get(Guid id, bool ignoreFilters = false) => clientActivityService.Get(id, ignoreFilters);
    public IQueryable<ClientActivity> GetAll(bool ignoreFilters = false) => clientActivityService.GetAll(ignoreFilters);
    public ValueTask<ClientActivity> AddAsync(ClientActivity clientActivity) => clientActivityService.AddAsync(clientActivity);
    public ValueTask<ClientActivity> UpdateAsync(ClientActivity clientActivity) => clientActivityService.UpdateAsync(clientActivity);
    public ValueTask DeleteAsync(Guid id) => clientActivityService.DeleteAsync(id);

    public async ValueTask<ClientActivity> UpsertAsync(ClientActivity clientActivity)
    {
        ArgumentNullException.ThrowIfNull(clientActivity, nameof(clientActivity));

        return clientActivity.Id != Guid.Empty && Get(clientActivity.Id, ignoreFilters: true) is not null
            ? await UpdateAsync(clientActivity)
            : await AddAsync(clientActivity);
    }
}
