namespace cCoder.ClientRelationshipManagement.Platform.Models.Enums;

public enum EmailState
{
    Draft = 0,
    Approved = 10,
    Sending = 20,
    Sent = 30,
    Failed = 40,
    Cancelled = 50,
    Rejected = 60
}
