using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;

namespace cCoder.ClientRelationshipManagement.Brokers.Events;

public class ClientEventBroker(IEventHub eventHub) : IClientEventBroker
{
    public ValueTask RaiseClientAddEventAsync(EventMessage<Client> message) =>
        eventHub.RaiseEventAsync("client_add", message);

    public ValueTask RaiseClientUpdateEventAsync(EventMessage<Client> message) =>
        eventHub.RaiseEventAsync("client_update", message);

    public ValueTask RaiseClientDeleteEventAsync(EventMessage<Client> message) =>
        eventHub.RaiseEventAsync("client_delete", message);
}
