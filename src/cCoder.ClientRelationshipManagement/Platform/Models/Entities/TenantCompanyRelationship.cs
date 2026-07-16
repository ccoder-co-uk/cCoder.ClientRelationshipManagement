using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

public class TenantCompanyRelationship : ICrmEntity
{
    public Guid Id { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public string LegacyId { get; set; }
    public string TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public string AccountOwnerUserId { get; set; }
    public string AccountOwnerDisplayName { get; set; }
    public RelationshipStatus Status { get; set; }
    public SalesPipelineStage CurrentStage { get; set; }
    public RelationshipPriority Priority { get; set; }
    public string LeadSource { get; set; }
    public string InitialRoute { get; set; }
    public decimal? FitScore { get; set; }
    public string OpportunitySummary { get; set; }
    public string PreferredOpeningAngle { get; set; }
    public string ResearchSummary { get; set; }
    public string DataQualityNotes { get; set; }
    public bool IsArchived { get; set; }

    public virtual Company Company { get; set; }
    public virtual ICollection<RelationshipContact> Contacts { get; set; } = new List<RelationshipContact>();
    public virtual ICollection<Opportunity> Opportunities { get; set; } = new List<Opportunity>();
    public virtual ICollection<Activity> Activities { get; set; } = new List<Activity>();
    public virtual ICollection<Material> Materials { get; set; } = new List<Material>();
    public virtual ICollection<Email> Emails { get; set; } = new List<Email>();
    public virtual ICollection<ClientAccount> ClientAccounts { get; set; } = new List<ClientAccount>();
    public virtual ICollection<ProcessInstance> ProcessInstances { get; set; } = new List<ProcessInstance>();
    public virtual ICollection<ProcessTask> ProcessTasks { get; set; } = new List<ProcessTask>();
    public virtual ICollection<Lead> ConvertedLeads { get; set; } = new List<Lead>();
}
