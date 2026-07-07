using cCoder.ClientRelationshipManagement.Brokers;
using cCoder.ClientRelationshipManagement.Brokers.Events;
using cCoder.ClientRelationshipManagement.Brokers.Storages;
using cCoder.ClientRelationshipManagement.Data;
using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Services.Coordinations;
using cCoder.ClientRelationshipManagement.Services.Foundations;
using cCoder.ClientRelationshipManagement.Services.Orchestrations;
using cCoder.ClientRelationshipManagement.Services.Processings;
using cCoder.Eventing;
using cCoder.Eventing.Models;
using cCoder.Security.Objects.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace cCoder.ClientRelationshipManagement.Tests.Foundations;

internal static class TestSupport
{
    public const string TenantId = "corporate-linx";

    public static ServiceProvider CreateServiceProvider(params string[] privileges)
    {
        string[] grantedPrivileges = privileges.Length == 0
            ? AllPrivileges
            : privileges;
        var authInfo = new TestCRMAuthInfo(
            "unit-test-user",
            grantedPrivileges.Contains("client_read") ? [TenantId] : [],
            grantedPrivileges.Contains("client_write") ? [TenantId] : []);
        ServiceCollection services = new();
        var authorizationBroker = new FakeAuthorizationBroker(authInfo);
        var eventHub = new RecordingEventHub();

        services.AddSingleton<IAuthorizationBroker>(authorizationBroker);
        services.AddSingleton<ICRMAuthInfo>(authInfo);
        services.AddSingleton<IEventHub>(eventHub);
        services.AddSingleton(eventHub);
        services.AddSingleton<ICRMContextFactory>(_ =>
            new TestCRMContextFactory(authInfo));

        services.AddScoped<IClientBroker, ClientBroker>();
        services.AddScoped<ICompanyBroker, CompanyBroker>();
        services.AddScoped<IAddressBroker, AddressBroker>();
        services.AddScoped<IClientContactBroker, ClientContactBroker>();
        services.AddScoped<IClientOpportunityBroker, ClientOpportunityBroker>();
        services.AddScoped<IClientActivityBroker, ClientActivityBroker>();
        services.AddScoped<IClientMaterialBroker, ClientMaterialBroker>();
        services.AddScoped<IEmailBroker, EmailBroker>();
        services.AddScoped<IClientHandoffPackBroker, ClientHandoffPackBroker>();

        services.AddScoped<IClientEventBroker, ClientEventBroker>();
        services.AddScoped<ICompanyEventBroker, CompanyEventBroker>();
        services.AddScoped<IAddressEventBroker, AddressEventBroker>();
        services.AddScoped<IClientContactEventBroker, ClientContactEventBroker>();
        services.AddScoped<IClientOpportunityEventBroker, ClientOpportunityEventBroker>();
        services.AddScoped<IClientActivityEventBroker, ClientActivityEventBroker>();
        services.AddScoped<IClientMaterialEventBroker, ClientMaterialEventBroker>();
        services.AddScoped<IEmailEventBroker, EmailEventBroker>();
        services.AddScoped<IClientHandoffPackEventBroker, ClientHandoffPackEventBroker>();

        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IAddressService, AddressService>();
        services.AddScoped<IClientContactService, ClientContactService>();
        services.AddScoped<IClientOpportunityService, ClientOpportunityService>();
        services.AddScoped<IClientActivityService, ClientActivityService>();
        services.AddScoped<IClientMaterialService, ClientMaterialService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IClientHandoffPackService, ClientHandoffPackService>();
        services.AddScoped<IAuthorizationService, AuthorizationService>();
        services.AddScoped<IDeletionCoordinationService, DeletionCoordinationService>();

        services.AddScoped<IAuthorizationProcessingService, AuthorizationProcessingService>();
        services.AddScoped<IClientProcessingService, ClientProcessingService>();
        services.AddScoped<ICompanyProcessingService, CompanyProcessingService>();
        services.AddScoped<IAddressProcessingService, AddressProcessingService>();
        services.AddScoped<IClientContactProcessingService, ClientContactProcessingService>();
        services.AddScoped<IClientOpportunityProcessingService, ClientOpportunityProcessingService>();
        services.AddScoped<IClientActivityProcessingService, ClientActivityProcessingService>();
        services.AddScoped<IClientMaterialProcessingService, ClientMaterialProcessingService>();
        services.AddScoped<IEmailProcessingService, EmailProcessingService>();
        services.AddScoped<IClientHandoffPackProcessingService, ClientHandoffPackProcessingService>();

        services.AddScoped<IClientOrchestrationService, ClientOrchestrationService>();
        services.AddScoped<ICompanyOrchestrationService, CompanyOrchestrationService>();
        services.AddScoped<IAddressOrchestrationService, AddressOrchestrationService>();
        services.AddScoped<IClientContactOrchestrationService, ClientContactOrchestrationService>();
        services.AddScoped<IClientOpportunityOrchestrationService, ClientOpportunityOrchestrationService>();
        services.AddScoped<IClientActivityOrchestrationService, ClientActivityOrchestrationService>();
        services.AddScoped<IClientMaterialOrchestrationService, ClientMaterialOrchestrationService>();
        services.AddScoped<IEmailOrchestrationService, EmailOrchestrationService>();
        services.AddScoped<IClientHandoffPackOrchestrationService, ClientHandoffPackOrchestrationService>();

        return services.BuildServiceProvider();
    }

