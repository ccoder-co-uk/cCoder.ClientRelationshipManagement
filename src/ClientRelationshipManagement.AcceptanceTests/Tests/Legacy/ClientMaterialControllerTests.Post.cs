using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class ClientMaterialControllerTests
{
    [CRMAcceptanceFact]
    public async Task Post_CreatesClientMaterial()
    {
        cCoder.ClientRelationshipManagement.Models.Entities.Client client = NewClient();
        cCoder.ClientRelationshipManagement.Models.Entities.ClientMaterial expectedMaterial =
            NewClientMaterial(client.Id);

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            await dbContext.SaveChangesAsync();
        });

        cCoder.ClientRelationshipManagement.Models.Entities.ClientMaterial createdMaterial =
            await PostAsync<cCoder.ClientRelationshipManagement.Models.Entities.ClientMaterial>(BaseUrl, expectedMaterial);
        cCoder.ClientRelationshipManagement.Models.Entities.ClientMaterial? actualMaterial =
            await GetAsync<cCoder.ClientRelationshipManagement.Models.Entities.ClientMaterial>($"{BaseUrl}/{expectedMaterial.Id}");

        createdMaterial.Id.Should().Be(expectedMaterial.Id);
        actualMaterial.Should().NotBeNull();
        actualMaterial!.Name.Should().Be(expectedMaterial.Name);

        await DeleteEntitiesAsync<cCoder.ClientRelationshipManagement.Models.Entities.ClientMaterial>(expectedMaterial.Id);
        await DeleteEntitiesAsync<cCoder.ClientRelationshipManagement.Models.Entities.Client>(client.Id);
    }
}
