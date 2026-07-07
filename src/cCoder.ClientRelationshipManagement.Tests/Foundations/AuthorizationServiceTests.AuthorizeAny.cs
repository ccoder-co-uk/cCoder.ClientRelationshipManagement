using System.Security;
using Xunit;

namespace cCoder.ClientRelationshipManagement.Tests.Foundations;

public partial class AuthorizationServiceTests
{
    [Fact]
    public void AuthorizeAny_ShouldNotThrowWhenAnyTenantHasPrivilege()
    {
        SetupAuthInfo();

        authorizationService.AuthorizeAny("client_read");

        authorizationBrokerMock.Verify(broker => broker.GetCRMAuthInfo(), Moq.Times.Once);
        authorizationBrokerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void AuthorizeAny_ShouldThrowWhenNoTenantHasPrivilege()
    {
        SetupAuthInfo();

        Assert.Throws<SecurityException>(() =>
            authorizationService.AuthorizeAny("company_read"));

        authorizationBrokerMock.Verify(broker => broker.GetCRMAuthInfo(), Moq.Times.Once);
        authorizationBrokerMock.VerifyNoOtherCalls();
    }
}
