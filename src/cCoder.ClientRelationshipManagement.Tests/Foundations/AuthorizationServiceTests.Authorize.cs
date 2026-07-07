using System.Security;
using Xunit;

namespace cCoder.ClientRelationshipManagement.Tests.Foundations;

public partial class AuthorizationServiceTests
{
    [Fact]
    public void Authorize_ShouldNotThrowWhenTenantHasPrivilege()
    {
        SetupAuthInfo();

        authorizationService.Authorize(WriteableTenantId, "client_write");

        authorizationBrokerMock.Verify(broker => broker.GetCRMAuthInfo(), Moq.Times.Once);
        authorizationBrokerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void Authorize_ShouldThrowWhenTenantDoesNotHavePrivilege()
    {
        SetupAuthInfo();

        Assert.Throws<SecurityException>(() =>
            authorizationService.Authorize("denied-tenant", "client_write"));

        authorizationBrokerMock.Verify(broker => broker.GetCRMAuthInfo(), Moq.Times.Once);
        authorizationBrokerMock.VerifyNoOtherCalls();
    }
}
