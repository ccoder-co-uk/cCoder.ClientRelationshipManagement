namespace ClientRelationshipManagement.Web.Configuration;

public sealed class ImportWorkflowOptions
{
    public string HostedServicesBaseUrl { get; set; } = "https://localhost:7295";
    public string AgentWorkspacePath { get; set; } = "Agent Workspace";
    public int UploadSessionExpiryMinutes { get; set; } = 120;
    public int ChunkSizeBytes { get; set; } = 4 * 1024 * 1024;
    public int ProcessingIntervalMinutes { get; set; } = 5;
    public int ProcessingBatchSize { get; set; } = 1000;
    public int OpportunityScoreThreshold { get; set; } = 75;
}
