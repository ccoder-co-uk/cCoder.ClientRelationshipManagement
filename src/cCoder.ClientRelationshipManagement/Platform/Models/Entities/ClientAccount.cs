using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

public class ClientAccount : ICrmEntity
{
    public Guid Id { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public Guid TenantCompanyRelationshipId { get; set; }
    public Guid? WonOpportunityId { get; set; }
    public ClientAccountStatus Status { get; set; }
    public DateTimeOffset? ContractSignedOn { get; set; }
    public DateTimeOffset? GoLiveOn { get; set; }
    public string AccountReference { get; set; }
    public string BillingNotes { get; set; }

    public virtual TenantCompanyRelationship TenantCompanyRelationship { get; set; }
    public virtual Opportunity WonOpportunity { get; set; }
    public virtual ICollection<HandoffPack> HandoffPacks { get; set; } = new List<HandoffPack>();
    public virtual ICollection<Activity> Activities { get; set; } = new List<Activity>();
    public virtual ICollection<Material> Materials { get; set; } = new List<Material>();
    public virtual ICollection<Email> Emails { get; set; } = new List<Email>();
    public virtual ICollection<ProcessInstance> ProcessInstances { get; set; } = new List<ProcessInstance>();
    public virtual ICollection<ProcessTask> ProcessTasks { get; set; } = new List<ProcessTask>();
}
