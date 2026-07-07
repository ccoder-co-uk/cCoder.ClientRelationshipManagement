using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Foundations;

namespace cCoder.ClientRelationshipManagement.Services.Processings;

internal class ClientMaterialProcessingService(IClientMaterialService clientMaterialService) : IClientMaterialProcessingService
{
    public ClientMaterial Get(Guid id, bool ignoreFilters = false) => clientMaterialService.Get(id, ignoreFilters);
    public IQueryable<ClientMaterial> GetAll(bool ignoreFilters = false) => clientMaterialService.GetAll(ignoreFilters);
    public ValueTask<ClientMaterial> AddAsync(ClientMaterial clientMaterial) => clientMaterialService.AddAsync(clientMaterial);
    public ValueTask<ClientMaterial> UpdateAsync(ClientMaterial clientMaterial) => clientMaterialService.UpdateAsync(clientMaterial);
    public ValueTask DeleteAsync(Guid id) => clientMaterialService.DeleteAsync(id);

    public async ValueTask<ClientMaterial> UpsertAsync(ClientMaterial clientMaterial)
    {
        ArgumentNullException.ThrowIfNull(clientMaterial, nameof(clientMaterial));

        return clientMaterial.Id != Guid.Empty && Get(clientMaterial.Id, ignoreFilters: true) is not null
            ? await UpdateAsync(clientMaterial)
            : await AddAsync(clientMaterial);
    }
}
