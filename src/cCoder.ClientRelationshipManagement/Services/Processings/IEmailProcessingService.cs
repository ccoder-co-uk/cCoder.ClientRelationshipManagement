using cCoder.ClientRelationshipManagement.Models.Entities;

namespace cCoder.ClientRelationshipManagement.Services.Processings;

public interface IEmailProcessingService
{
    Email Get(Guid id, bool ignoreFilters = false);
    IQueryable<Email> GetAll(bool ignoreFilters = false);
    ValueTask<Email> AddAsync(Email email);
    ValueTask<Email> UpdateAsync(Email email);
    ValueTask<Email> UpsertAsync(Email email);
    ValueTask DeleteAsync(Guid id);
}
