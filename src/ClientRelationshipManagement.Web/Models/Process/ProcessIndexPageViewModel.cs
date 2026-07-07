namespace ClientRelationshipManagement.Web.Models.Process;

public sealed class ProcessIndexPageViewModel
{
    public string Notice { get; init; } = string.Empty;
    public IReadOnlyList<ProcessDefinitionSummaryViewModel> Definitions { get; init; } = Array.Empty<ProcessDefinitionSummaryViewModel>();
    public ProcessDefinitionEditorViewModel NewDefinition { get; init; } = new();
}
