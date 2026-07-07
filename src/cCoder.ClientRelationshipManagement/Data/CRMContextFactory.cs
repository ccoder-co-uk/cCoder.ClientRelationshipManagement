using cCoder.ClientRelationshipManagement.Models.Configuration;
using cCoder.ClientRelationshipManagement.Models.Security;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Data;

public class CRMContextFactory(
    CRMConfiguration configuration,
    ICRMAuthInfo authInfo = null)
    : ICRMContextFactory
{
    public ClientRelationshipManagementDbContext CreateContext(bool useAdminConnection = false)
    {
        string connectionString = useAdminConnection
            ? configuration.AdminConnectionString
            : configuration.ConnectionString;

        DbContextOptions<ClientRelationshipManagementDbContext> options =
            new DbContextOptionsBuilder<ClientRelationshipManagementDbContext>()
                .UseSqlServer(connectionString)
                .Options;

        return new ClientRelationshipManagementDbContext(
            options,
            useAdminConnection ? null : authInfo);
    }
}
