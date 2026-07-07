namespace ClientRelationshipManagement.Web.Models.Documentation;

public sealed class DocumentationSectionDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public IReadOnlyList<string> Paragraphs { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Bullets { get; init; } = Array.Empty<string>();
}
