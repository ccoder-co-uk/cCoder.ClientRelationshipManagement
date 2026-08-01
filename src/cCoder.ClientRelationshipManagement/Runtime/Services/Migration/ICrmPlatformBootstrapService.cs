namespace cCoder.ClientRelationshipManagement.Runtime.Services.Migration;

public interface ICrmPlatformBootstrapService
{
    ValueTask InitialiseAsync(CancellationToken cancellationToken = default);
}
