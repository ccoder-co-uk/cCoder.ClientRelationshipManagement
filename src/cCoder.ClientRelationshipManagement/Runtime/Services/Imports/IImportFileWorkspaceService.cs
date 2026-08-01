using cCoder.ClientRelationshipManagement.Runtime.Models.Imports;

namespace cCoder.ClientRelationshipManagement.Runtime.Services.Imports;

public interface IImportFileWorkspaceService
{
    ValueTask<ImportUploadSessionResponse> CreateUploadSessionAsync(
        HostedImportUploadSessionRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<ImportStatusResponse> SaveChunkAsync(
        Guid importId,
        string uploadSessionId,
        int chunkIndex,
        Stream content,
        CancellationToken cancellationToken = default);

    ValueTask<ImportStatusResponse> CompleteUploadAsync(
        Guid importId,
        HostedImportCompleteUploadRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<ImportStatusResponse> AnalyseAsync(Guid importId, CancellationToken cancellationToken = default);
    ValueTask<ImportStatusResponse> GetStatusAsync(Guid importId, CancellationToken cancellationToken = default);
    ValueTask DeleteFilesAsync(Guid importId, CancellationToken cancellationToken = default);
}
