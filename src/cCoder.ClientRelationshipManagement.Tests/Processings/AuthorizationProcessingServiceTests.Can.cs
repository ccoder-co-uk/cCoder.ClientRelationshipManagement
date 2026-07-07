using Moq;
using Xunit;

namespace cCoder.ClientRelationshipManagement.Tests.Processings;

public partial class AuthorizationProcessingServiceTests
{
    [Fact]
    public void Can_ShouldDelegateToFoundationService()
    {
        authorizationServiceMock.Setup(service =>
            service.Can(TenantId, Privilege)).Returns(true);

        bool result = authorizationProcessingService.Can(TenantId, Privilege);

        Assert.True(result);
        authorizationServiceMock.Verify(
            service => service.Can(TenantId, Privilege), Times.Once);
        authorizationServiceMock.VerifyNoOtherCalls();
    }
}
