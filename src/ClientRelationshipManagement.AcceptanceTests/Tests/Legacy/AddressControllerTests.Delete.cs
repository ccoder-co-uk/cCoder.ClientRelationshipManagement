using System.Net;
using cCoder.ClientRelationshipManagement.Models.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class AddressControllerTests
{
    [CRMAcceptanceFact]
    public async Task Delete_RemovesAddress()
    {
        SeededAddressContext seededContext = await SeedDatabase();

        HttpStatusCode actualDeleteStatusCode = await DeleteAsync($"{BaseUrl}/{seededContext.Address.Id}");
        HttpStatusCode actualReadStatusCode = await GetStatusCodeAsync($"{BaseUrl}/{seededContext.Address.Id}");

        actualDeleteStatusCode.Should().Be(HttpStatusCode.NoContent);
        actualReadStatusCode.Should().Be(HttpStatusCode.NotFound);

        await DeleteEntitiesAsync<Company>(seededContext.Company.Id);
        await DeleteEntitiesAsync<Client>(seededContext.Client.Id);
    }

    [CRMAcceptanceFact]
    public async Task Delete_ClearsRelatedCompanyReferencesBeforeDeletingAddress()
    {
        SeededAddressContext seededContext = await SeedDatabase();

        HttpStatusCode actualDeleteStatusCode = await DeleteAsync($"{BaseUrl}/{seededContext.Address.Id}");
        HttpStatusCode actualReadStatusCode = await GetStatusCodeAsync($"{BaseUrl}/{seededContext.Address.Id}");

        Company? company = await QueryInAdminContextAsync(dbContext =>
            dbContext.Companies.IgnoreQueryFilters().FirstOrDefaultAsync(entity => entity.Id == seededContext.Company.Id));

        actualDeleteStatusCode.Should().Be(HttpStatusCode.NoContent);
        actualReadStatusCode.Should().Be(HttpStatusCode.NotFound);
        company.Should().NotBeNull();
        company!.RegisteredAddressId.Should().BeNull();

        await DeleteEntitiesAsync<Company>(seededContext.Company.Id);
        await DeleteEntitiesAsync<Client>(seededContext.Client.Id);
    }
}
