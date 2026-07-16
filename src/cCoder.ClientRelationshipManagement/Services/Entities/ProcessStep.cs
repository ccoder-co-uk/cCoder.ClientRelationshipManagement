using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Services.Entities;

public interface IProcessStepStorageBroker
{
    IQueryable<ProcessStep> SelectAll();
    ValueTask<ProcessStep> InsertAsync(ProcessStep entity, CancellationToken cancellationToken = default);
    ValueTask<ProcessStep> UpdateAsync(ProcessStep entity, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(ProcessStep entity, CancellationToken cancellationToken = default);
}

internal sealed class ProcessStepStorageBroker : IProcessStepStorageBroker
{
    readonly ClientRelationshipDbContext context;
    public ProcessStepStorageBroker(ClientRelationshipDbContext context) => this.context = context;
    public IQueryable<ProcessStep> SelectAll() => context.Set<ProcessStep>();
    public async ValueTask<ProcessStep> InsertAsync(ProcessStep entity, CancellationToken cancellationToken = default) { context.Set<ProcessStep>().Add(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask<ProcessStep> UpdateAsync(ProcessStep entity, CancellationToken cancellationToken = default) { ProcessStep local = context.Set<ProcessStep>().Local.FirstOrDefault(item => item.Id == entity.Id); if (local is null) context.Set<ProcessStep>().Update(entity); else context.Entry(local).CurrentValues.SetValues(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask DeleteAsync(ProcessStep entity, CancellationToken cancellationToken = default) { context.Set<ProcessStep>().Remove(entity); await context.SaveChangesAsync(cancellationToken); }

}

public interface IProcessStepFoundationService
{
    IQueryable<ProcessStep> RetrieveAll();
    IQueryable<ProcessStep> RetrieveWriteable();
    ValueTask<ProcessStep> AddAsync(ProcessStep entity, CancellationToken cancellationToken = default);
    ValueTask<ProcessStep> ModifyAsync(ProcessStep entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(ProcessStep entity, CancellationToken cancellationToken = default);
}

internal sealed class ProcessStepFoundationService(IProcessStepStorageBroker broker, ICRMAuthInfo auth) : IProcessStepFoundationService
{
    string[] Readable => auth.ReadableTenants?.Length > 0 ? auth.ReadableTenants : auth.WriteableTenants ?? [];
    string[] Writeable => auth.WriteableTenants ?? [];
    public IQueryable<ProcessStep> RetrieveAll() => Scope(broker.SelectAll(), Readable);
    public IQueryable<ProcessStep> RetrieveWriteable() => Scope(broker.SelectAll(), Writeable);

    public async ValueTask<ProcessStep> AddAsync(ProcessStep entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        if (Writeable.Length == 0) throw new UnauthorizedAccessException("The user has no writable CRM tenant.");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ProcessStep storage = Copy(entity);
        storage.CreatedOn = now;
        storage.CreatedBy = auth.SSOUserId;
        storage.LastUpdated = now;
        storage.LastUpdatedBy = auth.SSOUserId;
        ProcessStep persisted = await broker.InsertAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask<ProcessStep> ModifyAsync(ProcessStep entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        ProcessStep existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        ProcessStep storage = Copy(entity);
        storage.CreatedOn = existing.CreatedOn;
        storage.CreatedBy = existing.CreatedBy;
        storage.LastUpdated = DateTimeOffset.UtcNow;
        storage.LastUpdatedBy = auth.SSOUserId;
        ProcessStep persisted = await broker.UpdateAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask RemoveAsync(ProcessStep entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        ProcessStep existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        await broker.DeleteAsync(existing, cancellationToken);
    }

    IQueryable<ProcessStep> Scope(IQueryable<ProcessStep> source, string[] tenants) => source.Where(item => tenants.Contains(item.ProcessDefinition.TenantId));

    static ProcessStep Copy(ProcessStep source) => new()
    {
            Id = source.Id,
            CreatedBy = source.CreatedBy,
            LastUpdatedBy = source.LastUpdatedBy,
            CreatedOn = source.CreatedOn,
            LastUpdated = source.LastUpdated,
            ProcessDefinitionId = source.ProcessDefinitionId,
            Key = source.Key,
            Name = source.Name,
            Objective = source.Objective,
            RequiredFacts = source.RequiredFacts,
            ProducedFacts = source.ProducedFacts,
            ViabilityImpact = source.ViabilityImpact,
            Sequence = source.Sequence,
            IsEntryPoint = source.IsEntryPoint,
            IsActive = source.IsActive,
            ActionType = source.ActionType,
            DueAfterDays = source.DueAfterDays,
            DueAfterHours = source.DueAfterHours,
            RelationshipStatusOnActivate = source.RelationshipStatusOnActivate,
            SalesStageOnActivate = source.SalesStageOnActivate,
            ClientAccountStatusOnActivate = source.ClientAccountStatusOnActivate,
            TaskTitleTemplate = source.TaskTitleTemplate,
            TaskInstructionsTemplate = source.TaskInstructionsTemplate,
            EmailRecipientTarget = source.EmailRecipientTarget,
            EmailSubjectTemplate = source.EmailSubjectTemplate,
            EmailBodyTemplate = source.EmailBodyTemplate,
            CallScriptTemplate = source.CallScriptTemplate,
            QuestionSetTemplate = source.QuestionSetTemplate,
    };

    static void CopyPersisted(ProcessStep source, ProcessStep target)
    {
        target.Id = source.Id;
        target.CreatedBy = source.CreatedBy;
        target.LastUpdatedBy = source.LastUpdatedBy;
        target.CreatedOn = source.CreatedOn;
        target.LastUpdated = source.LastUpdated;
        target.ProcessDefinitionId = source.ProcessDefinitionId;
        target.Key = source.Key;
        target.Name = source.Name;
        target.Objective = source.Objective;
        target.RequiredFacts = source.RequiredFacts;
        target.ProducedFacts = source.ProducedFacts;
        target.ViabilityImpact = source.ViabilityImpact;
        target.Sequence = source.Sequence;
        target.IsEntryPoint = source.IsEntryPoint;
        target.IsActive = source.IsActive;
        target.ActionType = source.ActionType;
        target.DueAfterDays = source.DueAfterDays;
        target.DueAfterHours = source.DueAfterHours;
        target.RelationshipStatusOnActivate = source.RelationshipStatusOnActivate;
        target.SalesStageOnActivate = source.SalesStageOnActivate;
        target.ClientAccountStatusOnActivate = source.ClientAccountStatusOnActivate;
        target.TaskTitleTemplate = source.TaskTitleTemplate;
        target.TaskInstructionsTemplate = source.TaskInstructionsTemplate;
        target.EmailRecipientTarget = source.EmailRecipientTarget;
        target.EmailSubjectTemplate = source.EmailSubjectTemplate;
        target.EmailBodyTemplate = source.EmailBodyTemplate;
        target.CallScriptTemplate = source.CallScriptTemplate;
        target.QuestionSetTemplate = source.QuestionSetTemplate;
    }


    void EnsureAuthenticated()
    {
        if (string.IsNullOrWhiteSpace(auth.SSOUserId) || string.Equals(auth.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("A signed-in CRM user is required.");
    }
}

public interface IProcessStepProcessingService : IProcessStepFoundationService { }
internal sealed class ProcessStepProcessingService(IProcessStepFoundationService foundation) : IProcessStepProcessingService
{
    public IQueryable<ProcessStep> RetrieveAll() => foundation.RetrieveAll();
    public IQueryable<ProcessStep> RetrieveWriteable() => foundation.RetrieveWriteable();
    public ValueTask<ProcessStep> AddAsync(ProcessStep entity, CancellationToken cancellationToken = default) => foundation.AddAsync(entity, cancellationToken);
    public ValueTask<ProcessStep> ModifyAsync(ProcessStep entity, CancellationToken cancellationToken = default) => foundation.ModifyAsync(entity, cancellationToken);
    public ValueTask RemoveAsync(ProcessStep entity, CancellationToken cancellationToken = default) => foundation.RemoveAsync(entity, cancellationToken);
}

public interface IProcessStepEventBroker
{
    ValueTask RaiseAddAsync(EventMessage<ProcessStep> message);
    ValueTask RaiseUpdateAsync(EventMessage<ProcessStep> message);
    ValueTask RaiseDeleteAsync(EventMessage<ProcessStep> message);
}
internal sealed class ProcessStepEventBroker(IEventHub eventHub) : IProcessStepEventBroker
{
    public ValueTask RaiseAddAsync(EventMessage<ProcessStep> message) => eventHub.RaiseEventAsync("process_step_add", message);
    public ValueTask RaiseUpdateAsync(EventMessage<ProcessStep> message) => eventHub.RaiseEventAsync("process_step_update", message);
    public ValueTask RaiseDeleteAsync(EventMessage<ProcessStep> message) => eventHub.RaiseEventAsync("process_step_delete", message);
}
public interface IProcessStepEventFoundationService
{
    ValueTask RaiseAddAsync(ProcessStep entity);
    ValueTask RaiseUpdateAsync(ProcessStep entity);
    ValueTask RaiseDeleteAsync(ProcessStep entity);
}
internal sealed class ProcessStepEventFoundationService(IProcessStepEventBroker broker, ICRMAuthInfo auth) : IProcessStepEventFoundationService
{
    EventMessage<ProcessStep> Message(ProcessStep entity) => new() { AuthInfo = new EventAuthInfo { SSOUserId = auth.SSOUserId }, Data = entity };
    public ValueTask RaiseAddAsync(ProcessStep entity) => broker.RaiseAddAsync(Message(entity));
    public ValueTask RaiseUpdateAsync(ProcessStep entity) => broker.RaiseUpdateAsync(Message(entity));
    public ValueTask RaiseDeleteAsync(ProcessStep entity) => broker.RaiseDeleteAsync(Message(entity));
}
public interface IProcessStepEventProcessingService : IProcessStepEventFoundationService { }
internal sealed class ProcessStepEventProcessingService(IProcessStepEventFoundationService foundation) : IProcessStepEventProcessingService
{
    public ValueTask RaiseAddAsync(ProcessStep entity) => foundation.RaiseAddAsync(entity);
    public ValueTask RaiseUpdateAsync(ProcessStep entity) => foundation.RaiseUpdateAsync(entity);
    public ValueTask RaiseDeleteAsync(ProcessStep entity) => foundation.RaiseDeleteAsync(entity);
}

public interface IProcessStepOrchestrationService
{
    IQueryable<ProcessStep> RetrieveAll();
    IQueryable<ProcessStep> RetrieveWriteable();
    ValueTask<ProcessStep> AddAsync(ProcessStep entity, CancellationToken cancellationToken = default);
    ValueTask<ProcessStep> ModifyAsync(ProcessStep entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(ProcessStep entity, CancellationToken cancellationToken = default);
}
internal sealed class ProcessStepOrchestrationService(IProcessStepProcessingService processing, IProcessStepEventProcessingService events) : IProcessStepOrchestrationService
{
    public IQueryable<ProcessStep> RetrieveAll() => processing.RetrieveAll();
    public IQueryable<ProcessStep> RetrieveWriteable() => processing.RetrieveWriteable();
    public async ValueTask<ProcessStep> AddAsync(ProcessStep entity, CancellationToken cancellationToken = default) { ProcessStep persisted = await processing.AddAsync(entity, cancellationToken); await events.RaiseAddAsync(persisted); return persisted; }
    public async ValueTask<ProcessStep> ModifyAsync(ProcessStep entity, CancellationToken cancellationToken = default) { ProcessStep persisted = await processing.ModifyAsync(entity, cancellationToken); await events.RaiseUpdateAsync(persisted); return persisted; }
    public async ValueTask RemoveAsync(ProcessStep entity, CancellationToken cancellationToken = default) { await processing.RemoveAsync(entity, cancellationToken); await events.RaiseDeleteAsync(entity); }
}
