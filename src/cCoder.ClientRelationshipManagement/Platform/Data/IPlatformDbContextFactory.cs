namespace cCoder.ClientRelationshipManagement.Platform.Data;

public interface IPlatformDbContextFactory
{
    PlatformDbContext CreateDbContext(bool useAdminConnection = false);
}
