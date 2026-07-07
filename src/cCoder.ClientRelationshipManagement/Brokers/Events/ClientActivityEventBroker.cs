using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;

namespace cCoder.ClientRelationshipManagement.Brokers.Events;

public class ClientActivityEventBroker(IEventHub eventHub) : IClientActivityEventBroker
{
    public ValueTask RaiseClientActivityAddEventAsync(EventMessage<ClientActivity> message) =>
        eventHub.RaiseEventAsync("clientactivity_add", message);

    public ValueTask RaiseClientActivityUpdateEventAsync(EventMessage<ClientActivity> message) =>
        eventHub.RaiseEventAsync("clientactivity_update", message);

    public ValueTask RaiseClientActivityDeleteEventAsync(EventMessage<ClientActivity> message) =>
        eventHub.RaiseEventAsync("clientactivity_delete", message);
}
