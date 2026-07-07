using System.ComponentModel.DataAnnotations;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ClientRelationshipManagement.Web.Models.Leads;

public sealed class LeadEditorViewModel
{
    public Guid? Id { get; set; }
    public string Notice { get; set; } = string.Empty;
    public string FormTitle { get; set; } = string.Empty;
    public string SubmitLabel { get; set; } = string.Empty;

    [Required]
    public string TenantId { get; set; } = "default";

    public string SourceSystem { get; set; } = "Manual";
    public string SourceRecordId { get; set; } = string.Empty;
    public string SourceFileName { get; set; } = string.Empty;

    [Required]
    public string RawCompanyName { get; set; } = string.Empty;

    public string RawTradingName { get; set; } = string.Empty;
    public string RawCompanyNumber { get; set; } = string.Empty;
    public string RawVatNumber { get; set; } = string.Empty;
    public string RawWebsiteUrl { get; set; } = string.Empty;
    public string RawContactEmailAddress { get; set; } = string.Empty;
    public string RawContactPhoneNumber { get; set; } = string.Empty;
    public string RawAddressText { get; set; } = string.Empty;
    public string QualificationNotes { get; set; } = string.Empty;

    public string ContactName { get; set; } = string.Empty;
    public string ContactPosition { get; set; } = string.Empty;
    public string ContactEmailAddress { get; set; } = string.Empty;
    public string ContactPhoneNumber { get; set; } = string.Empty;
    public string ContactLinkedInUrl { get; set; } = string.Empty;

    public LeadStatus Status { get; set; } = LeadStatus.Imported;
    public string LinkedCompanyId { get; init; } = string.Empty;
    public string LinkedRelationshipId { get; init; } = string.Empty;
    public string LinkedOpportunityId { get; init; } = string.Empty;

    public IReadOnlyList<SelectListItem> StatusOptions { get; init; } = Array.Empty<SelectListItem>();
}
