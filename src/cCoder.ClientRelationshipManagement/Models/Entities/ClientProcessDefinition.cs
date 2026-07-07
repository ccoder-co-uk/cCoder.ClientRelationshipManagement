namespace cCoder.ClientRelationshipManagement.Models.Entities;

public class ClientProcessDefinition
{
    public Guid Id { get; set; }
    public string TenantId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public virtual ICollection<ClientProcessStep> Steps { get; set; } = new List<ClientProcessStep>();
    public virtual ICollection<ClientProcessInstance> Instances { get; set; } = new List<ClientProcessInstance>();
}
