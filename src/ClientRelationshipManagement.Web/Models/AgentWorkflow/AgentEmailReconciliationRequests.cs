namespace ClientRelationshipManagement.Web.Models.AgentWorkflow;

public sealed class ReconcileSentEmailRequest
{
    public string MailboxExternalId { get; set; }
    public Guid? OpportunityId { get; set; }
}

public sealed class CancelUnverifiedEmailRequest
{
    public string Reason { get; set; }
}
