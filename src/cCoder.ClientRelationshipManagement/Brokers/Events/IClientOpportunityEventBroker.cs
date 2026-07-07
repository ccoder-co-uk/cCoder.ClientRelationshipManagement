using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.Eventing.Models;

namespace cCoder.ClientRelationshipManagement.Brokers.Events;

public interface IClientOpportunityEventBroker
{
    ValueTask RaiseClientOpportunityAddEventAsync(EventMessage<ClientOpportunity> message);

    ValueTask RaiseClientOpportunityUpdateEventAsync(EventMessage<ClientOpportunity> message);

    ValueTask RaiseClientOpportunityDeleteEventAsync(EventMessage<ClientOpportunity> message);
}
