using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.Eventing.Models;

namespace cCoder.ClientRelationshipManagement.Brokers.Events;

public interface IAddressEventBroker
{
    ValueTask RaiseAddressAddEventAsync(EventMessage<Address> message);

    ValueTask RaiseAddressUpdateEventAsync(EventMessage<Address> message);

    ValueTask RaiseAddressDeleteEventAsync(EventMessage<Address> message);
}
