using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class ClientOpportunityControllerTests
{
    [CRMAcceptanceFact]
    public async Task Post_CreatesClientOpportunity()
    {
        cCoder.ClientRelationshipManagement.Models.Entities.Client client = NewClient();
        cCoder.ClientRelationshipManagement.Models.Entities.ClientOpportunity expectedOpportunity =
            NewClientOpportunity(client.Id);

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            await dbContext.SaveChangesAsync();
        });

        cCoder.ClientRelationshipManagement.Models.Entities.ClientOpportunity createdOpportunity =
            await PostAsync<cCoder.ClientRelationshipManagement.Models.Entities.ClientOpportunity>(BaseUrl, expectedOpportunity);
        cCoder.ClientRelationshipManagement.Models.Entities.ClientOpportunity? actualOpportunity =
            await GetAsync<cCoder.ClientRelationshipManagement.Models.Entities.ClientOpportunity>($"{BaseUrl}/{expectedOpportunity.Id}");

        createdOpportunity.Id.Should().Be(expectedOpportunity.Id);
        actualOpportunity.Should().NotBeNull();
        actualOpportunity!.PainSummary.Should().Be(expectedOpportunity.PainSummary);

        await DeleteEntitiesAsync<cCoder.ClientRelationshipManagement.Models.Entities.ClientOpportunity>(expectedOpportunity.Id);
        await DeleteEntitiesAsync<cCoder.ClientRelationshipManagement.Models.Entities.Client>(client.Id);
    }
}
