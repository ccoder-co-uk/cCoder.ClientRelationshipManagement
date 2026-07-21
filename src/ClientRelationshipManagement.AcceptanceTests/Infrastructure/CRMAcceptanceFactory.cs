using cCoder.Eventing;
using cCoder.Eventing.Models;
using cCoder.ClientRelationshipManagement.Brokers;
using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Configuration;
using cCoder.Security.Data.EF;
using cCoder.Security.Data.EF.Interfaces;
using cCoder.Security.Exposures;
using cCoder.Security.Services.Orchestrations.Interfaces;
using cCoder.Security.Objects;
using cCoder.Security.Objects.DTOs;
using cCoder.Security.Objects.Entities;
using ClientRelationshipManagement.Web.Services.Mail;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CRMProgram = ClientRelationshipManagement.Web.Program;

namespace ClientRelationshipManagement.AcceptanceTests.Infrastructure;

internal sealed class CRMAcceptanceFactory(AcceptanceSettings settings)
    : WebApplicationFactory<CRMProgram>
{
    readonly AcceptanceCRMAuthInfo acceptanceAuthInfo = new(settings.UserId);

    internal void GrantTenant(string tenantId) => acceptanceAuthInfo.GrantTenant(tenantId);

    internal void RevokeTenant(string tenantId) => acceptanceAuthInfo.RevokeTenant(tenantId);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Acceptance");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(
            [
                new KeyValuePair<string, string>("ConnectionStrings:CRM", settings.CrmConnectionString),
                new KeyValuePair<string, string>("ConnectionStrings:CRMAdmin", settings.CrmAdminConnectionString),
                new KeyValuePair<string, string>("ConnectionStrings:SSO", settings.SsoConnectionString),
                new KeyValuePair<string, string>("Settings:DecryptionKey", settings.DecryptionKey),
                new KeyValuePair<string, string>("AgentWorkflows:ExecutionUserId", settings.UserId),
                new KeyValuePair<string, string>("Mail:EmailSendingEnabled", "true"),
                new KeyValuePair<string, string>("Mail:Provider", "SendGrid"),
                new KeyValuePair<string, string>("Mail:ApiKey", "acceptance-sendgrid-key")
            ]);
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ISecurityDbContextFactory>();
            services.RemoveAll<IEventHub>();
            services.RemoveAll<CRMConfiguration>();
            services.RemoveAll<ClientRelationshipDbContext>();
            services.RemoveAll<DbContextOptions<ClientRelationshipDbContext>>();
            services.RemoveAll<IClientRelationshipDbContextFactory>();
            services.RemoveAll<IAuthorizationBroker>();
            services.RemoveAll<ICRMAuthInfo>();
            services.RemoveAll<IMailClientFactory>();
            services.AddSingleton<IEventHub, NoOpEventHub>();
            services.AddSingleton<IMailClientFactory, AcceptanceMailClientFactory>();
            services.AddSingleton(new CRMConfiguration
            {
                ConnectionString = settings.CrmConnectionString,
                AdminConnectionString = settings.CrmAdminConnectionString,
            });

            if (settings.BypassAuthentication)
            {
                services.RemoveAll<ISSOAuthInfo>();
                services.AddSingleton<ISSOAuthInfo>(new SSOAuthInfo { SSOUserId = settings.UserId });
            }

            services.AddSingleton<ICRMAuthInfo>(acceptanceAuthInfo);
            services.AddScoped<IClientRelationshipDbContextFactory>(provider =>
                new AcceptanceClientRelationshipDbContextFactory(
                    settings,
                    provider.GetRequiredService<ICRMAuthInfo>()));
            services.AddScoped(provider =>
                provider.GetRequiredService<IClientRelationshipDbContextFactory>()
                    .CreateDbContext(useAdminConnection: true));

            services.AddScoped<ISecurityDbContextFactory>(
                provider => new MSSQLSecurityDbContextFactory(settings.SsoConnectionString)
                {
                    GetAuthInfo = ignoreAuthInfo => ignoreAuthInfo
                        ? new SSOAuthInfo { SSOUserId = "Guest" }
                        : provider.GetRequiredService<ISSOAuthInfo>(),
                });
        });
    }

    internal async Task EnsureSessionUserCanLoginAsync()
    {
        using IServiceScope scope = Services.CreateScope();
        ISSOUserOrchestrationService userService = scope.ServiceProvider.GetRequiredService<ISSOUserOrchestrationService>();
        ISecurityDbContextFactory dbContextFactory = scope.ServiceProvider.GetRequiredService<ISecurityDbContextFactory>();

        _ = await userService.Register(new RegisterUser
        {
            DisplayName = "CRM Session User",
            Email = settings.SessionUserEmail,
            Password = settings.SessionUserPassword,
            PhoneNumber = "01234567890",
            Culture = "en-GB",
            AppId = 0,
            TenantId = string.Empty,
        });

        using var dbContext = dbContextFactory.CreateDbContext(true);

        SSOUser user = await dbContext.Users
            .IgnoreQueryFilters()
            .FirstAsync(foundUser => foundUser.Email == settings.SessionUserEmail);

        SSORole role = await dbContext.Roles
            .IgnoreQueryFilters()
            .FirstAsync(foundRole => foundRole.TenantId == AcceptanceSettings.TenantId);

        bool hasRole = await dbContext.UserRoles
            .IgnoreQueryFilters()
            .AnyAsync(foundUserRole => foundUserRole.UserId == user.Id && foundUserRole.RoleId == role.Id);

        if (!hasRole)
        {
            dbContext.UserRoles.Add(new SSOUserRole
            {
                UserId = user.Id,
                RoleId = role.Id,
            });

            await dbContext.SaveChangesAsync();
        }
    }

    sealed class AcceptanceMailClientFactory : IMailClientFactory
    {
        public IMailClient CreateClient() => new AcceptanceMailClient();
    }

    sealed class AcceptanceMailClient : IMailClient
    {
        public Task<MailSendResult> SendAsync(MailSendRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new MailSendResult
            {
                Success = true,
                ExternalMessageId = $"acceptance-{Guid.NewGuid():N}"
            });
    }

    sealed class NoOpEventHub : IEventHub
    {
        public void ListenToEvent<T, TService>(string name, Func<TService, T, ValueTask> handler)
        {
        }

        public ValueTask RaiseEventAsync<T>(string name, EventMessage<T> message) =>
            ValueTask.CompletedTask;

        public ValueTask RaiseEventsAsync<T>(string name, EventMessage<T>[] messages) =>
            ValueTask.CompletedTask;
    }

    sealed class AcceptanceCRMAuthInfo(string userId) : ICRMAuthInfo
    {
        readonly HashSet<string> tenantIds = [AcceptanceSettings.TenantId];

        public string SSOUserId { get; } = userId;

        public string[] ReadableTenants { get { lock (tenantIds) return [.. tenantIds]; } }

        public string[] WriteableTenants { get { lock (tenantIds) return [.. tenantIds]; } }

        public void GrantTenant(string tenantId)
        {
            lock (tenantIds) tenantIds.Add(tenantId);
        }

        public void RevokeTenant(string tenantId)
        {
            lock (tenantIds) tenantIds.Remove(tenantId);
        }
    }

    sealed class AcceptanceClientRelationshipDbContextFactory(
        AcceptanceSettings settings,
        ICRMAuthInfo authInfo)
        : IClientRelationshipDbContextFactory
    {
        public ClientRelationshipDbContext CreateDbContext(bool useAdminConnection = false)
        {
            string connectionString = useAdminConnection && !string.IsNullOrWhiteSpace(settings.CrmAdminConnectionString)
                ? settings.CrmAdminConnectionString
                : settings.CrmConnectionString;

            DbContextOptions<ClientRelationshipDbContext> options =
                new DbContextOptionsBuilder<ClientRelationshipDbContext>()
                    .UseSqlServer(connectionString)
                    .Options;

            return new ClientRelationshipDbContext(options, authInfo);
        }
    }
}
