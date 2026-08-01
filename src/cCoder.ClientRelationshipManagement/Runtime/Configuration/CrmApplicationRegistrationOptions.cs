namespace cCoder.ClientRelationshipManagement.Runtime.Configuration;

public sealed class CrmApplicationRegistrationOptions
{
    public bool IncludeAgentHostedServices { get; set; } = true;
    public bool IncludeAI { get; set; } = true;
    public bool IncludeApiDocumentation { get; set; } = true;
    public bool IncludeImportHostedServices { get; set; } = true;
    public bool IncludeLeadHostedServices { get; set; } = true;
    public bool IncludeMailHostedServices { get; set; } = true;
    public bool IncludeMvc { get; set; } = true;
    public bool IncludeSecurity { get; set; } = true;

    public bool IncludeHostedServices
    {
        get => IncludeAgentHostedServices
            && IncludeImportHostedServices
            && IncludeLeadHostedServices
            && IncludeMailHostedServices;
        set
        {
            IncludeAgentHostedServices = value;
            IncludeImportHostedServices = value;
            IncludeLeadHostedServices = value;
            IncludeMailHostedServices = value;
        }
    }
}
