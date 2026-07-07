namespace ClientRelationshipManagement.Web.Configuration;

public sealed class CrmApplicationRegistrationOptions
{
    public bool IncludeMvc { get; set; } = true;
    public bool IncludeHostedServices { get; set; } = true;
}
