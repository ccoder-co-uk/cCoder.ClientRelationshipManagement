namespace cCoder.ClientRelationshipManagement.Platform.Models.Configuration;

public class CRMConfiguration
{
    public const string SectionName = "CRM";

    public string ConnectionString { get; set; }
    public string AdminConnectionString { get; set; }
}
