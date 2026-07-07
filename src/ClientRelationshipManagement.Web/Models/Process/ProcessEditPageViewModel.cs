namespace ClientRelationshipManagement.Web.Models.Process;

public sealed class ProcessEditPageViewModel
{
    public string Notice { get; init; } = string.Empty;
    public ProcessDefinitionEditorViewModel Definition { get; init; } = new();
    public IReadOnlyList<ProcessStepEditorViewModel> Steps { get; init; } = Array.Empty<ProcessStepEditorViewModel>();
    public IReadOnlyList<string> SupportedTokens { get; init; } = Array.Empty<string>();
}
