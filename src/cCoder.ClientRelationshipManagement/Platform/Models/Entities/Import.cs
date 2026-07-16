using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

public class Import : ICrmEntity
{
    public Guid Id { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public Guid SourceId { get; set; }
    public string OriginalFileName { get; set; }
    public string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public string StoredFilePath { get; set; }
    public string StoredObjectKey { get; set; }
    public ImportJobStatus JobStatus { get; set; }
    public ImportUploadStatus UploadStatus { get; set; }
    public ImportProcessingStatus ProcessingStatus { get; set; }
    public long UploadedBytes { get; set; }
    public int TotalRowCount { get; set; }
    public int ImportedRowCount { get; set; }
    public int WarningCount { get; set; }
    public int ErrorCount { get; set; }
    public string WarningSummary { get; set; }
    public string ErrorSummary { get; set; }
    public string MappingSnapshotJson { get; set; }
    public string UserInstructions { get; set; }
    public string ProcessingCheckpoint { get; set; }
    public string UploadSessionId { get; set; }
    public DateTimeOffset? UploadSessionExpiresOn { get; set; }
    public DateTimeOffset? UploadedOn { get; set; }
    public DateTimeOffset? MarkedReadyOn { get; set; }
    public DateTimeOffset? ProcessingStartedOn { get; set; }
    public DateTimeOffset? ProcessingCompletedOn { get; set; }

    public virtual Source Source { get; set; }
    public virtual ICollection<ImportLink> Links { get; set; } = new List<ImportLink>();
}
