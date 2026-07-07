namespace cCoder.ClientRelationshipManagement.Models.Security;

public interface ICRMAuthInfo
{
    string SSOUserId { get; }

    string[] ReadableTenants { get; }

    string[] WriteableTenants { get; }
}
