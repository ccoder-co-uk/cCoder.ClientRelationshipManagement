using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;

namespace cCoder.ClientRelationshipManagement.Brokers.Events;

public class CompanyEventBroker(IEventHub eventHub) : ICompanyEventBroker
{
    public ValueTask RaiseCompanyAddEventAsync(EventMessage<Company> message) =>
        eventHub.RaiseEventAsync("company_add", message);

    public ValueTask RaiseCompanyUpdateEventAsync(EventMessage<Company> message) =>
        eventHub.RaiseEventAsync("company_update", message);

    public ValueTask RaiseCompanyDeleteEventAsync(EventMessage<Company> message) =>
        eventHub.RaiseEventAsync("company_delete", message);
}
