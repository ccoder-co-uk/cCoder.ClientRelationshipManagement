using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class CompanyControllerTests
{
    [CRMAcceptanceFact]
    public async Task Post_CreatesCompany()
    {
        cCoder.ClientRelationshipManagement.Models.Entities.Client client = NewClient();
        cCoder.ClientRelationshipManagement.Models.Entities.Company expectedCompany = NewCompany(client.Id);

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            await dbContext.SaveChangesAsync();
        });

        cCoder.ClientRelationshipManagement.Models.Entities.Company createdCompany =
            await PostAsync<cCoder.ClientRelationshipManagement.Models.Entities.Company>(BaseUrl, expectedCompany);
        cCoder.ClientRelationshipManagement.Models.Entities.Company? actualCompany =
            await GetAsync<cCoder.ClientRelationshipManagement.Models.Entities.Company>($"{BaseUrl}/{expectedCompany.Id}");

        createdCompany.Id.Should().Be(expectedCompany.Id);
        actualCompany.Should().NotBeNull();
        actualCompany!.Name.Should().Be(expectedCompany.Name);

        await DeleteEntitiesAsync<cCoder.ClientRelationshipManagement.Models.Entities.Company>(expectedCompany.Id);
        await DeleteEntitiesAsync<cCoder.ClientRelationshipManagement.Models.Entities.Client>(client.Id);
    }
}
