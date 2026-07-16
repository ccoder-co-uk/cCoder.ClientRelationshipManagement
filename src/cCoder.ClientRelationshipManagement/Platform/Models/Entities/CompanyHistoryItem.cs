namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

public class CompanyHistoryItem : ICrmEntity
{
    public Guid Id { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public Guid CompanyId { get; set; }
    public string TenantId { get; set; }
    public DateTimeOffset OccurredOn { get; set; }
    public string Lane { get; set; }
    public string EventType { get; set; }
    public string Summary { get; set; }
    public string Details { get; set; }
    public string FactKey { get; set; }
    public string FactValue { get; set; }
    public string Confidence { get; set; }
    public string SourceType { get; set; }
    public Guid? SourceId { get; set; }
    public Guid? ProcessDefinitionId { get; set; }
    public Guid? ProcessInstanceId { get; set; }
    public Guid? ProcessStepId { get; set; }
    public Guid? ProcessTaskId { get; set; }
    public bool IsPrivate { get; set; }

    public virtual Company Company { get; set; }
}
