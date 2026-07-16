namespace ClientRelationshipManagement.Web.Configuration;

public sealed class AiRoutingOptions
{
    public const string SectionName = "AgentAiRouting";

    public string DefaultProfile { get; set; } = "local-ollama";
    public Dictionary<string, AiRoutingProfileOptions> Profiles { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class AiRoutingProfileOptions
{
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string BaseProvider { get; set; } = "Ollama";
    public string CompletionEndpoint { get; set; } = string.Empty;
    public string ModelEndpoint { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int MaxConcurrency { get; set; } = 1;
    public string ExecutablePath { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string ReasoningEffort { get; set; } = "low";
    public bool UseOss { get; set; }
    public string LocalProvider { get; set; } = string.Empty;
}
