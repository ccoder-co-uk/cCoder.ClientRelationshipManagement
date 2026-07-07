using cCoder.ClientRelationshipManagement.Brokers;
using cCoder.ClientRelationshipManagement.Brokers.Events;
using cCoder.ClientRelationshipManagement.Brokers.Storages;
using cCoder.ClientRelationshipManagement.Data;
using cCoder.ClientRelationshipManagement.Models.Configuration;
using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Coordinations;
using cCoder.ClientRelationshipManagement.Services.Foundations;
using cCoder.ClientRelationshipManagement.Services.Foundations.Events;
using cCoder.ClientRelationshipManagement.Services.Orchestrations;
using cCoder.ClientRelationshipManagement.Services.Processings;
using cCoder.Eventing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CRMAuthBroker = cCoder.ClientRelationshipManagement.Brokers.AuthorizationBroker;

namespace cCoder.ClientRelationshipManagement.Services;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddClientRelationshipManagement(
        this IServiceCollection services,
        Action<CRMConfiguration> configure)
    {
        CRMConfiguration configuration = new();
        configure?.Invoke(configuration);

        if (string.IsNullOrWhiteSpace(configuration.ConnectionString))
            throw new InvalidOperationException("CRMConfiguration.ConnectionString is required.");

        if (string.IsNullOrWhiteSpace(configuration.AdminConnectionString))
            throw new InvalidOperationException(
                "CRMConfiguration.AdminConnectionString is required to apply CRM database migrations.");

        services.AddSingleton(configuration);
        services.AddDbContext<ClientRelationshipManagementDbContext>(options =>
            options.UseSqlServer(configuration.ConnectionString));

        services.AddScoped<IAuthorizationBroker, CRMAuthBroker>();
        services.AddScoped(provider => provider.GetRequiredService<IAuthorizationBroker>().GetCRMAuthInfo());
        services.AddScoped<ICRMContextFactory, CRMContextFactory>();
        RegisterCRMServices(services);

        services.AddControllers()
            .AddApplicationPart(typeof(IServiceCollectionExtensions).Assembly);

        return services;
    }

    private static void RegisterCRMServices(IServiceCollection services)
    {
        services.AddEventingTypes();

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

        services.AddScoped<IEventHandlerService, EventHandlerService>();
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
    }

    private static void AddEventingTypes(this IServiceCollection services)
    {
        services.AddEventingForType<Address>();
        services.AddEventingForType<Client>();
        services.AddEventingForType<ClientActivity>();
        services.AddEventingForType<ClientContact>();
        services.AddEventingForType<Email>();
        services.AddEventingForType<ClientHandoffPack>();
        services.AddEventingForType<ClientMaterial>();
        services.AddEventingForType<ClientOpportunity>();
        services.AddEventingForType<Company>();
    }
}
