namespace ClientRelationshipManagement.Web.Services.Leads;

public interface IAuthorityDataImportCoordinationService
{
    ValueTask<int> RunPendingImportsAsync(CancellationToken cancellationToken = default);
}
