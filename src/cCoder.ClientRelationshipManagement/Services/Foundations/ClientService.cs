using cCoder.ClientRelationshipManagement.Brokers.Events;
using cCoder.ClientRelationshipManagement.Brokers.Storages;
using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.Eventing.Models;

namespace cCoder.ClientRelationshipManagement.Services.Foundations;

internal partial class ClientService(
    IClientBroker clientBroker,
    IClientEventBroker clientEventBroker,
    ICRMAuthInfo authInfo)
    : IClientService
{
    public Client Get(Guid id, bool ignoreFilters = false) =>
        clientBroker.GetAllClients(ignoreFilters)
            .FirstOrDefault(client => client.Id == id);

    public IQueryable<Client> GetAll(bool ignoreFilters = false) =>
        clientBroker.GetAllClients(ignoreFilters);

    public async ValueTask<Client> AddAsync(Client client)
    {
        ValidateClient(client, nameof(client));

        if (client.Id == Guid.Empty)
            client.Id = Guid.NewGuid();

        if (Get(client.Id, ignoreFilters: true) is not null)
            throw new InvalidOperationException($"Client '{client.Id}' already exists.");

        DateTimeOffset now = DateTimeOffset.UtcNow;
        Client storageClient = new()
        {
            Id = client.Id,
            TenantId = client.TenantId,
            AccountOwner = client.AccountOwner,
            Status = client.Status,
            CurrentStage = client.CurrentStage,
            Priority = client.Priority,
            LeadSource = client.LeadSource,
            InitialRoute = client.InitialRoute,
            FitScore = client.FitScore,
            OpportunitySummary = client.OpportunitySummary,
            PreferredOpeningAngle = client.PreferredOpeningAngle,
            NextAction = client.NextAction,
            NextActionDueOn = client.NextActionDueOn,
            CreatedBy = client.CreatedBy ?? authInfo.SSOUserId,
            LastUpdatedBy = authInfo.SSOUserId,
            CreatedOn = client.CreatedOn == default ? now : client.CreatedOn,
            LastUpdated = now,
            IsArchived = client.IsArchived,
        };

        Client result = await clientBroker.AddClientAsync(storageClient);
        result.Company = client.Company;
        if (result.Company is not null)
        {
            result.Company.ClientId = result.Id;
            result.Company.Client = new Client { Id = result.Id, TenantId = result.TenantId };
        }

        result.Contacts = client.Contacts;
        result.Opportunities = client.Opportunities;
        result.Activities = client.Activities;
        result.Materials = client.Materials;
        result.HandoffPacks = client.HandoffPacks;
        await clientEventBroker.RaiseClientAddEventAsync(CreateEventMessage(result));
        return result;
    }

    public async ValueTask<Client> UpdateAsync(Client client)
    {
        ValidateClient(client, nameof(client));

        Client storageClient = new()
        {
            Id = client.Id,
            TenantId = client.TenantId,
            AccountOwner = client.AccountOwner,
            Status = client.Status,
            CurrentStage = client.CurrentStage,
            Priority = client.Priority,
            LeadSource = client.LeadSource,
            InitialRoute = client.InitialRoute,
            FitScore = client.FitScore,
            OpportunitySummary = client.OpportunitySummary,
            PreferredOpeningAngle = client.PreferredOpeningAngle,
            NextAction = client.NextAction,
            NextActionDueOn = client.NextActionDueOn,
            CreatedBy = client.CreatedBy,
            LastUpdatedBy = authInfo.SSOUserId,
            CreatedOn = client.CreatedOn,
            LastUpdated = DateTimeOffset.UtcNow,
            IsArchived = client.IsArchived,
        };

        Client result = await clientBroker.UpdateClientAsync(storageClient);
        result.Company = client.Company;
        if (result.Company is not null)
        {
            result.Company.ClientId = result.Id;
            result.Company.Client = new Client { Id = result.Id, TenantId = result.TenantId };
        }

        result.Contacts = client.Contacts;
        result.Opportunities = client.Opportunities;
        result.Activities = client.Activities;
        result.Materials = client.Materials;
        result.HandoffPacks = client.HandoffPacks;
        await clientEventBroker.RaiseClientUpdateEventAsync(CreateEventMessage(result));
        return result;
    }

    public async ValueTask DeleteAsync(Guid id)
    {
        Client client = Get(id, ignoreFilters: true);

        if (client is null)
            return;

        await clientEventBroker.RaiseClientDeleteEventAsync(CreateEventMessage(client));
        await clientBroker.DeleteClientAsync(client);
    }

    EventMessage<Client> CreateEventMessage(Client client) =>
        new()
        {
            AuthInfo = new EventAuthInfo
            {
                SSOUserId = authInfo.SSOUserId,
            },
            Data = client,
        };

    static void ValidateClient(Client client, string parameterName) =>
        ArgumentNullException.ThrowIfNull(client, parameterName);
}
