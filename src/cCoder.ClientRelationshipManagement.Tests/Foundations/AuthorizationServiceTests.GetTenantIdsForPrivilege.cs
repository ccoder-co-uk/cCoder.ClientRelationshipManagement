using Xunit;

namespace cCoder.ClientRelationshipManagement.Tests.Foundations;

public partial class AuthorizationServiceTests
{
    [Fact]
    public void GetTenantIdsForPrivilege_ShouldReturnReadableTenantsForClientRead()
    {
        SetupAuthInfo();

        IReadOnlyList<string> result =
            authorizationService.GetTenantIdsForPrivilege("CLIENT_READ");

        Assert.Equal([ReadableTenantId], result);
        authorizationBrokerMock.Verify(broker => broker.GetCRMAuthInfo(), Moq.Times.Once);
        authorizationBrokerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void GetTenantIdsForPrivilege_ShouldReturnWriteableTenantsForClientWrite()
    {
        SetupAuthInfo();

        IReadOnlyList<string> result =
            authorizationService.GetTenantIdsForPrivilege("client_write");

        Assert.Equal([WriteableTenantId], result);
        authorizationBrokerMock.Verify(broker => broker.GetCRMAuthInfo(), Moq.Times.Once);
        authorizationBrokerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void GetTenantIdsForPrivilege_ShouldReturnEmptyTenantsForUnknownPrivilege()
    {
        SetupAuthInfo();

        IReadOnlyList<string> result =
            authorizationService.GetTenantIdsForPrivilege("company_read");

        Assert.Empty(result);
        authorizationBrokerMock.Verify(broker => broker.GetCRMAuthInfo(), Moq.Times.Once);
        authorizationBrokerMock.VerifyNoOtherCalls();
    }
}
