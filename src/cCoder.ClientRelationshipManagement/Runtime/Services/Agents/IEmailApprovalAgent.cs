namespace cCoder.ClientRelationshipManagement.Runtime.Services.Agents;

public interface IEmailApprovalAgent
{
    ValueTask<int> RunAsync(CancellationToken cancellationToken = default);
}
