namespace ClientRelationshipManagement.Web.Services.Imports;

public interface IImportProcessingService
{
    ValueTask<int> ProcessReadyImportsAsync(CancellationToken cancellationToken = default);
}
