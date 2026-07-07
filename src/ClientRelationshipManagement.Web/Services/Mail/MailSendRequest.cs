namespace ClientRelationshipManagement.Web.Services.Mail;

public sealed class MailSendRequest
{
    public string FromDisplayName { get; init; }
    public string FromEmailAddress { get; init; }
    public string ReplyToAddresses { get; init; }
    public string ToAddresses { get; init; }
    public string CcAddresses { get; init; }
    public string BccAddresses { get; init; }
    public string Subject { get; init; }
    public string BodyHtml { get; init; }
    public string BodyText { get; init; }
    public bool IsBodyHtml { get; init; }
}
