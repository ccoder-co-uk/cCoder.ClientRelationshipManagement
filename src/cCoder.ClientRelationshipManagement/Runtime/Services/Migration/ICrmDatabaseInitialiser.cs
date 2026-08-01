namespace cCoder.ClientRelationshipManagement.Runtime.Services.Migration;

public interface ICrmDatabaseInitialiser
{
    ValueTask InitialiseAsync(CancellationToken cancellationToken = default);
}
