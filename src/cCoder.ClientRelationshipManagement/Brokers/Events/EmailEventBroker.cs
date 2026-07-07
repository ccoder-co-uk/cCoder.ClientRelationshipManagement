using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;

namespace cCoder.ClientRelationshipManagement.Brokers.Events;

public class EmailEventBroker(IEventHub eventHub) : IEmailEventBroker
{
    public ValueTask RaiseEmailAddEventAsync(EventMessage<Email> message) =>
        eventHub.RaiseEventAsync("email_add", message);

    public ValueTask RaiseEmailUpdateEventAsync(EventMessage<Email> message) =>
        eventHub.RaiseEventAsync("email_update", message);

    public ValueTask RaiseEmailDeleteEventAsync(EventMessage<Email> message) =>
        eventHub.RaiseEventAsync("email_delete", message);
}
