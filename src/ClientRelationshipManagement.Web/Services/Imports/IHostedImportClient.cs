using ClientRelationshipManagement.Web.Models.Imports;

namespace ClientRelationshipManagement.Web.Services.Imports;

public interface IHostedImportClient
{
    ValueTask<ImportUploadSessionResponse> CreateUploadSessionAsync(
        HostedImportUploadSessionRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<ImportStatusResponse> AnalyseAsync(Guid importId, CancellationToken cancellationToken = default);
    ValueTask<ImportStatusResponse> GetStatusAsync(Guid importId, CancellationToken cancellationToken = default);
    ValueTask DeleteFilesAsync(Guid importId, CancellationToken cancellationToken = default);
}
