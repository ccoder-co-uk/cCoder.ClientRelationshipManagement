namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

public class Address : ICrmEntity
{
    public Guid Id { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public string LegacyId { get; set; }
    public string SourceSystem { get; set; }
    public bool IsVerified { get; set; }
    public string PoBox { get; set; }
    public string Line1 { get; set; }
    public string Line2 { get; set; }
    public string TownOrCity { get; set; }
    public string StateOrProvince { get; set; }
    public string ZipOrPostalCode { get; set; }
    public string CountryId { get; set; }
    public string VerificationNotes { get; set; }

    public virtual ICollection<Company> RegisteredCompanies { get; set; } = new List<Company>();
}
