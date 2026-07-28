namespace cCoder.ClientRelationshipManagement.Platform.Models.Enums;

/// <summary>
/// Defines how a workflow step is executed. Unlike <see cref="ProcessActionType"/>,
/// this is an executable contract rather than a presentation category.
/// </summary>
public enum ProcessStepType
{
    AskAgent = 0,
    RegistryLookup = 10,
    WebSearch = 20,
    ExtractEvidence = 30,
    EvaluateRule = 40,
    CreateEmail = 50,
    HumanAction = 60,
    WaitForEvent = 70
}
