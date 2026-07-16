using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace ClientRelationshipManagement.Web.Services.Agents;

public interface IAiProviderSelectionService
{
    IReadOnlyList<AiProviderProfile> GetProfiles();
    ValueTask<AiProviderSelection> GetAsync(string userId, CancellationToken cancellationToken = default);
    ValueTask<AiProviderSelection> SetAsync(string userId, string profileKey, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<AiWorkLaneSelection>> GetWorkLanesAsync(
        string userId,
        CancellationToken cancellationToken = default);
    ValueTask<AiWorkLaneSelection> SetWorkLaneAsync(
        string userId,
        AgentWorkLane lane,
        string profileKey,
        int concurrency,
        CancellationToken cancellationToken = default);
    ValueTask<AiRoutingSelection> SetRoutingAsync(
        string userId,
        string sharedProfileKey,
        string sharedModel,
        int sharedConcurrency,
        IReadOnlyList<AiWorkLaneUpdate> workLanes,
        CancellationToken cancellationToken = default);
}

public sealed record AiProviderProfile(
    string Key,
    string DisplayName,
    string Description,
    string ProviderKey,
    string Model,
    string CompletionEndpoint,
    int MaxConcurrency,
    bool IsConfigured);

public sealed record AiProviderSelection(
    AiProviderProfile Profile,
    string Model,
    bool IsExplicitlySelected);

public sealed record AiWorkLaneSelection(
    AgentWorkLane Lane,
    AiProviderProfile Profile,
    string Model,
    bool IsEnabled,
    int Concurrency);

public sealed record AiWorkLaneUpdate(
    AgentWorkLane Lane,
    string ProfileKey,
    string Model,
    int Concurrency);

public sealed record AiRoutingSelection(
    AiProviderSelection SharedServices,
    IReadOnlyList<AiWorkLaneSelection> WorkLanes);
