using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Services.Entities;

public interface IEmailRecipientStorageBroker
{
    IQueryable<EmailRecipient> SelectAll();
    ValueTask<EmailRecipient> InsertAsync(EmailRecipient entity, CancellationToken cancellationToken = default);
    ValueTask<EmailRecipient> UpdateAsync(EmailRecipient entity, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(EmailRecipient entity, CancellationToken cancellationToken = default);
}

internal sealed class EmailRecipientStorageBroker : IEmailRecipientStorageBroker
{
    readonly ClientRelationshipDbContext context;
    public EmailRecipientStorageBroker(ClientRelationshipDbContext context) => this.context = context;
    public IQueryable<EmailRecipient> SelectAll() => context.Set<EmailRecipient>();
    public async ValueTask<EmailRecipient> InsertAsync(EmailRecipient entity, CancellationToken cancellationToken = default) { context.Set<EmailRecipient>().Add(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask<EmailRecipient> UpdateAsync(EmailRecipient entity, CancellationToken cancellationToken = default) { EmailRecipient local = context.Set<EmailRecipient>().Local.FirstOrDefault(item => item.Id == entity.Id); if (local is null) context.Set<EmailRecipient>().Update(entity); else context.Entry(local).CurrentValues.SetValues(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask DeleteAsync(EmailRecipient entity, CancellationToken cancellationToken = default) { context.Set<EmailRecipient>().Remove(entity); await context.SaveChangesAsync(cancellationToken); }

}

public interface IEmailRecipientFoundationService
{
    IQueryable<EmailRecipient> RetrieveAll();
    IQueryable<EmailRecipient> RetrieveWriteable();
    ValueTask<EmailRecipient> AddAsync(EmailRecipient entity, CancellationToken cancellationToken = default);
    ValueTask<EmailRecipient> ModifyAsync(EmailRecipient entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(EmailRecipient entity, CancellationToken cancellationToken = default);
}

internal sealed class EmailRecipientFoundationService(IEmailRecipientStorageBroker broker, ICRMAuthInfo auth) : IEmailRecipientFoundationService
{
    string[] Readable => auth.ReadableTenants?.Length > 0 ? auth.ReadableTenants : auth.WriteableTenants ?? [];
    string[] Writeable => auth.WriteableTenants ?? [];
    public IQueryable<EmailRecipient> RetrieveAll() => Scope(broker.SelectAll(), Readable);
    public IQueryable<EmailRecipient> RetrieveWriteable() => Scope(broker.SelectAll(), Writeable);

    public async ValueTask<EmailRecipient> AddAsync(EmailRecipient entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        if (Writeable.Length == 0) throw new UnauthorizedAccessException("The user has no writable CRM tenant.");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        EmailRecipient storage = Copy(entity);
        storage.CreatedOn = now;
        storage.CreatedBy = auth.SSOUserId;
        storage.LastUpdated = now;
        storage.LastUpdatedBy = auth.SSOUserId;
        EmailRecipient persisted = await broker.InsertAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask<EmailRecipient> ModifyAsync(EmailRecipient entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        EmailRecipient existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        EmailRecipient storage = Copy(entity);
        storage.CreatedOn = existing.CreatedOn;
        storage.CreatedBy = existing.CreatedBy;
        storage.LastUpdated = DateTimeOffset.UtcNow;
        storage.LastUpdatedBy = auth.SSOUserId;
        EmailRecipient persisted = await broker.UpdateAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask RemoveAsync(EmailRecipient entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        EmailRecipient existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        await broker.DeleteAsync(existing, cancellationToken);
    }

    IQueryable<EmailRecipient> Scope(IQueryable<EmailRecipient> source, string[] tenants) => source.Where(item => tenants.Contains(item.Email.TenantCompanyRelationship.TenantId));

    static EmailRecipient Copy(EmailRecipient source) => new()
    {
            Id = source.Id,
            CreatedBy = source.CreatedBy,
            LastUpdatedBy = source.LastUpdatedBy,
            CreatedOn = source.CreatedOn,
            LastUpdated = source.LastUpdated,
            EmailId = source.EmailId,
            CompanyContactId = source.CompanyContactId,
            Address = source.Address,
            RecipientType = source.RecipientType,
    };

    static void CopyPersisted(EmailRecipient source, EmailRecipient target)
    {
        target.Id = source.Id;
        target.CreatedBy = source.CreatedBy;
        target.LastUpdatedBy = source.LastUpdatedBy;
        target.CreatedOn = source.CreatedOn;
        target.LastUpdated = source.LastUpdated;
        target.EmailId = source.EmailId;
        target.CompanyContactId = source.CompanyContactId;
        target.Address = source.Address;
        target.RecipientType = source.RecipientType;
    }


    void EnsureAuthenticated()
    {
        if (string.IsNullOrWhiteSpace(auth.SSOUserId) || string.Equals(auth.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("A signed-in CRM user is required.");
    }
}

public interface IEmailRecipientProcessingService : IEmailRecipientFoundationService { }
internal sealed class EmailRecipientProcessingService(IEmailRecipientFoundationService foundation) : IEmailRecipientProcessingService
{
    public IQueryable<EmailRecipient> RetrieveAll() => foundation.RetrieveAll();
    public IQueryable<EmailRecipient> RetrieveWriteable() => foundation.RetrieveWriteable();
    public ValueTask<EmailRecipient> AddAsync(EmailRecipient entity, CancellationToken cancellationToken = default) => foundation.AddAsync(entity, cancellationToken);
    public ValueTask<EmailRecipient> ModifyAsync(EmailRecipient entity, CancellationToken cancellationToken = default) => foundation.ModifyAsync(entity, cancellationToken);
    public ValueTask RemoveAsync(EmailRecipient entity, CancellationToken cancellationToken = default) => foundation.RemoveAsync(entity, cancellationToken);
}

public interface IEmailRecipientEventBroker
{
    ValueTask RaiseAddAsync(EventMessage<EmailRecipient> message);
    ValueTask RaiseUpdateAsync(EventMessage<EmailRecipient> message);
    ValueTask RaiseDeleteAsync(EventMessage<EmailRecipient> message);
}
internal sealed class EmailRecipientEventBroker(IEventHub eventHub) : IEmailRecipientEventBroker
{
    public ValueTask RaiseAddAsync(EventMessage<EmailRecipient> message) => eventHub.RaiseEventAsync("email_recipient_add", message);
    public ValueTask RaiseUpdateAsync(EventMessage<EmailRecipient> message) => eventHub.RaiseEventAsync("email_recipient_update", message);
    public ValueTask RaiseDeleteAsync(EventMessage<EmailRecipient> message) => eventHub.RaiseEventAsync("email_recipient_delete", message);
}
public interface IEmailRecipientEventFoundationService
{
    ValueTask RaiseAddAsync(EmailRecipient entity);
    ValueTask RaiseUpdateAsync(EmailRecipient entity);
    ValueTask RaiseDeleteAsync(EmailRecipient entity);
}
internal sealed class EmailRecipientEventFoundationService(IEmailRecipientEventBroker broker, ICRMAuthInfo auth) : IEmailRecipientEventFoundationService
{
    EventMessage<EmailRecipient> Message(EmailRecipient entity) => new() { AuthInfo = new EventAuthInfo { SSOUserId = auth.SSOUserId }, Data = entity };
    public ValueTask RaiseAddAsync(EmailRecipient entity) => broker.RaiseAddAsync(Message(entity));
    public ValueTask RaiseUpdateAsync(EmailRecipient entity) => broker.RaiseUpdateAsync(Message(entity));
    public ValueTask RaiseDeleteAsync(EmailRecipient entity) => broker.RaiseDeleteAsync(Message(entity));
}
public interface IEmailRecipientEventProcessingService : IEmailRecipientEventFoundationService { }
internal sealed class EmailRecipientEventProcessingService(IEmailRecipientEventFoundationService foundation) : IEmailRecipientEventProcessingService
{
    public ValueTask RaiseAddAsync(EmailRecipient entity) => foundation.RaiseAddAsync(entity);
    public ValueTask RaiseUpdateAsync(EmailRecipient entity) => foundation.RaiseUpdateAsync(entity);
    public ValueTask RaiseDeleteAsync(EmailRecipient entity) => foundation.RaiseDeleteAsync(entity);
}

public interface IEmailRecipientOrchestrationService
{
    IQueryable<EmailRecipient> RetrieveAll();
    IQueryable<EmailRecipient> RetrieveWriteable();
    ValueTask<EmailRecipient> AddAsync(EmailRecipient entity, CancellationToken cancellationToken = default);
    ValueTask<EmailRecipient> ModifyAsync(EmailRecipient entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(EmailRecipient entity, CancellationToken cancellationToken = default);
}
internal sealed class EmailRecipientOrchestrationService(IEmailRecipientProcessingService processing, IEmailRecipientEventProcessingService events) : IEmailRecipientOrchestrationService
{
    public IQueryable<EmailRecipient> RetrieveAll() => processing.RetrieveAll();
    public IQueryable<EmailRecipient> RetrieveWriteable() => processing.RetrieveWriteable();
    public async ValueTask<EmailRecipient> AddAsync(EmailRecipient entity, CancellationToken cancellationToken = default) { EmailRecipient persisted = await processing.AddAsync(entity, cancellationToken); await events.RaiseAddAsync(persisted); return persisted; }
    public async ValueTask<EmailRecipient> ModifyAsync(EmailRecipient entity, CancellationToken cancellationToken = default) { EmailRecipient persisted = await processing.ModifyAsync(entity, cancellationToken); await events.RaiseUpdateAsync(persisted); return persisted; }
    public async ValueTask RemoveAsync(EmailRecipient entity, CancellationToken cancellationToken = default) { await processing.RemoveAsync(entity, cancellationToken); await events.RaiseDeleteAsync(entity); }
}
