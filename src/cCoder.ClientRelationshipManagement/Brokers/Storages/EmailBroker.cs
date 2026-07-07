using cCoder.ClientRelationshipManagement.Data;
using cCoder.ClientRelationshipManagement.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Brokers.Storages;

public class EmailBroker(ICRMContextFactory contextFactory) : IEmailBroker
{
    public IQueryable<Email> GetAllEmails(bool ignoreFilters)
    {
        ClientRelationshipManagementDbContext context = contextFactory.CreateContext();
        return ignoreFilters
            ? context.Emails.IgnoreQueryFilters()
            : context.Emails;
    }

    public async ValueTask<Email> AddEmailAsync(Email entity)
    {
        using ClientRelationshipManagementDbContext context = contextFactory.CreateContext();
        Email result = (await context.Emails.AddAsync(entity)).Entity;
        await context.SaveChangesAsync();
        return result;
    }

    public async ValueTask<Email> UpdateEmailAsync(Email entity)
    {
        using ClientRelationshipManagementDbContext context = contextFactory.CreateContext();
        Email result = context.Emails.Update(entity).Entity;
        await context.SaveChangesAsync();
        return result;
    }

    public async ValueTask<int> DeleteEmailAsync(Email entity)
    {
        using ClientRelationshipManagementDbContext context = contextFactory.CreateContext();
        context.Emails.Remove(entity);
        return await context.SaveChangesAsync();
    }
}
