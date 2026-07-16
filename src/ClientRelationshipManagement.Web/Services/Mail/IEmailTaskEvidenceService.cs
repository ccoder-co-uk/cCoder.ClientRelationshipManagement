namespace ClientRelationshipManagement.Web.Services.Mail;

public interface IEmailTaskEvidenceService
{
    ValueTask<EmailTaskEvidenceResult> GetAsync(
        Guid processTaskId,
        string executionUserId,
        CancellationToken cancellationToken = default);
}

public sealed class EmailTaskEvidenceResult
{
    public Guid ProcessTaskId { get; init; }
    public DateTimeOffset DueOn { get; init; }
    public bool IsDue { get; init; }
    public Guid? OutboundEmailId { get; init; }
    public string OutboundSubject { get; init; }
    public string OutboundRecipients { get; init; }
    public DateTimeOffset? OutboundSentOn { get; init; }
    public DateTimeOffset? MailboxCheckedThrough { get; init; }
    public bool MailboxIsFresh { get; init; }
    public bool HasMatchingEvidence => Candidates.Count > 0;
    public bool NoEvidenceConfirmed { get; init; }
    public string Status { get; init; }
    public IReadOnlyList<EmailEvidenceCandidate> Candidates { get; init; } = [];
}

public sealed class EmailEvidenceCandidate
{
    public Guid MailboxMessageId { get; init; }
    public string InternetMessageId { get; init; }
    public string FromAddress { get; init; }
    public string Subject { get; init; }
    public string Body { get; init; }
    public bool IsBodyHtml { get; init; }
    public DateTimeOffset ReceivedOn { get; init; }
    public int MatchScore { get; init; }
    public IReadOnlyList<string> MatchReasons { get; init; } = [];
}
