using System.Net;
using cCoder.ClientRelationshipManagement.Models.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class ClientContactControllerTests
{
    [CRMAcceptanceFact]
    public async Task Delete_RemovesClientContact()
    {
        SeededClientContactContext seededContext = await SeedDatabase();

        HttpStatusCode actualDeleteStatusCode = await DeleteAsync($"{BaseUrl}/{seededContext.ClientContact.Id}");
        HttpStatusCode actualReadStatusCode = await GetStatusCodeAsync($"{BaseUrl}/{seededContext.ClientContact.Id}");

        actualDeleteStatusCode.Should().Be(HttpStatusCode.NoContent);
        actualReadStatusCode.Should().Be(HttpStatusCode.NotFound);

        await DeleteEntitiesAsync<Client>(seededContext.Client.Id);
    }

    [CRMAcceptanceFact]
    public async Task Delete_RemovesChildItemsAndClearsDependentReferences()
    {
        Client client = NewClient();
        ClientContact clientContact = NewClientContact(client.Id);
        ClientOpportunity opportunity = NewClientOpportunity(client.Id, primaryContactId: clientContact.Id);
        ClientMaterial material = NewClientMaterial(client.Id, sentToContactId: clientContact.Id);
        Email email = NewEmail(client.Id, clientMaterialId: material.Id, sentToContactId: clientContact.Id);
        ClientActivity activity = NewClientActivity(
            client.Id,
            clientContactId: clientContact.Id,
            clientOpportunityId: opportunity.Id,
            clientMaterialId: material.Id);

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            dbContext.ClientContacts.Add(clientContact);
            dbContext.ClientOpportunities.Add(opportunity);
            dbContext.ClientMaterials.Add(material);
            dbContext.Emails.Add(email);
            dbContext.ClientActivities.Add(activity);
            await dbContext.SaveChangesAsync();
        });

        HttpStatusCode actualDeleteStatusCode = await DeleteAsync($"{BaseUrl}/{clientContact.Id}");
        HttpStatusCode actualReadStatusCode = await GetStatusCodeAsync($"{BaseUrl}/{clientContact.Id}");

        var persistedEntities = await QueryInAdminContextAsync(async dbContext => new
        {
            ActivityExists = await dbContext.ClientActivities.IgnoreQueryFilters().AnyAsync(entity => entity.Id == activity.Id),
            Material = await dbContext.ClientMaterials.IgnoreQueryFilters().FirstOrDefaultAsync(entity => entity.Id == material.Id),
            Email = await dbContext.Emails.IgnoreQueryFilters().FirstOrDefaultAsync(entity => entity.Id == email.Id),
            Opportunity = await dbContext.ClientOpportunities.IgnoreQueryFilters().FirstOrDefaultAsync(entity => entity.Id == opportunity.Id),
        });

        actualDeleteStatusCode.Should().Be(HttpStatusCode.NoContent);
        actualReadStatusCode.Should().Be(HttpStatusCode.NotFound);
        persistedEntities.ActivityExists.Should().BeFalse();
        persistedEntities.Material.Should().NotBeNull();
        persistedEntities.Material!.SentToContactId.Should().BeNull();
        persistedEntities.Email.Should().NotBeNull();
        persistedEntities.Email!.SentToContactId.Should().BeNull();
        persistedEntities.Opportunity.Should().NotBeNull();
        persistedEntities.Opportunity!.PrimaryContactId.Should().BeNull();

        await DeleteEntitiesAsync<Email>(email.Id);
        await DeleteEntitiesAsync<ClientMaterial>(material.Id);
        await DeleteEntitiesAsync<ClientOpportunity>(opportunity.Id);
        await DeleteEntitiesAsync<Client>(client.Id);
    }
}
