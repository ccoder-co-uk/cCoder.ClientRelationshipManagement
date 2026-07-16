using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

public class AgentMessage : ICrmEntity
{
    public Guid Id { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public string TenantId { get; set; } = "default";
    public Guid? AgentRunId { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? TenantCompanyRelationshipId { get; set; }
    public Guid? OpportunityId { get; set; }
    public Guid? ClientAccountId { get; set; }
    public Guid? ProcessTaskId { get; set; }
    public Guid? ProcessStepId { get; set; }
    public Guid? EmailId { get; set; }
    public Guid? ProcessDefinitionId { get; set; }
    public Guid? ProposedProcessDefinitionId { get; set; }
    public AgentMessageKind Kind { get; set; }
    public AgentMessageState State { get; set; }
    public string CorrelationKey { get; set; }
    public string Title { get; set; }
    public string Body { get; set; }
    public string AgentName { get; set; }
    public string ResponseNotes { get; set; }
    public string RespondedBy { get; set; }
    public DateTimeOffset? RespondedOn { get; set; }

    public virtual AgentRun AgentRun { get; set; }
    public virtual Lead Lead { get; set; }
    public virtual TenantCompanyRelationship TenantCompanyRelationship { get; set; }
    public virtual Opportunity Opportunity { get; set; }
    public virtual ClientAccount ClientAccount { get; set; }
    public virtual ProcessTask ProcessTask { get; set; }
    public virtual ProcessStep ProcessStep { get; set; }
    public virtual Email Email { get; set; }
    public virtual ProcessDefinition ProcessDefinition { get; set; }
    public virtual ProcessDefinition ProposedProcessDefinition { get; set; }
    public virtual ICollection<AgentMessageEntry> Entries { get; set; } = new List<AgentMessageEntry>();
}
