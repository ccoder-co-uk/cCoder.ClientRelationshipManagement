using cCoder.ClientRelationshipManagement.Models.Entities;
using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using Xunit;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

[Collection(CRMAcceptanceCollection.Name)]
public sealed partial class ClientActivityControllerTests(CRMAcceptanceFixture fixture)
    : CRMControllerAcceptanceTestBase(fixture)
{
    private string BaseUrl { get; } = "/Api/ClientActivity";

    private sealed record SeededClientActivityContext(Client Client, ClientActivity ClientActivity);

    private async Task<SeededClientActivityContext> SeedDatabase()
    {
        Client client = NewClient();
        ClientActivity activity = NewClientActivity(client.Id);

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            dbContext.ClientActivities.Add(activity);
            await dbContext.SaveChangesAsync();
        });

        return new SeededClientActivityContext(client, activity);
    }

    private async Task Teardown(SeededClientActivityContext context)
    {
        await DeleteEntitiesAsync<ClientActivity>(context.ClientActivity.Id);
        await DeleteEntitiesAsync<Client>(context.Client.Id);
    }
}
