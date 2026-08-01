using cCoder.ClientRelationshipManagement.Runtime.Services.Migration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace cCoder.ClientRelationshipManagement.Tests.Runtime;

public sealed class CrmApplicationLifecycleExtensionsTests
{
    [Fact]
    public async Task ShouldInitialiseCrmApplicationWithinScope()
    {
        TestCrmDatabaseInitialiser initialiser = new();
        ServiceCollection services = new();
        services.AddScoped<ICrmDatabaseInitialiser>(_ => initialiser);

        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        await serviceProvider.InitialiseCrmApplicationAsync();

        Assert.True(initialiser.WasInitialised);
    }

    sealed class TestCrmDatabaseInitialiser : ICrmDatabaseInitialiser
    {
        public bool WasInitialised { get; private set; }

        public ValueTask InitialiseAsync(CancellationToken cancellationToken = default)
        {
            WasInitialised = true;

            return ValueTask.CompletedTask;
        }
    }
}
