namespace cCoder.ClientRelationshipManagement.Platform.Models.Enums;

public enum ImportProcessingStatus
{
    NotReady = 0,
    WaitingForReady = 1,
    Ready = 2,
    Canonicalizing = 3,
    Staging = 4,
    Merging = 5,
    Ranking = 6,
    CreatingOpportunities = 7,
    Completed = 8,
    Failed = 9,
    Cancelled = 10
}
