namespace ClientRelationshipManagement.Web.Models.AgentWorkflow;

public sealed class CreateProcessDraftProposalRequest
{
    public Guid? AgentMessageId { get; set; }
    public string ProposedByAgent { get; set; }
    public string ChangeSummary { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string CorrelationKey { get; set; }
    public string ApprovalTitle { get; set; }
    public string ApprovalBody { get; set; }
    public List<ProcessStepDraftUpdateRequest> StepUpdates { get; set; } = [];
}

public sealed class AppendAgentMessageEntryRequest
{
    public string Body { get; set; } = string.Empty;
}

public sealed class ProcessStepDraftUpdateRequest
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; }
    public string Objective { get; set; }
    public string RequiredFacts { get; set; }
    public string ProducedFacts { get; set; }
    public string ViabilityImpact { get; set; }
    public string TaskInstructionsTemplate { get; set; }
    public string EmailSubjectTemplate { get; set; }
    public string EmailBodyTemplate { get; set; }
    public string CallScriptTemplate { get; set; }
    public string QuestionSetTemplate { get; set; }
}
