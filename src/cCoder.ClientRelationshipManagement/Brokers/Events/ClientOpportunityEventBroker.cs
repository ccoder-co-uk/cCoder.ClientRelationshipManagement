using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;

namespace cCoder.ClientRelationshipManagement.Brokers.Events;

public class ClientOpportunityEventBroker(IEventHub eventHub) : IClientOpportunityEventBroker
{
    public ValueTask RaiseClientOpportunityAddEventAsync(EventMessage<ClientOpportunity> message) =>
        eventHub.RaiseEventAsync("clientopportunity_add", message);

    public ValueTask RaiseClientOpportunityUpdateEventAsync(EventMessage<ClientOpportunity> message) =>
        eventHub.RaiseEventAsync("clientopportunity_update", message);

    public ValueTask RaiseClientOpportunityDeleteEventAsync(EventMessage<ClientOpportunity> message) =>
        eventHub.RaiseEventAsync("clientopportunity_delete", message);
}
