using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;

namespace cCoder.ClientRelationshipManagement.Brokers.Events;

public class AddressEventBroker(IEventHub eventHub) : IAddressEventBroker
{
    public ValueTask RaiseAddressAddEventAsync(EventMessage<Address> message) =>
        eventHub.RaiseEventAsync("address_add", message);

    public ValueTask RaiseAddressUpdateEventAsync(EventMessage<Address> message) =>
        eventHub.RaiseEventAsync("address_update", message);

    public ValueTask RaiseAddressDeleteEventAsync(EventMessage<Address> message) =>
        eventHub.RaiseEventAsync("address_delete", message);
}
