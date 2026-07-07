using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

public class RelationshipContact : AuditableEntity
{
    public string LegacyId { get; set; }
    public Guid TenantCompanyRelationshipId { get; set; }
    public Guid CompanyContactId { get; set; }
    public RelationshipContactStatus Status { get; set; }
    public bool IsPrimary { get; set; }
    public string RelationshipRoute { get; set; }
    public string Source { get; set; }
    public string Notes { get; set; }

    public virtual TenantCompanyRelationship TenantCompanyRelationship { get; set; }
    public virtual CompanyContact CompanyContact { get; set; }
    public virtual ICollection<Opportunity> Opportunities { get; set; } = new List<Opportunity>();
}
