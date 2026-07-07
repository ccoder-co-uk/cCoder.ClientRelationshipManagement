using cCoder.ClientRelationshipManagement.Brokers.Events;
using cCoder.ClientRelationshipManagement.Brokers.Storages;
using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.Eventing.Models;

namespace cCoder.ClientRelationshipManagement.Services.Foundations;

internal partial class ClientOpportunityService(
    IClientOpportunityBroker clientOpportunityBroker,
    IClientOpportunityEventBroker clientOpportunityEventBroker,
    ICRMAuthInfo authInfo)
    : IClientOpportunityService
{
    public ClientOpportunity Get(Guid id, bool ignoreFilters = false) =>
        clientOpportunityBroker.GetAllClientOpportunities(ignoreFilters)
            .FirstOrDefault(opportunity => opportunity.Id == id);

    public IQueryable<ClientOpportunity> GetAll(bool ignoreFilters = false) =>
        clientOpportunityBroker.GetAllClientOpportunities(ignoreFilters);

    public async ValueTask<ClientOpportunity> AddAsync(ClientOpportunity clientOpportunity)
    {
        ValidateClientOpportunity(clientOpportunity, nameof(clientOpportunity));

        if (clientOpportunity.Id == Guid.Empty)
            clientOpportunity.Id = Guid.NewGuid();

        if (Get(clientOpportunity.Id, ignoreFilters: true) is not null)
            throw new InvalidOperationException($"Client opportunity '{clientOpportunity.Id}' already exists.");

        DateTimeOffset now = DateTimeOffset.UtcNow;
        ClientOpportunity storageClientOpportunity = new()
        {
            Id = clientOpportunity.Id,
            ClientId = clientOpportunity.ClientId,
            PrimaryContactId = clientOpportunity.PrimaryContactId,
            Type = clientOpportunity.Type,
            Stage = clientOpportunity.Stage,
            EstimatedAnnualValue = clientOpportunity.EstimatedAnnualValue,
            Probability = clientOpportunity.Probability,
            PainSummary = clientOpportunity.PainSummary,
            ValueHypothesis = clientOpportunity.ValueHypothesis,
            DecisionProcess = clientOpportunity.DecisionProcess,
            NextAction = clientOpportunity.NextAction,
            NextActionDueOn = clientOpportunity.NextActionDueOn,
            CreatedBy = clientOpportunity.CreatedBy ?? authInfo.SSOUserId,
            LastUpdatedBy = authInfo.SSOUserId,
            CreatedOn = clientOpportunity.CreatedOn == default ? now : clientOpportunity.CreatedOn,
            LastUpdated = now,
        };

        ClientOpportunity result = await clientOpportunityBroker.AddClientOpportunityAsync(storageClientOpportunity);
        result.Client = clientOpportunity.Client;
        result.PrimaryContact = clientOpportunity.PrimaryContact;
        result.Activities = clientOpportunity.Activities;
        result.HandoffPacks = clientOpportunity.HandoffPacks;
        await clientOpportunityEventBroker.RaiseClientOpportunityAddEventAsync(CreateEventMessage(result));
        return result;
    }

    public async ValueTask<ClientOpportunity> UpdateAsync(ClientOpportunity clientOpportunity)
    {
        ValidateClientOpportunity(clientOpportunity, nameof(clientOpportunity));

        ClientOpportunity storageClientOpportunity = new()
        {
            Id = clientOpportunity.Id,
            ClientId = clientOpportunity.ClientId,
            PrimaryContactId = clientOpportunity.PrimaryContactId,
            Type = clientOpportunity.Type,
            Stage = clientOpportunity.Stage,
            EstimatedAnnualValue = clientOpportunity.EstimatedAnnualValue,
            Probability = clientOpportunity.Probability,
            PainSummary = clientOpportunity.PainSummary,
            ValueHypothesis = clientOpportunity.ValueHypothesis,
            DecisionProcess = clientOpportunity.DecisionProcess,
            NextAction = clientOpportunity.NextAction,
            NextActionDueOn = clientOpportunity.NextActionDueOn,
            CreatedBy = clientOpportunity.CreatedBy,
            LastUpdatedBy = authInfo.SSOUserId,
            CreatedOn = clientOpportunity.CreatedOn,
            LastUpdated = DateTimeOffset.UtcNow,
        };

        ClientOpportunity result = await clientOpportunityBroker.UpdateClientOpportunityAsync(storageClientOpportunity);
        result.Client = clientOpportunity.Client;
        result.PrimaryContact = clientOpportunity.PrimaryContact;
        result.Activities = clientOpportunity.Activities;
        result.HandoffPacks = clientOpportunity.HandoffPacks;
        await clientOpportunityEventBroker.RaiseClientOpportunityUpdateEventAsync(CreateEventMessage(result));
        return result;
    }

    public async ValueTask DeleteAsync(Guid id)
    {
        ClientOpportunity clientOpportunity = Get(id, ignoreFilters: true);

        if (clientOpportunity is null)
            return;

        await clientOpportunityEventBroker.RaiseClientOpportunityDeleteEventAsync(CreateEventMessage(clientOpportunity));
        await clientOpportunityBroker.DeleteClientOpportunityAsync(clientOpportunity);
    }

    EventMessage<ClientOpportunity> CreateEventMessage(ClientOpportunity clientOpportunity) =>
        new()
        {
            AuthInfo = new EventAuthInfo
            {
                SSOUserId = authInfo.SSOUserId,
            },
            Data = clientOpportunity,
        };

    static void ValidateClientOpportunity(ClientOpportunity clientOpportunity, string parameterName) =>
        ArgumentNullException.ThrowIfNull(clientOpportunity, parameterName);
}
