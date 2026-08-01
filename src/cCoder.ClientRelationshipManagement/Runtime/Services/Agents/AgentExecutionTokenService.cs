using cCoder.Security.Exposures;
using cCoder.Security.Models.Entities;

namespace cCoder.ClientRelationshipManagement.Runtime.Services.Agents;

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
