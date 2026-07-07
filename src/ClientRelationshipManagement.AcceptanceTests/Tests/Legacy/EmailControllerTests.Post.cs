using cCoder.ClientRelationshipManagement.Models.Entities;
using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class EmailControllerTests
{
    [CRMAcceptanceFact]
    public async Task Post_CreatesEmail()
    {
        Client client = NewClient();
        Email expectedEmail = NewEmail(client.Id);

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            await dbContext.SaveChangesAsync();
        });

        Email createdEmail = await PostAsync<Email>(BaseUrl, expectedEmail);
        Email? actualEmail = await GetAsync<Email>($"{BaseUrl}/{expectedEmail.Id}");

        createdEmail.Id.Should().Be(expectedEmail.Id);
        actualEmail.Should().NotBeNull();
        actualEmail!.Subject.Should().Be(expectedEmail.Subject);
        actualEmail.State.Should().Be(expectedEmail.State);

        await DeleteEntitiesAsync<Email>(expectedEmail.Id);
        await DeleteEntitiesAsync<Client>(client.Id);
    }
}
