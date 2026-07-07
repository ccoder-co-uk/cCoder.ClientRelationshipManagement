using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.Eventing.Models;

namespace cCoder.ClientRelationshipManagement.Brokers.Events;

public interface IEmailEventBroker
{
    ValueTask RaiseEmailAddEventAsync(EventMessage<Email> message);
    ValueTask RaiseEmailUpdateEventAsync(EventMessage<Email> message);
    ValueTask RaiseEmailDeleteEventAsync(EventMessage<Email> message);
}
