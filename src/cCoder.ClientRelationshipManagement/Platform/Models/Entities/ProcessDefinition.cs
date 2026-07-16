using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

public class ProcessDefinition : ICrmEntity
{
    public Guid Id { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public string TenantId { get; set; }
    public ProcessScopeType ScopeType { get; set; }
    public Guid? FamilyId { get; set; }
    public Guid? SupersedesProcessDefinitionId { get; set; }
    public int VersionNumber { get; set; }
    public ProcessDefinitionLifecycleState LifecycleState { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public string ChangeSummary { get; set; }
    public string ApprovalNotes { get; set; }
    public string ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedOn { get; set; }
    public string ProposedByAgent { get; set; }

    public virtual ICollection<ProcessStep> Steps { get; set; } = new List<ProcessStep>();
    public virtual ICollection<ProcessInstance> Instances { get; set; } = new List<ProcessInstance>();
    public virtual ProcessDefinition SupersedesProcessDefinition { get; set; }
    public virtual ICollection<ProcessDefinition> ProposedVersions { get; set; } = new List<ProcessDefinition>();
    public virtual ICollection<AgentMessage> Messages { get; set; } = new List<AgentMessage>();
    public virtual ICollection<AgentMessage> ProposedMessages { get; set; } = new List<AgentMessage>();
}
