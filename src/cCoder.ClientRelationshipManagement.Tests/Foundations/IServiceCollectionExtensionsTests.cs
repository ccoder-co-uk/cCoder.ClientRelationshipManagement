using cCoder.ClientRelationshipManagement.Services;
using cCoder.ClientRelationshipManagement.Services.Foundations.Events;
using cCoder.Eventing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace cCoder.ClientRelationshipManagement.Tests.Foundations;

public class IServiceCollectionExtensionsTests
{
    [Fact]
    public void ShouldListenToAllConfiguredEvents()
    {
        ServiceCollection services = new();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));
        services.AddEventing();
        services.AddClientRelationshipManagement(configuration =>
        {
            configuration.ConnectionString =
                "Server=(localdb)\\mssqllocaldb;Database=crm-unit-tests;Trusted_Connection=True;TrustServerCertificate=True;";
            configuration.AdminConnectionString = configuration.ConnectionString;
        });

        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        using IServiceScope scope = serviceProvider.CreateScope();
        IEventHandlerService sut = scope.ServiceProvider.GetRequiredService<IEventHandlerService>();

        Exception exception = Record.Exception(() => sut.ListenToAllEvents());

        Assert.Null(exception);
    }
}
