namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

public class ImportLink : AuditableEntity
{
    public Guid ImportId { get; set; }
    public Guid SourceId { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? CompanyContactId { get; set; }
    public string SourceRowKey { get; set; }
    public long? SourceRowNumber { get; set; }

    public virtual Import Import { get; set; }
    public virtual Source Source { get; set; }
    public virtual Company Company { get; set; }
    public virtual Lead Lead { get; set; }
    public virtual CompanyContact CompanyContact { get; set; }
}
