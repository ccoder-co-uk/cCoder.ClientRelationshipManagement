namespace ClientRelationshipManagement.Web.Models.Documentation;

public sealed class DocumentationPageViewModel
{
    public string Title { get; init; } = string.Empty;
    public string Eyebrow { get; init; } = string.Empty;
    public string Lead { get; init; } = string.Empty;
    public string CurrentSlug { get; init; } = string.Empty;
    public IReadOnlyList<DocumentationSectionDefinition> Sections { get; init; } =
        Array.Empty<DocumentationSectionDefinition>();
    public IReadOnlyList<DocumentationNavItemViewModel> Navigation { get; init; } =
        Array.Empty<DocumentationNavItemViewModel>();
}
