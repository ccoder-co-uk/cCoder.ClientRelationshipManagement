namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

public abstract class AuditableEntity
{
    public Guid Id { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
}
