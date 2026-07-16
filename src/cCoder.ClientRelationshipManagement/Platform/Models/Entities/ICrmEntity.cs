namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

public interface ICrmEntity
{
    Guid Id { get; set; }
    string CreatedBy { get; set; }
    string LastUpdatedBy { get; set; }
    DateTimeOffset CreatedOn { get; set; }
    DateTimeOffset LastUpdated { get; set; }
}
