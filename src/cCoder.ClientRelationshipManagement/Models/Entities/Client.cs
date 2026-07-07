using cCoder.ClientRelationshipManagement.Models.Enums;

namespace cCoder.ClientRelationshipManagement.Models.Entities;

public class Client
{
    public Guid Id { get; set; }
    public string TenantId { get; set; }
    public string AccountOwner { get; set; }
    public RelationshipStatus Status { get; set; }
    public PipelineStage CurrentStage { get; set; }
    public ClientPriority Priority { get; set; }
    public string LeadSource { get; set; }
    public string InitialRoute { get; set; }
    public decimal? FitScore { get; set; }
    public string OpportunitySummary { get; set; }
    public string PreferredOpeningAngle { get; set; }
    public string NextAction { get; set; }
    public DateTimeOffset? NextActionDueOn { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
    public bool IsArchived { get; set; }

    public virtual Company Company { get; set; }
    public virtual ICollection<ClientContact> Contacts { get; set; } = new List<ClientContact>();
    public virtual ICollection<ClientOpportunity> Opportunities { get; set; } = new List<ClientOpportunity>();
    public virtual ICollection<ClientActivity> Activities { get; set; } = new List<ClientActivity>();
    public virtual ICollection<ClientMaterial> Materials { get; set; } = new List<ClientMaterial>();
    public virtual ICollection<Email> Emails { get; set; } = new List<Email>();
    public virtual ICollection<ClientHandoffPack> HandoffPacks { get; set; } = new List<ClientHandoffPack>();
    public virtual ICollection<ClientProcessInstance> ProcessInstances { get; set; } = new List<ClientProcessInstance>();
    public virtual ICollection<ClientProcessTask> ProcessTasks { get; set; } = new List<ClientProcessTask>();
}
