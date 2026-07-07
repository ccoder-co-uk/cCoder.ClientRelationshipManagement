using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

public class Email : AuditableEntity
{
    public string LegacyId { get; set; }
    public Guid TenantCompanyRelationshipId { get; set; }
    public Guid? OpportunityId { get; set; }
    public Guid? ClientAccountId { get; set; }
    public Guid? MaterialId { get; set; }
    public Guid? CompanyContactId { get; set; }
    public string SenderUserId { get; set; }
    public string FromDisplayName { get; set; }
    public string FromEmailAddress { get; set; }
    public string ReplyToAddresses { get; set; }
    public string ToAddresses { get; set; }
    public string CcAddresses { get; set; }
    public string BccAddresses { get; set; }
    public string Subject { get; set; }
    public string BodyHtml { get; set; }
    public string BodyText { get; set; }
    public bool IsBodyHtml { get; set; }
    public EmailState State { get; set; }
    public DateTimeOffset? ApprovedOn { get; set; }
    public string ApprovedBy { get; set; }
    public DateTimeOffset? ScheduledSendTimeUtc { get; set; }
    public DateTimeOffset? LastSendAttemptOn { get; set; }
    public DateTimeOffset? SentOn { get; set; }
    public string ExternalMessageId { get; set; }
    public string LastError { get; set; }
    public int SendFailureCount { get; set; }

    public virtual TenantCompanyRelationship TenantCompanyRelationship { get; set; }
    public virtual Opportunity Opportunity { get; set; }
    public virtual ClientAccount ClientAccount { get; set; }
    public virtual Material Material { get; set; }
    public virtual CompanyContact CompanyContact { get; set; }
    public virtual ICollection<EmailRecipient> Recipients { get; set; } = new List<EmailRecipient>();
}
