using cCoder.ClientRelationshipManagement.Services.Foundations;

namespace cCoder.ClientRelationshipManagement.Services.Processings;

internal class AuthorizationProcessingService(IAuthorizationService authorizationService)
    : IAuthorizationProcessingService
{
    public IReadOnlyList<string> GetTenantIdsForPrivilege(string privilege) =>
        authorizationService.GetTenantIdsForPrivilege(privilege);

    public bool Can(string tenantId, string privilege) =>
        authorizationService.Can(tenantId, privilege);

    public void Authorize(string tenantId, string privilege) =>
        authorizationService.Authorize(tenantId, privilege);

    public void AuthorizeAny(string privilege) =>
        authorizationService.AuthorizeAny(privilege);
}
