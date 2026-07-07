using Xunit;

namespace cCoder.ClientRelationshipManagement.Tests.Foundations;

public partial class AuthorizationServiceTests
{
    [Fact]
    public void Can_ShouldReturnTrueWhenTenantHasPrivilege()
    {
        SetupAuthInfo();

        bool result = authorizationService.Can(ReadableTenantId, "client_read");

        Assert.True(result);
        authorizationBrokerMock.Verify(broker => broker.GetCRMAuthInfo(), Moq.Times.Once);
        authorizationBrokerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void Can_ShouldReturnFalseWhenTenantDoesNotHavePrivilege()
    {
        SetupAuthInfo();

        bool result = authorizationService.Can("denied-tenant", "client_read");

        Assert.False(result);
        authorizationBrokerMock.Verify(broker => broker.GetCRMAuthInfo(), Moq.Times.Once);
        authorizationBrokerMock.VerifyNoOtherCalls();
    }
}
