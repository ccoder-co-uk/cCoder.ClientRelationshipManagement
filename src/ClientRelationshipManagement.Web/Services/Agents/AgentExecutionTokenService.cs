using cCoder.Security.Exposures;
using cCoder.Security.Objects.Entities;

namespace ClientRelationshipManagement.Web.Services.Agents;

public sealed class AgentExecutionTokenService(ITokenManager tokenManager)
    : IAgentExecutionTokenService
{
    public async ValueTask<string> IssueAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return string.Empty;

        return (await tokenManager.IssueTokenAsync(userId, TokenUse.WorkflowExecution)).Id ?? string.Empty;
    }
}
