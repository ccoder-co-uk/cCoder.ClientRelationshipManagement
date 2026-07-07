namespace ClientRelationshipManagement.Web.Services.Leads;

public interface IAuthorityDataImportService
{
    ValueTask<int> RunPendingImportsAsync(CancellationToken cancellationToken = default);
}
