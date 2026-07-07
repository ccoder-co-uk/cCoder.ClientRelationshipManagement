namespace ClientRelationshipManagement.Web.Configuration;

public sealed class AuthorityDataOptions
{
    public bool Enabled { get; set; }
    public int IntervalHours { get; set; } = 24;
    public string DropPath { get; set; } = "..\\Authority Data";
    public string ArchivePath { get; set; } = "Agent Workspace\\Authority Data\\Archive";
    public string FailedPath { get; set; } = "Agent Workspace\\Authority Data\\Failed";
    public string SourceSystem { get; set; } = "CompaniesHouse";
    public string DefaultTenantId { get; set; } = "default";
    public int BatchSize { get; set; } = 5000;
    public int MergeBatchSize { get; set; } = 10000;
    public int MaxMergeChunksPerRun { get; set; } = 50;
    public int MaxRunMinutes { get; set; } = 55;
}
