using System.ComponentModel.DataAnnotations;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ClientRelationshipManagement.Web.Models.Clients;

public sealed class ClientEditorViewModel
{
    public Guid? Id { get; set; }

    public bool IsNew { get; set; }

    public string FormTitle { get; set; } = string.Empty;

    public string SubmitLabel { get; set; } = string.Empty;

    public string Notice { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Company name")]
    public string CompanyName { get; set; } = string.Empty;

    [Display(Name = "Trading name")]
    public string TradingName { get; set; } = string.Empty;

    [Display(Name = "Contact email")]
    public string ContactEmailAddress { get; set; } = string.Empty;

    [Display(Name = "Contact phone")]
    public string ContactPhoneNumber { get; set; } = string.Empty;

    [Display(Name = "Website")]
    public string WebsiteUrl { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Account owner")]
    public string AccountOwner { get; set; } = string.Empty;

    [Display(Name = "Lead source")]
    public string LeadSource { get; set; } = string.Empty;

    [Display(Name = "Initial route")]
    public string InitialRoute { get; set; } = string.Empty;

    [Display(Name = "Opportunity summary")]
    public string OpportunitySummary { get; set; } = string.Empty;

    [Display(Name = "Research summary")]
    public string ResearchSummary { get; set; } = string.Empty;

    [Display(Name = "Data quality notes")]
    public string DataQualityNotes { get; set; } = string.Empty;

    [Display(Name = "Preferred opening angle")]
    public string PreferredOpeningAngle { get; set; } = string.Empty;

    [Display(Name = "Fit score")]
    public decimal? FitScore { get; set; }

    [Display(Name = "Archived")]
    public bool IsArchived { get; set; }

    [Display(Name = "Verified company")]
    public bool IsVerified { get; set; }

    [Display(Name = "Status")]
    public RelationshipStatus Status { get; set; }

    [Display(Name = "Stage")]
    public SalesPipelineStage CurrentStage { get; set; }

    [Display(Name = "Priority")]
    public RelationshipPriority Priority { get; set; }

    public IReadOnlyList<SelectListItem> StatusOptions { get; init; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<SelectListItem> StageOptions { get; init; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<SelectListItem> PriorityOptions { get; init; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<SelectListItem> ActivityTypeOptions { get; init; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<SelectListItem> ActivityDirectionOptions { get; init; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<SelectListItem> OpportunityTypeOptions { get; init; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<ClientActivityTimelineItemViewModel> RecentActivities { get; init; } =
        Array.Empty<ClientActivityTimelineItemViewModel>();
    public IReadOnlyList<ClientCommunicationItemViewModel> Communications { get; init; } =
        Array.Empty<ClientCommunicationItemViewModel>();
    public IReadOnlyList<ClientOpportunitySummaryViewModel> Opportunities { get; init; } =
        Array.Empty<ClientOpportunitySummaryViewModel>();
    public IReadOnlyList<ClientScheduledActionItemViewModel> ScheduledActions { get; init; } =
        Array.Empty<ClientScheduledActionItemViewModel>();
}
