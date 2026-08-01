using Microsoft.Extensions.DependencyInjection;

namespace ClientRelationshipManagement.Web.Services.Migration;

public static class CrmApplicationLifecycleExtensions
{
    public static async ValueTask InitialiseCrmApplicationAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        using IServiceScope scope = serviceProvider.CreateScope();

        await scope.ServiceProvider
            .GetRequiredService<ICrmDatabaseInitialiser>()
            .InitialiseAsync(cancellationToken);
    }
}
