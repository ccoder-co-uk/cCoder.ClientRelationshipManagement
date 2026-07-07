using Moq;
using Xunit;

namespace cCoder.ClientRelationshipManagement.Tests.Processings;

public partial class AuthorizationProcessingServiceTests
{
    [Fact]
    public void AuthorizeAny_ShouldDelegateToFoundationService()
    {
        authorizationServiceMock.Setup(service =>
            service.AuthorizeAny(Privilege));

        authorizationProcessingService.AuthorizeAny(Privilege);

        authorizationServiceMock.Verify(
            service => service.AuthorizeAny(Privilege), Times.Once);
        authorizationServiceMock.VerifyNoOtherCalls();
    }
}
