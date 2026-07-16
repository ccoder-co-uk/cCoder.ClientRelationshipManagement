using System.Text.RegularExpressions;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using cCoder.ClientRelationshipManagement.Services.Foundations.Platform;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.Web.Services.Mail;

public sealed partial class EmailTaskEvidenceService(
    IProcessCoordinationService processes,
    IOperationsCoordinationService operations,
    ISalesCoordinationService sales)
    : IEmailTaskEvidenceService
{
    public async ValueTask<EmailTaskEvidenceResult> GetAsync(
        Guid processTaskId,
        string executionUserId,
        CancellationToken cancellationToken = default)
    {
        ProcessTask task = await processes.RetrieveTasks()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == processTaskId, cancellationToken);

        if (task is null)
            return null;

        Email outbound = await operations.RetrieveAllEmails()
            .AsNoTracking()
            .Include(item => item.Recipients)
            .Where(item => item.State == EmailState.Sent
                && item.SentOn.HasValue
                && ((task.OpportunityId.HasValue && item.OpportunityId == task.OpportunityId)
                    || (task.TenantCompanyRelationshipId.HasValue
                        && item.TenantCompanyRelationshipId == task.TenantCompanyRelationshipId)))
            .OrderByDescending(item => item.SentOn)
            .FirstOrDefaultAsync(cancellationToken);

        DateTimeOffset? mailboxCheckedThrough = await operations.RetrieveAutomationSettings(executionUserId)
            .AsNoTracking()
            .Where(item => item.UserId == executionUserId)
            .Select(item => item.LastMailboxSyncOn)
            .FirstOrDefaultAsync(cancellationToken);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        bool mailboxIsFresh = mailboxCheckedThrough.HasValue
            && mailboxCheckedThrough.Value >= now.AddMinutes(-10)
            && mailboxCheckedThrough.Value >= task.DueOn
            && (outbound is null
                || !outbound.SentOn.HasValue
                || mailboxCheckedThrough.Value >= outbound.SentOn.Value);

        if (outbound is null)
        {
            return new EmailTaskEvidenceResult
            {
                ProcessTaskId = task.Id,
                DueOn = task.DueOn,
                IsDue = task.DueOn <= now,
                MailboxCheckedThrough = mailboxCheckedThrough,
                MailboxIsFresh = mailboxIsFresh,
                NoEvidenceConfirmed = false,
                Status = "No sent outbound email is linked to this relationship, so absence of a reply cannot be confirmed."
            };
        }

        HashSet<string> expectedAddresses = ParseAddresses(outbound.ToAddresses)
            .Concat(outbound.Recipients.Select(item => item.Address))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(NormalizeAddress)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (task.TenantCompanyRelationshipId.HasValue)
        {
            List<string> contactAddresses = await sales.RetrieveRelationshipContacts()
                .AsNoTracking()
                .Where(item => item.TenantCompanyRelationshipId == task.TenantCompanyRelationshipId
                    && item.CompanyContact.EmailAddress != null)
                .Select(item => item.CompanyContact.EmailAddress)
                .ToListAsync(cancellationToken);
            expectedAddresses.UnionWith(contactAddresses.Select(NormalizeAddress));
        }

        DateTimeOffset searchFrom = outbound.SentOn!.Value.AddMinutes(-5);
        List<MailboxMessageRecord> messages = await operations.RetrieveMailboxMessages()
            .AsNoTracking()
            .Where(item => item.ReceivedOn >= searchFrom)
            .OrderByDescending(item => item.ReceivedOn)
            .Take(500)
            .ToListAsync(cancellationToken);

        List<EmailEvidenceCandidate> candidates = messages
            .Select(message => Score(message, outbound, expectedAddresses))
            .Where(item => item.MatchScore >= 45)
            .OrderByDescending(item => item.MatchScore)
            .ThenByDescending(item => item.ReceivedOn)
            .Take(10)
            .ToList();
        bool noEvidenceConfirmed = task.DueOn <= now && mailboxIsFresh && candidates.Count == 0;

        return new EmailTaskEvidenceResult
        {
            ProcessTaskId = task.Id,
            DueOn = task.DueOn,
            IsDue = task.DueOn <= now,
            OutboundEmailId = outbound.Id,
            OutboundSubject = outbound.Subject,
            OutboundRecipients = string.Join(", ", expectedAddresses.OrderBy(item => item)),
            OutboundSentOn = outbound.SentOn,
            MailboxCheckedThrough = mailboxCheckedThrough,
            MailboxIsFresh = mailboxIsFresh,
            NoEvidenceConfirmed = noEvidenceConfirmed,
            Status = candidates.Count > 0
                ? "Potential reply evidence was found. The LLM must read the candidates and classify the response."
                : noEvidenceConfirmed
                    ? "The task is due and a fresh mailbox check found no matching reply evidence."
                    : "No matching evidence was found, but the mailbox check is not fresh enough to confirm no reply.",
            Candidates = candidates
        };
    }

    static EmailEvidenceCandidate Score(
        MailboxMessageRecord message,
        Email outbound,
        HashSet<string> expectedAddresses)
    {
        List<string> reasons = [];
        int score = 0;
        string sender = NormalizeAddress(message.FromAddress);
        if (expectedAddresses.Contains(sender))
        {
            score += 100;
            reasons.Add("sender address matches an outbound recipient or CRM contact");
        }

        if (!string.IsNullOrWhiteSpace(outbound.ExternalMessageId)
            && (ContainsId(message.InReplyTo, outbound.ExternalMessageId)
                || ContainsId(message.References, outbound.ExternalMessageId)))
        {
            score += 90;
            reasons.Add("reply headers reference the outbound message");
        }

        string outboundSubject = NormalizeSubject(outbound.Subject);
        if (!string.IsNullOrWhiteSpace(outboundSubject)
            && string.Equals(NormalizeSubject(message.Subject), outboundSubject, StringComparison.OrdinalIgnoreCase))
        {
            score += 40;
            reasons.Add("subject matches after removing reply/forward prefixes");
        }

        string senderDomain = Domain(sender);
        if (!string.IsNullOrWhiteSpace(senderDomain)
            && expectedAddresses.Any(address => string.Equals(Domain(address), senderDomain, StringComparison.OrdinalIgnoreCase)))
        {
            score += 25;
            reasons.Add("sender domain matches an outbound recipient or CRM contact");
        }

        return new EmailEvidenceCandidate
        {
            MailboxMessageId = message.Id,
            InternetMessageId = message.InternetMessageId,
            FromAddress = message.FromAddress,
            Subject = message.Subject,
            Body = message.Body,
            IsBodyHtml = message.IsBodyHtml,
            ReceivedOn = message.ReceivedOn,
            MatchScore = score,
            MatchReasons = reasons
        };
    }

    static IEnumerable<string> ParseAddresses(string addresses) =>
        string.IsNullOrWhiteSpace(addresses)
            ? []
            : addresses.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    static string NormalizeAddress(string address) => address?.Trim().ToLowerInvariant() ?? string.Empty;

    static string Domain(string address)
    {
        int at = address?.LastIndexOf('@') ?? -1;
        return at >= 0 && at < address.Length - 1 ? address[(at + 1)..] : string.Empty;
    }

    static bool ContainsId(string value, string messageId) =>
        !string.IsNullOrWhiteSpace(value)
        && !string.IsNullOrWhiteSpace(messageId)
        && value.Contains(messageId, StringComparison.OrdinalIgnoreCase);

    static string NormalizeSubject(string subject) =>
        string.IsNullOrWhiteSpace(subject)
            ? string.Empty
            : SubjectPrefixRegex().Replace(subject.Trim(), string.Empty).Trim();

    [GeneratedRegex(@"^(?:(?:re|fw|fwd)\s*:\s*)+", RegexOptions.IgnoreCase)]
    private static partial Regex SubjectPrefixRegex();
}
