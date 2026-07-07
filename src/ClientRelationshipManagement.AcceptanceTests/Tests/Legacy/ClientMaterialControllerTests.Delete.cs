using System.Net;
using cCoder.ClientRelationshipManagement.Models.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class ClientMaterialControllerTests
{
    [CRMAcceptanceFact]
    public async Task Delete_RemovesClientMaterial()
    {
        SeededClientMaterialContext seededContext = await SeedDatabase();

        HttpStatusCode actualDeleteStatusCode = await DeleteAsync($"{BaseUrl}/{seededContext.ClientMaterial.Id}");
        HttpStatusCode actualReadStatusCode = await GetStatusCodeAsync($"{BaseUrl}/{seededContext.ClientMaterial.Id}");

        actualDeleteStatusCode.Should().Be(HttpStatusCode.NoContent);
        actualReadStatusCode.Should().Be(HttpStatusCode.NotFound);

        await DeleteEntitiesAsync<Client>(seededContext.Client.Id);
    }

    [CRMAcceptanceFact]
    public async Task Delete_RemovesChildItemsBeforeDeletingClientMaterial()
    {
        Client client = NewClient();
        ClientMaterial clientMaterial = NewClientMaterial(client.Id);
        Email email = NewEmail(client.Id, clientMaterialId: clientMaterial.Id);
        ClientActivity activity = NewClientActivity(
            client.Id,
            clientMaterialId: clientMaterial.Id);

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            dbContext.ClientMaterials.Add(clientMaterial);
            dbContext.Emails.Add(email);
            dbContext.ClientActivities.Add(activity);
            await dbContext.SaveChangesAsync();
        });

        HttpStatusCode actualDeleteStatusCode = await DeleteAsync($"{BaseUrl}/{clientMaterial.Id}");
        HttpStatusCode actualReadStatusCode = await GetStatusCodeAsync($"{BaseUrl}/{clientMaterial.Id}");

        bool activityExists = await QueryInAdminContextAsync(dbContext =>
            dbContext.ClientActivities.IgnoreQueryFilters().AnyAsync(entity => entity.Id == activity.Id));
        bool emailExists = await QueryInAdminContextAsync(dbContext =>
            dbContext.Emails.IgnoreQueryFilters().AnyAsync(entity => entity.Id == email.Id));

        actualDeleteStatusCode.Should().Be(HttpStatusCode.NoContent);
        actualReadStatusCode.Should().Be(HttpStatusCode.NotFound);
        activityExists.Should().BeFalse();
        emailExists.Should().BeFalse();

        await DeleteEntitiesAsync<Client>(client.Id);
    }
}
