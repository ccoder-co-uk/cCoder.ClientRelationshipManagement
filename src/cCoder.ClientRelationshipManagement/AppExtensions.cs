using cCoder.ClientRelationshipManagement.Data;
using cCoder.ClientRelationshipManagement.Models.Configuration;
using cCoder.ClientRelationshipManagement.Services.Foundations.Events;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace cCoder.ClientRelationshipManagement;

public static class AppExtensions
{
    public static WebApplication StartClientRelationshipManagementWeb(
        this WebApplication app,
        ILogger log = null)
    {
        log?.LogInformation("Initialising Client Relationship Management");

        using IServiceScope scope = app.Services.CreateScope();
        CRMConfiguration configuration = scope.ServiceProvider.GetRequiredService<CRMConfiguration>();

        if (string.IsNullOrWhiteSpace(configuration.AdminConnectionString))
            throw new InvalidOperationException(
                "CRMConfiguration.AdminConnectionString is required to apply CRM database migrations.");

        ICRMContextFactory contextFactory = scope.ServiceProvider.GetRequiredService<ICRMContextFactory>();
        using ClientRelationshipManagementDbContext dbContext = contextFactory.CreateContext(useAdminConnection: true);
        dbContext.Database.Migrate();

        scope.ServiceProvider.GetRequiredService<IEventHandlerService>()
            .ListenToAllEvents();

        return app;
    }
}
