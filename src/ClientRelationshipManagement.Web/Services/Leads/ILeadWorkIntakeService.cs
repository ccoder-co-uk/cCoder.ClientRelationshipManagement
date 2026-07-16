namespace ClientRelationshipManagement.Web.Services.Leads;

public interface ILeadWorkIntakeService
{
    ValueTask<LeadWorkIntakeResult> EnsureCapacityAsync(CancellationToken cancellationToken = default);
}

public sealed record LeadWorkIntakeResult(
    int ActiveWorkItems,
    int RunnableWorkItems,
    int PromotedCompanyCount);
