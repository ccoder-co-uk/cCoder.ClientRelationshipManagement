using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

public class Source : AuditableEntity
{
    public string Name { get; set; }
    public SourceType SourceType { get; set; }
    public string CountryCode { get; set; }
    public bool IsAuthoritative { get; set; }
    public string Notes { get; set; }

    public virtual ICollection<Import> Imports { get; set; } = new List<Import>();
    public virtual ICollection<ImportLink> ImportLinks { get; set; } = new List<ImportLink>();
    public virtual ICollection<Lead> Leads { get; set; } = new List<Lead>();
}
