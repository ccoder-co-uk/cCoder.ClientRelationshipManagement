using System.Net;
using cCoder.ClientRelationshipManagement.Models.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class ClientControllerTests
{
    [CRMAcceptanceFact]
    public async Task Delete_RemovesClient()
    {
        SeededClientContext seededContext = await SeedDatabase();

        HttpStatusCode actualDeleteStatusCode = await DeleteAsync($"{BaseUrl}/{seededContext.Client.Id}");
        HttpStatusCode actualReadStatusCode = await GetStatusCodeAsync($"{BaseUrl}/{seededContext.Client.Id}");

        actualDeleteStatusCode.Should().Be(HttpStatusCode.NoContent);
        actualReadStatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [CRMAcceptanceFact]
    public async Task Delete_RemovesRelatedItemsBeforeDeletingClient()
    {
        Client client = NewClient();
        Address address = NewAddress();
        Company company = NewCompany(client.Id, registeredAddressId: address.Id);
        ClientContact contact = NewClientContact(client.Id);
        ClientOpportunity opportunity = NewClientOpportunity(client.Id, primaryContactId: contact.Id);
        ClientMaterial material = NewClientMaterial(client.Id, sentToContactId: contact.Id);
        Email email = NewEmail(client.Id, clientMaterialId: material.Id, sentToContactId: contact.Id);
        ClientActivity activity = NewClientActivity(
            client.Id,
            clientContactId: contact.Id,
            clientOpportunityId: opportunity.Id,
            clientMaterialId: material.Id);
        ClientHandoffPack handoffPack = NewClientHandoffPack(client.Id, opportunity.Id);

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            dbContext.Addresses.Add(address);
            dbContext.Companies.Add(company);
            dbContext.ClientContacts.Add(contact);
            dbContext.ClientOpportunities.Add(opportunity);
            dbContext.ClientMaterials.Add(material);
            dbContext.Emails.Add(email);
            dbContext.ClientActivities.Add(activity);
            dbContext.ClientHandoffPacks.Add(handoffPack);
            await dbContext.SaveChangesAsync();
        });

        HttpStatusCode actualDeleteStatusCode = await DeleteAsync($"{BaseUrl}/{client.Id}");
        HttpStatusCode actualReadStatusCode = await GetStatusCodeAsync($"{BaseUrl}/{client.Id}");

        var remainingEntities = await QueryInAdminContextAsync(async dbContext => new
        {
            CompanyExists = await dbContext.Companies.IgnoreQueryFilters().AnyAsync(entity => entity.Id == company.Id),
            ContactExists = await dbContext.ClientContacts.IgnoreQueryFilters().AnyAsync(entity => entity.Id == contact.Id),
            OpportunityExists = await dbContext.ClientOpportunities.IgnoreQueryFilters().AnyAsync(entity => entity.Id == opportunity.Id),
            MaterialExists = await dbContext.ClientMaterials.IgnoreQueryFilters().AnyAsync(entity => entity.Id == material.Id),
            EmailExists = await dbContext.Emails.IgnoreQueryFilters().AnyAsync(entity => entity.Id == email.Id),
            ActivityExists = await dbContext.ClientActivities.IgnoreQueryFilters().AnyAsync(entity => entity.Id == activity.Id),
            HandoffPackExists = await dbContext.ClientHandoffPacks.IgnoreQueryFilters().AnyAsync(entity => entity.Id == handoffPack.Id),
            AddressExists = await dbContext.Addresses.IgnoreQueryFilters().AnyAsync(entity => entity.Id == address.Id),
        });

        actualDeleteStatusCode.Should().Be(HttpStatusCode.NoContent);
        actualReadStatusCode.Should().Be(HttpStatusCode.NotFound);
        remainingEntities.CompanyExists.Should().BeFalse();
        remainingEntities.ContactExists.Should().BeFalse();
        remainingEntities.OpportunityExists.Should().BeFalse();
        remainingEntities.MaterialExists.Should().BeFalse();
        remainingEntities.EmailExists.Should().BeFalse();
        remainingEntities.ActivityExists.Should().BeFalse();
        remainingEntities.HandoffPackExists.Should().BeFalse();
        remainingEntities.AddressExists.Should().BeTrue();

        await DeleteEntitiesAsync<Address>(address.Id);
    }
}
