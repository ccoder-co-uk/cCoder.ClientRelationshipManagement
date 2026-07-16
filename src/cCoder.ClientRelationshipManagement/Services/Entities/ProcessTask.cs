using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Services.Entities;

public interface IProcessTaskStorageBroker
{
    IQueryable<ProcessTask> SelectAll();
    ValueTask<ProcessTask> InsertAsync(ProcessTask entity, CancellationToken cancellationToken = default);
    ValueTask<ProcessTask> UpdateAsync(ProcessTask entity, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(ProcessTask entity, CancellationToken cancellationToken = default);
}

internal sealed class ProcessTaskStorageBroker : IProcessTaskStorageBroker
{
    readonly ClientRelationshipDbContext context;
    public ProcessTaskStorageBroker(ClientRelationshipDbContext context) => this.context = context;
    public IQueryable<ProcessTask> SelectAll() => context.Set<ProcessTask>();
    public async ValueTask<ProcessTask> InsertAsync(ProcessTask entity, CancellationToken cancellationToken = default) { context.Set<ProcessTask>().Add(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask<ProcessTask> UpdateAsync(ProcessTask entity, CancellationToken cancellationToken = default) { ProcessTask local = context.Set<ProcessTask>().Local.FirstOrDefault(item => item.Id == entity.Id); if (local is null) context.Set<ProcessTask>().Update(entity); else context.Entry(local).CurrentValues.SetValues(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask DeleteAsync(ProcessTask entity, CancellationToken cancellationToken = default) { context.Set<ProcessTask>().Remove(entity); await context.SaveChangesAsync(cancellationToken); }

}

public interface IProcessTaskFoundationService
{
    IQueryable<ProcessTask> RetrieveAll();
    IQueryable<ProcessTask> RetrieveWriteable();
    ValueTask<ProcessTask> AddAsync(ProcessTask entity, CancellationToken cancellationToken = default);
    ValueTask<ProcessTask> ModifyAsync(ProcessTask entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(ProcessTask entity, CancellationToken cancellationToken = default);
}

internal sealed class ProcessTaskFoundationService(IProcessTaskStorageBroker broker, ICRMAuthInfo auth) : IProcessTaskFoundationService
{
    string[] Readable => auth.ReadableTenants?.Length > 0 ? auth.ReadableTenants : auth.WriteableTenants ?? [];
    string[] Writeable => auth.WriteableTenants ?? [];
    public IQueryable<ProcessTask> RetrieveAll() => Scope(broker.SelectAll(), Readable);
    public IQueryable<ProcessTask> RetrieveWriteable() => Scope(broker.SelectAll(), Writeable);

    public async ValueTask<ProcessTask> AddAsync(ProcessTask entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        if (Writeable.Length == 0) throw new UnauthorizedAccessException("The user has no writable CRM tenant.");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ProcessTask storage = Copy(entity);
        storage.CreatedOn = now;
        storage.CreatedBy = auth.SSOUserId;
        storage.LastUpdated = now;
        storage.LastUpdatedBy = auth.SSOUserId;
        ProcessTask persisted = await broker.InsertAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask<ProcessTask> ModifyAsync(ProcessTask entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        ProcessTask existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        ProcessTask storage = Copy(entity);
        storage.CreatedOn = existing.CreatedOn;
        storage.CreatedBy = existing.CreatedBy;
        storage.LastUpdated = DateTimeOffset.UtcNow;
        storage.LastUpdatedBy = auth.SSOUserId;
        ProcessTask persisted = await broker.UpdateAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask RemoveAsync(ProcessTask entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        ProcessTask existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        await broker.DeleteAsync(existing, cancellationToken);
    }

    IQueryable<ProcessTask> Scope(IQueryable<ProcessTask> source, string[] tenants) => source.Where(item => tenants.Contains(item.ProcessStep.ProcessDefinition.TenantId));

    static ProcessTask Copy(ProcessTask source) => new()
    {
            Id = source.Id,
            CreatedBy = source.CreatedBy,
            LastUpdatedBy = source.LastUpdatedBy,
            CreatedOn = source.CreatedOn,
            LastUpdated = source.LastUpdated,
            ProcessInstanceId = source.ProcessInstanceId,
            ProcessStepId = source.ProcessStepId,
            LeadId = source.LeadId,
            TenantCompanyRelationshipId = source.TenantCompanyRelationshipId,
            OpportunityId = source.OpportunityId,
            ClientAccountId = source.ClientAccountId,
            EmailId = source.EmailId,
            ActionType = source.ActionType,
            State = source.State,
            DueOn = source.DueOn,
            RenderedTitle = source.RenderedTitle,
            RenderedInstructions = source.RenderedInstructions,
            RenderedEmailSubject = source.RenderedEmailSubject,
            RenderedEmailBody = source.RenderedEmailBody,
            RenderedCallScript = source.RenderedCallScript,
            RenderedQuestionSet = source.RenderedQuestionSet,
            CompletionOutcomeKey = source.CompletionOutcomeKey,
            CompletionNotes = source.CompletionNotes,
            CompletedOn = source.CompletedOn,
            CompletedBy = source.CompletedBy,
            AgentClaimId = source.AgentClaimId,
            AgentClaimedBy = source.AgentClaimedBy,
            AgentClaimedOn = source.AgentClaimedOn,
            AgentClaimExpiresOn = source.AgentClaimExpiresOn,
    };

    static void CopyPersisted(ProcessTask source, ProcessTask target)
    {
        target.Id = source.Id;
        target.CreatedBy = source.CreatedBy;
        target.LastUpdatedBy = source.LastUpdatedBy;
        target.CreatedOn = source.CreatedOn;
        target.LastUpdated = source.LastUpdated;
        target.ProcessInstanceId = source.ProcessInstanceId;
        target.ProcessStepId = source.ProcessStepId;
        target.LeadId = source.LeadId;
        target.TenantCompanyRelationshipId = source.TenantCompanyRelationshipId;
        target.OpportunityId = source.OpportunityId;
        target.ClientAccountId = source.ClientAccountId;
        target.EmailId = source.EmailId;
        target.ActionType = source.ActionType;
        target.State = source.State;
        target.DueOn = source.DueOn;
        target.RenderedTitle = source.RenderedTitle;
        target.RenderedInstructions = source.RenderedInstructions;
        target.RenderedEmailSubject = source.RenderedEmailSubject;
        target.RenderedEmailBody = source.RenderedEmailBody;
        target.RenderedCallScript = source.RenderedCallScript;
        target.RenderedQuestionSet = source.RenderedQuestionSet;
        target.CompletionOutcomeKey = source.CompletionOutcomeKey;
        target.CompletionNotes = source.CompletionNotes;
        target.CompletedOn = source.CompletedOn;
        target.CompletedBy = source.CompletedBy;
        target.AgentClaimId = source.AgentClaimId;
        target.AgentClaimedBy = source.AgentClaimedBy;
        target.AgentClaimedOn = source.AgentClaimedOn;
        target.AgentClaimExpiresOn = source.AgentClaimExpiresOn;
    }


    void EnsureAuthenticated()
    {
        if (string.IsNullOrWhiteSpace(auth.SSOUserId) || string.Equals(auth.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("A signed-in CRM user is required.");
    }
}

public interface IProcessTaskProcessingService : IProcessTaskFoundationService { }
internal sealed class ProcessTaskProcessingService(IProcessTaskFoundationService foundation) : IProcessTaskProcessingService
{
    public IQueryable<ProcessTask> RetrieveAll() => foundation.RetrieveAll();
    public IQueryable<ProcessTask> RetrieveWriteable() => foundation.RetrieveWriteable();
    public ValueTask<ProcessTask> AddAsync(ProcessTask entity, CancellationToken cancellationToken = default) => foundation.AddAsync(entity, cancellationToken);
    public ValueTask<ProcessTask> ModifyAsync(ProcessTask entity, CancellationToken cancellationToken = default) => foundation.ModifyAsync(entity, cancellationToken);
    public ValueTask RemoveAsync(ProcessTask entity, CancellationToken cancellationToken = default) => foundation.RemoveAsync(entity, cancellationToken);
}

public interface IProcessTaskEventBroker
{
    ValueTask RaiseAddAsync(EventMessage<ProcessTask> message);
    ValueTask RaiseUpdateAsync(EventMessage<ProcessTask> message);
    ValueTask RaiseDeleteAsync(EventMessage<ProcessTask> message);
}
internal sealed class ProcessTaskEventBroker(IEventHub eventHub) : IProcessTaskEventBroker
{
    public ValueTask RaiseAddAsync(EventMessage<ProcessTask> message) => eventHub.RaiseEventAsync("process_task_add", message);
    public ValueTask RaiseUpdateAsync(EventMessage<ProcessTask> message) => eventHub.RaiseEventAsync("process_task_update", message);
    public ValueTask RaiseDeleteAsync(EventMessage<ProcessTask> message) => eventHub.RaiseEventAsync("process_task_delete", message);
}
public interface IProcessTaskEventFoundationService
{
    ValueTask RaiseAddAsync(ProcessTask entity);
    ValueTask RaiseUpdateAsync(ProcessTask entity);
    ValueTask RaiseDeleteAsync(ProcessTask entity);
}
internal sealed class ProcessTaskEventFoundationService(IProcessTaskEventBroker broker, ICRMAuthInfo auth) : IProcessTaskEventFoundationService
{
    EventMessage<ProcessTask> Message(ProcessTask entity) => new() { AuthInfo = new EventAuthInfo { SSOUserId = auth.SSOUserId }, Data = entity };
    public ValueTask RaiseAddAsync(ProcessTask entity) => broker.RaiseAddAsync(Message(entity));
    public ValueTask RaiseUpdateAsync(ProcessTask entity) => broker.RaiseUpdateAsync(Message(entity));
    public ValueTask RaiseDeleteAsync(ProcessTask entity) => broker.RaiseDeleteAsync(Message(entity));
}
public interface IProcessTaskEventProcessingService : IProcessTaskEventFoundationService { }
internal sealed class ProcessTaskEventProcessingService(IProcessTaskEventFoundationService foundation) : IProcessTaskEventProcessingService
{
    public ValueTask RaiseAddAsync(ProcessTask entity) => foundation.RaiseAddAsync(entity);
    public ValueTask RaiseUpdateAsync(ProcessTask entity) => foundation.RaiseUpdateAsync(entity);
    public ValueTask RaiseDeleteAsync(ProcessTask entity) => foundation.RaiseDeleteAsync(entity);
}

public interface IProcessTaskOrchestrationService
{
    IQueryable<ProcessTask> RetrieveAll();
    IQueryable<ProcessTask> RetrieveWriteable();
    ValueTask<ProcessTask> AddAsync(ProcessTask entity, CancellationToken cancellationToken = default);
    ValueTask<ProcessTask> ModifyAsync(ProcessTask entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(ProcessTask entity, CancellationToken cancellationToken = default);
}
internal sealed class ProcessTaskOrchestrationService(IProcessTaskProcessingService processing, IProcessTaskEventProcessingService events) : IProcessTaskOrchestrationService
{
    public IQueryable<ProcessTask> RetrieveAll() => processing.RetrieveAll();
    public IQueryable<ProcessTask> RetrieveWriteable() => processing.RetrieveWriteable();
    public async ValueTask<ProcessTask> AddAsync(ProcessTask entity, CancellationToken cancellationToken = default) { ProcessTask persisted = await processing.AddAsync(entity, cancellationToken); await events.RaiseAddAsync(persisted); return persisted; }
    public async ValueTask<ProcessTask> ModifyAsync(ProcessTask entity, CancellationToken cancellationToken = default) { ProcessTask persisted = await processing.ModifyAsync(entity, cancellationToken); await events.RaiseUpdateAsync(persisted); return persisted; }
    public async ValueTask RemoveAsync(ProcessTask entity, CancellationToken cancellationToken = default) { await processing.RemoveAsync(entity, cancellationToken); await events.RaiseDeleteAsync(entity); }
}
