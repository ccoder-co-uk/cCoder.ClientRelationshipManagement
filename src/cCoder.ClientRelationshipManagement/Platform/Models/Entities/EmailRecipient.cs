using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

public class EmailRecipient : ICrmEntity
{
    public Guid Id { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public Guid EmailId { get; set; }
    public Guid? CompanyContactId { get; set; }
    public string Address { get; set; }
    public EmailRecipientType RecipientType { get; set; }

    public virtual Email Email { get; set; }
    public virtual CompanyContact CompanyContact { get; set; }
}
