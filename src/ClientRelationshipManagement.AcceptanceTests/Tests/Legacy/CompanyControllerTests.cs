using cCoder.ClientRelationshipManagement.Models.Entities;
using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using Xunit;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

[Collection(CRMAcceptanceCollection.Name)]
public sealed partial class CompanyControllerTests(CRMAcceptanceFixture fixture)
    : CRMControllerAcceptanceTestBase(fixture)
{
    private string BaseUrl { get; } = "/Api/Company";

    private sealed record SeededCompanyContext(Client Client, Company Company);

    private async Task<SeededCompanyContext> SeedDatabase()
    {
        Client client = NewClient();
        Company company = NewCompany(client.Id);

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            dbContext.Companies.Add(company);
            await dbContext.SaveChangesAsync();
        });

        return new SeededCompanyContext(client, company);
    }

    private async Task Teardown(SeededCompanyContext context)
    {
        await DeleteEntitiesAsync<Company>(context.Company.Id);
        await DeleteEntitiesAsync<Client>(context.Client.Id);
    }
}
