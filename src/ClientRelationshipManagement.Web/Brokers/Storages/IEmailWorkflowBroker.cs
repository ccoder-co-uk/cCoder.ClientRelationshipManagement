using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
namespace ClientRelationshipManagement.Web.Brokers.Storages;
public interface IEmailWorkflowBroker
{
    IQueryable<Email> Emails { get; }
    IQueryable<TenantCompanyRelationship> Relationships { get; }
    IQueryable<Material> Materials { get; }
    IQueryable<CompanyContact> CompanyContacts { get; }
    IQueryable<RelationshipContact> RelationshipContacts { get; }
    IQueryable<EmailRecipient> EmailRecipients { get; }
    IQueryable<Activity> Activities { get; }
    void Add(Email entity); void Add(Material entity); void Add(EmailRecipient entity); void Add(Activity entity);
    void RemoveEmailRecipients(IEnumerable<EmailRecipient> entities);
    ValueTask SaveAsync(CancellationToken cancellationToken = default);
}
