using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.Eventing.Models;

namespace cCoder.ClientRelationshipManagement.Brokers.Events;

public interface IClientEventBroker
{
    ValueTask RaiseClientAddEventAsync(EventMessage<Client> message);

    ValueTask RaiseClientUpdateEventAsync(EventMessage<Client> message);

    ValueTask RaiseClientDeleteEventAsync(EventMessage<Client> message);
}
