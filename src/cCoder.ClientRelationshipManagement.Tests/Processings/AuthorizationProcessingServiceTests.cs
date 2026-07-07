using cCoder.ClientRelationshipManagement.Services.Foundations;
using cCoder.ClientRelationshipManagement.Services.Processings;
using Moq;

namespace cCoder.ClientRelationshipManagement.Tests.Processings;

public partial class AuthorizationProcessingServiceTests
{
    const string TenantId = "crm-tenant";
    const string Privilege = "client_read";

    readonly Mock<IAuthorizationService> authorizationServiceMock;
    readonly AuthorizationProcessingService authorizationProcessingService;

    public AuthorizationProcessingServiceTests()
    {
        authorizationServiceMock = new Mock<IAuthorizationService>(MockBehavior.Strict);
        authorizationProcessingService =
            new AuthorizationProcessingService(authorizationServiceMock.Object);
    }
}
