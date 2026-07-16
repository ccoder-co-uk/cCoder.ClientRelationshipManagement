using Microsoft.AspNetCore.Mvc.Rendering;

namespace ClientRelationshipManagement.Web.Models.Home;

public class HomeDashboardViewModel
{
    public string Notice { get; init; }
    public bool AutoApproveProcessEmails { get; init; }
    public int TotalClients { get; init; }
    public int ActiveLeads { get; init; }
    public int ActiveOpportunities { get; init; }
    public int CandidateCompanies { get; init; }
    public int SuppressedCompanies { get; init; }
    public int TotalOpenActions { get; init; }
    public int OverdueActions { get; init; }
    public int DueTodayActions { get; init; }
    public LaneActionStatsViewModel LeadActions { get; init; } = new();
    public LaneActionStatsViewModel OpportunityActions { get; init; } = new();
    public LaneActionStatsViewModel ClientActions { get; init; } = new();
    public LaneAgentHealthViewModel LeadAgentHealth { get; init; } = new();
    public LaneAgentHealthViewModel OpportunityAgentHealth { get; init; } = new();
    public LaneAgentHealthViewModel ClientAgentHealth { get; init; } = new();
    public int AdditionalActionCount { get; init; }
    public long QueueVersion { get; init; }
    public IReadOnlyList<SelectListItem> StatusOptions { get; init; } =
        Array.Empty<SelectListItem>();
    public IReadOnlyList<SelectListItem> StageOptions { get; init; } =
        Array.Empty<SelectListItem>();
    public IReadOnlyList<ClientStateSummaryViewModel> ClientStateSummaries { get; init; } =
        Array.Empty<ClientStateSummaryViewModel>();
    public IReadOnlyList<TodoItemViewModel> TodoItems { get; init; } =
        Array.Empty<TodoItemViewModel>();
}

public sealed class LaneAgentHealthViewModel
{
    public string Status { get; init; } = "unknown";
    public int SampleSize { get; init; }
    public int Succeeded { get; init; }
    public int Failed { get; init; }
    public string Label => Status switch
    {
        "healthy" => $"Agent healthy: all {SampleSize} recent tasks succeeded",
        "degraded" => $"Agent needs attention: {Succeeded} of {SampleSize} recent tasks succeeded",
        "failing" => $"Agent failing: all {SampleSize} recent tasks failed",
        _ => "Agent health unavailable: no completed tasks recorded for this lane"
    };
}
