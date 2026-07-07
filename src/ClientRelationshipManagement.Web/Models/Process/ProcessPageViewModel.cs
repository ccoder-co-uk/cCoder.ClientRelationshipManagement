namespace ClientRelationshipManagement.Web.Models.Process;

public sealed class ProcessPageViewModel
{
    public string Notice { get; init; } = string.Empty;
    public Guid SelectedProcessId { get; init; }
    public IReadOnlyList<ProcessDefinitionSummaryViewModel> Definitions { get; init; } = Array.Empty<ProcessDefinitionSummaryViewModel>();
    public ProcessDefinitionEditorViewModel Definition { get; init; } = new();
    public IReadOnlyList<ProcessStepEditorViewModel> Steps { get; init; } = Array.Empty<ProcessStepEditorViewModel>();
    public IReadOnlyList<string> SupportedTokens { get; init; } = Array.Empty<string>();
}
