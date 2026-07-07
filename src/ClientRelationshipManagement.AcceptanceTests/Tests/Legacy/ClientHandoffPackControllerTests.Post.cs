using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class ClientHandoffPackControllerTests
{
    [CRMAcceptanceFact]
    public async Task Post_CreatesClientHandoffPack()
    {
        cCoder.ClientRelationshipManagement.Models.Entities.Client client = NewClient();
        cCoder.ClientRelationshipManagement.Models.Entities.ClientOpportunity opportunity =
            NewClientOpportunity(client.Id);
        cCoder.ClientRelationshipManagement.Models.Entities.ClientHandoffPack expectedHandoffPack =
            NewClientHandoffPack(client.Id, opportunity.Id);

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            dbContext.ClientOpportunities.Add(opportunity);
            await dbContext.SaveChangesAsync();
        });

        cCoder.ClientRelationshipManagement.Models.Entities.ClientHandoffPack createdHandoffPack =
            await PostAsync<cCoder.ClientRelationshipManagement.Models.Entities.ClientHandoffPack>(BaseUrl, expectedHandoffPack);
        cCoder.ClientRelationshipManagement.Models.Entities.ClientHandoffPack? actualHandoffPack =
            await GetAsync<cCoder.ClientRelationshipManagement.Models.Entities.ClientHandoffPack>($"{BaseUrl}/{expectedHandoffPack.Id}");

        createdHandoffPack.Id.Should().Be(expectedHandoffPack.Id);
        actualHandoffPack.Should().NotBeNull();
        actualHandoffPack!.LegalEntity.Should().Be(expectedHandoffPack.LegalEntity);

        await DeleteEntitiesAsync<cCoder.ClientRelationshipManagement.Models.Entities.ClientHandoffPack>(expectedHandoffPack.Id);
        await DeleteEntitiesAsync<cCoder.ClientRelationshipManagement.Models.Entities.ClientOpportunity>(opportunity.Id);
        await DeleteEntitiesAsync<cCoder.ClientRelationshipManagement.Models.Entities.Client>(client.Id);
    }
}
