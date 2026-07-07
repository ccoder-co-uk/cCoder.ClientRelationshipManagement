using cCoder.ClientRelationshipManagement.Models.Enums;

namespace cCoder.ClientRelationshipManagement.Models.Entities;

public class ClientHandoffPack
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public Guid ClientOpportunityId { get; set; }
    public string SignedContractPath { get; set; }
    public string LegalEntity { get; set; }
    public string PrimaryCommercialContact { get; set; }
    public string PrimaryOperationalContact { get; set; }
    public string PrimaryTechnicalContact { get; set; }
    public string AgreedScope { get; set; }
    public string CommercialTermsSummary { get; set; }
    public string PromisedOutcomes { get; set; }
    public string KnownRisks { get; set; }
    public string OnboardingOwner { get; set; }
    public ClientHandoffStatus Status { get; set; }
    public DateTimeOffset? HandedOffOn { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public virtual Client Client { get; set; }
    public virtual ClientOpportunity ClientOpportunity { get; set; }
}
