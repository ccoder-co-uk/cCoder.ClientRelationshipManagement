namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

public class MailboxMessageRecord : ICrmEntity
{
    public Guid Id { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public string ExternalId { get; set; }
    public string InternetMessageId { get; set; }
    public string ConversationId { get; set; }
    public string InReplyTo { get; set; }
    public string References { get; set; }
    public string FromAddress { get; set; }
    public string ToAddresses { get; set; }
    public string CcAddresses { get; set; }
    public string Subject { get; set; }
    public string Body { get; set; }
    public bool IsBodyHtml { get; set; }
    public DateTimeOffset ReceivedOn { get; set; }
    public Guid? TenantCompanyRelationshipId { get; set; }
    public Guid? OpportunityId { get; set; }
    public Guid? CompanyContactId { get; set; }

    public virtual TenantCompanyRelationship TenantCompanyRelationship { get; set; }
    public virtual Opportunity Opportunity { get; set; }
    public virtual CompanyContact CompanyContact { get; set; }
}
