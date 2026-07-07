using cCoder.ClientRelationshipManagement.Brokers;
using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Services.Foundations;
using cCoder.Security.Objects.Entities;
using Moq;

namespace cCoder.ClientRelationshipManagement.Tests.Foundations;

public partial class AuthorizationServiceTests
{
    const string ReadableTenantId = "readable-tenant";
    const string WriteableTenantId = "writeable-tenant";

    readonly Mock<IAuthorizationBroker> authorizationBrokerMock;
    readonly AuthorizationService authorizationService;

    public AuthorizationServiceTests()
    {
        authorizationBrokerMock = new Mock<IAuthorizationBroker>(MockBehavior.Strict);
        authorizationService = new AuthorizationService(authorizationBrokerMock.Object);
    }

    void SetupAuthInfo() =>
        authorizationBrokerMock.Setup(broker => broker.GetCRMAuthInfo())
            .Returns(new TestCRMAuthInfo());

    sealed class TestCRMAuthInfo : ICRMAuthInfo
    {
        public string SSOUserId { get; init; } = "unit-test-user";
        public string[] ReadableTenants { get; init; } = [ReadableTenantId];
        public string[] WriteableTenants { get; init; } = [WriteableTenantId];
    }
}
