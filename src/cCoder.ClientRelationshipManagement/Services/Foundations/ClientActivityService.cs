using cCoder.ClientRelationshipManagement.Brokers.Events;
using cCoder.ClientRelationshipManagement.Brokers.Storages;
using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.Eventing.Models;

namespace cCoder.ClientRelationshipManagement.Services.Foundations;

internal partial class ClientActivityService(
    IClientActivityBroker clientActivityBroker,
    IClientActivityEventBroker clientActivityEventBroker,
    ICRMAuthInfo authInfo)
    : IClientActivityService
{
    public ClientActivity Get(Guid id, bool ignoreFilters = false) =>
        clientActivityBroker.GetAllClientActivities(ignoreFilters)
            .FirstOrDefault(activity => activity.Id == id);

    public IQueryable<ClientActivity> GetAll(bool ignoreFilters = false) =>
        clientActivityBroker.GetAllClientActivities(ignoreFilters);

    public async ValueTask<ClientActivity> AddAsync(ClientActivity clientActivity)
    {
        ValidateClientActivity(clientActivity, nameof(clientActivity));

        if (clientActivity.Id == Guid.Empty)
            clientActivity.Id = Guid.NewGuid();

        if (Get(clientActivity.Id, ignoreFilters: true) is not null)
            throw new InvalidOperationException($"Client activity '{clientActivity.Id}' already exists.");

        ClientActivity storageClientActivity = new()
        {
            Id = clientActivity.Id,
            ClientId = clientActivity.ClientId,
            ClientContactId = clientActivity.ClientContactId,
            ClientOpportunityId = clientActivity.ClientOpportunityId,
            ClientMaterialId = clientActivity.ClientMaterialId,
            ActivityOn = clientActivity.ActivityOn,
            Type = clientActivity.Type,
            Direction = clientActivity.Direction,
            Summary = clientActivity.Summary,
            Outcome = clientActivity.Outcome,
            NextAction = clientActivity.NextAction,
            NextActionDueOn = clientActivity.NextActionDueOn,
            CreatedBy = clientActivity.CreatedBy ?? authInfo.SSOUserId,
            CreatedOn = clientActivity.CreatedOn == default ? DateTimeOffset.UtcNow : clientActivity.CreatedOn,
        };

        ClientActivity result = await clientActivityBroker.AddClientActivityAsync(storageClientActivity);
        result.Client = clientActivity.Client;
        result.ClientContact = clientActivity.ClientContact;
        result.ClientOpportunity = clientActivity.ClientOpportunity;
        result.ClientMaterial = clientActivity.ClientMaterial;
        await clientActivityEventBroker.RaiseClientActivityAddEventAsync(CreateEventMessage(result));
        return result;
    }

    public async ValueTask<ClientActivity> UpdateAsync(ClientActivity clientActivity)
    {
        ValidateClientActivity(clientActivity, nameof(clientActivity));

        ClientActivity storageClientActivity = new()
        {
            Id = clientActivity.Id,
            ClientId = clientActivity.ClientId,
            ClientContactId = clientActivity.ClientContactId,
            ClientOpportunityId = clientActivity.ClientOpportunityId,
            ClientMaterialId = clientActivity.ClientMaterialId,
            ActivityOn = clientActivity.ActivityOn,
            Type = clientActivity.Type,
            Direction = clientActivity.Direction,
            Summary = clientActivity.Summary,
            Outcome = clientActivity.Outcome,
            NextAction = clientActivity.NextAction,
            NextActionDueOn = clientActivity.NextActionDueOn,
            CreatedBy = clientActivity.CreatedBy,
            CreatedOn = clientActivity.CreatedOn,
        };

        ClientActivity result = await clientActivityBroker.UpdateClientActivityAsync(storageClientActivity);
        result.Client = clientActivity.Client;
        result.ClientContact = clientActivity.ClientContact;
        result.ClientOpportunity = clientActivity.ClientOpportunity;
        result.ClientMaterial = clientActivity.ClientMaterial;
        await clientActivityEventBroker.RaiseClientActivityUpdateEventAsync(CreateEventMessage(result));
        return result;
    }

    public async ValueTask DeleteAsync(Guid id)
    {
        ClientActivity clientActivity = Get(id, ignoreFilters: true);

        if (clientActivity is null)
            return;

        await clientActivityEventBroker.RaiseClientActivityDeleteEventAsync(CreateEventMessage(clientActivity));
        await clientActivityBroker.DeleteClientActivityAsync(clientActivity);
    }

    EventMessage<ClientActivity> CreateEventMessage(ClientActivity clientActivity) =>
        new()
        {
            AuthInfo = new EventAuthInfo
            {
                SSOUserId = authInfo.SSOUserId,
            },
            Data = clientActivity,
        };

    static void ValidateClientActivity(ClientActivity clientActivity, string parameterName) =>
        ArgumentNullException.ThrowIfNull(clientActivity, parameterName);
}
