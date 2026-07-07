using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;

namespace cCoder.ClientRelationshipManagement.Brokers.Events;

public class ClientHandoffPackEventBroker(IEventHub eventHub) : IClientHandoffPackEventBroker
{
    public ValueTask RaiseClientHandoffPackAddEventAsync(EventMessage<ClientHandoffPack> message) =>
        eventHub.RaiseEventAsync("clienthandoffpack_add", message);

    public ValueTask RaiseClientHandoffPackUpdateEventAsync(EventMessage<ClientHandoffPack> message) =>
        eventHub.RaiseEventAsync("clienthandoffpack_update", message);

    public ValueTask RaiseClientHandoffPackDeleteEventAsync(EventMessage<ClientHandoffPack> message) =>
        eventHub.RaiseEventAsync("clienthandoffpack_delete", message);
}
