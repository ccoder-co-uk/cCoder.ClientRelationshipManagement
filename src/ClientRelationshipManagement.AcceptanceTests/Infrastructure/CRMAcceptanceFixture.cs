using cCoder.Security.Exposures;
using cCoder.Security.Objects.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClientRelationshipManagement.AcceptanceTests.Infrastructure;

public sealed class CRMAcceptanceFixture : IAsyncLifetime
{
    AcceptanceDatabaseManager databaseManager;

    internal AcceptanceSettings Settings { get; private set; } = null!;

    internal CRMAcceptanceFactory Factory { get; private set; } = null!;

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        string crmConnectionString = ReadRequiredConnectionString("CCODER_ACCEPTANCE_CRM_CONNECTION_STRING");
        string ssoConnectionString = ReadRequiredConnectionString("CCODER_ACCEPTANCE_SSO_CONNECTION_STRING");
        string crmAdminConnectionString = ReadOptionalConnectionString("CCODER_ACCEPTANCE_CRM_ADMIN_CONNECTION_STRING")
            ?? crmConnectionString;

        Settings = new AcceptanceSettings
        {
            CrmConnectionString = crmConnectionString,
            CrmAdminConnectionString = crmAdminConnectionString,
            SsoConnectionString = ssoConnectionString,
            DecryptionKey = "000000000000000000000000000000000000000000000000",
        };

        databaseManager = new AcceptanceDatabaseManager(Settings);
        await databaseManager.ResetDatabasesAsync();

        Factory = new CRMAcceptanceFactory(Settings);
        Client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();

        if (Factory is not null)
            await Factory.DisposeAsync();

        if (databaseManager is not null)
            await databaseManager.DropDatabasesAsync();
    }

    static string ReadRequiredConnectionString(string variableName) =>
        ReadOptionalConnectionString(variableName)
        ?? throw new InvalidOperationException(
            $"Acceptance test environment variable '{variableName}' is required.");

    static string ReadOptionalConnectionString(string variableName)
    {
        string connectionString =
            Environment.GetEnvironmentVariable(variableName)
            ?? Environment.GetEnvironmentVariable(variableName, EnvironmentVariableTarget.Machine);

        return string.IsNullOrWhiteSpace(connectionString)
            ? null
            : connectionString;
    }
    
    internal async Task<string> IssueAgentTokenAsync(string userId = null)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ITokenManager tokenManager = scope.ServiceProvider.GetRequiredService<ITokenManager>();
        return (await tokenManager.IssueTokenAsync(userId ?? Settings.UserId, TokenUse.WorkflowExecution)).Id;
    }
}

[CollectionDefinition(Name)]
public sealed class CRMAcceptanceCollection : ICollectionFixture<CRMAcceptanceFixture>
{
    public const string Name = "CRM acceptance";
}
