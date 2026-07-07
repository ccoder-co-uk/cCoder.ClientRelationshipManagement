using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Foundations;

namespace cCoder.ClientRelationshipManagement.Services.Processings;

internal class ClientContactProcessingService(IClientContactService clientContactService) : IClientContactProcessingService
{
    public ClientContact Get(Guid id, bool ignoreFilters = false) => clientContactService.Get(id, ignoreFilters);
    public IQueryable<ClientContact> GetAll(bool ignoreFilters = false) => clientContactService.GetAll(ignoreFilters);
    public ValueTask<ClientContact> AddAsync(ClientContact clientContact) => clientContactService.AddAsync(clientContact);
    public ValueTask<ClientContact> UpdateAsync(ClientContact clientContact) => clientContactService.UpdateAsync(clientContact);
    public ValueTask DeleteAsync(Guid id) => clientContactService.DeleteAsync(id);

    public async ValueTask<ClientContact> UpsertAsync(ClientContact clientContact)
    {
        ArgumentNullException.ThrowIfNull(clientContact, nameof(clientContact));

        return clientContact.Id != Guid.Empty && Get(clientContact.Id, ignoreFilters: true) is not null
            ? await UpdateAsync(clientContact)
            : await AddAsync(clientContact);
    }
}
