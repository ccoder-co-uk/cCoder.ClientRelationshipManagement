using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;

namespace cCoder.ClientRelationshipManagement.Brokers.Events;

public class ClientMaterialEventBroker(IEventHub eventHub) : IClientMaterialEventBroker
{
    public ValueTask RaiseClientMaterialAddEventAsync(EventMessage<ClientMaterial> message) =>
        eventHub.RaiseEventAsync("clientmaterial_add", message);

    public ValueTask RaiseClientMaterialUpdateEventAsync(EventMessage<ClientMaterial> message) =>
        eventHub.RaiseEventAsync("clientmaterial_update", message);

    public ValueTask RaiseClientMaterialDeleteEventAsync(EventMessage<ClientMaterial> message) =>
        eventHub.RaiseEventAsync("clientmaterial_delete", message);
}
