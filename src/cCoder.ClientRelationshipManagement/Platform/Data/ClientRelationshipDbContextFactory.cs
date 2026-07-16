using cCoder.ClientRelationshipManagement.Models.Security;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public class ClientRelationshipDbContextFactory(
    Platform.Models.Configuration.CRMConfiguration configuration,
    ICRMAuthInfo authInfo)
    : IClientRelationshipDbContextFactory
{
    public ClientRelationshipDbContext CreateDbContext(bool useAdminConnection = false)
    {
        string connectionString = useAdminConnection && !string.IsNullOrWhiteSpace(configuration.AdminConnectionString)
            ? configuration.AdminConnectionString
            : configuration.ConnectionString;

        DbContextOptions<ClientRelationshipDbContext> options =
            new DbContextOptionsBuilder<ClientRelationshipDbContext>()
                .UseSqlServer(connectionString, sqlServer => sqlServer.CommandTimeout(600))
                .Options;

        return new ClientRelationshipDbContext(options, authInfo);
    }
}
