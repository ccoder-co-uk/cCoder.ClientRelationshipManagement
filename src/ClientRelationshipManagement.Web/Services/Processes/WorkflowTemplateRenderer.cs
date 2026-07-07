using ClientRelationshipManagement.Web.Utilities;
using PlatformEntities = cCoder.ClientRelationshipManagement.Platform.Models.Entities;

namespace ClientRelationshipManagement.Web.Services.Processes;

internal static class WorkflowTemplateRenderer
{
    public static IReadOnlyList<string> SupportedTokens { get; } =
    [
        "{{Lead.RawCompanyName}}",
        "{{Lead.RawTradingName}}",
        "{{Lead.RawVatNumber}}",
        "{{Lead.RawWebsiteUrl}}",
        "{{Lead.QualificationNotes}}",
        "{{Company.OfficialName}}",
        "{{Company.LegalEntityName}}",
        "{{Company.TradingName}}",
        "{{Company.WebsiteUrl}}",
        "{{Company.ContactEmailAddress}}",
        "{{Company.ContactPhoneNumber}}",
        "{{Company.RegisteredOfficeText}}",
        "{{Contact.Name}}",
        "{{Contact.Position}}",
        "{{Contact.EmailAddress}}",
        "{{Contact.PhoneNumber}}",
        "{{Relationship.AccountOwnerDisplayName}}",
        "{{Relationship.AccountOwnerUserId}}",
        "{{Relationship.LeadSource}}",
        "{{Relationship.InitialRoute}}",
        "{{Relationship.PreferredOpeningAngle}}",
        "{{Relationship.OpportunitySummary}}",
        "{{Opportunity.PainSummary}}",
        "{{Opportunity.ValueHypothesis}}",
        "{{Opportunity.DecisionProcess}}",
        "{{ClientAccount.AccountReference}}",
        "{{Now.Date}}",
        "{{Now.DateTime}}"
    ];

    public static string Render(
        string template,
        PlatformEntities.Lead lead = null,
        PlatformEntities.Company company = null,
        PlatformEntities.CompanyContact companyContact = null,
        PlatformEntities.TenantCompanyRelationship relationship = null,
        PlatformEntities.Opportunity opportunity = null,
        PlatformEntities.ClientAccount clientAccount = null,
        DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(template))
            return null;

        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Lead.RawCompanyName"] = lead?.RawCompanyName,
            ["Lead.RawTradingName"] = lead?.RawTradingName,
            ["Lead.RawVatNumber"] = lead?.RawVatNumber,
            ["Lead.RawWebsiteUrl"] = lead?.RawWebsiteUrl,
            ["Lead.QualificationNotes"] = lead?.QualificationNotes,
            ["Company.OfficialName"] = CompanyNames.ResolvePreferredName(company),
            ["Company.LegalEntityName"] = company?.LegalEntityName,
            ["Company.TradingName"] = company?.TradingName,
            ["Company.WebsiteUrl"] = company?.WebsiteUrl,
            ["Company.ContactEmailAddress"] = company?.ContactEmailAddress,
            ["Company.ContactPhoneNumber"] = company?.ContactPhoneNumber,
            ["Company.RegisteredOfficeText"] = company?.RegisteredOfficeText,
            ["Contact.Name"] = companyContact?.Name,
            ["Contact.Position"] = companyContact?.Position,
            ["Contact.EmailAddress"] = companyContact?.EmailAddress,
            ["Contact.PhoneNumber"] = companyContact?.PhoneNumber,
            ["Relationship.AccountOwnerDisplayName"] = relationship?.AccountOwnerDisplayName,
            ["Relationship.AccountOwnerUserId"] = relationship?.AccountOwnerUserId,
            ["Relationship.LeadSource"] = relationship?.LeadSource,
            ["Relationship.InitialRoute"] = relationship?.InitialRoute,
            ["Relationship.PreferredOpeningAngle"] = relationship?.PreferredOpeningAngle,
            ["Relationship.OpportunitySummary"] = relationship?.OpportunitySummary,
            ["Opportunity.PainSummary"] = opportunity?.PainSummary,
            ["Opportunity.ValueHypothesis"] = opportunity?.ValueHypothesis,
            ["Opportunity.DecisionProcess"] = opportunity?.DecisionProcess,
            ["ClientAccount.AccountReference"] = clientAccount?.AccountReference,
            ["Now.Date"] = (now ?? DateTimeOffset.UtcNow).ToString("dd MMM yyyy"),
            ["Now.DateTime"] = (now ?? DateTimeOffset.UtcNow).ToString("dd MMM yyyy HH:mm")
        };

        string rendered = template;
        foreach ((string key, string value) in values)
            rendered = rendered.Replace($"{{{{{key}}}}}", value ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        return rendered.Trim();
    }
}
