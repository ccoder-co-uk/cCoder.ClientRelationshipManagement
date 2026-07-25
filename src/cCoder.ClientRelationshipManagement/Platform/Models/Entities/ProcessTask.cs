using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

public class ProcessTask : ICrmEntity
{
    public Guid Id { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public Guid ProcessInstanceId { get; set; }
    public Guid ProcessStepId { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? TenantCompanyRelationshipId { get; set; }
    public Guid? OpportunityId { get; set; }
    public Guid? ClientAccountId { get; set; }
    public Guid? EmailId { get; set; }
    public ProcessActionType ActionType { get; set; }
    public ProcessTaskState State { get; set; }
    public DateTimeOffset DueOn { get; set; }
    public string RenderedTitle { get; set; }
    public string RenderedInstructions { get; set; }
    public string RenderedEmailSubject { get; set; }
    public string RenderedEmailBody { get; set; }
    public string RenderedCallScript { get; set; }
    public string RenderedQuestionSet { get; set; }
    public string CompletionOutcomeKey { get; set; }
    public string CompletionNotes { get; set; }
    public DateTimeOffset? CompletedOn { get; set; }
    public string CompletedBy { get; set; }
    public Guid? AgentClaimId { get; set; }
    public string AgentClaimedBy { get; set; }
    public DateTimeOffset? AgentClaimedOn { get; set; }
    public DateTimeOffset? AgentClaimExpiresOn { get; set; }

    public virtual ProcessInstance ProcessInstance { get; set; }
    public virtual ProcessStep ProcessStep { get; set; }
    public virtual Lead Lead { get; set; }
    public virtual TenantCompanyRelationship TenantCompanyRelationship { get; set; }
    public virtual Opportunity Opportunity { get; set; }
    public virtual ClientAccount ClientAccount { get; set; }
    public virtual Email Email { get; set; }
    public virtual ICollection<ProcessStepTaskRun> StepTaskRuns { get; set; } = new List<ProcessStepTaskRun>();
}
