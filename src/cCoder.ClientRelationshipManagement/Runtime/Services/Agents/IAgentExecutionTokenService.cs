namespace cCoder.ClientRelationshipManagement.Runtime.Services.Agents;

public interface IAgentExecutionTokenService
{
    ValueTask<string> IssueAsync(string userId);
}
