using cCoder.ClientRelationshipManagement.Models.Entities;

namespace cCoder.ClientRelationshipManagement.Services.Coordinations;

public interface IDeletionCoordinationService
{
    ValueTask HandleAddressDeleteAsync(Address address);
    ValueTask HandleClientDeleteAsync(Client client);
    ValueTask HandleClientContactDeleteAsync(ClientContact clientContact);
    ValueTask HandleClientMaterialDeleteAsync(ClientMaterial clientMaterial);
    ValueTask HandleEmailDeleteAsync(Email email);
    ValueTask HandleClientOpportunityDeleteAsync(ClientOpportunity clientOpportunity);
}
