using cCoder.ClientRelationshipManagement.Models.Enums;

namespace cCoder.ClientRelationshipManagement.Models.Entities;

public class ClientActivity
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public Guid? ClientContactId { get; set; }
    public Guid? ClientOpportunityId { get; set; }
    public Guid? ClientMaterialId { get; set; }
    public DateTimeOffset ActivityOn { get; set; }
    public ClientActivityType Type { get; set; }
    public ClientActivityDirection Direction { get; set; }
    public string Summary { get; set; }
    public string Outcome { get; set; }
    public string NextAction { get; set; }
    public DateTimeOffset? NextActionDueOn { get; set; }
    public string CreatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }

    public virtual Client Client { get; set; }
    public virtual ClientContact ClientContact { get; set; }
    public virtual ClientOpportunity ClientOpportunity { get; set; }
    public virtual ClientMaterial ClientMaterial { get; set; }
}
