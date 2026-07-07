using cCoder.ClientRelationshipManagement.Brokers;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace cCoder.ClientRelationshipManagement.Platform;

public static class PlatformServiceCollectionExtensions
{
    public static IServiceCollection AddCrmPlatform(
        this IServiceCollection services,
        Action<PlatformConfiguration> configure)
    {
        PlatformConfiguration configuration = new();
        configure?.Invoke(configuration);

        if (string.IsNullOrWhiteSpace(configuration.ConnectionString))
            throw new InvalidOperationException("PlatformConfiguration.ConnectionString is required.");

        if (string.IsNullOrWhiteSpace(configuration.AdminConnectionString))
            throw new InvalidOperationException("PlatformConfiguration.AdminConnectionString is required.");

        services.AddSingleton(configuration);
        services.AddDbContext<PlatformDbContext>(options =>
            options.UseSqlServer(configuration.ConnectionString));

        services.AddScoped<IAuthorizationBroker, AuthorizationBroker>();
        services.AddScoped(provider => provider.GetRequiredService<IAuthorizationBroker>().GetCRMAuthInfo());
        services.AddScoped<IPlatformDbContextFactory, PlatformDbContextFactory>();

        return services;
    }
}
