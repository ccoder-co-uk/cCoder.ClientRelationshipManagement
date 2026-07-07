using cCoder.ClientRelationshipManagement.Models.Entities;

namespace cCoder.ClientRelationshipManagement.Brokers.Storages;

public interface IClientOpportunityBroker
{
    IQueryable<ClientOpportunity> GetAllClientOpportunities(bool ignoreFilters);

    ValueTask<ClientOpportunity> AddClientOpportunityAsync(ClientOpportunity entity);

    ValueTask<ClientOpportunity> UpdateClientOpportunityAsync(ClientOpportunity entity);

    ValueTask<int> DeleteClientOpportunityAsync(ClientOpportunity entity);
}
