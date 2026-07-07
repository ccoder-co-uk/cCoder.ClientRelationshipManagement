using cCoder.Security.Exposures;

namespace ClientRelationshipManagement.Web.Services.Agents;

public sealed class AgentExecutionTokenService(IAccountManager accountManager)
    : IAgentExecutionTokenService
{
    public async ValueTask<string> IssueAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return string.Empty;

        return (await accountManager.IssueTokenAsync(userId)).Id ?? string.Empty;
    }
}
