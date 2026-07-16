using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
namespace ClientRelationshipManagement.Web.Brokers.Storages;
public sealed class EmailWorkflowBroker(IClientRelationshipDbContextFactory factory) : IEmailWorkflowBroker, IDisposable
{
    readonly ClientRelationshipDbContext context = factory.CreateDbContext(useAdminConnection: true);
    public IQueryable<Email> Emails => context.Emails; public IQueryable<TenantCompanyRelationship> Relationships => context.TenantCompanyRelationships;
    public IQueryable<Material> Materials => context.Materials; public IQueryable<CompanyContact> CompanyContacts => context.CompanyContacts;
    public IQueryable<RelationshipContact> RelationshipContacts => context.RelationshipContacts; public IQueryable<EmailRecipient> EmailRecipients => context.EmailRecipients;
    public IQueryable<Activity> Activities => context.Activities;
    public void Add(Email x) => context.Emails.Add(x); public void Add(Material x) => context.Materials.Add(x);
    public void Add(EmailRecipient x) => context.EmailRecipients.Add(x); public void Add(Activity x) => context.Activities.Add(x);
    public void RemoveEmailRecipients(IEnumerable<EmailRecipient> x) => context.EmailRecipients.RemoveRange(x);
    public ValueTask SaveAsync(CancellationToken token = default) => new(context.SaveChangesAsync(token)); public void Dispose() => context.Dispose();
}
