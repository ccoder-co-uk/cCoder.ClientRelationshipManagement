using cCoder.ClientRelationshipManagement.Models.Enums;

namespace cCoder.ClientRelationshipManagement.Models.Entities;

public class ClientContact
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public string Name { get; set; }
    public string Position { get; set; }
    public string EmailAddress { get; set; }
    public string PhoneNumber { get; set; }
    public string LinkedInUrl { get; set; }
    public string Source { get; set; }
    public string RelationshipRoute { get; set; }
    public ClientContactStatus Status { get; set; }
    public bool IsPrimary { get; set; }
    public string Notes { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public virtual Client Client { get; set; }
    public virtual ICollection<ClientActivity> Activities { get; set; } = new List<ClientActivity>();
    public virtual ICollection<Email> Emails { get; set; } = new List<Email>();
}
