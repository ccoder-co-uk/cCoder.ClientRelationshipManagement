using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Foundations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace cCoder.ClientRelationshipManagement.Tests.Foundations;

public partial class CompanyServiceTests
{
    [Fact]
    public async Task AddAsync_ShouldAddCompanyAndRaiseEvent()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        ICompanyService companyService = serviceProvider.GetRequiredService<ICompanyService>();
        Guid clientId = Guid.NewGuid();

        Company company = await companyService.AddAsync(new Company
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Name = "Industrial Metal Services",
            Client = new Client
            {
                Id = clientId,
                TenantId = TestSupport.TenantId
            }
        });

        Assert.Equal("Industrial Metal Services", company.Name);
        Assert.Contains(
            serviceProvider.GetRequiredService<TestSupport.RecordingEventHub>().RaisedEvents,
            record => record.Name == "company_add");
    }

    [Fact]
    public async Task Get_ShouldReturnCompany()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        ICompanyService companyService = serviceProvider.GetRequiredService<ICompanyService>();
        Company company = await AddCompanyAsync(companyService);

        Company result = companyService.Get(company.Id, ignoreFilters: true);

        Assert.Equal(company.Id, result.Id);
    }

    [Fact]
    public async Task GetAll_ShouldReturnCompanies()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        ICompanyService companyService = serviceProvider.GetRequiredService<ICompanyService>();
        Company company = await AddCompanyAsync(companyService);

        Assert.Contains(companyService.GetAll(ignoreFilters: true), item => item.Id == company.Id);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateCompanyAndRaiseEvent()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        ICompanyService companyService = serviceProvider.GetRequiredService<ICompanyService>();
        Company company = await AddCompanyAsync(companyService);
        company.Name = "Updated Industrial Metal Services";

        await companyService.UpdateAsync(company);

        Assert.Equal("Updated Industrial Metal Services", companyService.Get(company.Id, ignoreFilters: true).Name);
        Assert.Contains(
            serviceProvider.GetRequiredService<TestSupport.RecordingEventHub>().RaisedEvents,
            record => record.Name == "company_update");
    }

    [Fact]
    public async Task AddAndUpdateAsync_ShouldManageCompanyAuditFields()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        ICompanyService companyService = serviceProvider.GetRequiredService<ICompanyService>();
        Company company = await AddCompanyAsync(companyService);

        Assert.Equal("unit-test-user", company.CreatedBy);
        Assert.Equal("unit-test-user", company.LastUpdatedBy);
        Assert.NotEqual(default, company.CreatedOn);
        Assert.NotEqual(default, company.LastUpdated);
        Assert.True(company.LastUpdated >= company.CreatedOn);

        string createdBy = company.CreatedBy;
        DateTimeOffset createdOn = company.CreatedOn;
        DateTimeOffset originalLastUpdated = company.LastUpdated;

        await Task.Delay(10);
        company.Name = "Audited Update Ltd";
        Company updatedCompany = await companyService.UpdateAsync(company);

        Assert.Equal(createdBy, updatedCompany.CreatedBy);
        Assert.Equal(createdOn, updatedCompany.CreatedOn);
        Assert.Equal("unit-test-user", updatedCompany.LastUpdatedBy);
        Assert.True(updatedCompany.LastUpdated > originalLastUpdated);
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteCompanyAndRaiseEvent()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        ICompanyService companyService = serviceProvider.GetRequiredService<ICompanyService>();
        Company company = await AddCompanyAsync(companyService);

        await companyService.DeleteAsync(company.Id);

        Assert.Null(companyService.Get(company.Id, ignoreFilters: true));
        Assert.Contains(
            serviceProvider.GetRequiredService<TestSupport.RecordingEventHub>().RaisedEvents,
            record => record.Name == "company_delete");
    }

    static async ValueTask<Company> AddCompanyAsync(ICompanyService companyService)
    {
        Guid clientId = Guid.NewGuid();

        return await companyService.AddAsync(new Company
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Name = "Industrial Metal Services",
            Client = new Client
            {
                Id = clientId,
                TenantId = TestSupport.TenantId
            }
        });
    }
}
