using cCoder.ClientRelationshipManagement.Brokers.Transactions;
using cCoder.ClientRelationshipManagement.Services.Entities;
using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Services.Foundations.Platform;

internal sealed class ImportCoordinationService(IImportOrchestrationService imports, ISourceOrchestrationService sources, IImportLinkOrchestrationService importLinks, ICRMAuthInfo authInfo, ICRMTransactionBroker transaction) : IImportCoordinationService
{
    public async ValueTask<Import> CreateAsync(CreateImportCommand command, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        if (string.IsNullOrWhiteSpace(command.FileName)) throw new ArgumentException("A file name is required.");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Source source = command.SourceId.HasValue
            ? await sources.RetrieveAll().FirstOrDefaultAsync(item => item.Id == command.SourceId, cancellationToken)
            : null;
        string name = string.IsNullOrWhiteSpace(command.SourceName) ? "Unspecified source" : command.SourceName.Trim();
        string country = string.IsNullOrWhiteSpace(command.CountryCode) ? "GB" : command.CountryCode.Trim().ToUpperInvariant();
        source ??= await sources.RetrieveAll().FirstOrDefaultAsync(item => item.Name == name && item.CountryCode == country, cancellationToken);
        if (source is null)
        {
            source = new Source { Id = Guid.NewGuid(), Name = name, SourceType = command.SourceType,
                CountryCode = country, IsAuthoritative = command.IsAuthoritative || command.SourceType == SourceType.Authority,
                Notes = Normalize(command.SourceNotes), CreatedBy = authInfo.SSOUserId, LastUpdatedBy = authInfo.SSOUserId,
                CreatedOn = now, LastUpdated = now };
            await sources.AddAsync(source, cancellationToken);
        }
        Import import = new() { Id = Guid.NewGuid(), SourceId = source.Id, OriginalFileName = command.FileName,
            ContentType = command.ContentType, SizeBytes = command.SizeBytes, JobStatus = ImportJobStatus.Draft,
            UploadStatus = ImportUploadStatus.NotStarted, ProcessingStatus = ImportProcessingStatus.NotReady,
            UserInstructions = Normalize(command.UserInstructions), CreatedBy = authInfo.SSOUserId,
            LastUpdatedBy = authInfo.SSOUserId, CreatedOn = now, LastUpdated = now };
        await imports.AddAsync(import, cancellationToken);
        return import;
    }

    public async ValueTask<Import> SaveMappingAsync(Guid id, string mappingJson, string instructions, CancellationToken cancellationToken = default)
    {
        Import import = await FindAsync(id, cancellationToken); if (import is null) return null;
        import.MappingSnapshotJson = mappingJson; import.UserInstructions = Normalize(instructions); Touch(import);
        await transaction.CommitAsync(cancellationToken); return import;
    }
    public async ValueTask<Import> MarkReadyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Import import = await FindAsync(id, cancellationToken); if (import is null) return null;
        if (import.UploadStatus != ImportUploadStatus.Uploaded) throw new InvalidOperationException("Upload must be completed before the import can be marked ready.");
        if (string.IsNullOrWhiteSpace(import.MappingSnapshotJson)) throw new InvalidOperationException("Mapping must be reviewed before the import can be marked ready.");
        import.JobStatus = ImportJobStatus.Ready; import.ProcessingStatus = ImportProcessingStatus.Ready;
        Touch(import); await transaction.CommitAsync(cancellationToken); return import;
    }
    public async ValueTask<Import> CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Import import = await FindAsync(id, cancellationToken); if (import is null) return null;
        if (import.JobStatus == ImportJobStatus.Processing) throw new InvalidOperationException("An active import cannot be cancelled.");
        import.JobStatus = ImportJobStatus.Cancelled; import.ProcessingStatus = ImportProcessingStatus.Cancelled;
        Touch(import); await transaction.CommitAsync(cancellationToken); return import;
    }
    public async ValueTask<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Import import = await FindAsync(id, cancellationToken); if (import is null) return false;
        if (import.JobStatus == ImportJobStatus.Processing) throw new InvalidOperationException("An active import cannot be deleted.");
        ImportLink[] links = await importLinks.RetrieveAll().Where(item => item.ImportId == id).ToArrayAsync(cancellationToken);
        foreach (ImportLink link in links) await importLinks.RemoveAsync(link, cancellationToken);
        await imports.RemoveAsync(import, cancellationToken); return true;
    }
    async ValueTask<Import> FindAsync(Guid id, CancellationToken token) { EnsureAuthenticated(); return await imports.RetrieveAll().FirstOrDefaultAsync(item => item.Id == id, token); }
    void Touch(Import import) { import.LastUpdated = DateTimeOffset.UtcNow; import.LastUpdatedBy = authInfo.SSOUserId; }
    static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    public IQueryable<Import> RetrieveAllImports()
    {
        EnsureAuthenticated();
        return imports.RetrieveAll();
    }

    public IQueryable<Source> RetrieveAllSources()
    {
        EnsureAuthenticated();
        return sources.RetrieveAll();
    }
    public IQueryable<ImportLink> RetrieveAllLinks() { EnsureAuthenticated(); return importLinks.RetrieveAll(); }
    readonly List<ImportLink> pendingLinks = [];
    readonly List<Source> pendingSources = [];
    readonly List<Import> pendingImports = [];
    public void Add(ImportLink link) { EnsureAuthenticated(); pendingLinks.Add(link); }
    public void Add(Source source) { EnsureAuthenticated(); pendingSources.Add(source); }
    public void Add(Import import) { EnsureAuthenticated(); pendingImports.Add(import); }

    public async ValueTask SaveAsync(CancellationToken cancellationToken = default)
    {
        foreach (Source source in pendingSources) await sources.AddAsync(source, cancellationToken);
        foreach (Import import in pendingImports) await imports.AddAsync(import, cancellationToken);
        foreach (ImportLink link in pendingLinks) await importLinks.AddAsync(link, cancellationToken);
        pendingSources.Clear(); pendingImports.Clear(); pendingLinks.Clear();
        await transaction.CommitAsync(cancellationToken);
    }


    void EnsureAuthenticated()
    {
        if (string.IsNullOrWhiteSpace(authInfo.SSOUserId)
            || string.Equals(authInfo.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("A signed-in CRM user is required.");
    }
}
