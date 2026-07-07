namespace ClientRelationshipManagement.Web.Services.Migration;

public interface ICrmDatabaseInitialiser
{
    ValueTask InitialiseAsync(CancellationToken cancellationToken = default);
}
