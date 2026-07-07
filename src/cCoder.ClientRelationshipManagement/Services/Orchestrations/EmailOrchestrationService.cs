using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Coordinations;
using cCoder.ClientRelationshipManagement.Services.Processings;

namespace cCoder.ClientRelationshipManagement.Services.Orchestrations;

internal class EmailOrchestrationService(
    IEmailProcessingService emailService,
    IClientProcessingService clientService,
    IDeletionCoordinationService deletionCoordinationService,
    IAuthorizationProcessingService authorizationService)
    : IEmailOrchestrationService
{
    public Email Get(Guid id, bool ignoreFilters = false)
    {
        Email email = emailService.Get(id, ignoreFilters: true);
        if (email is null)
            return null;

        AuthorizeRead(email.ClientId);
        return ignoreFilters ? email : emailService.Get(id);
    }

    public IQueryable<Email> GetAll(bool ignoreFilters = false)
    {
        authorizationService.AuthorizeAny("client_read");
        return emailService.GetAll(ignoreFilters);
    }

    public async ValueTask<Email> AddAsync(Email email)
    {
        AuthorizeWrite(email?.ClientId);
        return await emailService.AddAsync(email);
    }

    public async ValueTask<Email> UpdateAsync(Email email)
    {
        Guid clientId = email?.ClientId ?? emailService.Get(email.Id, ignoreFilters: true)?.ClientId ?? Guid.Empty;
        AuthorizeWrite(clientId);
        return await emailService.UpdateAsync(email);
    }

    public async ValueTask<Email> UpsertAsync(Email email)
    {
        Email existingEmail = email is not null && email.Id != Guid.Empty
            ? emailService.Get(email.Id, ignoreFilters: true)
            : null;

        Guid clientId = email?.ClientId ?? existingEmail?.ClientId ?? Guid.Empty;
        AuthorizeWrite(clientId);
        return await emailService.UpsertAsync(email);
    }

    public async ValueTask DeleteAsync(Guid id)
    {
        Email email = emailService.Get(id, ignoreFilters: true);
        if (email is null)
            return;

        AuthorizeWrite(email.ClientId);
        await deletionCoordinationService.HandleEmailDeleteAsync(email);
        await emailService.DeleteAsync(id);
    }

    void AuthorizeRead(Guid? clientId) =>
        authorizationService.Authorize(ResolveTenantId(clientId), "client_read");

    void AuthorizeWrite(Guid? clientId) =>
        authorizationService.Authorize(ResolveTenantId(clientId), "client_write");

    string ResolveTenantId(Guid? clientId) =>
        clientId is Guid id && id != Guid.Empty
            ? clientService.Get(id, ignoreFilters: true)?.TenantId
            : null;
}
