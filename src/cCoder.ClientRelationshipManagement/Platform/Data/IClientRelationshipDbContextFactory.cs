namespace cCoder.ClientRelationshipManagement.Platform.Data;

public interface IClientRelationshipDbContextFactory
{
    ClientRelationshipDbContext CreateDbContext(bool useAdminConnection = false);
}
