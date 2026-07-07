using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Coordinations;
using cCoder.ClientRelationshipManagement.Services.Orchestrations;
using cCoder.Eventing;

namespace cCoder.ClientRelationshipManagement.Services.Foundations.Events;

internal class EventHandlerService(IEventHub eventHub) : IEventHandlerService
{
    public void ListenToAllEvents()
    {
        ListenToClientEvents();
        ListenToCompanyEvents();
        ListenToAddressEvents();
        ListenToDeleteEvents();
    }

    void ListenToClientEvents()
    {
        eventHub.ListenToEvent<Client, ICompanyOrchestrationService>(
            "client_add",
            (service, client) => AddOrUpdateCompanyAsync(service, client));

        eventHub.ListenToEvent<Client, ICompanyOrchestrationService>(
            "client_update",
            (service, client) => AddOrUpdateCompanyAsync(service, client));
    }

    void ListenToCompanyEvents()
    {
        eventHub.ListenToEvent<Company, IAddressOrchestrationService>(
            "company_add",
            (service, company) => AddOrUpdateRegisteredAddressAsync(service, company));

        eventHub.ListenToEvent<Company, IAddressOrchestrationService>(
            "company_update",
            (service, company) => AddOrUpdateRegisteredAddressAsync(service, company));
    }

    void ListenToAddressEvents()
    {
        eventHub.ListenToEvent<Address, ICompanyOrchestrationService>(
            "address_add",
            (service, address) => LinkCompaniesToRegisteredAddressAsync(service, address));

        eventHub.ListenToEvent<Address, ICompanyOrchestrationService>(
            "address_update",
            (service, address) => LinkCompaniesToRegisteredAddressAsync(service, address));
    }

    void ListenToDeleteEvents()
    {
        eventHub.ListenToEvent<Address, IDeletionCoordinationService>(
            "address_delete",
            (service, address) => service.HandleAddressDeleteAsync(address));

        eventHub.ListenToEvent<Client, IDeletionCoordinationService>(
            "client_delete",
            (service, client) => service.HandleClientDeleteAsync(client));

        eventHub.ListenToEvent<ClientContact, IDeletionCoordinationService>(
            "clientcontact_delete",
            (service, clientContact) => service.HandleClientContactDeleteAsync(clientContact));

        eventHub.ListenToEvent<ClientMaterial, IDeletionCoordinationService>(
            "clientmaterial_delete",
            (service, clientMaterial) => service.HandleClientMaterialDeleteAsync(clientMaterial));

        eventHub.ListenToEvent<Email, IDeletionCoordinationService>(
            "email_delete",
            (service, email) => service.HandleEmailDeleteAsync(email));

        eventHub.ListenToEvent<ClientOpportunity, IDeletionCoordinationService>(
            "clientopportunity_delete",
            (service, clientOpportunity) => service.HandleClientOpportunityDeleteAsync(clientOpportunity));
    }

    static async ValueTask AddOrUpdateCompanyAsync(
        ICompanyOrchestrationService companyService,
        Client client)
    {
        Company company = client?.Company;
        if (company is null)
            return;

        company.ClientId = client.Id;
        company.Client = ToClientReference(client);
        EnsureCompanyCanResolveTenant(company, client);

        await companyService.UpsertAsync(company);
    }

    static async ValueTask AddOrUpdateRegisteredAddressAsync(
        IAddressOrchestrationService addressService,
        Company company)
    {
        Address address = company?.RegisteredAddress;
        if (address is null)
            return;

        EnsureAddressCanResolveTenant(address, company);

        await addressService.UpsertAsync(address);
    }

    static async ValueTask LinkCompaniesToRegisteredAddressAsync(
        ICompanyOrchestrationService companyService,
        Address address)
    {
        foreach (Company company in address?.Companies ?? [])
        {
            company.RegisteredAddressId = address.Id;
            company.RegisteredAddress = null;

            await companyService.UpsertAsync(company);
        }
    }

    static void EnsureCompanyCanResolveTenant(Company company, Client client)
    {
        if (company.Client?.Id == client.Id)
            return;

        company.Client = new Client
        {
            Id = client.Id,
            TenantId = client.TenantId,
        };
    }

    static Client ToClientReference(Client client) =>
        new()
        {
            Id = client.Id,
            TenantId = client.TenantId,
        };

    static void EnsureAddressCanResolveTenant(Address address, Company company)
    {
        if (address.Companies.Any(existing => existing.Id == company.Id))
            return;

        address.Companies.Add(company);
    }
}
