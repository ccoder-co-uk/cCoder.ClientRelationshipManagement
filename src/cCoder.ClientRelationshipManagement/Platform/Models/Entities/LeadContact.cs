namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

public class LeadContact : ICrmEntity
{
    public Guid Id { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public Guid LeadId { get; set; }
    public bool IsPrimary { get; set; }
    public string Name { get; set; }
    public string Position { get; set; }
    public string EmailAddress { get; set; }
    public string PhoneNumber { get; set; }
    public string LinkedInUrl { get; set; }
    public string Notes { get; set; }

    public virtual Lead Lead { get; set; }
}
