using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.Eventing.Models;

namespace cCoder.ClientRelationshipManagement.Brokers.Events;

public interface IClientContactEventBroker
{
    ValueTask RaiseClientContactAddEventAsync(EventMessage<ClientContact> message);

    ValueTask RaiseClientContactUpdateEventAsync(EventMessage<ClientContact> message);

    ValueTask RaiseClientContactDeleteEventAsync(EventMessage<ClientContact> message);
}
