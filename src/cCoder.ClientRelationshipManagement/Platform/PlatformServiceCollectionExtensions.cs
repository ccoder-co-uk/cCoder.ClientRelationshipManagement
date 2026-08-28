using cCoder.ClientRelationshipManagement.Brokers;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Configuration;
using cCoder.ClientRelationshipManagement.Services.Foundations.Platform;
using cCoder.ClientRelationshipManagement.Services.Entities;
using cCoder.ClientRelationshipManagement.Brokers.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace cCoder.ClientRelationshipManagement.Platform;

public static class PlatformServiceCollectionExtensions
{
    public static IServiceCollection AddCrmPlatform(
        this IServiceCollection services,
        CRMDataConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (string.IsNullOrWhiteSpace(configuration.ConnectionString))
            throw new InvalidOperationException("CRMData.ConnectionString is required.");

        if (string.IsNullOrWhiteSpace(configuration.AdminConnectionString))
            configuration.AdminConnectionString = configuration.ConnectionString;

        services.AddSingleton(configuration);
        services.AddDbContext<ClientRelationshipDbContext>(options =>
            options.UseSqlServer(
                configuration.ConnectionString,
                sqlServer => sqlServer.CommandTimeout(600)));

        services.AddScoped<IAuthorizationBroker, AuthorizationBroker>();
        services.AddScoped(provider => provider.GetRequiredService<IAuthorizationBroker>().GetCRMAuthInfo());
        services.AddScoped<IClientRelationshipDbContextFactory, ClientRelationshipDbContextFactory>();
        services.AddCrmEntityStacks();
        services.AddScoped<ICRMTransactionBroker, CRMTransactionBroker>();
        services.AddScoped<IImportCoordinationService, ImportCoordinationService>();
        services.AddScoped<IOperationsCoordinationService, OperationsCoordinationService>();
        services.AddScoped<ISalesCoordinationService, SalesCoordinationService>();
        services.AddScoped<IProcessCoordinationService, ProcessCoordinationService>();

        return services;
    }
}