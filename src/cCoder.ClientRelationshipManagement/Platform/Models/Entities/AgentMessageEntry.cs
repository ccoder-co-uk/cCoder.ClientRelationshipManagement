namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

public class AgentMessageEntry : ICrmEntity
{
    public Guid Id { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public Guid AgentMessageId { get; set; }
    public string Role { get; set; }
    public string Body { get; set; }

    public virtual AgentMessage AgentMessage { get; set; }
}
