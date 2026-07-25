namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

/// <summary>
/// A durable, source-addressable fact about a company. Workflow instances may
/// consume this evidence, but it remains attached to the company between runs.
/// </summary>
public class CompanyEvidence : ICrmEntity
{
    public Guid Id { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public Guid CompanyId { get; set; }
    public string Key { get; set; }
    public string ValueJson { get; set; }
    public string SourceUrl { get; set; }
    public string SourceTitle { get; set; }
    public string SourceSnippet { get; set; }
    public string Extractor { get; set; }
    public string ResourceHash { get; set; }
    public DateTimeOffset ObservedOn { get; set; }

    public virtual Company Company { get; set; }
}
