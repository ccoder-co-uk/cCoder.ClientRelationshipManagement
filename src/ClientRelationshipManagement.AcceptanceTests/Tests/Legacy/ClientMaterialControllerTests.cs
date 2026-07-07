using cCoder.ClientRelationshipManagement.Models.Entities;
using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using Xunit;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

[Collection(CRMAcceptanceCollection.Name)]
public sealed partial class ClientMaterialControllerTests(CRMAcceptanceFixture fixture)
    : CRMControllerAcceptanceTestBase(fixture)
{
    private string BaseUrl { get; } = "/Api/ClientMaterial";

    private sealed record SeededClientMaterialContext(Client Client, ClientMaterial ClientMaterial);

    private async Task<SeededClientMaterialContext> SeedDatabase()
    {
        Client client = NewClient();
        ClientMaterial material = NewClientMaterial(client.Id);

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            dbContext.ClientMaterials.Add(material);
            await dbContext.SaveChangesAsync();
        });

        return new SeededClientMaterialContext(client, material);
    }

    private async Task Teardown(SeededClientMaterialContext context)
    {
        await DeleteEntitiesAsync<ClientMaterial>(context.ClientMaterial.Id);
        await DeleteEntitiesAsync<Client>(context.Client.Id);
    }
}
