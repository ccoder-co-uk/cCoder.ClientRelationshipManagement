namespace cCoder.ClientRelationshipManagement.Services.Foundations;

public interface IAuthorizationService
{
    IReadOnlyList<string> GetTenantIdsForPrivilege(string privilege);

    bool Can(string tenantId, string privilege);

    void Authorize(string tenantId, string privilege);

    void AuthorizeAny(string privilege);
}
