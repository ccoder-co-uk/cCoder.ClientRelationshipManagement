namespace ClientRelationshipManagement.Web.Services.Agents;

public interface IAgentExecutionTokenService
{
    ValueTask<string> IssueAsync(string userId);
}
