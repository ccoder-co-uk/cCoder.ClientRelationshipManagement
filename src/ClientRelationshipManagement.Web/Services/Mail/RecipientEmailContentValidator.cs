using System.Text.RegularExpressions;
using System.Net.Mail;

namespace ClientRelationshipManagement.Web.Services.Mail;

public static partial class RecipientEmailContentValidator
{
    public static bool ContainsInternalDraftingGuidance(string body) =>
        !string.IsNullOrWhiteSpace(body) && InternalGuidanceHeading().IsMatch(body);

    public static IReadOnlyList<string> Validate(string recipients, string subject, string body)
    {
        List<string> errors = [];
        if (string.IsNullOrWhiteSpace(recipients))
            errors.Add("A recipient email address is required.");
        else if (recipients.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(address => !MailAddress.TryCreate(address, out _)))
            errors.Add("Every recipient must be a valid email address.");

        if (string.IsNullOrWhiteSpace(subject))
            errors.Add("A subject is required.");
        else if (subject.Length > 512)
            errors.Add("The subject must not exceed 512 characters.");

        if (string.IsNullOrWhiteSpace(body))
            errors.Add("Email content is required.");
        else
        {
            if (ContainsInternalDraftingGuidance(body))
                errors.Add("Email content contains internal drafting guidance.");
            if (UnresolvedTemplateToken().IsMatch(subject ?? string.Empty) || UnresolvedTemplateToken().IsMatch(body))
                errors.Add("Email content contains an unresolved template token.");
            if (EmptySalutation().IsMatch(body))
                errors.Add("The salutation is missing a recipient name or audience.");
            if (PlaceholderText().IsMatch(body))
                errors.Add("Email content contains recipient-facing placeholder text.");
        }

        return errors;
    }

    [GeneratedRegex(@"(?im)^\s*(?:avoid\s+leading\s+with|lead\s+with|internal\s+(?:ai\s+)?(?:instruction|guidance|notes?)|ai\s+instruction)s?\s*:")]
    private static partial Regex InternalGuidanceHeading();

    [GeneratedRegex(@"\{\{[^}]+\}\}|\[\s*(?:name|recipient|contact|company)\s*\]|<\s*(?:name|recipient|contact|company)\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex UnresolvedTemplateToken();

    [GeneratedRegex(@"(?im)^\s*(?:hello|hi|dear)\s*[,!]?\s*$")]
    private static partial Regex EmptySalutation();

    [GeneratedRegex(@"(?i)\b(?:recipient|contact)\s+(?:name|pending|here)\b")]
    private static partial Regex PlaceholderText();
}
