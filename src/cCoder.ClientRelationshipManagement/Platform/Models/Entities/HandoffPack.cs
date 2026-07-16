namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

public class HandoffPack : ICrmEntity
{
    public Guid Id { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public string LegacyId { get; set; }
    public Guid ClientAccountId { get; set; }
    public string AgreedScope { get; set; }
    public string CommercialTermsSummary { get; set; }
    public string PromisedOutcomes { get; set; }
    public string PrimaryCommercialContact { get; set; }
    public string PrimaryOperationalContact { get; set; }
    public string PrimaryTechnicalContact { get; set; }
    public string KnownRisks { get; set; }
    public string OnboardingOwner { get; set; }
    public string LegalEntity { get; set; }
    public string SignedContractPath { get; set; }
    public string Status { get; set; }

    public virtual ClientAccount ClientAccount { get; set; }
}