    public static async ValueTask<Client> AddClientAsync(IServiceProvider serviceProvider)
    {
        IClientService clientService = serviceProvider.GetRequiredService<IClientService>();

        return await clientService.AddAsync(new Client
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            AccountOwner = "Unit Test",
            Company = new Company
            {
                Id = Guid.NewGuid(),
                Name = "Unit Test Company"
            }
        });
    }

    public static async ValueTask<ClientOpportunity> AddOpportunityAsync(
        IServiceProvider serviceProvider,
        Guid clientId)
    {
        IClientOpportunityService service =
            serviceProvider.GetRequiredService<IClientOpportunityService>();

        return await service.AddAsync(new ClientOpportunity
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
        });
    }

    static readonly string[] AllPrivileges =
    [
        "client_read",
        "client_write",
    ];

    sealed class TestCRMContextFactory(ICRMAuthInfo authInfo) : ICRMContextFactory
    {
        readonly InMemoryDatabaseRoot databaseRoot = new();
        readonly string databaseName = Guid.NewGuid().ToString();

        public ClientRelationshipManagementDbContext CreateContext(bool useAdminConnection = false)
        {
            DbContextOptions<ClientRelationshipManagementDbContext> options =
                new DbContextOptionsBuilder<ClientRelationshipManagementDbContext>()
                    .UseInMemoryDatabase(databaseName, databaseRoot)
                    .Options;

            return new ClientRelationshipManagementDbContext(
                options,
                useAdminConnection ? null : authInfo);
        }
    }

    sealed class FakeAuthorizationBroker(ICRMAuthInfo authInfo) : IAuthorizationBroker
    {
        public ICRMAuthInfo GetCRMAuthInfo() =>
            authInfo;

        public SSOUser GetCurrentUser() =>
            new()
            {
                Id = authInfo.SSOUserId
            };

        public IReadOnlyList<string> GetTenantIdsForPrivilege(string privilege) =>
            privilege switch
            {
                "client_read" => authInfo.ReadableTenants,
                "client_write" => authInfo.WriteableTenants,
                _ => [],
            };
    }

    internal sealed class RecordingEventHub : IEventHub
    {
        public List<EventRecord> RaisedEvents { get; } = [];

        public void ListenToEvent<T, TService>(string name, Func<TService, T, ValueTask> handler)
        {
        }

        public ValueTask RaiseEventAsync<T>(string name, EventMessage<T> message)
        {
            RaisedEvents.Add(new EventRecord(name, message.Data, message.AuthInfo?.SSOUserId));
            return ValueTask.CompletedTask;
        }

        public async ValueTask RaiseEventsAsync<T>(string name, EventMessage<T>[] messages)
        {
            foreach (EventMessage<T> message in messages)
                await RaiseEventAsync(name, message);
        }
    }

    internal sealed record EventRecord(string Name, object Data, string UserId);

    sealed class TestCRMAuthInfo(
        string ssoUserId,
        string[] readableTenants,
        string[] writeableTenants)
        : ICRMAuthInfo
    {
        public string SSOUserId { get; } = ssoUserId;
        public string[] ReadableTenants { get; } = readableTenants;
        public string[] WriteableTenants { get; } = writeableTenants;
    }
}
