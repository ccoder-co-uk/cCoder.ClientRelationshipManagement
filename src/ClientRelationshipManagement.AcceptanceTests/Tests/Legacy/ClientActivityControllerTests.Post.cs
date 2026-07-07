using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class ClientActivityControllerTests
{
    [CRMAcceptanceFact]
    public async Task Post_CreatesClientActivity()
    {
        cCoder.ClientRelationshipManagement.Models.Entities.Client client = NewClient();
        cCoder.ClientRelationshipManagement.Models.Entities.ClientActivity expectedActivity =
            NewClientActivity(client.Id);

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            await dbContext.SaveChangesAsync();
        });

        cCoder.ClientRelationshipManagement.Models.Entities.ClientActivity createdActivity =
            await PostAsync<cCoder.ClientRelationshipManagement.Models.Entities.ClientActivity>(BaseUrl, expectedActivity);
        cCoder.ClientRelationshipManagement.Models.Entities.ClientActivity? actualActivity =
            await GetAsync<cCoder.ClientRelationshipManagement.Models.Entities.ClientActivity>($"{BaseUrl}/{expectedActivity.Id}");

        createdActivity.Id.Should().Be(expectedActivity.Id);
        actualActivity.Should().NotBeNull();
        actualActivity!.Summary.Should().Be(expectedActivity.Summary);

        await DeleteEntitiesAsync<cCoder.ClientRelationshipManagement.Models.Entities.ClientActivity>(expectedActivity.Id);
        await DeleteEntitiesAsync<cCoder.ClientRelationshipManagement.Models.Entities.Client>(client.Id);
    }
}
