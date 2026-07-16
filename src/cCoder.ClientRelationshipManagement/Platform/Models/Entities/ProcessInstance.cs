using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

public class ProcessInstance : ICrmEntity
{
    public Guid Id { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public Guid ProcessDefinitionId { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? TenantCompanyRelationshipId { get; set; }
    public Guid? OpportunityId { get; set; }
    public Guid? ClientAccountId { get; set; }
    public Guid? CurrentProcessStepId { get; set; }
    public Guid? CurrentProcessTaskId { get; set; }
    public ProcessInstanceState State { get; set; }
    public string CompletionOutcomeKey { get; set; }
    public DateTimeOffset StartedOn { get; set; }
    public DateTimeOffset? CompletedOn { get; set; }

    public virtual ProcessDefinition ProcessDefinition { get; set; }
    public virtual Lead Lead { get; set; }
    public virtual TenantCompanyRelationship TenantCompanyRelationship { get; set; }
    public virtual Opportunity Opportunity { get; set; }
    public virtual ClientAccount ClientAccount { get; set; }
    public virtual ProcessStep CurrentProcessStep { get; set; }
    public virtual ProcessTask CurrentProcessTask { get; set; }
    public virtual ICollection<ProcessTask> Tasks { get; set; } = new List<ProcessTask>();
}
