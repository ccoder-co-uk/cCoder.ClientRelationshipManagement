using cCoder.ClientRelationshipManagement.Platform.Models.Entities;

namespace ClientRelationshipManagement.Web.Services.Agents;

public interface IProcessDraftService
{
    ValueTask<ProcessDefinition> CreateDraftAsync(
        Guid sourceProcessDefinitionId,
        string proposedBy,
        string proposedByAgent,
        string changeSummary,
        string name,
        string description,
        IReadOnlyList<ProcessStepDraftUpdate> stepUpdates,
        CancellationToken cancellationToken = default);

    ValueTask<ProcessDefinition> ActivateDraftAsync(
        Guid draftProcessDefinitionId,
        string approvedBy,
        string approvalNotes,
        CancellationToken cancellationToken = default);
}

public sealed class ProcessStepDraftUpdate
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; }
    public string Objective { get; set; }
    public string RequiredFacts { get; set; }
    public string ProducedFacts { get; set; }
    public string ViabilityImpact { get; set; }
    public string TaskInstructionsTemplate { get; set; }
    public string EmailSubjectTemplate { get; set; }
    public string EmailBodyTemplate { get; set; }
    public string CallScriptTemplate { get; set; }
    public string QuestionSetTemplate { get; set; }
}
