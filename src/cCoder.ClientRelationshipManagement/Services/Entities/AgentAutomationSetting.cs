using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Services.Entities;

public interface IAgentAutomationSettingStorageBroker
{
    IQueryable<AgentAutomationSetting> SelectAll();
    ValueTask<AgentAutomationSetting> InsertAsync(AgentAutomationSetting entity, CancellationToken cancellationToken = default);
    ValueTask<AgentAutomationSetting> UpdateAsync(AgentAutomationSetting entity, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(AgentAutomationSetting entity, CancellationToken cancellationToken = default);
}

internal sealed class AgentAutomationSettingStorageBroker : IAgentAutomationSettingStorageBroker
{
    readonly ClientRelationshipDbContext context;
    public AgentAutomationSettingStorageBroker(ClientRelationshipDbContext context) => this.context = context;
    public IQueryable<AgentAutomationSetting> SelectAll() => context.Set<AgentAutomationSetting>();
    public async ValueTask<AgentAutomationSetting> InsertAsync(AgentAutomationSetting entity, CancellationToken cancellationToken = default) { context.Set<AgentAutomationSetting>().Add(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask<AgentAutomationSetting> UpdateAsync(AgentAutomationSetting entity, CancellationToken cancellationToken = default) { AgentAutomationSetting local = context.Set<AgentAutomationSetting>().Local.FirstOrDefault(item => item.Id == entity.Id); if (local is null) context.Set<AgentAutomationSetting>().Update(entity); else context.Entry(local).CurrentValues.SetValues(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask DeleteAsync(AgentAutomationSetting entity, CancellationToken cancellationToken = default) { context.Set<AgentAutomationSetting>().Remove(entity); await context.SaveChangesAsync(cancellationToken); }

}

public interface IAgentAutomationSettingFoundationService
{
    IQueryable<AgentAutomationSetting> RetrieveAll();
    IQueryable<AgentAutomationSetting> RetrieveWriteable();
    ValueTask<AgentAutomationSetting> AddAsync(AgentAutomationSetting entity, CancellationToken cancellationToken = default);
    ValueTask<AgentAutomationSetting> ModifyAsync(AgentAutomationSetting entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(AgentAutomationSetting entity, CancellationToken cancellationToken = default);
}

internal sealed class AgentAutomationSettingFoundationService(IAgentAutomationSettingStorageBroker broker, ICRMAuthInfo auth) : IAgentAutomationSettingFoundationService
{
    string[] Readable => auth.ReadableTenants?.Length > 0 ? auth.ReadableTenants : auth.WriteableTenants ?? [];
    string[] Writeable => auth.WriteableTenants ?? [];
    public IQueryable<AgentAutomationSetting> RetrieveAll() => Scope(broker.SelectAll(), Readable);
    public IQueryable<AgentAutomationSetting> RetrieveWriteable() => Scope(broker.SelectAll(), Writeable);

    public async ValueTask<AgentAutomationSetting> AddAsync(AgentAutomationSetting entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        if (string.IsNullOrWhiteSpace(entity.UserId)) entity.UserId = auth.SSOUserId;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        AgentAutomationSetting storage = Copy(entity);
        storage.CreatedOn = now;
        storage.CreatedBy = auth.SSOUserId;
        storage.LastUpdated = now;
        storage.LastUpdatedBy = auth.SSOUserId;
        AgentAutomationSetting persisted = await broker.InsertAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask<AgentAutomationSetting> ModifyAsync(AgentAutomationSetting entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        AgentAutomationSetting existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        AgentAutomationSetting storage = Copy(entity);
        storage.CreatedOn = existing.CreatedOn;
        storage.CreatedBy = existing.CreatedBy;
        storage.LastUpdated = DateTimeOffset.UtcNow;
        storage.LastUpdatedBy = auth.SSOUserId;
        AgentAutomationSetting persisted = await broker.UpdateAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask RemoveAsync(AgentAutomationSetting entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        AgentAutomationSetting existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        await broker.DeleteAsync(existing, cancellationToken);
    }

    IQueryable<AgentAutomationSetting> Scope(IQueryable<AgentAutomationSetting> source, string[] tenants) => source.Where(item => item.UserId == auth.SSOUserId);

    static AgentAutomationSetting Copy(AgentAutomationSetting source) => new()
    {
            Id = source.Id,
            CreatedBy = source.CreatedBy,
            LastUpdatedBy = source.LastUpdatedBy,
            CreatedOn = source.CreatedOn,
            LastUpdated = source.LastUpdated,
            UserId = source.UserId,
            AutoApproveProcessEmails = source.AutoApproveProcessEmails,
            LastMailboxSyncOn = source.LastMailboxSyncOn,
            MailboxEvidenceBackfillCompletedOn = source.MailboxEvidenceBackfillCompletedOn,
            SelectedAiProfileKey = source.SelectedAiProfileKey,
            SelectedAiModel = source.SelectedAiModel,
            ApprovalAgentConcurrency = source.ApprovalAgentConcurrency,
            LeadAiProfileKey = source.LeadAiProfileKey,
            LeadAiModel = source.LeadAiModel,
            LeadAgentConcurrency = source.LeadAgentConcurrency,
            OpportunityAiProfileKey = source.OpportunityAiProfileKey,
            OpportunityAiModel = source.OpportunityAiModel,
            OpportunityAgentConcurrency = source.OpportunityAgentConcurrency,
            ClientAiProfileKey = source.ClientAiProfileKey,
            ClientAiModel = source.ClientAiModel,
            ClientAgentConcurrency = source.ClientAgentConcurrency,
    };

    static void CopyPersisted(AgentAutomationSetting source, AgentAutomationSetting target)
    {
        target.Id = source.Id;
        target.CreatedBy = source.CreatedBy;
        target.LastUpdatedBy = source.LastUpdatedBy;
        target.CreatedOn = source.CreatedOn;
        target.LastUpdated = source.LastUpdated;
        target.UserId = source.UserId;
        target.AutoApproveProcessEmails = source.AutoApproveProcessEmails;
        target.LastMailboxSyncOn = source.LastMailboxSyncOn;
        target.MailboxEvidenceBackfillCompletedOn = source.MailboxEvidenceBackfillCompletedOn;
        target.SelectedAiProfileKey = source.SelectedAiProfileKey;
        target.SelectedAiModel = source.SelectedAiModel;
        target.ApprovalAgentConcurrency = source.ApprovalAgentConcurrency;
        target.LeadAiProfileKey = source.LeadAiProfileKey;
        target.LeadAiModel = source.LeadAiModel;
        target.LeadAgentConcurrency = source.LeadAgentConcurrency;
        target.OpportunityAiProfileKey = source.OpportunityAiProfileKey;
        target.OpportunityAiModel = source.OpportunityAiModel;
        target.OpportunityAgentConcurrency = source.OpportunityAgentConcurrency;
        target.ClientAiProfileKey = source.ClientAiProfileKey;
        target.ClientAiModel = source.ClientAiModel;
        target.ClientAgentConcurrency = source.ClientAgentConcurrency;
    }


    void EnsureAuthenticated()
    {
        if (string.IsNullOrWhiteSpace(auth.SSOUserId) || string.Equals(auth.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("A signed-in CRM user is required.");
    }
}

public interface IAgentAutomationSettingProcessingService : IAgentAutomationSettingFoundationService { }
internal sealed class AgentAutomationSettingProcessingService(IAgentAutomationSettingFoundationService foundation) : IAgentAutomationSettingProcessingService
{
    public IQueryable<AgentAutomationSetting> RetrieveAll() => foundation.RetrieveAll();
    public IQueryable<AgentAutomationSetting> RetrieveWriteable() => foundation.RetrieveWriteable();
    public ValueTask<AgentAutomationSetting> AddAsync(AgentAutomationSetting entity, CancellationToken cancellationToken = default) => foundation.AddAsync(entity, cancellationToken);
    public ValueTask<AgentAutomationSetting> ModifyAsync(AgentAutomationSetting entity, CancellationToken cancellationToken = default) => foundation.ModifyAsync(entity, cancellationToken);
    public ValueTask RemoveAsync(AgentAutomationSetting entity, CancellationToken cancellationToken = default) => foundation.RemoveAsync(entity, cancellationToken);
}

public interface IAgentAutomationSettingEventBroker
{
    ValueTask RaiseAddAsync(EventMessage<AgentAutomationSetting> message);
    ValueTask RaiseUpdateAsync(EventMessage<AgentAutomationSetting> message);
    ValueTask RaiseDeleteAsync(EventMessage<AgentAutomationSetting> message);
}
internal sealed class AgentAutomationSettingEventBroker(IEventHub eventHub) : IAgentAutomationSettingEventBroker
{
    public ValueTask RaiseAddAsync(EventMessage<AgentAutomationSetting> message) => eventHub.RaiseEventAsync("agent_automation_setting_add", message);
    public ValueTask RaiseUpdateAsync(EventMessage<AgentAutomationSetting> message) => eventHub.RaiseEventAsync("agent_automation_setting_update", message);
    public ValueTask RaiseDeleteAsync(EventMessage<AgentAutomationSetting> message) => eventHub.RaiseEventAsync("agent_automation_setting_delete", message);
}
public interface IAgentAutomationSettingEventFoundationService
{
    ValueTask RaiseAddAsync(AgentAutomationSetting entity);
    ValueTask RaiseUpdateAsync(AgentAutomationSetting entity);
    ValueTask RaiseDeleteAsync(AgentAutomationSetting entity);
}
internal sealed class AgentAutomationSettingEventFoundationService(IAgentAutomationSettingEventBroker broker, ICRMAuthInfo auth) : IAgentAutomationSettingEventFoundationService
{
    EventMessage<AgentAutomationSetting> Message(AgentAutomationSetting entity) => new() { AuthInfo = new EventAuthInfo { SSOUserId = auth.SSOUserId }, Data = entity };
    public ValueTask RaiseAddAsync(AgentAutomationSetting entity) => broker.RaiseAddAsync(Message(entity));
    public ValueTask RaiseUpdateAsync(AgentAutomationSetting entity) => broker.RaiseUpdateAsync(Message(entity));
    public ValueTask RaiseDeleteAsync(AgentAutomationSetting entity) => broker.RaiseDeleteAsync(Message(entity));
}
public interface IAgentAutomationSettingEventProcessingService : IAgentAutomationSettingEventFoundationService { }
internal sealed class AgentAutomationSettingEventProcessingService(IAgentAutomationSettingEventFoundationService foundation) : IAgentAutomationSettingEventProcessingService
{
    public ValueTask RaiseAddAsync(AgentAutomationSetting entity) => foundation.RaiseAddAsync(entity);
    public ValueTask RaiseUpdateAsync(AgentAutomationSetting entity) => foundation.RaiseUpdateAsync(entity);
    public ValueTask RaiseDeleteAsync(AgentAutomationSetting entity) => foundation.RaiseDeleteAsync(entity);
}

public interface IAgentAutomationSettingOrchestrationService
{
    IQueryable<AgentAutomationSetting> RetrieveAll();
    IQueryable<AgentAutomationSetting> RetrieveWriteable();
    ValueTask<AgentAutomationSetting> AddAsync(AgentAutomationSetting entity, CancellationToken cancellationToken = default);
    ValueTask<AgentAutomationSetting> ModifyAsync(AgentAutomationSetting entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(AgentAutomationSetting entity, CancellationToken cancellationToken = default);
}
internal sealed class AgentAutomationSettingOrchestrationService(IAgentAutomationSettingProcessingService processing, IAgentAutomationSettingEventProcessingService events) : IAgentAutomationSettingOrchestrationService
{
    public IQueryable<AgentAutomationSetting> RetrieveAll() => processing.RetrieveAll();
    public IQueryable<AgentAutomationSetting> RetrieveWriteable() => processing.RetrieveWriteable();
    public async ValueTask<AgentAutomationSetting> AddAsync(AgentAutomationSetting entity, CancellationToken cancellationToken = default) { AgentAutomationSetting persisted = await processing.AddAsync(entity, cancellationToken); await events.RaiseAddAsync(persisted); return persisted; }
    public async ValueTask<AgentAutomationSetting> ModifyAsync(AgentAutomationSetting entity, CancellationToken cancellationToken = default) { AgentAutomationSetting persisted = await processing.ModifyAsync(entity, cancellationToken); await events.RaiseUpdateAsync(persisted); return persisted; }
    public async ValueTask RemoveAsync(AgentAutomationSetting entity, CancellationToken cancellationToken = default) { await processing.RemoveAsync(entity, cancellationToken); await events.RaiseDeleteAsync(entity); }
}
