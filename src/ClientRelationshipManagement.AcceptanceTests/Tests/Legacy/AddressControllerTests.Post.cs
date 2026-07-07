using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class AddressControllerTests
{
    [CRMAcceptanceFact]
    public async Task Post_CreatesAddress()
    {
        cCoder.ClientRelationshipManagement.Models.Entities.Client client = NewClient();
        cCoder.ClientRelationshipManagement.Models.Entities.Company company = NewCompany(client.Id);
        cCoder.ClientRelationshipManagement.Models.Entities.Address expectedAddress = NewAddress();
        cCoder.ClientRelationshipManagement.Models.Entities.Company authorizationCompany = NewCompany(client.Id, company.Id);

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            dbContext.Companies.Add(company);
            await dbContext.SaveChangesAsync();
        });

        authorizationCompany.Client = new cCoder.ClientRelationshipManagement.Models.Entities.Client
        {
            Id = client.Id,
            TenantId = client.TenantId,
        };

        cCoder.ClientRelationshipManagement.Models.Entities.Address createdAddress =
            await PostAsync<cCoder.ClientRelationshipManagement.Models.Entities.Address>(
                BaseUrl,
                ToPayload(expectedAddress, authorizationCompany));

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            cCoder.ClientRelationshipManagement.Models.Entities.Company persistedCompany =
                await dbContext.Companies.IgnoreQueryFilters().SingleAsync(item => item.Id == company.Id);

            persistedCompany.RegisteredAddressId = expectedAddress.Id;
            await dbContext.SaveChangesAsync();
        });

        cCoder.ClientRelationshipManagement.Models.Entities.Address? actualAddress =
            await GetAsync<cCoder.ClientRelationshipManagement.Models.Entities.Address>($"{BaseUrl}/{expectedAddress.Id}");

        createdAddress.Id.Should().Be(expectedAddress.Id);
        actualAddress.Should().NotBeNull();
        actualAddress!.Line1.Should().Be(expectedAddress.Line1);

        await DeleteEntitiesAsync<cCoder.ClientRelationshipManagement.Models.Entities.Company>(company.Id);
        await DeleteEntitiesAsync<cCoder.ClientRelationshipManagement.Models.Entities.Address>(expectedAddress.Id);
        await DeleteEntitiesAsync<cCoder.ClientRelationshipManagement.Models.Entities.Client>(client.Id);
    }
}
