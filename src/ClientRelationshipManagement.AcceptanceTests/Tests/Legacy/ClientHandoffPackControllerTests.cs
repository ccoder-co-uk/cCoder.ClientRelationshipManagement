using cCoder.ClientRelationshipManagement.Models.Entities;
using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using Xunit;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

[Collection(CRMAcceptanceCollection.Name)]
public sealed partial class ClientHandoffPackControllerTests(CRMAcceptanceFixture fixture)
    : CRMControllerAcceptanceTestBase(fixture)
{
    private string BaseUrl { get; } = "/Api/ClientHandoffPack";

    private sealed record SeededClientHandoffPackContext(
        Client Client,
        ClientOpportunity ClientOpportunity,
        ClientHandoffPack ClientHandoffPack);

    private async Task<SeededClientHandoffPackContext> SeedDatabase()
    {
        Client client = NewClient();
        ClientOpportunity opportunity = NewClientOpportunity(client.Id);
        ClientHandoffPack handoffPack = NewClientHandoffPack(client.Id, opportunity.Id);

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            dbContext.ClientOpportunities.Add(opportunity);
            dbContext.ClientHandoffPacks.Add(handoffPack);
            await dbContext.SaveChangesAsync();
        });

        return new SeededClientHandoffPackContext(client, opportunity, handoffPack);
    }

    private async Task Teardown(SeededClientHandoffPackContext context)
    {
        await DeleteEntitiesAsync<ClientHandoffPack>(context.ClientHandoffPack.Id);
        await DeleteEntitiesAsync<ClientOpportunity>(context.ClientOpportunity.Id);
        await DeleteEntitiesAsync<Client>(context.Client.Id);
    }
}
