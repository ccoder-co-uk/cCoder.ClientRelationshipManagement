using cCoder.ClientRelationshipManagement.Models.Entities;
using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using Xunit;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

[Collection(CRMAcceptanceCollection.Name)]
public sealed partial class ClientControllerTests(CRMAcceptanceFixture fixture)
    : CRMControllerAcceptanceTestBase(fixture)
{
    private string BaseUrl { get; } = "/Api/Client";

    private sealed record SeededClientContext(Client Client);

    private async Task<SeededClientContext> SeedDatabase()
    {
        Client client = NewClient();

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            await dbContext.SaveChangesAsync();
        });

        return new SeededClientContext(client);
    }

    private async Task Teardown(SeededClientContext context) =>
        await DeleteEntitiesAsync<Client>(context.Client.Id);
}
