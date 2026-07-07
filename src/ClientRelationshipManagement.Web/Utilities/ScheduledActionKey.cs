namespace ClientRelationshipManagement.Web.Utilities;

public readonly record struct ScheduledActionKey(Guid ClientId, string ActionText, DateTimeOffset DueOn)
{
    public static ScheduledActionKey Create(Guid clientId, string actionText, DateTimeOffset dueOn) =>
        new(
            clientId,
            NormalizeAction(actionText),
            dueOn.ToUniversalTime());

    public static string NormalizeAction(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();

    public static bool Matches(Guid clientId, string actionText, DateTimeOffset? dueOn, ScheduledActionKey key) =>
        clientId == key.ClientId
        && dueOn.HasValue
        && dueOn.Value.ToUniversalTime() == key.DueOn
        && NormalizeAction(actionText) == key.ActionText;
}
