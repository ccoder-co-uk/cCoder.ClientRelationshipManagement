using Moq;
using Xunit;

namespace cCoder.ClientRelationshipManagement.Tests.Processings;

public partial class AuthorizationProcessingServiceTests
{
    [Fact]
    public void GetTenantIdsForPrivilege_ShouldDelegateToFoundationService()
    {
        string[] tenantIds = [TenantId];
        authorizationServiceMock.Setup(service =>
            service.GetTenantIdsForPrivilege(Privilege)).Returns(tenantIds);

        IReadOnlyList<string> result =
            authorizationProcessingService.GetTenantIdsForPrivilege(Privilege);

        Assert.Same(tenantIds, result);
        authorizationServiceMock.Verify(
            service => service.GetTenantIdsForPrivilege(Privilege), Times.Once);
        authorizationServiceMock.VerifyNoOtherCalls();
    }
}
