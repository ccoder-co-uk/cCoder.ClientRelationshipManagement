using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.Eventing.Models;

namespace cCoder.ClientRelationshipManagement.Brokers.Events;

public interface IClientActivityEventBroker
{
    ValueTask RaiseClientActivityAddEventAsync(EventMessage<ClientActivity> message);

    ValueTask RaiseClientActivityUpdateEventAsync(EventMessage<ClientActivity> message);

    ValueTask RaiseClientActivityDeleteEventAsync(EventMessage<ClientActivity> message);
}
