namespace ClientRelationshipManagement.Web.Services.Agents;

public interface IAgentWorkspaceService
{
    string RootPath { get; }
    string GetTaskAgentWorkingDirectory();
    string GetProcessOptimiserWorkingDirectory();
    string GetProcessOptimiserSessionHistoryDirectory();
    ValueTask<string> ReadTaskAgentPromptAsync(CancellationToken cancellationToken = default);
    ValueTask<string> ReadProcessOptimiserPromptAsync(CancellationToken cancellationToken = default);
}
