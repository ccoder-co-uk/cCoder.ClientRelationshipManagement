using PlatformEntities = cCoder.ClientRelationshipManagement.Platform.Models.Entities;

namespace ClientRelationshipManagement.Web.Utilities;

internal static class CompanyNames
{
    static readonly HashSet<string> PlaceholderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "unknown",
        "n/a",
        "na",
        "not known",
        "unnamed",
        "imported company"
    };

    public static string ResolvePreferredName(PlatformEntities.Company company)
    {
        if (company is null)
            return string.Empty;

        string officialName = Normalize(company.OfficialName);
        string tradingName = Normalize(company.TradingName);
        string legalEntityName = Normalize(company.LegalEntityName);

        if (!string.IsNullOrWhiteSpace(tradingName))
        {
            if (IsPlaceholderName(officialName))
                return tradingName;

            if (!string.IsNullOrWhiteSpace(legalEntityName)
                && string.Equals(officialName, legalEntityName, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(tradingName, legalEntityName, StringComparison.OrdinalIgnoreCase))
            {
                return tradingName;
            }
        }

        return FirstNonEmpty(officialName, tradingName, legalEntityName);
    }

    public static bool IsPlaceholderName(string value) =>
        string.IsNullOrWhiteSpace(value)
        || PlaceholderNames.Contains(value.Trim());

    static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    static string Normalize(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
}
