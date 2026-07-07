using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;

namespace cCoder.ClientRelationshipManagement.Brokers.Events;

public class ClientContactEventBroker(IEventHub eventHub) : IClientContactEventBroker
{
    public ValueTask RaiseClientContactAddEventAsync(EventMessage<ClientContact> message) =>
        eventHub.RaiseEventAsync("clientcontact_add", message);

    public ValueTask RaiseClientContactUpdateEventAsync(EventMessage<ClientContact> message) =>
        eventHub.RaiseEventAsync("clientcontact_update", message);

    public ValueTask RaiseClientContactDeleteEventAsync(EventMessage<ClientContact> message) =>
        eventHub.RaiseEventAsync("clientcontact_delete", message);
}
