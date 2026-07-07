using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.Eventing.Models;

namespace cCoder.ClientRelationshipManagement.Brokers.Events;

public interface IClientMaterialEventBroker
{
    ValueTask RaiseClientMaterialAddEventAsync(EventMessage<ClientMaterial> message);

    ValueTask RaiseClientMaterialUpdateEventAsync(EventMessage<ClientMaterial> message);

    ValueTask RaiseClientMaterialDeleteEventAsync(EventMessage<ClientMaterial> message);
}
