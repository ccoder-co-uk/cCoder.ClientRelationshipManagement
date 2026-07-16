using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

public class Activity : ICrmEntity
{
    public Guid Id { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public string LegacyId { get; set; }
    public Guid TenantCompanyRelationshipId { get; set; }
    public Guid? OpportunityId { get; set; }
    public Guid? ClientAccountId { get; set; }
    public Guid? CompanyContactId { get; set; }
    public Guid? MaterialId { get; set; }
    public DateTimeOffset ActivityOn { get; set; }
    public ActivityType Type { get; set; }
    public ActivityDirection Direction { get; set; }
    public string Summary { get; set; }
    public string Outcome { get; set; }
    public string NextAction { get; set; }
    public DateTimeOffset? NextActionDueOn { get; set; }

    public virtual TenantCompanyRelationship TenantCompanyRelationship { get; set; }
    public virtual Opportunity Opportunity { get; set; }
    public virtual ClientAccount ClientAccount { get; set; }
    public virtual CompanyContact CompanyContact { get; set; }
    public virtual Material Material { get; set; }
}
