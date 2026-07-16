using System.Text.Json;
using cCoder.AI.Models.Responses;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using ClientRelationshipManagement.Web.Configuration;
using Microsoft.Extensions.Options;

namespace ClientRelationshipManagement.Web.Services.Agents;

public sealed class AgentSessionArchiveService(
    IAgentWorkspaceService agentWorkspaceService,
    IOptions<AgentWorkflowOptions> options)
    : IAgentSessionArchiveService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async ValueTask ArchiveCompletedRunAsync(
        AgentRunKind kind,
        Guid runId,
        string executionUserId,
        string provider,
        string model,
        string workingDirectory,
        string systemPrompt,
        string instructions,
        int processedItemCount,
        AgentRunResponse response,
        CancellationToken cancellationToken = default)
    {
        var archive = new AgentSessionArchive
        {
            ArchivedOnUtc = DateTimeOffset.UtcNow,
            ErrorMessage = string.Empty,
            ExecutionUserId = executionUserId,
            FinalMessage = response.FinalMessage,
            Instructions = instructions,
            Iterations = response.Iterations,
            Kind = kind.ToString(),
            Model = model,
            ProcessedItemCount = processedItemCount,
            Provider = provider,
            RunId = runId,
            SessionState = response.Succeeded ? "Succeeded" : "Failed",
            SystemPrompt = systemPrompt,
            WorkingDirectory = workingDirectory,
            IterationResponses = response.IterationResponses,
        };

        await WriteArchiveAsync(kind, runId, archive, cancellationToken);
    }

    public async ValueTask ArchiveFailedRunAsync(
        AgentRunKind kind,
        Guid runId,
        string executionUserId,
        string provider,
        string model,
        string workingDirectory,
        string systemPrompt,
        string instructions,
        int processedItemCount,
        Exception exception,
        CancellationToken cancellationToken = default)
    {
        var archive = new AgentSessionArchive
        {
            ArchivedOnUtc = DateTimeOffset.UtcNow,
            ErrorMessage = exception.ToString(),
            ExecutionUserId = executionUserId,
            FinalMessage = string.Empty,
            Instructions = instructions,
            Iterations = 0,
            Kind = kind.ToString(),
            Model = model,
            ProcessedItemCount = processedItemCount,
            Provider = provider,
            RunId = runId,
            SessionState = "Exception",
            SystemPrompt = systemPrompt,
            WorkingDirectory = workingDirectory,
            IterationResponses = Array.Empty<AgentIterationResponse>(),
        };

        await WriteArchiveAsync(kind, runId, archive, cancellationToken);
    }

    private async ValueTask WriteArchiveAsync(
        AgentRunKind kind,
        Guid runId,
        AgentSessionArchive archive,
        CancellationToken cancellationToken)
    {
        string directoryPath = kind == AgentRunKind.TaskAgent
            ? agentWorkspaceService.GetTaskAgentSessionHistoryDirectory()
            : agentWorkspaceService.GetProcessOptimiserSessionHistoryDirectory();
        Directory.CreateDirectory(directoryPath);

        string fileName =
            $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}__{kind}__{runId:N}.json";

        string filePath = Path.Combine(directoryPath, fileName);
        string content = JsonSerializer.Serialize(archive, JsonSerializerOptions);
        await File.WriteAllTextAsync(filePath, content, cancellationToken);

        EnforceRollingLimit(directoryPath);
    }

    private void EnforceRollingLimit(string directoryPath)
    {
        int limit = Math.Max(1, options.Value.SessionArchiveLimit);

        FileInfo[] files = new DirectoryInfo(directoryPath)
            .GetFiles("*.json", SearchOption.TopDirectoryOnly)
            .OrderByDescending(file => file.CreationTimeUtc)
            .ToArray();

        foreach (FileInfo file in files.Skip(limit))
        {
            file.Delete();
        }
    }

    private sealed class AgentSessionArchive
    {
        public DateTimeOffset ArchivedOnUtc { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string ExecutionUserId { get; set; } = string.Empty;
        public string FinalMessage { get; set; } = string.Empty;
        public string Instructions { get; set; } = string.Empty;
        public IReadOnlyList<AgentIterationResponse> IterationResponses { get; set; } = Array.Empty<AgentIterationResponse>();
        public int Iterations { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int ProcessedItemCount { get; set; }
        public string Provider { get; set; } = string.Empty;
        public Guid RunId { get; set; }
        public string SessionState { get; set; } = string.Empty;
        public string SystemPrompt { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;
    }
}
