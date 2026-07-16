using cCoder.ClientRelationshipManagement.Platform.Models.Entities;

namespace cCoder.ClientRelationshipManagement.Services.Foundations.Platform;

public interface IImportCoordinationService
{
    IQueryable<Import> RetrieveAllImports();
    IQueryable<Source> RetrieveAllSources();
    IQueryable<ImportLink> RetrieveAllLinks();
    void Add(ImportLink link);
    void Add(Source source);
    void Add(Import import);
    ValueTask<Import> CreateAsync(CreateImportCommand command, CancellationToken cancellationToken = default);
    ValueTask<Import> SaveMappingAsync(Guid id, string mappingJson, string instructions, CancellationToken cancellationToken = default);
    ValueTask<Import> MarkReadyAsync(Guid id, CancellationToken cancellationToken = default);
    ValueTask<Import> CancelAsync(Guid id, CancellationToken cancellationToken = default);
    ValueTask<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    ValueTask SaveAsync(CancellationToken cancellationToken = default);
}

public sealed record CreateImportCommand(Guid? SourceId, string SourceName,
    cCoder.ClientRelationshipManagement.Platform.Models.Enums.SourceType SourceType,
    string CountryCode, bool IsAuthoritative, string SourceNotes,
    string FileName, string ContentType, long SizeBytes, string UserInstructions);
