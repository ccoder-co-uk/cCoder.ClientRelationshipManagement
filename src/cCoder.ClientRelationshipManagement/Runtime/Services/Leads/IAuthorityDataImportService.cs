namespace cCoder.ClientRelationshipManagement.Runtime.Services.Leads;

public interface IAuthorityDataImportCoordinationService
{
    ValueTask<int> RunPendingImportsAsync(CancellationToken cancellationToken = default);
}
