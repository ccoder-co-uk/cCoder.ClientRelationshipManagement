using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Services.Entities;

public interface IImportStorageBroker
{
    IQueryable<Import> SelectAll();
    ValueTask<Import> InsertAsync(Import entity, CancellationToken cancellationToken = default);
    ValueTask<Import> UpdateAsync(Import entity, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(Import entity, CancellationToken cancellationToken = default);
}

internal sealed class ImportStorageBroker : IImportStorageBroker
{
    readonly ClientRelationshipDbContext context;
    public ImportStorageBroker(ClientRelationshipDbContext context) => this.context = context;
    public IQueryable<Import> SelectAll() => context.Set<Import>();
    public async ValueTask<Import> InsertAsync(Import entity, CancellationToken cancellationToken = default) { context.Set<Import>().Add(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask<Import> UpdateAsync(Import entity, CancellationToken cancellationToken = default) { Import local = context.Set<Import>().Local.FirstOrDefault(item => item.Id == entity.Id); if (local is null) context.Set<Import>().Update(entity); else context.Entry(local).CurrentValues.SetValues(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask DeleteAsync(Import entity, CancellationToken cancellationToken = default) { context.Set<Import>().Remove(entity); await context.SaveChangesAsync(cancellationToken); }

}

public interface IImportFoundationService
{
    IQueryable<Import> RetrieveAll();
    IQueryable<Import> RetrieveWriteable();
    ValueTask<Import> AddAsync(Import entity, CancellationToken cancellationToken = default);
    ValueTask<Import> ModifyAsync(Import entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(Import entity, CancellationToken cancellationToken = default);
}

internal sealed class ImportFoundationService(IImportStorageBroker broker, ICRMAuthInfo auth) : IImportFoundationService
{
    string[] Readable => auth.ReadableTenants?.Length > 0 ? auth.ReadableTenants : auth.WriteableTenants ?? [];
    string[] Writeable => auth.WriteableTenants ?? [];
    public IQueryable<Import> RetrieveAll() => Scope(broker.SelectAll(), Readable);
    public IQueryable<Import> RetrieveWriteable() => Scope(broker.SelectAll(), Writeable);

    public async ValueTask<Import> AddAsync(Import entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        if (Writeable.Length == 0) throw new UnauthorizedAccessException("The user has no writable CRM tenant.");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Import storage = Copy(entity);
        storage.CreatedOn = now;
        storage.CreatedBy = auth.SSOUserId;
        storage.LastUpdated = now;
        storage.LastUpdatedBy = auth.SSOUserId;
        Import persisted = await broker.InsertAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask<Import> ModifyAsync(Import entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        Import existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        Import storage = Copy(entity);
        storage.CreatedOn = existing.CreatedOn;
        storage.CreatedBy = existing.CreatedBy;
        storage.LastUpdated = DateTimeOffset.UtcNow;
        storage.LastUpdatedBy = auth.SSOUserId;
        Import persisted = await broker.UpdateAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask RemoveAsync(Import entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        Import existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        await broker.DeleteAsync(existing, cancellationToken);
    }

    IQueryable<Import> Scope(IQueryable<Import> source, string[] tenants) => source;

    static Import Copy(Import source) => new()
    {
            Id = source.Id,
            CreatedBy = source.CreatedBy,
            LastUpdatedBy = source.LastUpdatedBy,
            CreatedOn = source.CreatedOn,
            LastUpdated = source.LastUpdated,
            SourceId = source.SourceId,
            OriginalFileName = source.OriginalFileName,
            ContentType = source.ContentType,
            SizeBytes = source.SizeBytes,
            StoredFilePath = source.StoredFilePath,
            StoredObjectKey = source.StoredObjectKey,
            JobStatus = source.JobStatus,
            UploadStatus = source.UploadStatus,
            ProcessingStatus = source.ProcessingStatus,
            UploadedBytes = source.UploadedBytes,
            TotalRowCount = source.TotalRowCount,
            ImportedRowCount = source.ImportedRowCount,
            WarningCount = source.WarningCount,
            ErrorCount = source.ErrorCount,
            WarningSummary = source.WarningSummary,
            ErrorSummary = source.ErrorSummary,
            MappingSnapshotJson = source.MappingSnapshotJson,
            UserInstructions = source.UserInstructions,
            ProcessingCheckpoint = source.ProcessingCheckpoint,
            UploadSessionId = source.UploadSessionId,
            UploadSessionExpiresOn = source.UploadSessionExpiresOn,
            UploadedOn = source.UploadedOn,
            MarkedReadyOn = source.MarkedReadyOn,
            ProcessingStartedOn = source.ProcessingStartedOn,
            ProcessingCompletedOn = source.ProcessingCompletedOn,
    };

    static void CopyPersisted(Import source, Import target)
    {
        target.Id = source.Id;
        target.CreatedBy = source.CreatedBy;
        target.LastUpdatedBy = source.LastUpdatedBy;
        target.CreatedOn = source.CreatedOn;
        target.LastUpdated = source.LastUpdated;
        target.SourceId = source.SourceId;
        target.OriginalFileName = source.OriginalFileName;
        target.ContentType = source.ContentType;
        target.SizeBytes = source.SizeBytes;
        target.StoredFilePath = source.StoredFilePath;
        target.StoredObjectKey = source.StoredObjectKey;
        target.JobStatus = source.JobStatus;
        target.UploadStatus = source.UploadStatus;
        target.ProcessingStatus = source.ProcessingStatus;
        target.UploadedBytes = source.UploadedBytes;
        target.TotalRowCount = source.TotalRowCount;
        target.ImportedRowCount = source.ImportedRowCount;
        target.WarningCount = source.WarningCount;
        target.ErrorCount = source.ErrorCount;
        target.WarningSummary = source.WarningSummary;
        target.ErrorSummary = source.ErrorSummary;
        target.MappingSnapshotJson = source.MappingSnapshotJson;
        target.UserInstructions = source.UserInstructions;
        target.ProcessingCheckpoint = source.ProcessingCheckpoint;
        target.UploadSessionId = source.UploadSessionId;
        target.UploadSessionExpiresOn = source.UploadSessionExpiresOn;
        target.UploadedOn = source.UploadedOn;
        target.MarkedReadyOn = source.MarkedReadyOn;
        target.ProcessingStartedOn = source.ProcessingStartedOn;
        target.ProcessingCompletedOn = source.ProcessingCompletedOn;
    }


    void EnsureAuthenticated()
    {
        if (string.IsNullOrWhiteSpace(auth.SSOUserId) || string.Equals(auth.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("A signed-in CRM user is required.");
    }
}

public interface IImportProcessingService : IImportFoundationService { }
internal sealed class ImportProcessingService(IImportFoundationService foundation) : IImportProcessingService
{
    public IQueryable<Import> RetrieveAll() => foundation.RetrieveAll();
    public IQueryable<Import> RetrieveWriteable() => foundation.RetrieveWriteable();
    public ValueTask<Import> AddAsync(Import entity, CancellationToken cancellationToken = default) => foundation.AddAsync(entity, cancellationToken);
    public ValueTask<Import> ModifyAsync(Import entity, CancellationToken cancellationToken = default) => foundation.ModifyAsync(entity, cancellationToken);
    public ValueTask RemoveAsync(Import entity, CancellationToken cancellationToken = default) => foundation.RemoveAsync(entity, cancellationToken);
}

public interface IImportEventBroker
{
    ValueTask RaiseAddAsync(EventMessage<Import> message);
    ValueTask RaiseUpdateAsync(EventMessage<Import> message);
    ValueTask RaiseDeleteAsync(EventMessage<Import> message);
}
internal sealed class ImportEventBroker(IEventHub eventHub) : IImportEventBroker
{
    public ValueTask RaiseAddAsync(EventMessage<Import> message) => eventHub.RaiseEventAsync("import_add", message);
    public ValueTask RaiseUpdateAsync(EventMessage<Import> message) => eventHub.RaiseEventAsync("import_update", message);
    public ValueTask RaiseDeleteAsync(EventMessage<Import> message) => eventHub.RaiseEventAsync("import_delete", message);
}
public interface IImportEventFoundationService
{
    ValueTask RaiseAddAsync(Import entity);
    ValueTask RaiseUpdateAsync(Import entity);
    ValueTask RaiseDeleteAsync(Import entity);
}
internal sealed class ImportEventFoundationService(IImportEventBroker broker, ICRMAuthInfo auth) : IImportEventFoundationService
{
    EventMessage<Import> Message(Import entity) => new() { AuthInfo = new EventAuthInfo { SSOUserId = auth.SSOUserId }, Data = entity };
    public ValueTask RaiseAddAsync(Import entity) => broker.RaiseAddAsync(Message(entity));
    public ValueTask RaiseUpdateAsync(Import entity) => broker.RaiseUpdateAsync(Message(entity));
    public ValueTask RaiseDeleteAsync(Import entity) => broker.RaiseDeleteAsync(Message(entity));
}
public interface IImportEventProcessingService : IImportEventFoundationService { }
internal sealed class ImportEventProcessingService(IImportEventFoundationService foundation) : IImportEventProcessingService
{
    public ValueTask RaiseAddAsync(Import entity) => foundation.RaiseAddAsync(entity);
    public ValueTask RaiseUpdateAsync(Import entity) => foundation.RaiseUpdateAsync(entity);
    public ValueTask RaiseDeleteAsync(Import entity) => foundation.RaiseDeleteAsync(entity);
}

public interface IImportOrchestrationService
{
    IQueryable<Import> RetrieveAll();
    IQueryable<Import> RetrieveWriteable();
    ValueTask<Import> AddAsync(Import entity, CancellationToken cancellationToken = default);
    ValueTask<Import> ModifyAsync(Import entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(Import entity, CancellationToken cancellationToken = default);
}
internal sealed class ImportOrchestrationService(IImportProcessingService processing, IImportEventProcessingService events) : IImportOrchestrationService
{
    public IQueryable<Import> RetrieveAll() => processing.RetrieveAll();
    public IQueryable<Import> RetrieveWriteable() => processing.RetrieveWriteable();
    public async ValueTask<Import> AddAsync(Import entity, CancellationToken cancellationToken = default) { Import persisted = await processing.AddAsync(entity, cancellationToken); await events.RaiseAddAsync(persisted); return persisted; }
    public async ValueTask<Import> ModifyAsync(Import entity, CancellationToken cancellationToken = default) { Import persisted = await processing.ModifyAsync(entity, cancellationToken); await events.RaiseUpdateAsync(persisted); return persisted; }
    public async ValueTask RemoveAsync(Import entity, CancellationToken cancellationToken = default) { await processing.RemoveAsync(entity, cancellationToken); await events.RaiseDeleteAsync(entity); }
}
