using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace ClientRelationshipManagement.Web.Documentation;

public sealed class ClientStateGuideEntry
{
    public required RelationshipStatus Status { get; init; }
    public required string Summary { get; init; }
    public required string ProgressionHint { get; init; }
}

public static class ClientStateGuide
{
    public static IReadOnlyList<ClientStateGuideEntry> Entries { get; } =
    [
        new ClientStateGuideEntry
        {
            Status = RelationshipStatus.Prospect,
            Summary = "Early-stage leads that fit the target profile and are still being qualified.",
            ProgressionHint = "Move forward once research is complete and an active opportunity is being shaped."
        },
        new ClientStateGuideEntry
        {
            Status = RelationshipStatus.ActiveOpportunity,
            Summary = "Accounts with live commercial momentum, discovery work, or proposal activity underway.",
            ProgressionHint = "Promote to Contracted when commercial terms are agreed and a contract path is confirmed."
        },
        new ClientStateGuideEntry
        {
            Status = RelationshipStatus.Contracted,
            Summary = "Accounts that have committed commercially and are awaiting full delivery mobilisation.",
            ProgressionHint = "Advance to Onboarding when the handoff into implementation starts."
        },
        new ClientStateGuideEntry
        {
            Status = RelationshipStatus.Onboarding,
            Summary = "Newly won clients being introduced to delivery, operational contacts, and initial setup work.",
            ProgressionHint = "Mark as Client once onboarding is complete and business-as-usual management begins."
        },
        new ClientStateGuideEntry
        {
            Status = RelationshipStatus.Client,
            Summary = "Active live clients under ongoing relationship management.",
            ProgressionHint = "Move to Dormant if activity cools off, or open a new opportunity if expansion work starts."
        },
        new ClientStateGuideEntry
        {
            Status = RelationshipStatus.Dormant,
            Summary = "Past or paused relationships that still matter but currently lack active engagement.",
            ProgressionHint = "Return to Prospect or Active Opportunity when a re-engagement route becomes viable."
        },
        new ClientStateGuideEntry
        {
            Status = RelationshipStatus.Disqualified,
            Summary = "Accounts intentionally taken out of the active funnel because the fit or timing is wrong.",
            ProgressionHint = "Reopen only when the qualification criteria have materially changed."
        }
    ];

    public static string GetTooltip(RelationshipStatus status)
    {
        ClientStateGuideEntry entry = Entries.FirstOrDefault(item => item.Status == status);

        return entry is null
            ? "See documentation for the full client state model."
            : $"{entry.Summary} {entry.ProgressionHint}";
    }
}
