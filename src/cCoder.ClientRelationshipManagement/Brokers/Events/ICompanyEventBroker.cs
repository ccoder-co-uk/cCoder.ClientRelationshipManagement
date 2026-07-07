using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.Eventing.Models;

namespace cCoder.ClientRelationshipManagement.Brokers.Events;

public interface ICompanyEventBroker
{
    ValueTask RaiseCompanyAddEventAsync(EventMessage<Company> message);

    ValueTask RaiseCompanyUpdateEventAsync(EventMessage<Company> message);

    ValueTask RaiseCompanyDeleteEventAsync(EventMessage<Company> message);
}
