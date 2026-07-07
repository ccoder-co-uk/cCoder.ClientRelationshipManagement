using cCoder.ClientRelationshipManagement.Models.Security;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public class PlatformDbContextFactory(
    Platform.Models.Configuration.PlatformConfiguration configuration,
    ICRMAuthInfo authInfo)
    : IPlatformDbContextFactory
{
    public PlatformDbContext CreateDbContext(bool useAdminConnection = false)
    {
        string connectionString = useAdminConnection && !string.IsNullOrWhiteSpace(configuration.AdminConnectionString)
            ? configuration.AdminConnectionString
            : configuration.ConnectionString;

        DbContextOptions<PlatformDbContext> options =
            new DbContextOptionsBuilder<PlatformDbContext>()
                .UseSqlServer(connectionString)
                .Options;

        return new PlatformDbContext(options, authInfo);
    }
}
