using cCoder.ClientRelationshipManagement.Models.Entities;

namespace cCoder.ClientRelationshipManagement.Services.Orchestrations;

public interface IClientContactOrchestrationService
{
    ClientContact Get(Guid id, bool ignoreFilters = false);
    IQueryable<ClientContact> GetAll(bool ignoreFilters = false);
    ValueTask<ClientContact> AddAsync(ClientContact clientContact);
    ValueTask<ClientContact> UpdateAsync(ClientContact clientContact);
    ValueTask<ClientContact> UpsertAsync(ClientContact clientContact);
    ValueTask DeleteAsync(Guid id);
}
