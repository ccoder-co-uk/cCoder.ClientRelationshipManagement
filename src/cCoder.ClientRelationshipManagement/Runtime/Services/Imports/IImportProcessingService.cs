namespace cCoder.ClientRelationshipManagement.Runtime.Services.Imports;

public interface IImportProcessingService
{
    ValueTask<int> ProcessReadyImportsAsync(CancellationToken cancellationToken = default);
}
