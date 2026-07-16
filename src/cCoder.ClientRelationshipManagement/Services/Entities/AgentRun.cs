using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Services.Entities;

public interface IAgentRunStorageBroker
{
    IQueryable<AgentRun> SelectAll();
    ValueTask<AgentRun> InsertAsync(AgentRun entity, CancellationToken cancellationToken = default);
    ValueTask<AgentRun> UpdateAsync(AgentRun entity, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(AgentRun entity, CancellationToken cancellationToken = default);
}

internal sealed class AgentRunStorageBroker : IAgentRunStorageBroker
{
    readonly ClientRelationshipDbContext context;
    public AgentRunStorageBroker(ClientRelationshipDbContext context) => this.context = context;
    public IQueryable<AgentRun> SelectAll() => context.Set<AgentRun>();
    public async ValueTask<AgentRun> InsertAsync(AgentRun entity, CancellationToken cancellationToken = default) { context.Set<AgentRun>().Add(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask<AgentRun> UpdateAsync(AgentRun entity, CancellationToken cancellationToken = default) { AgentRun local = context.Set<AgentRun>().Local.FirstOrDefault(item => item.Id == entity.Id); if (local is null) context.Set<AgentRun>().Update(entity); else context.Entry(local).CurrentValues.SetValues(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask DeleteAsync(AgentRun entity, CancellationToken cancellationToken = default) { context.Set<AgentRun>().Remove(entity); await context.SaveChangesAsync(cancellationToken); }

}

public interface IAgentRunFoundationService
{
    IQueryable<AgentRun> RetrieveAll();
    IQueryable<AgentRun> RetrieveWriteable();
    ValueTask<AgentRun> AddAsync(AgentRun entity, CancellationToken cancellationToken = default);
    ValueTask<AgentRun> ModifyAsync(AgentRun entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(AgentRun entity, CancellationToken cancellationToken = default);
}

internal sealed class AgentRunFoundationService(IAgentRunStorageBroker broker, ICRMAuthInfo auth) : IAgentRunFoundationService
{
    string[] Readable => auth.ReadableTenants?.Length > 0 ? auth.ReadableTenants : auth.WriteableTenants ?? [];
    string[] Writeable => auth.WriteableTenants ?? [];
    public IQueryable<AgentRun> RetrieveAll() => Scope(broker.SelectAll(), Readable);
    public IQueryable<AgentRun> RetrieveWriteable() => Scope(broker.SelectAll(), Writeable);

    public async ValueTask<AgentRun> AddAsync(AgentRun entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        if (string.IsNullOrWhiteSpace(entity.ExecutionUserId)) entity.ExecutionUserId = auth.SSOUserId;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        AgentRun storage = Copy(entity);
        storage.CreatedOn = now;
        storage.CreatedBy = auth.SSOUserId;
        storage.LastUpdated = now;
        storage.LastUpdatedBy = auth.SSOUserId;
        AgentRun persisted = await broker.InsertAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask<AgentRun> ModifyAsync(AgentRun entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        AgentRun existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        AgentRun storage = Copy(entity);
        storage.CreatedOn = existing.CreatedOn;
        storage.CreatedBy = existing.CreatedBy;
        storage.LastUpdated = DateTimeOffset.UtcNow;
        storage.LastUpdatedBy = auth.SSOUserId;
        AgentRun persisted = await broker.UpdateAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask RemoveAsync(AgentRun entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        AgentRun existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        await broker.DeleteAsync(existing, cancellationToken);
    }

    IQueryable<AgentRun> Scope(IQueryable<AgentRun> source, string[] tenants) => source.Where(item => item.ExecutionUserId == auth.SSOUserId);

    static AgentRun Copy(AgentRun source) => new()
    {
            Id = source.Id,
            CreatedBy = source.CreatedBy,
            LastUpdatedBy = source.LastUpdatedBy,
            CreatedOn = source.CreatedOn,
            LastUpdated = source.LastUpdated,
            Kind = source.Kind,
            WorkLane = source.WorkLane,
            ProcessTaskId = source.ProcessTaskId,
            ProcessStepId = source.ProcessStepId,
            ProcessStepKey = source.ProcessStepKey,
            State = source.State,
            ExecutionUserId = source.ExecutionUserId,
            Provider = source.Provider,
            Model = source.Model,
            WorkingDirectory = source.WorkingDirectory,
            Summary = source.Summary,
            ErrorMessage = source.ErrorMessage,
            Iterations = source.Iterations,
            ProcessedItemCount = source.ProcessedItemCount,
            StartedOn = source.StartedOn,
            CompletedOn = source.CompletedOn,
    };

    static void CopyPersisted(AgentRun source, AgentRun target)
    {
        target.Id = source.Id;
        target.CreatedBy = source.CreatedBy;
        target.LastUpdatedBy = source.LastUpdatedBy;
        target.CreatedOn = source.CreatedOn;
        target.LastUpdated = source.LastUpdated;
        target.Kind = source.Kind;
        target.WorkLane = source.WorkLane;
        target.ProcessTaskId = source.ProcessTaskId;
        target.ProcessStepId = source.ProcessStepId;
        target.ProcessStepKey = source.ProcessStepKey;
        target.State = source.State;
        target.ExecutionUserId = source.ExecutionUserId;
        target.Provider = source.Provider;
        target.Model = source.Model;
        target.WorkingDirectory = source.WorkingDirectory;
        target.Summary = source.Summary;
        target.ErrorMessage = source.ErrorMessage;
        target.Iterations = source.Iterations;
        target.ProcessedItemCount = source.ProcessedItemCount;
        target.StartedOn = source.StartedOn;
        target.CompletedOn = source.CompletedOn;
    }


    void EnsureAuthenticated()
    {
        if (string.IsNullOrWhiteSpace(auth.SSOUserId) || string.Equals(auth.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("A signed-in CRM user is required.");
    }
}

public interface IAgentRunProcessingService : IAgentRunFoundationService { }
internal sealed class AgentRunProcessingService(IAgentRunFoundationService foundation) : IAgentRunProcessingService
{
    public IQueryable<AgentRun> RetrieveAll() => foundation.RetrieveAll();
    public IQueryable<AgentRun> RetrieveWriteable() => foundation.RetrieveWriteable();
    public ValueTask<AgentRun> AddAsync(AgentRun entity, CancellationToken cancellationToken = default) => foundation.AddAsync(entity, cancellationToken);
    public ValueTask<AgentRun> ModifyAsync(AgentRun entity, CancellationToken cancellationToken = default) => foundation.ModifyAsync(entity, cancellationToken);
    public ValueTask RemoveAsync(AgentRun entity, CancellationToken cancellationToken = default) => foundation.RemoveAsync(entity, cancellationToken);
}

public interface IAgentRunEventBroker
{
    ValueTask RaiseAddAsync(EventMessage<AgentRun> message);
    ValueTask RaiseUpdateAsync(EventMessage<AgentRun> message);
    ValueTask RaiseDeleteAsync(EventMessage<AgentRun> message);
}
internal sealed class AgentRunEventBroker(IEventHub eventHub) : IAgentRunEventBroker
{
    public ValueTask RaiseAddAsync(EventMessage<AgentRun> message) => eventHub.RaiseEventAsync("agent_run_add", message);
    public ValueTask RaiseUpdateAsync(EventMessage<AgentRun> message) => eventHub.RaiseEventAsync("agent_run_update", message);
    public ValueTask RaiseDeleteAsync(EventMessage<AgentRun> message) => eventHub.RaiseEventAsync("agent_run_delete", message);
}
public interface IAgentRunEventFoundationService
{
    ValueTask RaiseAddAsync(AgentRun entity);
    ValueTask RaiseUpdateAsync(AgentRun entity);
    ValueTask RaiseDeleteAsync(AgentRun entity);
}
internal sealed class AgentRunEventFoundationService(IAgentRunEventBroker broker, ICRMAuthInfo auth) : IAgentRunEventFoundationService
{
    EventMessage<AgentRun> Message(AgentRun entity) => new() { AuthInfo = new EventAuthInfo { SSOUserId = auth.SSOUserId }, Data = entity };
    public ValueTask RaiseAddAsync(AgentRun entity) => broker.RaiseAddAsync(Message(entity));
    public ValueTask RaiseUpdateAsync(AgentRun entity) => broker.RaiseUpdateAsync(Message(entity));
    public ValueTask RaiseDeleteAsync(AgentRun entity) => broker.RaiseDeleteAsync(Message(entity));
}
public interface IAgentRunEventProcessingService : IAgentRunEventFoundationService { }
internal sealed class AgentRunEventProcessingService(IAgentRunEventFoundationService foundation) : IAgentRunEventProcessingService
{
    public ValueTask RaiseAddAsync(AgentRun entity) => foundation.RaiseAddAsync(entity);
    public ValueTask RaiseUpdateAsync(AgentRun entity) => foundation.RaiseUpdateAsync(entity);
    public ValueTask RaiseDeleteAsync(AgentRun entity) => foundation.RaiseDeleteAsync(entity);
}

public interface IAgentRunOrchestrationService
{
    IQueryable<AgentRun> RetrieveAll();
    IQueryable<AgentRun> RetrieveWriteable();
    ValueTask<AgentRun> AddAsync(AgentRun entity, CancellationToken cancellationToken = default);
    ValueTask<AgentRun> ModifyAsync(AgentRun entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(AgentRun entity, CancellationToken cancellationToken = default);
}
internal sealed class AgentRunOrchestrationService(IAgentRunProcessingService processing, IAgentRunEventProcessingService events) : IAgentRunOrchestrationService
{
    public IQueryable<AgentRun> RetrieveAll() => processing.RetrieveAll();
    public IQueryable<AgentRun> RetrieveWriteable() => processing.RetrieveWriteable();
    public async ValueTask<AgentRun> AddAsync(AgentRun entity, CancellationToken cancellationToken = default) { AgentRun persisted = await processing.AddAsync(entity, cancellationToken); await events.RaiseAddAsync(persisted); return persisted; }
    public async ValueTask<AgentRun> ModifyAsync(AgentRun entity, CancellationToken cancellationToken = default) { AgentRun persisted = await processing.ModifyAsync(entity, cancellationToken); await events.RaiseUpdateAsync(persisted); return persisted; }
    public async ValueTask RemoveAsync(AgentRun entity, CancellationToken cancellationToken = default) { await processing.RemoveAsync(entity, cancellationToken); await events.RaiseDeleteAsync(entity); }
}
