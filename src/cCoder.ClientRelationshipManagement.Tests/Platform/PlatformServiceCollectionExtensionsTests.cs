using cCoder.ClientRelationshipManagement.Platform;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace cCoder.ClientRelationshipManagement.Tests.Platform;

public sealed class PlatformServiceCollectionExtensionsTests
{
    [Fact]
    public void ShouldRegisterPlatformServicesWhenConfigurationIsValid()
    {
        IServiceCollection services = new ServiceCollection();

        services.AddCrmPlatform(new CRMDataConfiguration
        {
            ConnectionString = "Server=(localdb)\\MSSQLLocalDB;Database=crm-platform-tests;",
            AdminConnectionString = "Server=(localdb)\\MSSQLLocalDB;Database=crm-platform-tests;"
        });

        ServiceDescriptor platformConfigurationDescriptor = Assert.Single(services, item => item.ServiceType == typeof(CRMDataConfiguration));
        ServiceDescriptor dbContextFactoryDescriptor = Assert.Single(services, item => item.ServiceType == typeof(IClientRelationshipDbContextFactory));

        Assert.NotNull(platformConfigurationDescriptor.ImplementationInstance);
        Assert.Equal(ServiceLifetime.Scoped, dbContextFactoryDescriptor.Lifetime);
    }

    [Fact]
    public void ShouldUseRegularConnectionWhenAdminConnectionIsOmitted()
    {
        IServiceCollection services = new ServiceCollection();

        CRMDataConfiguration configuration = new()
        {
            ConnectionString = "Server=(localdb)\\MSSQLLocalDB;Database=crm-platform-tests;"
        };

        services.AddCrmPlatform(configuration);

        Assert.Equal(configuration.ConnectionString, configuration.AdminConnectionString);
    }
}