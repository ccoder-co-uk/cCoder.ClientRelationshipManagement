using cCoder.ClientRelationshipManagement.Models.Enums;

namespace cCoder.ClientRelationshipManagement.Models.Entities;

public class ClientMaterial
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public Guid? SentToContactId { get; set; }
    public string Name { get; set; }
    public string FilePath { get; set; }
    public ClientMaterialType Type { get; set; }
    public ClientMaterialStatus Status { get; set; }
    public DateTimeOffset? SentOn { get; set; }
    public string Notes { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public virtual Client Client { get; set; }
    public virtual ClientContact SentToContact { get; set; }
    public virtual Email Email { get; set; }
    public virtual ICollection<ClientActivity> Activities { get; set; } = new List<ClientActivity>();
}
