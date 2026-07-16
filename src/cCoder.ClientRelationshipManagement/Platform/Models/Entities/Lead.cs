using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

public class Lead : ICrmEntity
{
    public Guid Id { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public Guid? SourceId { get; set; }
    public string SourceSystem { get; set; }
    public string SourceRecordId { get; set; }
    public string SourceFileName { get; set; }
    public string TenantId { get; set; }
    public LeadStatus Status { get; set; }
    public string RawCompanyName { get; set; }
    public string RawTradingName { get; set; }
    public string RawCompanyNumber { get; set; }
    public string RawVatNumber { get; set; }
    public string RawWebsiteUrl { get; set; }
    public string RawContactEmailAddress { get; set; }
    public string RawContactPhoneNumber { get; set; }
    public string QualificationNotes { get; set; }
    public int? RankingScore { get; set; }
    public string RankingRationale { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? TenantCompanyRelationshipId { get; set; }
    public Guid? OpportunityId { get; set; }

    public virtual Source Source { get; set; }
    public virtual Company Company { get; set; }
    public virtual TenantCompanyRelationship TenantCompanyRelationship { get; set; }
    public virtual Opportunity Opportunity { get; set; }
    public virtual ICollection<LeadContact> Contacts { get; set; } = new List<LeadContact>();
    public virtual ICollection<ProcessInstance> ProcessInstances { get; set; } = new List<ProcessInstance>();
    public virtual ICollection<ProcessTask> ProcessTasks { get; set; } = new List<ProcessTask>();
}
