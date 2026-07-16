namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

public class CompanyContact : ICrmEntity
{
    public Guid Id { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public string LegacyId { get; set; }
    public Guid CompanyId { get; set; }
    public string SourceSystem { get; set; }
    public bool IsVerified { get; set; }
    public bool IsPrimary { get; set; }
    public string Name { get; set; }
    public string Position { get; set; }
    public string EmailAddress { get; set; }
    public string PhoneNumber { get; set; }
    public string LinkedInUrl { get; set; }
    public string Notes { get; set; }

    public virtual Company Company { get; set; }
    public virtual ICollection<RelationshipContact> RelationshipContacts { get; set; } = new List<RelationshipContact>();
    public virtual ICollection<Activity> Activities { get; set; } = new List<Activity>();
    public virtual ICollection<Material> Materials { get; set; } = new List<Material>();
    public virtual ICollection<Email> Emails { get; set; } = new List<Email>();
    public virtual ICollection<EmailRecipient> EmailRecipients { get; set; } = new List<EmailRecipient>();
}
