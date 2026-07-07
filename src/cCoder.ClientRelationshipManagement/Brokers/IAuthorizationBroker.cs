using cCoder.ClientRelationshipManagement.Models.Security;

namespace cCoder.ClientRelationshipManagement.Brokers;

public interface IAuthorizationBroker
{
    ICRMAuthInfo GetCRMAuthInfo();
}