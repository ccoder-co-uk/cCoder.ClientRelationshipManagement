using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

public class Material : AuditableEntity
{
    public string LegacyId { get; set; }
    public Guid TenantCompanyRelationshipId { get; set; }
    public Guid? OpportunityId { get; set; }
    public Guid? ClientAccountId { get; set; }
    public Guid? CompanyContactId { get; set; }
    public string Name { get; set; }
    public MaterialType Type { get; set; }
    public MaterialStatus Status { get; set; }
    public string Notes { get; set; }
    public string FilePath { get; set; }
    public DateTimeOffset? SentOn { get; set; }

    public virtual TenantCompanyRelationship TenantCompanyRelationship { get; set; }
    public virtual Opportunity Opportunity { get; set; }
    public virtual ClientAccount ClientAccount { get; set; }
    public virtual CompanyContact CompanyContact { get; set; }
    public virtual ICollection<Activity> Activities { get; set; } = new List<Activity>();
    public virtual ICollection<Email> Emails { get; set; } = new List<Email>();
}
