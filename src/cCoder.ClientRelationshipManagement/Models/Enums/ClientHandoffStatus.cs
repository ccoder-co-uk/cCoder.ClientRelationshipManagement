namespace cCoder.ClientRelationshipManagement.Models.Enums;

public enum ClientHandoffStatus
{
    NotStarted = 0,
    Drafting = 10,
    ReadyForReview = 20,
    AcceptedByOnboarding = 30,
    ReworkRequired = 40,
    Completed = 50
}
