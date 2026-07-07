using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class ClientControllerTests
{
    [CRMAcceptanceFact]
    public async Task Post_CreatesClient()
    {
        cCoder.ClientRelationshipManagement.Models.Entities.Client expectedClient = NewClient();

        cCoder.ClientRelationshipManagement.Models.Entities.Client createdClient =
            await PostAsync<cCoder.ClientRelationshipManagement.Models.Entities.Client>(BaseUrl, expectedClient);
        cCoder.ClientRelationshipManagement.Models.Entities.Client? actualClient =
            await GetAsync<cCoder.ClientRelationshipManagement.Models.Entities.Client>($"{BaseUrl}/{expectedClient.Id}");

        createdClient.Id.Should().Be(expectedClient.Id);
        actualClient.Should().NotBeNull();
        actualClient!.AccountOwner.Should().Be(expectedClient.AccountOwner);
        actualClient.Status.Should().Be(expectedClient.Status);

        await DeleteEntitiesAsync<cCoder.ClientRelationshipManagement.Models.Entities.Client>(expectedClient.Id);
    }
}
