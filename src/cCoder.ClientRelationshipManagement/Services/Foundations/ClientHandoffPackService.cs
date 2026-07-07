using cCoder.ClientRelationshipManagement.Brokers.Events;
using cCoder.ClientRelationshipManagement.Brokers.Storages;
using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.Eventing.Models;

namespace cCoder.ClientRelationshipManagement.Services.Foundations;

internal partial class ClientHandoffPackService(
    IClientHandoffPackBroker clientHandoffPackBroker,
    IClientHandoffPackEventBroker clientHandoffPackEventBroker,
    ICRMAuthInfo authInfo)
    : IClientHandoffPackService
{
    public ClientHandoffPack Get(Guid id, bool ignoreFilters = false) =>
        clientHandoffPackBroker.GetAllClientHandoffPacks(ignoreFilters)
            .FirstOrDefault(handoffPack => handoffPack.Id == id);

    public IQueryable<ClientHandoffPack> GetAll(bool ignoreFilters = false) =>
        clientHandoffPackBroker.GetAllClientHandoffPacks(ignoreFilters);

    public async ValueTask<ClientHandoffPack> AddAsync(ClientHandoffPack clientHandoffPack)
    {
        ValidateClientHandoffPack(clientHandoffPack, nameof(clientHandoffPack));

        if (clientHandoffPack.Id == Guid.Empty)
            clientHandoffPack.Id = Guid.NewGuid();

        if (Get(clientHandoffPack.Id, ignoreFilters: true) is not null)
            throw new InvalidOperationException($"Client handoff pack '{clientHandoffPack.Id}' already exists.");

        DateTimeOffset now = DateTimeOffset.UtcNow;
        ClientHandoffPack storageClientHandoffPack = new()
        {
            Id = clientHandoffPack.Id,
            ClientId = clientHandoffPack.ClientId,
            ClientOpportunityId = clientHandoffPack.ClientOpportunityId,
            SignedContractPath = clientHandoffPack.SignedContractPath,
            LegalEntity = clientHandoffPack.LegalEntity,
            PrimaryCommercialContact = clientHandoffPack.PrimaryCommercialContact,
            PrimaryOperationalContact = clientHandoffPack.PrimaryOperationalContact,
            PrimaryTechnicalContact = clientHandoffPack.PrimaryTechnicalContact,
            AgreedScope = clientHandoffPack.AgreedScope,
            CommercialTermsSummary = clientHandoffPack.CommercialTermsSummary,
            PromisedOutcomes = clientHandoffPack.PromisedOutcomes,
            KnownRisks = clientHandoffPack.KnownRisks,
            OnboardingOwner = clientHandoffPack.OnboardingOwner,
            Status = clientHandoffPack.Status,
            HandedOffOn = clientHandoffPack.HandedOffOn,
            CreatedBy = clientHandoffPack.CreatedBy ?? authInfo.SSOUserId,
            LastUpdatedBy = authInfo.SSOUserId,
            CreatedOn = clientHandoffPack.CreatedOn == default ? now : clientHandoffPack.CreatedOn,
            LastUpdated = now,
        };

        ClientHandoffPack result = await clientHandoffPackBroker.AddClientHandoffPackAsync(storageClientHandoffPack);
        result.Client = clientHandoffPack.Client;
        result.ClientOpportunity = clientHandoffPack.ClientOpportunity;
        await clientHandoffPackEventBroker.RaiseClientHandoffPackAddEventAsync(CreateEventMessage(result));
        return result;
    }

    public async ValueTask<ClientHandoffPack> UpdateAsync(ClientHandoffPack clientHandoffPack)
    {
        ValidateClientHandoffPack(clientHandoffPack, nameof(clientHandoffPack));

        ClientHandoffPack storageClientHandoffPack = new()
        {
            Id = clientHandoffPack.Id,
            ClientId = clientHandoffPack.ClientId,
            ClientOpportunityId = clientHandoffPack.ClientOpportunityId,
            SignedContractPath = clientHandoffPack.SignedContractPath,
            LegalEntity = clientHandoffPack.LegalEntity,
            PrimaryCommercialContact = clientHandoffPack.PrimaryCommercialContact,
            PrimaryOperationalContact = clientHandoffPack.PrimaryOperationalContact,
            PrimaryTechnicalContact = clientHandoffPack.PrimaryTechnicalContact,
            AgreedScope = clientHandoffPack.AgreedScope,
            CommercialTermsSummary = clientHandoffPack.CommercialTermsSummary,
            PromisedOutcomes = clientHandoffPack.PromisedOutcomes,
            KnownRisks = clientHandoffPack.KnownRisks,
            OnboardingOwner = clientHandoffPack.OnboardingOwner,
            Status = clientHandoffPack.Status,
            HandedOffOn = clientHandoffPack.HandedOffOn,
            CreatedBy = clientHandoffPack.CreatedBy,
            LastUpdatedBy = authInfo.SSOUserId,
            CreatedOn = clientHandoffPack.CreatedOn,
            LastUpdated = DateTimeOffset.UtcNow,
        };

        ClientHandoffPack result = await clientHandoffPackBroker.UpdateClientHandoffPackAsync(storageClientHandoffPack);
        result.Client = clientHandoffPack.Client;
        result.ClientOpportunity = clientHandoffPack.ClientOpportunity;
        await clientHandoffPackEventBroker.RaiseClientHandoffPackUpdateEventAsync(CreateEventMessage(result));
        return result;
    }

    public async ValueTask DeleteAsync(Guid id)
    {
        ClientHandoffPack clientHandoffPack = Get(id, ignoreFilters: true);

        if (clientHandoffPack is null)
            return;

        await clientHandoffPackEventBroker.RaiseClientHandoffPackDeleteEventAsync(CreateEventMessage(clientHandoffPack));
        await clientHandoffPackBroker.DeleteClientHandoffPackAsync(clientHandoffPack);
    }

    EventMessage<ClientHandoffPack> CreateEventMessage(ClientHandoffPack clientHandoffPack) =>
        new()
        {
            AuthInfo = new EventAuthInfo
            {
                SSOUserId = authInfo.SSOUserId,
            },
            Data = clientHandoffPack,
        };

    static void ValidateClientHandoffPack(ClientHandoffPack clientHandoffPack, string parameterName) =>
        ArgumentNullException.ThrowIfNull(clientHandoffPack, parameterName);
}
