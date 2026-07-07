using cCoder.ClientRelationshipManagement.Platform.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace cCoder.ClientRelationshipManagement.Platform;

public static class PlatformAppExtensions
{
    public static WebApplication StartCrmPlatform(
        this WebApplication app,
        ILogger log = null)
    {
        log?.LogInformation("Initialising CRM platform schemas");

        using IServiceScope scope = app.Services.CreateScope();
        IPlatformDbContextFactory contextFactory = scope.ServiceProvider.GetRequiredService<IPlatformDbContextFactory>();
        using PlatformDbContext dbContext = contextFactory.CreateDbContext(useAdminConnection: true);
        dbContext.Database.Migrate();

        return app;
    }
}
