using cCoder.ClientRelationshipManagement.Models.Entities;
using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using Xunit;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

[Collection(CRMAcceptanceCollection.Name)]
public sealed partial class EmailControllerTests(CRMAcceptanceFixture fixture)
    : CRMControllerAcceptanceTestBase(fixture)
{
    private string BaseUrl { get; } = "/Api/Email";

    private sealed record SeededEmailContext(Client Client, Email Email);

    private async Task<SeededEmailContext> SeedDatabase()
    {
        Client client = NewClient();
        Email email = NewEmail(client.Id);

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            dbContext.Emails.Add(email);
            await dbContext.SaveChangesAsync();
        });

        return new SeededEmailContext(client, email);
    }

    private async Task Teardown(SeededEmailContext context)
    {
        await DeleteEntitiesAsync<Email>(context.Email.Id);
        await DeleteEntitiesAsync<Client>(context.Client.Id);
    }
}
