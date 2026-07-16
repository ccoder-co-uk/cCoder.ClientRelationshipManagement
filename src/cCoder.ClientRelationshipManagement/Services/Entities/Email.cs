using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Services.Entities;

public interface IEmailStorageBroker
{
    IQueryable<Email> SelectAll();
    ValueTask<Email> InsertAsync(Email entity, CancellationToken cancellationToken = default);
    ValueTask<Email> UpdateAsync(Email entity, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(Email entity, CancellationToken cancellationToken = default);
}

internal sealed class EmailStorageBroker : IEmailStorageBroker
{
    readonly ClientRelationshipDbContext context;
    public EmailStorageBroker(ClientRelationshipDbContext context) => this.context = context;
    public IQueryable<Email> SelectAll() => context.Set<Email>();
    public async ValueTask<Email> InsertAsync(Email entity, CancellationToken cancellationToken = default) { context.Set<Email>().Add(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask<Email> UpdateAsync(Email entity, CancellationToken cancellationToken = default) { Email local = context.Set<Email>().Local.FirstOrDefault(item => item.Id == entity.Id); if (local is null) context.Set<Email>().Update(entity); else context.Entry(local).CurrentValues.SetValues(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask DeleteAsync(Email entity, CancellationToken cancellationToken = default) { context.Set<Email>().Remove(entity); await context.SaveChangesAsync(cancellationToken); }

}

public interface IEmailFoundationService
{
    IQueryable<Email> RetrieveAll();
    IQueryable<Email> RetrieveWriteable();
    ValueTask<Email> AddAsync(Email entity, CancellationToken cancellationToken = default);
    ValueTask<Email> ModifyAsync(Email entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(Email entity, CancellationToken cancellationToken = default);
}

internal sealed class EmailFoundationService(IEmailStorageBroker broker, ICRMAuthInfo auth) : IEmailFoundationService
{
    string[] Readable => auth.ReadableTenants?.Length > 0 ? auth.ReadableTenants : auth.WriteableTenants ?? [];
    string[] Writeable => auth.WriteableTenants ?? [];
    public IQueryable<Email> RetrieveAll() => Scope(broker.SelectAll(), Readable);
    public IQueryable<Email> RetrieveWriteable() => Scope(broker.SelectAll(), Writeable);

    public async ValueTask<Email> AddAsync(Email entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        if (Writeable.Length == 0) throw new UnauthorizedAccessException("The user has no writable CRM tenant.");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Email storage = Copy(entity);
        storage.CreatedOn = now;
        storage.CreatedBy = auth.SSOUserId;
        storage.LastUpdated = now;
        storage.LastUpdatedBy = auth.SSOUserId;
        Email persisted = await broker.InsertAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask<Email> ModifyAsync(Email entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        Email existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        Email storage = Copy(entity);
        storage.CreatedOn = existing.CreatedOn;
        storage.CreatedBy = existing.CreatedBy;
        storage.LastUpdated = DateTimeOffset.UtcNow;
        storage.LastUpdatedBy = auth.SSOUserId;
        Email persisted = await broker.UpdateAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask RemoveAsync(Email entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        Email existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        await broker.DeleteAsync(existing, cancellationToken);
    }

    IQueryable<Email> Scope(IQueryable<Email> source, string[] tenants) => source.Where(item => tenants.Contains(item.TenantCompanyRelationship.TenantId));

    static Email Copy(Email source) => new()
    {
            Id = source.Id,
            CreatedBy = source.CreatedBy,
            LastUpdatedBy = source.LastUpdatedBy,
            CreatedOn = source.CreatedOn,
            LastUpdated = source.LastUpdated,
            LegacyId = source.LegacyId,
            TenantCompanyRelationshipId = source.TenantCompanyRelationshipId,
            OpportunityId = source.OpportunityId,
            ClientAccountId = source.ClientAccountId,
            MaterialId = source.MaterialId,
            CompanyContactId = source.CompanyContactId,
            SenderUserId = source.SenderUserId,
            FromDisplayName = source.FromDisplayName,
            FromEmailAddress = source.FromEmailAddress,
            ReplyToAddresses = source.ReplyToAddresses,
            ToAddresses = source.ToAddresses,
            CcAddresses = source.CcAddresses,
            BccAddresses = source.BccAddresses,
            Subject = source.Subject,
            BodyHtml = source.BodyHtml,
            BodyText = source.BodyText,
            IsBodyHtml = source.IsBodyHtml,
            State = source.State,
            ApprovedOn = source.ApprovedOn,
            ApprovedBy = source.ApprovedBy,
            ScheduledSendTimeUtc = source.ScheduledSendTimeUtc,
            LastSendAttemptOn = source.LastSendAttemptOn,
            SentOn = source.SentOn,
            ExternalMessageId = source.ExternalMessageId,
            LastError = source.LastError,
            SendFailureCount = source.SendFailureCount,
    };

    static void CopyPersisted(Email source, Email target)
    {
        target.Id = source.Id;
        target.CreatedBy = source.CreatedBy;
        target.LastUpdatedBy = source.LastUpdatedBy;
        target.CreatedOn = source.CreatedOn;
        target.LastUpdated = source.LastUpdated;
        target.LegacyId = source.LegacyId;
        target.TenantCompanyRelationshipId = source.TenantCompanyRelationshipId;
        target.OpportunityId = source.OpportunityId;
        target.ClientAccountId = source.ClientAccountId;
        target.MaterialId = source.MaterialId;
        target.CompanyContactId = source.CompanyContactId;
        target.SenderUserId = source.SenderUserId;
        target.FromDisplayName = source.FromDisplayName;
        target.FromEmailAddress = source.FromEmailAddress;
        target.ReplyToAddresses = source.ReplyToAddresses;
        target.ToAddresses = source.ToAddresses;
        target.CcAddresses = source.CcAddresses;
        target.BccAddresses = source.BccAddresses;
        target.Subject = source.Subject;
        target.BodyHtml = source.BodyHtml;
        target.BodyText = source.BodyText;
        target.IsBodyHtml = source.IsBodyHtml;
        target.State = source.State;
        target.ApprovedOn = source.ApprovedOn;
        target.ApprovedBy = source.ApprovedBy;
        target.ScheduledSendTimeUtc = source.ScheduledSendTimeUtc;
        target.LastSendAttemptOn = source.LastSendAttemptOn;
        target.SentOn = source.SentOn;
        target.ExternalMessageId = source.ExternalMessageId;
        target.LastError = source.LastError;
        target.SendFailureCount = source.SendFailureCount;
    }


    void EnsureAuthenticated()
    {
        if (string.IsNullOrWhiteSpace(auth.SSOUserId) || string.Equals(auth.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("A signed-in CRM user is required.");
    }
}

public interface IEmailProcessingService : IEmailFoundationService { }
internal sealed class EmailProcessingService(IEmailFoundationService foundation) : IEmailProcessingService
{
    public IQueryable<Email> RetrieveAll() => foundation.RetrieveAll();
    public IQueryable<Email> RetrieveWriteable() => foundation.RetrieveWriteable();
    public ValueTask<Email> AddAsync(Email entity, CancellationToken cancellationToken = default) => foundation.AddAsync(entity, cancellationToken);
    public ValueTask<Email> ModifyAsync(Email entity, CancellationToken cancellationToken = default) => foundation.ModifyAsync(entity, cancellationToken);
    public ValueTask RemoveAsync(Email entity, CancellationToken cancellationToken = default) => foundation.RemoveAsync(entity, cancellationToken);
}

public interface IEmailEventBroker
{
    ValueTask RaiseAddAsync(EventMessage<Email> message);
    ValueTask RaiseUpdateAsync(EventMessage<Email> message);
    ValueTask RaiseDeleteAsync(EventMessage<Email> message);
}
internal sealed class EmailEventBroker(IEventHub eventHub) : IEmailEventBroker
{
    public ValueTask RaiseAddAsync(EventMessage<Email> message) => eventHub.RaiseEventAsync("email_add", message);
    public ValueTask RaiseUpdateAsync(EventMessage<Email> message) => eventHub.RaiseEventAsync("email_update", message);
    public ValueTask RaiseDeleteAsync(EventMessage<Email> message) => eventHub.RaiseEventAsync("email_delete", message);
}
public interface IEmailEventFoundationService
{
    ValueTask RaiseAddAsync(Email entity);
    ValueTask RaiseUpdateAsync(Email entity);
    ValueTask RaiseDeleteAsync(Email entity);
}
internal sealed class EmailEventFoundationService(IEmailEventBroker broker, ICRMAuthInfo auth) : IEmailEventFoundationService
{
    EventMessage<Email> Message(Email entity) => new() { AuthInfo = new EventAuthInfo { SSOUserId = auth.SSOUserId }, Data = entity };
    public ValueTask RaiseAddAsync(Email entity) => broker.RaiseAddAsync(Message(entity));
    public ValueTask RaiseUpdateAsync(Email entity) => broker.RaiseUpdateAsync(Message(entity));
    public ValueTask RaiseDeleteAsync(Email entity) => broker.RaiseDeleteAsync(Message(entity));
}
public interface IEmailEventProcessingService : IEmailEventFoundationService { }
internal sealed class EmailEventProcessingService(IEmailEventFoundationService foundation) : IEmailEventProcessingService
{
    public ValueTask RaiseAddAsync(Email entity) => foundation.RaiseAddAsync(entity);
    public ValueTask RaiseUpdateAsync(Email entity) => foundation.RaiseUpdateAsync(entity);
    public ValueTask RaiseDeleteAsync(Email entity) => foundation.RaiseDeleteAsync(entity);
}

public interface IEmailOrchestrationService
{
    IQueryable<Email> RetrieveAll();
    IQueryable<Email> RetrieveWriteable();
    ValueTask<Email> AddAsync(Email entity, CancellationToken cancellationToken = default);
    ValueTask<Email> ModifyAsync(Email entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(Email entity, CancellationToken cancellationToken = default);
}
internal sealed class EmailOrchestrationService(IEmailProcessingService processing, IEmailEventProcessingService events) : IEmailOrchestrationService
{
    public IQueryable<Email> RetrieveAll() => processing.RetrieveAll();
    public IQueryable<Email> RetrieveWriteable() => processing.RetrieveWriteable();
    public async ValueTask<Email> AddAsync(Email entity, CancellationToken cancellationToken = default) { Email persisted = await processing.AddAsync(entity, cancellationToken); await events.RaiseAddAsync(persisted); return persisted; }
    public async ValueTask<Email> ModifyAsync(Email entity, CancellationToken cancellationToken = default) { Email persisted = await processing.ModifyAsync(entity, cancellationToken); await events.RaiseUpdateAsync(persisted); return persisted; }
    public async ValueTask RemoveAsync(Email entity, CancellationToken cancellationToken = default) { await processing.RemoveAsync(entity, cancellationToken); await events.RaiseDeleteAsync(entity); }
}
