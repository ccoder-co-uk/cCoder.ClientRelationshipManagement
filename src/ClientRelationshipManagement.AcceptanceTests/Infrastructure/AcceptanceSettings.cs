namespace ClientRelationshipManagement.AcceptanceTests.Infrastructure;

public sealed class AcceptanceSettings
{
    public const string TenantId = "crm-acceptance";

    public string CrmConnectionString { get; init; } = string.Empty;

    public string CrmAdminConnectionString { get; init; } = string.Empty;

    public string SsoConnectionString { get; init; } = string.Empty;

    public string DecryptionKey { get; init; } = string.Empty;

    public string UserId { get; init; } = "crm-acceptance-user";

    public bool BypassAuthentication { get; init; } = true;

    public bool GrantCrmPrivileges { get; init; } = true;

    public string SessionUserEmail { get; init; } = "crm.session@example.com";

    public string SessionUserPassword { get; init; } = "AcceptancePass01!";
}
