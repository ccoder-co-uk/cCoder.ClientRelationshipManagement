using cCoder.ClientRelationshipManagement.Platform.Data;
using ClientRelationshipManagement.Web.Brokers.Loggings;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.Web.Services.Migration;

public sealed class CrmDatabaseInitialiser(
    IClientRelationshipDbContextFactory dbContextFactory,
    ICrmPlatformBootstrapService crmPlatformBootstrapService,
    ILoggingBroker<CrmDatabaseInitialiser> loggingBroker)
    : ICrmDatabaseInitialiser
{
    public async ValueTask InitialiseAsync(CancellationToken cancellationToken = default)
    {
        loggingBroker.LogInformation("Initialising CRM platform schemas");

        using ClientRelationshipDbContext dbContext = dbContextFactory.CreateDbContext(useAdminConnection: true);
        await dbContext.Database.MigrateAsync(cancellationToken);
        await crmPlatformBootstrapService.InitialiseAsync(cancellationToken);
    }
}
