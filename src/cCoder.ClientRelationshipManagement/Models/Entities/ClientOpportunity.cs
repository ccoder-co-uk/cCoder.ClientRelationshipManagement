using cCoder.ClientRelationshipManagement.Models.Enums;

namespace cCoder.ClientRelationshipManagement.Models.Entities;

public class ClientOpportunity
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public Guid? PrimaryContactId { get; set; }
    public ClientOpportunityType Type { get; set; }
    public PipelineStage Stage { get; set; }
    public decimal? EstimatedAnnualValue { get; set; }
    public decimal? Probability { get; set; }
    public string PainSummary { get; set; }
    public string ValueHypothesis { get; set; }
    public string DecisionProcess { get; set; }
    public string NextAction { get; set; }
    public DateTimeOffset? NextActionDueOn { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public virtual Client Client { get; set; }
    public virtual ClientContact PrimaryContact { get; set; }
    public virtual ICollection<ClientActivity> Activities { get; set; } = new List<ClientActivity>();
    public virtual ICollection<ClientHandoffPack> HandoffPacks { get; set; } = new List<ClientHandoffPack>();
}
