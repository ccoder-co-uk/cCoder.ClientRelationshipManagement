namespace cCoder.ClientRelationshipManagement.Models.Configuration;

public class CRMConfiguration
{
    public string ConnectionString { get; set; }

    public string AdminConnectionString { get; set; }

    public CRMConfiguration UseSqlServer(string connectionString)
    {
        ConnectionString = connectionString;
        return this;
    }

    public CRMConfiguration UseSqlServerAdminConnection(string connectionString)
    {
        AdminConnectionString = connectionString;
        return this;
    }
}
