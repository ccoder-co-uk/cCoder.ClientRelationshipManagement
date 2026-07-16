namespace ClientRelationshipManagement.Web.Models.Home;

public sealed class HomeDashboardStatsViewModel
{
    public int TotalClients { get; init; }
    public int ActiveLeads { get; init; }
    public int ActiveOpportunities { get; init; }
    public int CandidateCompanies { get; init; }
    public int SuppressedCompanies { get; init; }
    public int TotalOpenActions { get; init; }
    public int DueTodayActions { get; init; }
    public int OverdueActions { get; init; }
    public LaneActionStatsViewModel LeadActions { get; init; } = new();
    public LaneActionStatsViewModel OpportunityActions { get; init; } = new();
    public LaneActionStatsViewModel ClientActions { get; init; } = new();
    public LaneAgentHealthViewModel LeadAgentHealth { get; init; } = new();
    public LaneAgentHealthViewModel OpportunityAgentHealth { get; init; } = new();
    public LaneAgentHealthViewModel ClientAgentHealth { get; init; } = new();
    public int AdditionalActionCount { get; init; }
    public long QueueVersion { get; init; }
    public DateTimeOffset UpdatedOn { get; init; }
    public IReadOnlyList<Guid> ActiveTaskIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<HomeDashboardStateStatViewModel> ClientStates { get; init; } =
        Array.Empty<HomeDashboardStateStatViewModel>();
}

public sealed class HomeDashboardStateStatViewModel
{
    public string Key { get; init; } = string.Empty;
    public int Count { get; init; }
}
