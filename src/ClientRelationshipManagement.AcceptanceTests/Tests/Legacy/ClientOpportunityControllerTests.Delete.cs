using System.Net;
using cCoder.ClientRelationshipManagement.Models.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class ClientOpportunityControllerTests
{
    [CRMAcceptanceFact]
    public async Task Delete_RemovesClientOpportunity()
    {
        SeededClientOpportunityContext seededContext = await SeedDatabase();

        HttpStatusCode actualDeleteStatusCode = await DeleteAsync($"{BaseUrl}/{seededContext.ClientOpportunity.Id}");
        HttpStatusCode actualReadStatusCode = await GetStatusCodeAsync($"{BaseUrl}/{seededContext.ClientOpportunity.Id}");

        actualDeleteStatusCode.Should().Be(HttpStatusCode.NoContent);
        actualReadStatusCode.Should().Be(HttpStatusCode.NotFound);

        await DeleteEntitiesAsync<Client>(seededContext.Client.Id);
    }

    [CRMAcceptanceFact]
    public async Task Delete_RemovesChildItemsBeforeDeletingClientOpportunity()
    {
        Client client = NewClient();
        ClientOpportunity clientOpportunity = NewClientOpportunity(client.Id);
        ClientActivity activity = NewClientActivity(
            client.Id,
            clientOpportunityId: clientOpportunity.Id);
        ClientHandoffPack handoffPack = NewClientHandoffPack(client.Id, clientOpportunity.Id);

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            dbContext.ClientOpportunities.Add(clientOpportunity);
            dbContext.ClientActivities.Add(activity);
            dbContext.ClientHandoffPacks.Add(handoffPack);
            await dbContext.SaveChangesAsync();
        });

        HttpStatusCode actualDeleteStatusCode = await DeleteAsync($"{BaseUrl}/{clientOpportunity.Id}");
        HttpStatusCode actualReadStatusCode = await GetStatusCodeAsync($"{BaseUrl}/{clientOpportunity.Id}");

        var remainingEntities = await QueryInAdminContextAsync(async dbContext => new
        {
            ActivityExists = await dbContext.ClientActivities.IgnoreQueryFilters().AnyAsync(entity => entity.Id == activity.Id),
            HandoffPackExists = await dbContext.ClientHandoffPacks.IgnoreQueryFilters().AnyAsync(entity => entity.Id == handoffPack.Id),
        });

        actualDeleteStatusCode.Should().Be(HttpStatusCode.NoContent);
        actualReadStatusCode.Should().Be(HttpStatusCode.NotFound);
        remainingEntities.ActivityExists.Should().BeFalse();
        remainingEntities.HandoffPackExists.Should().BeFalse();

        await DeleteEntitiesAsync<Client>(client.Id);
    }
}
