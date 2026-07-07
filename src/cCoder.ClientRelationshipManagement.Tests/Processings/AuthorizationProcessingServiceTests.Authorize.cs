using Moq;
using Xunit;

namespace cCoder.ClientRelationshipManagement.Tests.Processings;

public partial class AuthorizationProcessingServiceTests
{
    [Fact]
    public void Authorize_ShouldDelegateToFoundationService()
    {
        authorizationServiceMock.Setup(service =>
            service.Authorize(TenantId, Privilege));

        authorizationProcessingService.Authorize(TenantId, Privilege);

        authorizationServiceMock.Verify(
            service => service.Authorize(TenantId, Privilege), Times.Once);
        authorizationServiceMock.VerifyNoOtherCalls();
    }
}
