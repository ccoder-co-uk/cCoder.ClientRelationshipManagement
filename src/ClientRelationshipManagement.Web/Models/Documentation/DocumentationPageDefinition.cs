namespace ClientRelationshipManagement.Web.Models.Documentation;

public sealed class DocumentationPageDefinition
{
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Eyebrow { get; init; } = string.Empty;
    public string Lead { get; init; } = string.Empty;
    public IReadOnlyList<DocumentationSectionDefinition> Sections { get; init; } =
        Array.Empty<DocumentationSectionDefinition>();
    public IReadOnlyList<DocumentationPageDefinition> Children { get; init; } =
        Array.Empty<DocumentationPageDefinition>();
}
