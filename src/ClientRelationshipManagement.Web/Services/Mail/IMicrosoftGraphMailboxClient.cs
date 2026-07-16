namespace ClientRelationshipManagement.Web.Services.Mail;

public interface IMicrosoftGraphMailboxClient : IMailClient
{
    Task<IReadOnlyList<MailboxMessage>> ReceiveAsync(
        DateTimeOffset receivedSince,
        int maximumMessages,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MailboxMessage>> RetrieveSentAsync(
        DateTimeOffset sentSince,
        int maximumMessages,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MailboxMessage>> RetrieveSentAsync(
        DateTimeOffset sentSince,
        DateTimeOffset sentUntil,
        int maximumMessages,
        CancellationToken cancellationToken = default);
}

public sealed class MailboxMessage
{
    public string ExternalId { get; init; }
    public string InternetMessageId { get; init; }
    public string ConversationId { get; init; }
    public string InReplyTo { get; init; }
    public string References { get; init; }
    public string FromAddress { get; init; }
    public string ToAddresses { get; init; }
    public string CcAddresses { get; init; }
    public string Subject { get; init; }
    public string Body { get; init; }
    public bool IsBodyHtml { get; init; }
    public DateTimeOffset ReceivedOn { get; init; }
}
