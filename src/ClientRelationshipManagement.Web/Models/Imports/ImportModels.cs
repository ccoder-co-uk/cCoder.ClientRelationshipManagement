using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace ClientRelationshipManagement.Web.Models.Imports;

public sealed class ImportListPageViewModel
{
    public string Notice { get; init; } = string.Empty;
    public string HostedServicesBaseUrl { get; init; } = string.Empty;
    public int ChunkSizeBytes { get; init; }
    public ImportCreateViewModel NewImport { get; init; } = new();
    public IReadOnlyList<ImportListItemViewModel> Imports { get; init; } = [];
    public IReadOnlyList<SourceOptionViewModel> Sources { get; init; } = [];
}

public sealed class ImportCreateViewModel
{
    public Guid? SourceId { get; init; }
    public string SourceName { get; init; } = string.Empty;
    public SourceType SourceType { get; init; } = SourceType.Other;
    public string CountryCode { get; init; } = "GB";
    public bool IsAuthoritative { get; init; }
    public string SourceNotes { get; init; } = string.Empty;
    public string UserInstructions { get; init; } = string.Empty;
}

public sealed class SourceOptionViewModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string CountryCode { get; init; } = string.Empty;
    public bool IsAuthoritative { get; init; }
}

public sealed class ImportListItemViewModel
{
    public Guid Id { get; init; }
    public string SourceName { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string JobStatus { get; init; } = string.Empty;
    public string UploadStatus { get; init; } = string.Empty;
    public string ProcessingStatus { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public long UploadedBytes { get; init; }
    public int TotalRowCount { get; init; }
    public int ImportedRowCount { get; init; }
    public int WarningCount { get; init; }
    public int ErrorCount { get; init; }
    public string WarningSummary { get; init; } = string.Empty;
    public string ErrorSummary { get; init; } = string.Empty;
    public string MappingSnapshotJson { get; init; } = string.Empty;
    public string CreatedOn { get; init; } = string.Empty;
}

public sealed class CreateImportRequest
{
    public Guid? SourceId { get; init; }
    public string SourceName { get; init; } = string.Empty;
    public SourceType SourceType { get; init; } = SourceType.Other;
    public string CountryCode { get; init; } = "GB";
    public bool IsAuthoritative { get; init; }
    public string SourceNotes { get; init; } = string.Empty;
    public string UserInstructions { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
}

public sealed class SaveImportMappingRequest
{
    public string MappingSnapshotJson { get; init; } = string.Empty;
    public string UserInstructions { get; init; } = string.Empty;
}

public sealed class ImportUploadSessionResponse
{
    public Guid ImportId { get; init; }
    public string UploadSessionId { get; init; } = string.Empty;
    public string ChunkUploadUrl { get; init; } = string.Empty;
    public string CompleteUploadUrl { get; init; } = string.Empty;
    public string StatusUrl { get; init; } = string.Empty;
    public int ChunkSizeBytes { get; init; }
    public DateTimeOffset ExpiresOn { get; init; }
}

public sealed class ImportStatusResponse
{
    public Guid Id { get; init; }
    public string JobStatus { get; init; } = string.Empty;
    public string UploadStatus { get; init; } = string.Empty;
    public string ProcessingStatus { get; init; } = string.Empty;
    public long UploadedBytes { get; init; }
    public long SizeBytes { get; init; }
    public int TotalRowCount { get; init; }
    public int ImportedRowCount { get; init; }
    public int WarningCount { get; init; }
    public int ErrorCount { get; init; }
    public string WarningSummary { get; init; } = string.Empty;
    public string ErrorSummary { get; init; } = string.Empty;
    public string MappingSnapshotJson { get; init; } = string.Empty;
}

public sealed class HostedImportUploadSessionRequest
{
    public Guid ImportId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
}

public sealed class HostedImportCompleteUploadRequest
{
    public string UploadSessionId { get; init; } = string.Empty;
    public int TotalChunks { get; init; }
}
