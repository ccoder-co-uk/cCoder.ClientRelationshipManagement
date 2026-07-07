using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Processings;

namespace cCoder.ClientRelationshipManagement.Services.Coordinations;

internal class DeletionCoordinationService(
    IClientActivityProcessingService clientActivityService,
    IClientContactProcessingService clientContactService,
    IClientHandoffPackProcessingService clientHandoffPackService,
    IClientMaterialProcessingService clientMaterialService,
    IEmailProcessingService emailService,
    IClientOpportunityProcessingService clientOpportunityService,
    ICompanyProcessingService companyService)
    : IDeletionCoordinationService
{
    public async ValueTask HandleAddressDeleteAsync(Address address)
    {
        Guid addressId = address?.Id ?? Guid.Empty;
        if (addressId == Guid.Empty)
            return;

        List<Company> companies =
        [
            .. companyService.GetAll(ignoreFilters: true)
                .Where(company => company.RegisteredAddressId == addressId)
        ];

        foreach (Company company in companies)
        {
            company.RegisteredAddressId = null;
            company.RegisteredAddress = null;

            await companyService.UpdateAsync(company);
        }
    }

    public async ValueTask HandleClientDeleteAsync(Client client)
    {
        Guid clientId = client?.Id ?? Guid.Empty;
        if (clientId == Guid.Empty)
            return;

        await DeleteHandoffPacksAsync(handoffPack =>
            handoffPack.ClientId == clientId);

        await DeleteActivitiesAsync(activity =>
            activity.ClientId == clientId);

        await DeleteEmailsAsync(email =>
            email.ClientId == clientId);

        await DeleteMaterialsAsync(material =>
            material.ClientId == clientId);

        await DeleteOpportunitiesAsync(opportunity =>
            opportunity.ClientId == clientId);

        await DeleteContactsAsync(contact =>
            contact.ClientId == clientId);

        Company company = companyService.GetAll(ignoreFilters: true)
            .FirstOrDefault(existingCompany => existingCompany.ClientId == clientId);

        if (company is not null)
            await companyService.DeleteAsync(company.Id);
    }

    public async ValueTask HandleClientContactDeleteAsync(ClientContact clientContact)
    {
        Guid clientContactId = clientContact?.Id ?? Guid.Empty;
        if (clientContactId == Guid.Empty)
            return;

        await DeleteActivitiesAsync(activity =>
            activity.ClientContactId == clientContactId);

        List<ClientMaterial> materials =
        [
            .. clientMaterialService.GetAll(ignoreFilters: true)
                .Where(material => material.SentToContactId == clientContactId)
        ];

        foreach (ClientMaterial material in materials)
        {
            material.SentToContactId = null;
            material.SentToContact = null;

            await clientMaterialService.UpdateAsync(material);
        }

        List<Email> emails =
        [
            .. emailService.GetAll(ignoreFilters: true)
                .Where(email => email.SentToContactId == clientContactId)
        ];

        foreach (Email email in emails)
        {
            email.SentToContactId = null;
            email.SentToContact = null;

            await emailService.UpdateAsync(email);
        }

        List<ClientOpportunity> opportunities =
        [
            .. clientOpportunityService.GetAll(ignoreFilters: true)
                .Where(opportunity => opportunity.PrimaryContactId == clientContactId)
        ];

        foreach (ClientOpportunity opportunity in opportunities)
        {
            opportunity.PrimaryContactId = null;
            opportunity.PrimaryContact = null;

            await clientOpportunityService.UpdateAsync(opportunity);
        }
    }

    public async ValueTask HandleClientMaterialDeleteAsync(ClientMaterial clientMaterial)
    {
        Guid clientMaterialId = clientMaterial?.Id ?? Guid.Empty;
        if (clientMaterialId == Guid.Empty)
            return;

        await DeleteActivitiesAsync(activity =>
            activity.ClientMaterialId == clientMaterialId);

        await DeleteEmailsAsync(email =>
            email.ClientMaterialId == clientMaterialId);
    }

    public ValueTask HandleEmailDeleteAsync(Email email)
    {
        Guid emailId = email?.Id ?? Guid.Empty;
        if (emailId == Guid.Empty)
            return ValueTask.CompletedTask;

        return ValueTask.CompletedTask;
    }

    public async ValueTask HandleClientOpportunityDeleteAsync(ClientOpportunity clientOpportunity)
    {
        Guid clientOpportunityId = clientOpportunity?.Id ?? Guid.Empty;
        if (clientOpportunityId == Guid.Empty)
            return;

        await DeleteHandoffPacksAsync(handoffPack =>
            handoffPack.ClientOpportunityId == clientOpportunityId);

        await DeleteActivitiesAsync(activity =>
            activity.ClientOpportunityId == clientOpportunityId);
    }

    async ValueTask DeleteActivitiesAsync(Func<ClientActivity, bool> predicate)
    {
        List<Guid> activityIds =
        [
            .. clientActivityService.GetAll(ignoreFilters: true)
                .Where(predicate)
                .Select(activity => activity.Id)
        ];

        foreach (Guid activityId in activityIds)
            await clientActivityService.DeleteAsync(activityId);
    }

    async ValueTask DeleteContactsAsync(Func<ClientContact, bool> predicate)
    {
        List<Guid> contactIds =
        [
            .. clientContactService.GetAll(ignoreFilters: true)
                .Where(predicate)
                .Select(contact => contact.Id)
        ];

        foreach (Guid contactId in contactIds)
            await clientContactService.DeleteAsync(contactId);
    }

    async ValueTask DeleteHandoffPacksAsync(Func<ClientHandoffPack, bool> predicate)
    {
        List<Guid> handoffPackIds =
        [
            .. clientHandoffPackService.GetAll(ignoreFilters: true)
                .Where(predicate)
                .Select(handoffPack => handoffPack.Id)
        ];

        foreach (Guid handoffPackId in handoffPackIds)
            await clientHandoffPackService.DeleteAsync(handoffPackId);
    }

    async ValueTask DeleteMaterialsAsync(Func<ClientMaterial, bool> predicate)
    {
        List<Guid> materialIds =
        [
            .. clientMaterialService.GetAll(ignoreFilters: true)
                .Where(predicate)
                .Select(material => material.Id)
        ];

        foreach (Guid materialId in materialIds)
            await clientMaterialService.DeleteAsync(materialId);
    }

    async ValueTask DeleteOpportunitiesAsync(Func<ClientOpportunity, bool> predicate)
    {
        List<Guid> opportunityIds =
        [
            .. clientOpportunityService.GetAll(ignoreFilters: true)
                .Where(predicate)
                .Select(opportunity => opportunity.Id)
        ];

        foreach (Guid opportunityId in opportunityIds)
            await clientOpportunityService.DeleteAsync(opportunityId);
    }

    async ValueTask DeleteEmailsAsync(Func<Email, bool> predicate)
    {
        List<Guid> emailIds =
        [
            .. emailService.GetAll(ignoreFilters: true)
                .Where(predicate)
                .Select(email => email.Id)
        ];

        foreach (Guid emailId in emailIds)
            await emailService.DeleteAsync(emailId);
    }
}
