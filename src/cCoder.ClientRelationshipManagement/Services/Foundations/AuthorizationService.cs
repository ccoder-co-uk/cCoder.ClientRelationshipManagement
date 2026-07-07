using System.Security;
using cCoder.ClientRelationshipManagement.Brokers;
using cCoder.ClientRelationshipManagement.Models.Security;

namespace cCoder.ClientRelationshipManagement.Services.Foundations;

internal class AuthorizationService(IAuthorizationBroker authorizationBroker) : IAuthorizationService
{
    public IReadOnlyList<string> GetTenantIdsForPrivilege(string privilege)
    {
        string normalizedPrivilege = privilege.ToLowerInvariant();

        ICRMAuthInfo authInfo = authorizationBroker.GetCRMAuthInfo();

        if (normalizedPrivilege == "client_read")
            return authInfo.ReadableTenants;

        if (normalizedPrivilege == "client_write")
            return authInfo.WriteableTenants;

        return [];
    }

    public bool Can(string tenantId, string privilege) => 
        GetTenantIdsForPrivilege(privilege).Contains(tenantId);

    public void Authorize(string tenantId, string privilege)
    {
        if (!Can(tenantId, privilege))
            throw new SecurityException($"Privilege '{privilege}' is not granted for tenant '{tenantId}'.");
    }

    public void AuthorizeAny(string privilege)
    {
        if (!GetTenantIdsForPrivilege(privilege).Any())
            throw new SecurityException($"Privilege '{privilege}' is not granted for any tenant.");
    }
}