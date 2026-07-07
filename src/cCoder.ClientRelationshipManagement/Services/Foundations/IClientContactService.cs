using cCoder.ClientRelationshipManagement.Models.Entities;

namespace cCoder.ClientRelationshipManagement.Services.Foundations;

public interface IClientContactService
{
    ClientContact Get(Guid id, bool ignoreFilters = false);
    IQueryable<ClientContact> GetAll(bool ignoreFilters = false);
    ValueTask<ClientContact> AddAsync(ClientContact clientContact);
    ValueTask<ClientContact> UpdateAsync(ClientContact clientContact);
    ValueTask DeleteAsync(Guid id);
}
