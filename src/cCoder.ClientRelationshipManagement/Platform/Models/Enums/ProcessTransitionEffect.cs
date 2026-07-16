namespace cCoder.ClientRelationshipManagement.Platform.Models.Enums;

public enum ProcessTransitionEffect
{
    None = 0,
    QualifyLeadAndCreateOpportunity = 10,
    DeferLead = 15,
    RejectLead = 20,
    CreateClientAccount = 30,
    CloseOpportunityAsWon = 40,
    CloseOpportunityAsLost = 50,
    CloseClientAccount = 60
}
