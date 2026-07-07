namespace cCoder.ClientRelationshipManagement.Models.Entities;

public class Address
{
    public Guid Id { get; set; }
    public string PoBox { get; set; }
    public string Line1 { get; set; }
    public string Line2 { get; set; }
    public string ZipOrPostalCode { get; set; }
    public string TownOrCity { get; set; }
    public string StateOrProvince { get; set; }
    public string CountryId { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public virtual ICollection<Company> Companies { get; set; } = new List<Company>();
}
