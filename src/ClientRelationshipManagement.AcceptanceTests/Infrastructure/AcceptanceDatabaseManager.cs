using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.Security.Data.EF;
using cCoder.Security.Objects;
using cCoder.Security.Objects.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.AcceptanceTests.Infrastructure;

internal sealed class AcceptanceDatabaseManager(AcceptanceSettings settings)
{
    public async Task ResetDatabasesAsync()
    {
        using var sso = CreateSsoContext();

        EnsureSafeAcceptanceDatabase(sso.Database.GetConnectionString()!, "dev-Members");
        EnsureSafeAcceptanceDatabase(settings.CrmAdminConnectionString, "dev-CRM");

        ForceDropDatabase(sso.Database.GetConnectionString());
        ForceDropDatabase(settings.CrmAdminConnectionString);

        sso.Migrate();
        MigrateCrmPlatform();
        await SeedSecurityAsync();
    }

    public Task DropDatabasesAsync()
    {
        using var sso = CreateSsoContext();

        EnsureSafeAcceptanceDatabase(sso.Database.GetConnectionString()!, "dev-Members");
        EnsureSafeAcceptanceDatabase(settings.CrmAdminConnectionString, "dev-CRM");

        ForceDropDatabase(sso.Database.GetConnectionString());
        ForceDropDatabase(settings.CrmAdminConnectionString);

        return Task.CompletedTask;
    }

    async Task SeedSecurityAsync()
    {
        using var sso = CreateSsoContext();

        DateTimeOffset now = DateTimeOffset.UtcNow;
        var tenant = new Tenant
        {
            Id = AcceptanceSettings.TenantId,
            Name = "CRM Acceptance",
            Description = "Tenant used by CRM acceptance tests.",
            CreatedBy = settings.UserId,
            LastUpdatedBy = settings.UserId,
            CreatedOn = now,
            LastUpdated = now,
        };

        var user = new SSOUser
        {
            Id = settings.UserId,
            DisplayName = "CRM Acceptance User",
            Email = "crm.acceptance@example.com",
        };

        sso.Tenants.Add(tenant);
        sso.Users.Add(user);

        if (settings.GrantCrmPrivileges)
        {
            var role = new SSORole
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Name = "CRM Acceptance Administrators",
                Description = "Full CRM privileges for acceptance tests.",
                Privs = string.Join(
                    ',',
                    "client_read",
                    "client_write"),
            };

            sso.Roles.Add(role);
            sso.UserRoles.Add(new SSOUserRole
            {
                RoleId = role.Id,
                UserId = user.Id,
            });
        }

        await sso.SaveChangesAsync();
    }

    cCoder.Security.Data.EF.SecurityDbContext CreateSsoContext() =>
        new MSSQLSecurityDbContextFactory(settings.SsoConnectionString)
        {
            GetAuthInfo = _ => new SSOAuthInfo { SSOUserId = "Guest" },
        }.CreateDbContext(true);

    void MigrateCrmPlatform()
    {
        DbContextOptions<PlatformDbContext> options =
            new DbContextOptionsBuilder<PlatformDbContext>()
                .UseSqlServer(settings.CrmAdminConnectionString)
                .Options;

        using var dbContext = new PlatformDbContext(options);
        dbContext.Database.Migrate();
    }

    static void EnsureSafeAcceptanceDatabase(string connectionString, string protectedDatabaseName)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Acceptance database connection string is empty.");

        SqlConnectionStringBuilder builder = CreateAcceptanceConnectionStringBuilder(connectionString);
        string databaseName = builder.InitialCatalog ?? string.Empty;

        if (string.IsNullOrWhiteSpace(databaseName))
            throw new InvalidOperationException("Acceptance database name is empty.");

        if (databaseName.Equals(protectedDatabaseName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Refusing to run acceptance database operations against protected database '{protectedDatabaseName}'.");

        if (!databaseName.Contains("accept", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Refusing to run acceptance database operations against non-acceptance database '{databaseName}'.");
    }

    static void ForceDropDatabase(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        SqlConnectionStringBuilder builder = CreateAcceptanceConnectionStringBuilder(connectionString);
        string databaseName = builder.InitialCatalog ?? string.Empty;

        if (string.IsNullOrWhiteSpace(databaseName))
            return;

        builder.InitialCatalog = "master";
        builder.AttachDBFilename = string.Empty;

        using SqlConnection connection = new(builder.ConnectionString);
        connection.Open();

        using SqlCommand command = connection.CreateCommand();
        command.CommandText = @"
IF DB_ID(@databaseName) IS NOT NULL
BEGIN
    DECLARE @sql nvarchar(max) =
        N'ALTER DATABASE [' + REPLACE(@databaseName, ']', ']]') + N'] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;'
        + N'DROP DATABASE [' + REPLACE(@databaseName, ']', ']]') + N']';
    EXEC(@sql);
END";
        _ = command.Parameters.AddWithValue("@databaseName", databaseName);
        command.ExecuteNonQuery();
    }

    static SqlConnectionStringBuilder CreateAcceptanceConnectionStringBuilder(string connectionString) =>
        new(connectionString)
        {
            Encrypt = true,
            TrustServerCertificate = true,
        };
}
