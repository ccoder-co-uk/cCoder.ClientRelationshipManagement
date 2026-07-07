using cCoder.ClientRelationshipManagement.Models.Entities;
using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using Xunit;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

[Collection(CRMAcceptanceCollection.Name)]
public sealed partial class ClientOpportunityControllerTests(CRMAcceptanceFixture fixture)
    : CRMControllerAcceptanceTestBase(fixture)
{
    private string BaseUrl { get; } = "/Api/ClientOpportunity";

    private sealed record SeededClientOpportunityContext(Client Client, ClientOpportunity ClientOpportunity);

    private async Task<SeededClientOpportunityContext> SeedDatabase()
    {
        Client client = NewClient();
        ClientOpportunity opportunity = NewClientOpportunity(client.Id);

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            dbContext.ClientOpportunities.Add(opportunity);
            await dbContext.SaveChangesAsync();
        });

        return new SeededClientOpportunityContext(client, opportunity);
    }

    private async Task Teardown(SeededClientOpportunityContext context)
    {
        await DeleteEntitiesAsync<ClientOpportunity>(context.ClientOpportunity.Id);
        await DeleteEntitiesAsync<Client>(context.Client.Id);
    }
}
