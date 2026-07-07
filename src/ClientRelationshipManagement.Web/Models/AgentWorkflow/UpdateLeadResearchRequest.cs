namespace ClientRelationshipManagement.Web.Models.AgentWorkflow;

public sealed class UpdateLeadResearchRequest
{
    public string RawCompanyName { get; set; }
    public string RawTradingName { get; set; }
    public string RawCompanyNumber { get; set; }
    public string RawVatNumber { get; set; }
    public string RawWebsiteUrl { get; set; }
    public string RawContactEmailAddress { get; set; }
    public string RawContactPhoneNumber { get; set; }
    public string RawAddressText { get; set; }
    public string QualificationNotes { get; set; }
    public string ContactName { get; set; }
    public string ContactPosition { get; set; }
    public string ContactEmailAddress { get; set; }
    public string ContactPhoneNumber { get; set; }
    public string ContactLinkedInUrl { get; set; }
}
