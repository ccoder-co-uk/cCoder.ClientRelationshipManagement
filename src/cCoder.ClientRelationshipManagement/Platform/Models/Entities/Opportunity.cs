using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

public class Opportunity : ICrmEntity
{
    public Guid Id { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public string LegacyId { get; set; }
    public Guid TenantCompanyRelationshipId { get; set; }
    public Guid? PrimaryRelationshipContactId { get; set; }
    public OpportunityType Type { get; set; }
    public SalesPipelineStage Stage { get; set; }
    public decimal? EstimatedAnnualValue { get; set; }
    public decimal? Probability { get; set; }
    public string PainSummary { get; set; }
    public string ValueHypothesis { get; set; }
    public string DecisionProcess { get; set; }
    public DateTimeOffset? WonOn { get; set; }
    public DateTimeOffset? LostOn { get; set; }

    public virtual TenantCompanyRelationship TenantCompanyRelationship { get; set; }
    public virtual RelationshipContact PrimaryRelationshipContact { get; set; }
    public virtual ICollection<Activity> Activities { get; set; } = new List<Activity>();
    public virtual ICollection<Material> Materials { get; set; } = new List<Material>();
    public virtual ICollection<Email> Emails { get; set; } = new List<Email>();
    public virtual ICollection<ProcessInstance> ProcessInstances { get; set; } = new List<ProcessInstance>();
    public virtual ICollection<ProcessTask> ProcessTasks { get; set; } = new List<ProcessTask>();
    public virtual ICollection<Lead> Leads { get; set; } = new List<Lead>();
}
