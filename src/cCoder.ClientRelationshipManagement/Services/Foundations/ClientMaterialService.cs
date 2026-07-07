using cCoder.ClientRelationshipManagement.Brokers.Events;
using cCoder.ClientRelationshipManagement.Brokers.Storages;
using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.Eventing.Models;

namespace cCoder.ClientRelationshipManagement.Services.Foundations;

internal partial class ClientMaterialService(
    IClientMaterialBroker clientMaterialBroker,
    IClientMaterialEventBroker clientMaterialEventBroker,
    ICRMAuthInfo authInfo)
    : IClientMaterialService
{
    public ClientMaterial Get(Guid id, bool ignoreFilters = false) =>
        clientMaterialBroker.GetAllClientMaterials(ignoreFilters)
            .FirstOrDefault(material => material.Id == id);

    public IQueryable<ClientMaterial> GetAll(bool ignoreFilters = false) =>
        clientMaterialBroker.GetAllClientMaterials(ignoreFilters);

    public async ValueTask<ClientMaterial> AddAsync(ClientMaterial clientMaterial)
    {
        ValidateClientMaterial(clientMaterial, nameof(clientMaterial));

        if (clientMaterial.Id == Guid.Empty)
            clientMaterial.Id = Guid.NewGuid();

        if (Get(clientMaterial.Id, ignoreFilters: true) is not null)
            throw new InvalidOperationException($"Client material '{clientMaterial.Id}' already exists.");

        DateTimeOffset now = DateTimeOffset.UtcNow;
        ClientMaterial storageClientMaterial = new()
        {
            Id = clientMaterial.Id,
            ClientId = clientMaterial.ClientId,
            SentToContactId = clientMaterial.SentToContactId,
            Name = clientMaterial.Name,
            FilePath = clientMaterial.FilePath,
            Type = clientMaterial.Type,
            Status = clientMaterial.Status,
            SentOn = clientMaterial.SentOn,
            Notes = clientMaterial.Notes,
            CreatedBy = clientMaterial.CreatedBy ?? authInfo.SSOUserId,
            LastUpdatedBy = authInfo.SSOUserId,
            CreatedOn = clientMaterial.CreatedOn == default ? now : clientMaterial.CreatedOn,
            LastUpdated = now,
        };

        ClientMaterial result = await clientMaterialBroker.AddClientMaterialAsync(storageClientMaterial);
        result.Client = clientMaterial.Client;
        result.SentToContact = clientMaterial.SentToContact;
        result.Activities = clientMaterial.Activities;
        await clientMaterialEventBroker.RaiseClientMaterialAddEventAsync(CreateEventMessage(result));
        return result;
    }

    public async ValueTask<ClientMaterial> UpdateAsync(ClientMaterial clientMaterial)
    {
        ValidateClientMaterial(clientMaterial, nameof(clientMaterial));

        ClientMaterial storageClientMaterial = new()
        {
            Id = clientMaterial.Id,
            ClientId = clientMaterial.ClientId,
            SentToContactId = clientMaterial.SentToContactId,
            Name = clientMaterial.Name,
            FilePath = clientMaterial.FilePath,
            Type = clientMaterial.Type,
            Status = clientMaterial.Status,
            SentOn = clientMaterial.SentOn,
            Notes = clientMaterial.Notes,
            CreatedBy = clientMaterial.CreatedBy,
            LastUpdatedBy = authInfo.SSOUserId,
            CreatedOn = clientMaterial.CreatedOn,
            LastUpdated = DateTimeOffset.UtcNow,
        };

        ClientMaterial result = await clientMaterialBroker.UpdateClientMaterialAsync(storageClientMaterial);
        result.Client = clientMaterial.Client;
        result.SentToContact = clientMaterial.SentToContact;
        result.Activities = clientMaterial.Activities;
        await clientMaterialEventBroker.RaiseClientMaterialUpdateEventAsync(CreateEventMessage(result));
        return result;
    }

    public async ValueTask DeleteAsync(Guid id)
    {
        ClientMaterial clientMaterial = Get(id, ignoreFilters: true);

        if (clientMaterial is null)
            return;

        await clientMaterialEventBroker.RaiseClientMaterialDeleteEventAsync(CreateEventMessage(clientMaterial));
        await clientMaterialBroker.DeleteClientMaterialAsync(clientMaterial);
    }

    EventMessage<ClientMaterial> CreateEventMessage(ClientMaterial clientMaterial) =>
        new()
        {
            AuthInfo = new EventAuthInfo
            {
                SSOUserId = authInfo.SSOUserId,
            },
            Data = clientMaterial,
        };

    static void ValidateClientMaterial(ClientMaterial clientMaterial, string parameterName) =>
        ArgumentNullException.ThrowIfNull(clientMaterial, parameterName);
}
