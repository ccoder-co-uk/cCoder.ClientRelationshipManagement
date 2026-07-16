namespace ClientRelationshipManagement.Web.Services.Agents;

public interface IEmailApprovalAgent
{
    ValueTask<int> RunAsync(CancellationToken cancellationToken = default);
}
