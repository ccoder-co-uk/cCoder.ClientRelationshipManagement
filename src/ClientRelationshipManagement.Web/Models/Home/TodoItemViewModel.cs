using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace ClientRelationshipManagement.Web.Models.Home;

public class TodoItemViewModel
{
    public Guid Id { get; init; }
    public Guid ClientId { get; init; }
    public string SourceType { get; init; }
    public string SourceLabel { get; init; }
    public string Lane { get; init; }
    public int SourcePriority { get; init; }
    public string Title { get; init; }
    public string Context { get; init; }
    public string Description { get; init; }
    public string DetailUrl { get; init; }
    public DateTimeOffset DueOn { get; init; }
    public string DueLabel { get; init; }
    public bool IsOverdue { get; init; }
    public bool IsAgentWorking { get; init; }
    public ProcessActionType? ProcessActionType { get; init; }
    public string ProcessInstructions { get; init; }
    public string ProcessCallScript { get; init; }
    public string ProcessQuestionSet { get; init; }
    public IReadOnlyList<TodoOutcomeOptionViewModel> OutcomeOptions { get; init; } = Array.Empty<TodoOutcomeOptionViewModel>();
    public Guid? DraftEmailId { get; init; }
    public Guid? DraftEmailMaterialId { get; init; }
    public Guid? DraftEmailClientOpportunityId { get; init; }
    public string DraftEmailDirectionValue { get; init; }
    public string DraftEmailStatusLabel { get; init; }
    public string DraftEmailToAddresses { get; init; }
    public string DraftEmailCcAddresses { get; init; }
    public string DraftEmailBccAddresses { get; init; }
    public string DraftEmailScheduledSendOnValue { get; init; }
    public string DraftEmailSubject { get; init; }
    public string DraftEmailBody { get; init; }
    public bool IsProcessTask => ProcessActionType.HasValue;
    public bool RequiresOutcomeSelection => OutcomeOptions.Count > 1;
    public bool HasProcessGuidance =>
        !string.IsNullOrWhiteSpace(ProcessInstructions)
        || !string.IsNullOrWhiteSpace(ProcessCallScript)
        || !string.IsNullOrWhiteSpace(ProcessQuestionSet);
    public bool HasDraftEmail =>
        DraftEmailId.HasValue
        && !string.IsNullOrWhiteSpace(DraftEmailSubject)
        && !string.IsNullOrWhiteSpace(DraftEmailBody);
}
