using System.Text.RegularExpressions;

namespace ClientRelationshipManagement.Web.Services.Mail;

public static partial class RecipientEmailContentValidator
{
    public static bool ContainsInternalDraftingGuidance(string body) =>
        !string.IsNullOrWhiteSpace(body) && InternalGuidanceHeading().IsMatch(body);

    [GeneratedRegex(@"(?im)^\s*(?:avoid\s+leading\s+with|lead\s+with|internal\s+(?:ai\s+)?(?:instruction|guidance|notes?)|ai\s+instruction)s?\s*:")]
    private static partial Regex InternalGuidanceHeading();
}
