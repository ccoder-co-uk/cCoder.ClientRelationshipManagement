namespace ClientRelationshipManagement.Web.Models.AgentWorkflow;

public sealed class AgentReplacementEmailDraftRequest
{
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string ToAddresses { get; set; }
    public string CcAddresses { get; set; }
    public string BccAddresses { get; set; }
    public string ApprovalTitle { get; set; }
    public string ApprovalBody { get; set; }
}
