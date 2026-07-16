using cCoder.ClientRelationshipManagement.Models.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public class PlatformDesignTimeDbContextFactory : IDesignTimeDbContextFactory<PlatformDbContext>
{
    public PlatformDbContext CreateDbContext(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__CRMAdmin", EnvironmentVariableTarget.Process)
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__CRM", EnvironmentVariableTarget.Process)
            ?? Environment.GetEnvironmentVariable("CRM__AdminConnectionString", EnvironmentVariableTarget.Process)
            ?? Environment.GetEnvironmentVariable("CRM__ConnectionString", EnvironmentVariableTarget.Process)
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=dev-CRMPlatform;Trusted_Connection=True;TrustServerCertificate=True;";

        DbContextOptions<PlatformDbContext> options =
            new DbContextOptionsBuilder<PlatformDbContext>()
                .UseSqlServer(connectionString, sqlServer => sqlServer.CommandTimeout(600))
                .Options;

        return new PlatformDbContext(
            options,
            new DesignTimeAuthInfo());
    }

    sealed class DesignTimeAuthInfo : ICRMAuthInfo
    {
        public string SSOUserId => "design-time";

        public string[] ReadableTenants => [];

        public string[] WriteableTenants => [];
    }
}
