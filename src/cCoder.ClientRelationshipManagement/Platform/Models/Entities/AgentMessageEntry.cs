namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

public class AgentMessageEntry : AuditableEntity
{
    public Guid AgentMessageId { get; set; }
    public string Role { get; set; }
    public string Body { get; set; }

    public virtual AgentMessage AgentMessage { get; set; }
}
