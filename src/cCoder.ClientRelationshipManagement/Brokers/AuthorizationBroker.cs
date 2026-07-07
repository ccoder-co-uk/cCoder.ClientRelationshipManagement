using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.Security.Data.EF.Interfaces;
using cCoder.Security.Objects;

namespace cCoder.ClientRelationshipManagement.Brokers;

internal class AuthorizationBroker(
    ISecurityDbContextFactory securityDbContextFactory,
    ISSOAuthInfo ssoAuthInfo)
    : IAuthorizationBroker
{
    ICRMAuthInfo crmAuthInfo;

    public ICRMAuthInfo GetCRMAuthInfo()
    {
        if(crmAuthInfo is not null)
            return crmAuthInfo;

        crmAuthInfo =  new CRMAuthInfo
        {
            SSOUserId = ssoAuthInfo?.SSOUserId ?? "Guest",
            ReadableTenants = [.. GetTenantIdsForPrivilege("client_read")],
            WriteableTenants = [.. GetTenantIdsForPrivilege("client_write")],
        };

        return crmAuthInfo;
    }

    IReadOnlyList<string> GetTenantIdsForPrivilege(string privilege)
    {
        using var securityContext = securityDbContextFactory.CreateDbContext();
        string normalizedPrivilege = privilege.ToLowerInvariant();

        return [.. securityContext.GetCurrentUser()
            .Roles
            .Where(role => role.Role.Privs.Contains(normalizedPrivilege))
            .Select(role => role.Role.TenantId)
            .Distinct()];
    }
}
