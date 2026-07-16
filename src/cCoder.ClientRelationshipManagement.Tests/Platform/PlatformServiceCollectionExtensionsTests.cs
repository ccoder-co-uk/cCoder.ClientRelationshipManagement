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

        services.AddCrmPlatform(configuration =>
        {
            configuration.ConnectionString = "Server=(localdb)\\MSSQLLocalDB;Database=crm-platform-tests;";
            configuration.AdminConnectionString = "Server=(localdb)\\MSSQLLocalDB;Database=crm-platform-tests;";
        });

        ServiceDescriptor platformConfigurationDescriptor = Assert.Single(services, item => item.ServiceType == typeof(CRMConfiguration));
        ServiceDescriptor dbContextFactoryDescriptor = Assert.Single(services, item => item.ServiceType == typeof(IClientRelationshipDbContextFactory));

        Assert.NotNull(platformConfigurationDescriptor.ImplementationInstance);
        Assert.Equal(ServiceLifetime.Scoped, dbContextFactoryDescriptor.Lifetime);
    }

    [Fact]
    public void ShouldRequireAdminConnectionString()
    {
        IServiceCollection services = new ServiceCollection();

        Action action = () => services.AddCrmPlatform(configuration =>
        {
            configuration.ConnectionString = "Server=(localdb)\\MSSQLLocalDB;Database=crm-platform-tests;";
            configuration.AdminConnectionString = string.Empty;
        });

        Assert.Throws<InvalidOperationException>(action);
    }
}
