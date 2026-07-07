using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Foundations;

namespace cCoder.ClientRelationshipManagement.Services.Processings;

internal class EmailProcessingService(IEmailService emailService) : IEmailProcessingService
{
    public Email Get(Guid id, bool ignoreFilters = false) => emailService.Get(id, ignoreFilters);
    public IQueryable<Email> GetAll(bool ignoreFilters = false) => emailService.GetAll(ignoreFilters);
    public ValueTask<Email> AddAsync(Email email) => emailService.AddAsync(email);
    public ValueTask<Email> UpdateAsync(Email email) => emailService.UpdateAsync(email);
    public ValueTask DeleteAsync(Guid id) => emailService.DeleteAsync(id);

    public async ValueTask<Email> UpsertAsync(Email email)
    {
        ArgumentNullException.ThrowIfNull(email, nameof(email));

        return email.Id != Guid.Empty && Get(email.Id, ignoreFilters: true) is not null
            ? await UpdateAsync(email)
            : await AddAsync(email);
    }
}
