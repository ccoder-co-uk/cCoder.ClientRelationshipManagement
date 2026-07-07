using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.Eventing.Models;

namespace cCoder.ClientRelationshipManagement.Brokers.Events;

public interface IClientHandoffPackEventBroker
{
    ValueTask RaiseClientHandoffPackAddEventAsync(EventMessage<ClientHandoffPack> message);

    ValueTask RaiseClientHandoffPackUpdateEventAsync(EventMessage<ClientHandoffPack> message);

    ValueTask RaiseClientHandoffPackDeleteEventAsync(EventMessage<ClientHandoffPack> message);
}
