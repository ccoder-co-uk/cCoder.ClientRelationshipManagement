namespace cCoder.ClientRelationshipManagement.Services.Processings;

public interface IAuthorizationProcessingService
{
    IReadOnlyList<string> GetTenantIdsForPrivilege(string privilege);

    bool Can(string tenantId, string privilege);

    void Authorize(string tenantId, string privilege);

    void AuthorizeAny(string privilege);
}
