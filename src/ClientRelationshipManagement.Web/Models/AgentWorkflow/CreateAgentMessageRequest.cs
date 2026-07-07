using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace ClientRelationshipManagement.Web.Models.AgentWorkflow;

public sealed class CreateAgentMessageRequest
{
    public Guid? AgentRunId { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? OpportunityId { get; set; }
    public Guid? ClientAccountId { get; set; }
    public Guid? ProcessTaskId { get; set; }
    public Guid? EmailId { get; set; }
    public Guid? ProcessDefinitionId { get; set; }
    public Guid? ProposedProcessDefinitionId { get; set; }
    public AgentMessageKind Kind { get; set; } = AgentMessageKind.Information;
    public AgentMessageState State { get; set; } = AgentMessageState.Pending;
    public string CorrelationKey { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string AgentName { get; set; }
}
