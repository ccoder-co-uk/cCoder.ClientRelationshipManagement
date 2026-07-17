namespace ClientRelationshipManagement.Web.Services.Processes;

public interface IProcessValidationService
{
    ValueTask<ProcessValidationResult> ValidateAsync(
        IReadOnlyCollection<string> tenantIds,
        CancellationToken cancellationToken = default);

    ValueTask<ProcessValidationResult> ValidateDefinitionAsync(
        Guid processDefinitionId,
        CancellationToken cancellationToken = default);

    ValueTask<ProcessValidationResult> ValidateActivationAsync(
        Guid processDefinitionId,
        CancellationToken cancellationToken = default);
}

public sealed class ProcessValidationResult
{
    public DateTimeOffset ValidatedOn { get; init; }
    public IReadOnlyList<ProcessValidationIssue> Issues { get; init; } = [];
    public bool IsValid => Issues.All(issue => issue.Severity != ProcessValidationSeverity.Error);
}

public sealed class ProcessValidationIssue
{
    public ProcessValidationSeverity Severity { get; init; }
    public string TenantId { get; init; } = string.Empty;
    public Guid ProcessDefinitionId { get; init; }
    public string ProcessName { get; init; } = string.Empty;
    public Guid? ProcessStepId { get; init; }
    public string StepName { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public enum ProcessValidationSeverity
{
    Information = 0,
    Warning = 10,
    Error = 20
}
