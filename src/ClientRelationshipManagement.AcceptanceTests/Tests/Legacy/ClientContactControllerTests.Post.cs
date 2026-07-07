using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class ClientContactControllerTests
{
    [CRMAcceptanceFact]
    public async Task Post_CreatesClientContact()
    {
        cCoder.ClientRelationshipManagement.Models.Entities.Client client = NewClient();
        cCoder.ClientRelationshipManagement.Models.Entities.ClientContact expectedContact = NewClientContact(client.Id);

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            await dbContext.SaveChangesAsync();
        });

        cCoder.ClientRelationshipManagement.Models.Entities.ClientContact createdContact =
            await PostAsync<cCoder.ClientRelationshipManagement.Models.Entities.ClientContact>(BaseUrl, expectedContact);
        cCoder.ClientRelationshipManagement.Models.Entities.ClientContact? actualContact =
            await GetAsync<cCoder.ClientRelationshipManagement.Models.Entities.ClientContact>($"{BaseUrl}/{expectedContact.Id}");

        createdContact.Id.Should().Be(expectedContact.Id);
        actualContact.Should().NotBeNull();
        actualContact!.Name.Should().Be(expectedContact.Name);

        await DeleteEntitiesAsync<cCoder.ClientRelationshipManagement.Models.Entities.ClientContact>(expectedContact.Id);
        await DeleteEntitiesAsync<cCoder.ClientRelationshipManagement.Models.Entities.Client>(client.Id);
    }
}
