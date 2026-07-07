namespace cCoder.ClientRelationshipManagement.Data;

public interface ICRMContextFactory
{
    ClientRelationshipManagementDbContext CreateContext(bool useAdminConnection = false);
}
