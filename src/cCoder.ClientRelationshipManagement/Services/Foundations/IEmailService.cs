using cCoder.ClientRelationshipManagement.Models.Entities;

namespace cCoder.ClientRelationshipManagement.Services.Foundations;

public interface IEmailService
{
    Email Get(Guid id, bool ignoreFilters = false);
    IQueryable<Email> GetAll(bool ignoreFilters = false);
    ValueTask<Email> AddAsync(Email email);
    ValueTask<Email> UpdateAsync(Email email);
    ValueTask DeleteAsync(Guid id);
}
