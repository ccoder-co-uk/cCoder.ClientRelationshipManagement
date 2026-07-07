using ClientRelationshipManagement.Web.Configuration;
using Microsoft.Extensions.Options;

namespace ClientRelationshipManagement.Web.Services.Agents;

public sealed class AgentWorkspaceService(
    IHostEnvironment environment,
    IOptions<AgentWorkflowOptions> options)
    : IAgentWorkspaceService
{
    public string RootPath
    {
        get
        {
            string configuredPath = options.Value.AgentWorkspacePath;
            if (Path.IsPathRooted(configuredPath))
                return configuredPath;

            return Path.GetFullPath(Path.Combine(
                environment.ContentRootPath,
                "..",
                "..",
                configuredPath));
        }
    }

    public string GetTaskAgentWorkingDirectory() =>
        Path.Combine(RootPath, "Task Agent");

    public string GetProcessOptimiserWorkingDirectory() =>
        Path.Combine(RootPath, "Process Optimiser");

    public string GetProcessOptimiserSessionHistoryDirectory() =>
        Path.Combine(GetProcessOptimiserWorkingDirectory(), "Session History");

    public ValueTask<string> ReadTaskAgentPromptAsync(CancellationToken cancellationToken = default) =>
        ReadFileAsync(Path.Combine(GetTaskAgentWorkingDirectory(), "system-prompt.md"), cancellationToken);

    public ValueTask<string> ReadProcessOptimiserPromptAsync(CancellationToken cancellationToken = default) =>
        ReadFileAsync(Path.Combine(GetProcessOptimiserWorkingDirectory(), "system-prompt.md"), cancellationToken);

    static async ValueTask<string> ReadFileAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            return string.Empty;

        return await File.ReadAllTextAsync(path, cancellationToken);
    }
}
