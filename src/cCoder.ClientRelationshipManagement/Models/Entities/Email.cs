using cCoder.ClientRelationshipManagement.Models.Enums;

namespace cCoder.ClientRelationshipManagement.Models.Entities;

public class Email
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public Guid? ClientMaterialId { get; set; }
    public Guid? SentToContactId { get; set; }
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
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public virtual Client Client { get; set; }
    public virtual ClientMaterial ClientMaterial { get; set; }
    public virtual ClientContact SentToContact { get; set; }
}
