namespace ClientRelationshipManagement.Web.Services.Migration;

public interface ICrmPlatformBootstrapService
{
    ValueTask InitialiseAsync(CancellationToken cancellationToken = default);
}
