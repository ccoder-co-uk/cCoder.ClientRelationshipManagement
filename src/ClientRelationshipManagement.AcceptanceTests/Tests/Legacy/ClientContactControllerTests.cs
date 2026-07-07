using cCoder.ClientRelationshipManagement.Models.Entities;
using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using Xunit;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

[Collection(CRMAcceptanceCollection.Name)]
public sealed partial class ClientContactControllerTests(CRMAcceptanceFixture fixture)
    : CRMControllerAcceptanceTestBase(fixture)
{
    private string BaseUrl { get; } = "/Api/ClientContact";

    private sealed record SeededClientContactContext(Client Client, ClientContact ClientContact);

    private async Task<SeededClientContactContext> SeedDatabase()
    {
        Client client = NewClient();
        ClientContact contact = NewClientContact(client.Id);

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            dbContext.ClientContacts.Add(contact);
            await dbContext.SaveChangesAsync();
        });

        return new SeededClientContactContext(client, contact);
    }

    private async Task Teardown(SeededClientContactContext context)
    {
        await DeleteEntitiesAsync<ClientContact>(context.ClientContact.Id);
        await DeleteEntitiesAsync<Client>(context.Client.Id);
    }
}
