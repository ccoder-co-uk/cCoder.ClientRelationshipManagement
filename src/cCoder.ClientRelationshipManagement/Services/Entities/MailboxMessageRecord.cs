using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Services.Entities;

public interface IMailboxMessageRecordStorageBroker
{
    IQueryable<MailboxMessageRecord> SelectAll();
    ValueTask<MailboxMessageRecord> InsertAsync(MailboxMessageRecord entity, CancellationToken cancellationToken = default);
    ValueTask<MailboxMessageRecord> UpdateAsync(MailboxMessageRecord entity, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(MailboxMessageRecord entity, CancellationToken cancellationToken = default);
}

internal sealed class MailboxMessageRecordStorageBroker : IMailboxMessageRecordStorageBroker
{
    readonly ClientRelationshipDbContext context;
    public MailboxMessageRecordStorageBroker(ClientRelationshipDbContext context) => this.context = context;
    public IQueryable<MailboxMessageRecord> SelectAll() => context.Set<MailboxMessageRecord>();
    public async ValueTask<MailboxMessageRecord> InsertAsync(MailboxMessageRecord entity, CancellationToken cancellationToken = default) { context.Set<MailboxMessageRecord>().Add(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask<MailboxMessageRecord> UpdateAsync(MailboxMessageRecord entity, CancellationToken cancellationToken = default) { MailboxMessageRecord local = context.Set<MailboxMessageRecord>().Local.FirstOrDefault(item => item.Id == entity.Id); if (local is null) context.Set<MailboxMessageRecord>().Update(entity); else context.Entry(local).CurrentValues.SetValues(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask DeleteAsync(MailboxMessageRecord entity, CancellationToken cancellationToken = default) { context.Set<MailboxMessageRecord>().Remove(entity); await context.SaveChangesAsync(cancellationToken); }

}

public interface IMailboxMessageRecordFoundationService
{
    IQueryable<MailboxMessageRecord> RetrieveAll();
    IQueryable<MailboxMessageRecord> RetrieveWriteable();
    ValueTask<MailboxMessageRecord> AddAsync(MailboxMessageRecord entity, CancellationToken cancellationToken = default);
    ValueTask<MailboxMessageRecord> ModifyAsync(MailboxMessageRecord entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(MailboxMessageRecord entity, CancellationToken cancellationToken = default);
}

internal sealed class MailboxMessageRecordFoundationService(IMailboxMessageRecordStorageBroker broker, ICRMAuthInfo auth) : IMailboxMessageRecordFoundationService
{
    string[] Readable => auth.ReadableTenants?.Length > 0 ? auth.ReadableTenants : auth.WriteableTenants ?? [];
    string[] Writeable => auth.WriteableTenants ?? [];
    public IQueryable<MailboxMessageRecord> RetrieveAll() => Scope(broker.SelectAll(), Readable);
    public IQueryable<MailboxMessageRecord> RetrieveWriteable() => Scope(broker.SelectAll(), Writeable);

    public async ValueTask<MailboxMessageRecord> AddAsync(MailboxMessageRecord entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        if (Writeable.Length == 0) throw new UnauthorizedAccessException("The user has no writable CRM tenant.");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        MailboxMessageRecord storage = Copy(entity);
        storage.CreatedOn = now;
        storage.CreatedBy = auth.SSOUserId;
        storage.LastUpdated = now;
        storage.LastUpdatedBy = auth.SSOUserId;
        MailboxMessageRecord persisted = await broker.InsertAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask<MailboxMessageRecord> ModifyAsync(MailboxMessageRecord entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        MailboxMessageRecord existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        MailboxMessageRecord storage = Copy(entity);
        storage.CreatedOn = existing.CreatedOn;
        storage.CreatedBy = existing.CreatedBy;
        storage.LastUpdated = DateTimeOffset.UtcNow;
        storage.LastUpdatedBy = auth.SSOUserId;
        MailboxMessageRecord persisted = await broker.UpdateAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask RemoveAsync(MailboxMessageRecord entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        MailboxMessageRecord existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        await broker.DeleteAsync(existing, cancellationToken);
    }

    IQueryable<MailboxMessageRecord> Scope(IQueryable<MailboxMessageRecord> source, string[] tenants) =>
        source.Where(item =>
            (item.TenantCompanyRelationshipId.HasValue
                && tenants.Contains(item.TenantCompanyRelationship.TenantId))
            || (!item.TenantCompanyRelationshipId.HasValue
                && item.CreatedBy == auth.SSOUserId));

    static MailboxMessageRecord Copy(MailboxMessageRecord source) => new()
    {
            Id = source.Id,
            CreatedBy = source.CreatedBy,
            LastUpdatedBy = source.LastUpdatedBy,
            CreatedOn = source.CreatedOn,
            LastUpdated = source.LastUpdated,
            ExternalId = source.ExternalId,
            InternetMessageId = source.InternetMessageId,
            ConversationId = source.ConversationId,
            InReplyTo = source.InReplyTo,
            References = source.References,
            FromAddress = source.FromAddress,
            ToAddresses = source.ToAddresses,
            CcAddresses = source.CcAddresses,
            Subject = source.Subject,
            Body = source.Body,
            IsBodyHtml = source.IsBodyHtml,
            ReceivedOn = source.ReceivedOn,
            TenantCompanyRelationshipId = source.TenantCompanyRelationshipId,
            OpportunityId = source.OpportunityId,
            CompanyContactId = source.CompanyContactId,
    };

    static void CopyPersisted(MailboxMessageRecord source, MailboxMessageRecord target)
    {
        target.Id = source.Id;
        target.CreatedBy = source.CreatedBy;
        target.LastUpdatedBy = source.LastUpdatedBy;
        target.CreatedOn = source.CreatedOn;
        target.LastUpdated = source.LastUpdated;
        target.ExternalId = source.ExternalId;
        target.InternetMessageId = source.InternetMessageId;
        target.ConversationId = source.ConversationId;
        target.InReplyTo = source.InReplyTo;
        target.References = source.References;
        target.FromAddress = source.FromAddress;
        target.ToAddresses = source.ToAddresses;
        target.CcAddresses = source.CcAddresses;
        target.Subject = source.Subject;
        target.Body = source.Body;
        target.IsBodyHtml = source.IsBodyHtml;
        target.ReceivedOn = source.ReceivedOn;
        target.TenantCompanyRelationshipId = source.TenantCompanyRelationshipId;
        target.OpportunityId = source.OpportunityId;
        target.CompanyContactId = source.CompanyContactId;
    }


    void EnsureAuthenticated()
    {
        if (string.IsNullOrWhiteSpace(auth.SSOUserId) || string.Equals(auth.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("A signed-in CRM user is required.");
    }
}

public interface IMailboxMessageRecordProcessingService : IMailboxMessageRecordFoundationService { }
internal sealed class MailboxMessageRecordProcessingService(IMailboxMessageRecordFoundationService foundation) : IMailboxMessageRecordProcessingService
{
    public IQueryable<MailboxMessageRecord> RetrieveAll() => foundation.RetrieveAll();
    public IQueryable<MailboxMessageRecord> RetrieveWriteable() => foundation.RetrieveWriteable();
    public ValueTask<MailboxMessageRecord> AddAsync(MailboxMessageRecord entity, CancellationToken cancellationToken = default) => foundation.AddAsync(entity, cancellationToken);
    public ValueTask<MailboxMessageRecord> ModifyAsync(MailboxMessageRecord entity, CancellationToken cancellationToken = default) => foundation.ModifyAsync(entity, cancellationToken);
    public ValueTask RemoveAsync(MailboxMessageRecord entity, CancellationToken cancellationToken = default) => foundation.RemoveAsync(entity, cancellationToken);
}

public interface IMailboxMessageRecordEventBroker
{
    ValueTask RaiseAddAsync(EventMessage<MailboxMessageRecord> message);
    ValueTask RaiseUpdateAsync(EventMessage<MailboxMessageRecord> message);
    ValueTask RaiseDeleteAsync(EventMessage<MailboxMessageRecord> message);
}
internal sealed class MailboxMessageRecordEventBroker(IEventHub eventHub) : IMailboxMessageRecordEventBroker
{
    public ValueTask RaiseAddAsync(EventMessage<MailboxMessageRecord> message) => eventHub.RaiseEventAsync("mailbox_message_record_add", message);
    public ValueTask RaiseUpdateAsync(EventMessage<MailboxMessageRecord> message) => eventHub.RaiseEventAsync("mailbox_message_record_update", message);
    public ValueTask RaiseDeleteAsync(EventMessage<MailboxMessageRecord> message) => eventHub.RaiseEventAsync("mailbox_message_record_delete", message);
}
public interface IMailboxMessageRecordEventFoundationService
{
    ValueTask RaiseAddAsync(MailboxMessageRecord entity);
    ValueTask RaiseUpdateAsync(MailboxMessageRecord entity);
    ValueTask RaiseDeleteAsync(MailboxMessageRecord entity);
}
internal sealed class MailboxMessageRecordEventFoundationService(IMailboxMessageRecordEventBroker broker, ICRMAuthInfo auth) : IMailboxMessageRecordEventFoundationService
{
    EventMessage<MailboxMessageRecord> Message(MailboxMessageRecord entity) => new() { AuthInfo = new EventAuthInfo { SSOUserId = auth.SSOUserId }, Data = entity };
    public ValueTask RaiseAddAsync(MailboxMessageRecord entity) => broker.RaiseAddAsync(Message(entity));
    public ValueTask RaiseUpdateAsync(MailboxMessageRecord entity) => broker.RaiseUpdateAsync(Message(entity));
    public ValueTask RaiseDeleteAsync(MailboxMessageRecord entity) => broker.RaiseDeleteAsync(Message(entity));
}
public interface IMailboxMessageRecordEventProcessingService : IMailboxMessageRecordEventFoundationService { }
internal sealed class MailboxMessageRecordEventProcessingService(IMailboxMessageRecordEventFoundationService foundation) : IMailboxMessageRecordEventProcessingService
{
    public ValueTask RaiseAddAsync(MailboxMessageRecord entity) => foundation.RaiseAddAsync(entity);
    public ValueTask RaiseUpdateAsync(MailboxMessageRecord entity) => foundation.RaiseUpdateAsync(entity);
    public ValueTask RaiseDeleteAsync(MailboxMessageRecord entity) => foundation.RaiseDeleteAsync(entity);
}

public interface IMailboxMessageRecordOrchestrationService
{
    IQueryable<MailboxMessageRecord> RetrieveAll();
    IQueryable<MailboxMessageRecord> RetrieveWriteable();
    ValueTask<MailboxMessageRecord> AddAsync(MailboxMessageRecord entity, CancellationToken cancellationToken = default);
    ValueTask<MailboxMessageRecord> ModifyAsync(MailboxMessageRecord entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(MailboxMessageRecord entity, CancellationToken cancellationToken = default);
}
internal sealed class MailboxMessageRecordOrchestrationService(IMailboxMessageRecordProcessingService processing, IMailboxMessageRecordEventProcessingService events) : IMailboxMessageRecordOrchestrationService
{
    public IQueryable<MailboxMessageRecord> RetrieveAll() => processing.RetrieveAll();
    public IQueryable<MailboxMessageRecord> RetrieveWriteable() => processing.RetrieveWriteable();
    public async ValueTask<MailboxMessageRecord> AddAsync(MailboxMessageRecord entity, CancellationToken cancellationToken = default) { MailboxMessageRecord persisted = await processing.AddAsync(entity, cancellationToken); await events.RaiseAddAsync(persisted); return persisted; }
    public async ValueTask<MailboxMessageRecord> ModifyAsync(MailboxMessageRecord entity, CancellationToken cancellationToken = default) { MailboxMessageRecord persisted = await processing.ModifyAsync(entity, cancellationToken); await events.RaiseUpdateAsync(persisted); return persisted; }
    public async ValueTask RemoveAsync(MailboxMessageRecord entity, CancellationToken cancellationToken = default) { await processing.RemoveAsync(entity, cancellationToken); await events.RaiseDeleteAsync(entity); }
}
