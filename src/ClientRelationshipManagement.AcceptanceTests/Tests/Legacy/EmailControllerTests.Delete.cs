using System.Net;
using cCoder.ClientRelationshipManagement.Models.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class EmailControllerTests
{
    [CRMAcceptanceFact]
    public async Task Delete_RemovesEmail()
    {
        SeededEmailContext seededContext = await SeedDatabase();

        HttpStatusCode actualDeleteStatusCode = await DeleteAsync($"{BaseUrl}/{seededContext.Email.Id}");
        HttpStatusCode actualReadStatusCode = await GetStatusCodeAsync($"{BaseUrl}/{seededContext.Email.Id}");

        actualDeleteStatusCode.Should().Be(HttpStatusCode.NoContent);
        actualReadStatusCode.Should().Be(HttpStatusCode.NotFound);

        await DeleteEntitiesAsync<Client>(seededContext.Client.Id);
    }

    [CRMAcceptanceFact]
    public async Task Delete_RemovesEmailWithoutDeletingLinkedMaterial()
    {
        Client client = NewClient();
        ClientMaterial material = NewClientMaterial(client.Id);
        Email email = NewEmail(client.Id, clientMaterialId: material.Id);

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            dbContext.ClientMaterials.Add(material);
            dbContext.Emails.Add(email);
            await dbContext.SaveChangesAsync();
        });

        HttpStatusCode actualDeleteStatusCode = await DeleteAsync($"{BaseUrl}/{email.Id}");
        HttpStatusCode actualReadStatusCode = await GetStatusCodeAsync($"{BaseUrl}/{email.Id}");

        bool materialExists = await QueryInAdminContextAsync(dbContext =>
            dbContext.ClientMaterials.IgnoreQueryFilters().AnyAsync(entity => entity.Id == material.Id));

        actualDeleteStatusCode.Should().Be(HttpStatusCode.NoContent);
        actualReadStatusCode.Should().Be(HttpStatusCode.NotFound);
        materialExists.Should().BeTrue();

        await DeleteEntitiesAsync<ClientMaterial>(material.Id);
        await DeleteEntitiesAsync<Client>(client.Id);
    }
}
