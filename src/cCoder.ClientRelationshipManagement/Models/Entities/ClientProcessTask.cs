using cCoder.ClientRelationshipManagement.Models.Enums;

namespace cCoder.ClientRelationshipManagement.Models.Entities;

public class ClientProcessTask
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public Guid ClientProcessInstanceId { get; set; }
    public Guid ClientProcessStepId { get; set; }
    public Guid? EmailId { get; set; }
    public ClientProcessActionType ActionType { get; set; }
    public ClientProcessTaskState State { get; set; }
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
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public virtual Client Client { get; set; }
    public virtual ClientProcessInstance ClientProcessInstance { get; set; }
    public virtual ClientProcessStep ClientProcessStep { get; set; }
    public virtual Email Email { get; set; }
}
