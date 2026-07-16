namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

public sealed class AgentAutomationSetting : AuditableEntity
{
    public string UserId { get; set; }
    public bool AutoApproveProcessEmails { get; set; }
    public DateTimeOffset? LastMailboxSyncOn { get; set; }
    public DateTimeOffset? MailboxEvidenceBackfillCompletedOn { get; set; }
    public string SelectedAiProfileKey { get; set; }
    public string SelectedAiModel { get; set; }
    public int ApprovalAgentConcurrency { get; set; } = 2;
    public string LeadAiProfileKey { get; set; }
    public string LeadAiModel { get; set; }
    public int LeadAgentConcurrency { get; set; } = 1;
    public string OpportunityAiProfileKey { get; set; }
    public string OpportunityAiModel { get; set; }
    public int OpportunityAgentConcurrency { get; set; } = 1;
    public string ClientAiProfileKey { get; set; }
    public string ClientAiModel { get; set; }
    public int ClientAgentConcurrency { get; set; } = 1;
}
