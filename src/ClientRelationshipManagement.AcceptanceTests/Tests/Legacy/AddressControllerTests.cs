using cCoder.ClientRelationshipManagement.Models.Entities;
using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using Xunit;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

[Collection(CRMAcceptanceCollection.Name)]
public sealed partial class AddressControllerTests(CRMAcceptanceFixture fixture)
    : CRMControllerAcceptanceTestBase(fixture)
{
    private string BaseUrl { get; } = "/Api/Address";

    private sealed record SeededAddressContext(Client Client, Company Company, Address Address);

    private async Task<SeededAddressContext> SeedDatabase()
    {
        Client client = NewClient();
        Address address = NewAddress();
        Company company = NewCompany(client.Id, registeredAddressId: address.Id);

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            dbContext.Addresses.Add(address);
            dbContext.Companies.Add(company);
            await dbContext.SaveChangesAsync();
        });

        return new SeededAddressContext(client, company, address);
    }

    private async Task Teardown(SeededAddressContext context)
    {
        await DeleteEntitiesAsync<Company>(context.Company.Id);
        await DeleteEntitiesAsync<Address>(context.Address.Id);
        await DeleteEntitiesAsync<Client>(context.Client.Id);
    }
}
