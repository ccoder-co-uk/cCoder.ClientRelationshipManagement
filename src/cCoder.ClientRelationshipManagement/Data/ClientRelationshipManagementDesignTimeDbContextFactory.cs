using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace cCoder.ClientRelationshipManagement.Data;

public class ClientRelationshipManagementDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<ClientRelationshipManagementDbContext>
{
    public ClientRelationshipManagementDbContext CreateDbContext(string[] args)
    {
        DbContextOptions<ClientRelationshipManagementDbContext> options =
            new DbContextOptionsBuilder<ClientRelationshipManagementDbContext>()
                .UseSqlServer(
                    "Server=(localdb)\\mssqllocaldb;Database=cCoderCRMDesignTime;Trusted_Connection=True;MultipleActiveResultSets=true")
                .Options;

        return new ClientRelationshipManagementDbContext(options);
    }
}
