using cCoder.ClientRelationshipManagement.Models.Entities;

namespace cCoder.ClientRelationshipManagement.Brokers.Storages;

public interface IEmailBroker
{
    IQueryable<Email> GetAllEmails(bool ignoreFilters);
    ValueTask<Email> AddEmailAsync(Email entity);
    ValueTask<Email> UpdateEmailAsync(Email entity);
    ValueTask<int> DeleteEmailAsync(Email entity);
}
