namespace cCoder.ClientRelationshipManagement.Models.Entities;

public class Company
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public string Name { get; set; }
    public string LegalEntityName { get; set; }
    public string TradingName { get; set; }
    public string CompanyNumber { get; set; }
    public string VatNumber { get; set; }
    public string ContactEmailAddress { get; set; }
    public string ContactPhoneNumber { get; set; }
    public string WebsiteUrl { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
    public bool IsActive { get; set; }
    public bool IsVerified { get; set; }
    public Guid? RegisteredAddressId { get; set; }

    public virtual Client Client { get; set; }
    public virtual Address RegisteredAddress { get; set; }
}
