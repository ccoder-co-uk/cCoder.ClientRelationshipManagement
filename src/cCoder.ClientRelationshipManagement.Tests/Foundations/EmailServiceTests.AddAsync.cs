using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Models.Enums;
using cCoder.ClientRelationshipManagement.Services.Foundations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace cCoder.ClientRelationshipManagement.Tests.Foundations;

public partial class EmailServiceTests
{
    [Fact]
    public async Task AddAsync_ShouldAddEmailAndRaiseEvent()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        Client client = await TestSupport.AddClientAsync(serviceProvider);
        IEmailService service = serviceProvider.GetRequiredService<IEmailService>();

        Email email = await service.AddAsync(new Email
        {
            Id = Guid.NewGuid(),
            ClientId = client.Id,
            Subject = "Introduction Email",
            BodyText = "Hello from CRM",
            State = EmailState.Draft
        });

        Assert.Equal("Introduction Email", email.Subject);
        Assert.Contains(
            serviceProvider.GetRequiredService<TestSupport.RecordingEventHub>().RaisedEvents,
            record => record.Name == "email_add");
    }

    [Fact]
    public async Task Get_ShouldReturnEmail()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        Email email = await AddEmailAsync(serviceProvider);
        IEmailService service = serviceProvider.GetRequiredService<IEmailService>();

        Email result = service.Get(email.Id, ignoreFilters: true);

        Assert.Equal(email.Id, result.Id);
    }

    [Fact]
    public async Task GetAll_ShouldReturnEmails()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        Email email = await AddEmailAsync(serviceProvider);
        IEmailService service = serviceProvider.GetRequiredService<IEmailService>();

        Assert.Contains(service.GetAll(ignoreFilters: true), item => item.Id == email.Id);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateEmailAndRaiseEvent()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        Email email = await AddEmailAsync(serviceProvider);
        IEmailService service = serviceProvider.GetRequiredService<IEmailService>();
        email.Subject = "Updated Introduction Email";

        await service.UpdateAsync(email);

        Assert.Equal("Updated Introduction Email", service.Get(email.Id, ignoreFilters: true).Subject);
        Assert.Contains(
            serviceProvider.GetRequiredService<TestSupport.RecordingEventHub>().RaisedEvents,
            record => record.Name == "email_update");
    }

    [Fact]
    public async Task AddAndUpdateAsync_ShouldManageEmailAuditFields()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        Email email = await AddEmailAsync(serviceProvider);
        IEmailService service = serviceProvider.GetRequiredService<IEmailService>();

        Assert.Equal("unit-test-user", email.CreatedBy);
        Assert.Equal("unit-test-user", email.LastUpdatedBy);
        Assert.NotEqual(default, email.CreatedOn);
        Assert.NotEqual(default, email.LastUpdated);
        Assert.True(email.LastUpdated >= email.CreatedOn);

        string createdBy = email.CreatedBy;
        DateTimeOffset createdOn = email.CreatedOn;
        DateTimeOffset originalLastUpdated = email.LastUpdated;

        await Task.Delay(10);
        email.Subject = "Audited email update";
        Email updatedEmail = await service.UpdateAsync(email);

        Assert.Equal(createdBy, updatedEmail.CreatedBy);
        Assert.Equal(createdOn, updatedEmail.CreatedOn);
        Assert.Equal("unit-test-user", updatedEmail.LastUpdatedBy);
        Assert.True(updatedEmail.LastUpdated > originalLastUpdated);
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteEmailAndRaiseEvent()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        Email email = await AddEmailAsync(serviceProvider);
        IEmailService service = serviceProvider.GetRequiredService<IEmailService>();

        await service.DeleteAsync(email.Id);

        Assert.Null(service.Get(email.Id, ignoreFilters: true));
        Assert.Contains(
            serviceProvider.GetRequiredService<TestSupport.RecordingEventHub>().RaisedEvents,
            record => record.Name == "email_delete");
    }

    static async ValueTask<Email> AddEmailAsync(IServiceProvider serviceProvider)
    {
        Client client = await TestSupport.AddClientAsync(serviceProvider);
        IEmailService service = serviceProvider.GetRequiredService<IEmailService>();

        return await service.AddAsync(new Email
        {
            Id = Guid.NewGuid(),
            ClientId = client.Id,
            Subject = "Introduction Email",
            BodyText = "Hello from CRM",
            State = EmailState.Draft
        });
    }
}
