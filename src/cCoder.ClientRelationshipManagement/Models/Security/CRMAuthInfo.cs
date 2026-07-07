namespace cCoder.ClientRelationshipManagement.Models.Security;

internal class CRMAuthInfo : ICRMAuthInfo
{
    public string SSOUserId { get; init; } = "Guest";

    public string[] ReadableTenants { get; init; } = [];

    public string[] WriteableTenants { get; init; } = [];
}
