using cCoder.AI.Models.Configurations;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using cCoder.ClientRelationshipManagement.Services.Foundations.Platform;
using ClientRelationshipManagement.Web.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClientRelationshipManagement.Web.Services.Agents;

public sealed class AiProviderSelectionService : IAiProviderSelectionService
{
    readonly IOperationsCoordinationService operations;
    readonly AiRoutingOptions routingOptions;
    readonly IReadOnlyList<AiProviderProfile> profiles;

    public AiProviderSelectionService(
        IOperationsCoordinationService operations,
        IOptions<AiRoutingOptions> routingOptions,
        AIConfiguration aiConfiguration)
    {
        this.operations = operations;
        this.routingOptions = routingOptions.Value;
        profiles = BuildProfiles(aiConfiguration, this.routingOptions);
    }

    public IReadOnlyList<AiProviderProfile> GetProfiles() => profiles;

    public async ValueTask<AiProviderSelection> GetAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        string selectedKey = null;
        string selectedModel = null;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var selectedSetting = await operations.RetrieveAutomationSettings(userId)
                .AsNoTracking()
                .Where(item => item.UserId == userId)
                .Select(item => new { item.SelectedAiProfileKey, item.SelectedAiModel })
                .FirstOrDefaultAsync(cancellationToken);
            selectedKey = selectedSetting?.SelectedAiProfileKey;
            selectedModel = selectedSetting?.SelectedAiModel;
        }

        AiProviderProfile selected = FindProfile(selectedKey) is { IsConfigured: true } explicitProfile
            ? explicitProfile
            : FindProfile(routingOptions.DefaultProfile) is { IsConfigured: true } defaultProfile
                ? defaultProfile
                : profiles.FirstOrDefault(profile => profile.IsConfigured)
            ?? throw new InvalidOperationException("No AI routing profiles are configured.");

        return new(selected, ResolveModel(selected, selectedModel), FindProfile(selectedKey) is not null);
    }

    public async ValueTask<AiProviderSelection> SetAsync(
        string userId,
        string profileKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("An execution user is required.", nameof(userId));

        AiProviderProfile selected = FindProfile(profileKey)
            ?? throw new ArgumentException("The selected AI profile is not configured.", nameof(profileKey));
        EnsureAvailable(selected);

        AgentAutomationSetting setting = await GetOrCreateAsync(userId, cancellationToken);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        setting.SelectedAiProfileKey = selected.Key;
        setting.SelectedAiModel = selected.Model;
        setting.LastUpdatedBy = userId;
        setting.LastUpdated = now;
        await operations.SaveAsync(cancellationToken);

        return new(selected, selected.Model, true);
    }

    public async ValueTask<IReadOnlyList<AiWorkLaneSelection>> GetWorkLanesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        AgentAutomationSetting setting = null;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            setting = await operations.RetrieveAutomationSettings(userId)
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        }

        AiProviderProfile fallback = FindProfile(setting?.SelectedAiProfileKey) is { IsConfigured: true } explicitProfile
            ? explicitProfile
            : FindProfile(routingOptions.DefaultProfile) is { IsConfigured: true } defaultProfile
                ? defaultProfile
                : profiles.FirstOrDefault(profile => profile.IsConfigured)
            ?? throw new InvalidOperationException("No AI routing profiles are configured.");

        return
        [
            BuildLane(AgentWorkLane.Lead, setting?.LeadAiProfileKey, setting?.LeadAiModel, setting?.LeadAgentConcurrency ?? 1, fallback),
            BuildLane(AgentWorkLane.Opportunity, setting?.OpportunityAiProfileKey, setting?.OpportunityAiModel, setting?.OpportunityAgentConcurrency ?? 1, fallback),
            BuildLane(AgentWorkLane.Client, setting?.ClientAiProfileKey, setting?.ClientAiModel, setting?.ClientAgentConcurrency ?? 1, fallback)
        ];
    }

    public async ValueTask<AiWorkLaneSelection> SetWorkLaneAsync(
        string userId,
        AgentWorkLane lane,
        string profileKey,
        int concurrency,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("An execution user is required.", nameof(userId));

        string selectedKey = profileKey?.Trim() ?? string.Empty;
        bool enabled = !selectedKey.Equals("none", StringComparison.OrdinalIgnoreCase);
        AiProviderProfile profile = enabled
            ? FindProfile(selectedKey)
                ?? throw new ArgumentException("The selected AI profile is not configured.", nameof(profileKey))
            : null;
        if (enabled)
            EnsureAvailable(profile);
        int selectedConcurrency = enabled ? ClampConcurrency(concurrency, profile) : 1;

        AgentAutomationSetting setting = await GetOrCreateAsync(userId, cancellationToken);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        string selectedModel = enabled ? profile.Model : string.Empty;
        SetLane(setting, lane, enabled ? profile.Key : "none", selectedModel, selectedConcurrency);
        setting.LastUpdatedBy = userId;
        setting.LastUpdated = now;
        await operations.SaveAsync(cancellationToken);
        return new(lane, profile, selectedModel, enabled, selectedConcurrency);
    }

    public async ValueTask<AiRoutingSelection> SetRoutingAsync(
        string userId,
        string sharedProfileKey,
        string sharedModel,
        int sharedConcurrency,
        IReadOnlyList<AiWorkLaneUpdate> workLanes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("An execution user is required.", nameof(userId));

        AiProviderProfile sharedProfile = FindProfile(sharedProfileKey)
            ?? throw new ArgumentException("The selected shared-services AI profile is not configured.", nameof(sharedProfileKey));
        EnsureAvailable(sharedProfile);
        string selectedSharedModel = ResolveModel(sharedProfile, sharedModel);

        IReadOnlyList<AiWorkLaneUpdate> updates = workLanes ?? [];
        if (updates.Count != Enum.GetValues<AgentWorkLane>().Length
            || updates.Select(item => item.Lane).Distinct().Count() != updates.Count)
        {
            throw new ArgumentException("A single configuration is required for every commercial work lane.", nameof(workLanes));
        }

        List<AiWorkLaneSelection> selections = [];
        foreach (AiWorkLaneUpdate update in updates)
        {
            string selectedKey = update.ProfileKey?.Trim() ?? string.Empty;
            bool enabled = !selectedKey.Equals("none", StringComparison.OrdinalIgnoreCase);
            AiProviderProfile profile = enabled
                ? FindProfile(selectedKey)
                    ?? throw new ArgumentException($"The selected AI profile for {update.Lane} is not configured.", nameof(workLanes))
                : null;
            if (enabled)
                EnsureAvailable(profile);
            string selectedModel = enabled ? ResolveModel(profile, update.Model) : string.Empty;

            selections.Add(new AiWorkLaneSelection(
                update.Lane,
                profile,
                selectedModel,
                enabled,
                enabled ? ClampConcurrency(update.Concurrency, profile) : 1));
        }

        AgentAutomationSetting setting = await GetOrCreateAsync(userId, cancellationToken);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        setting.SelectedAiProfileKey = sharedProfile.Key;
        setting.SelectedAiModel = selectedSharedModel;
        setting.ApprovalAgentConcurrency = ClampConcurrency(sharedConcurrency, sharedProfile);
        foreach (AiWorkLaneSelection selection in selections)
        {
            SetLane(
                setting,
                selection.Lane,
                selection.IsEnabled ? selection.Profile.Key : "none",
                selection.Model,
                selection.Concurrency);
        }

        setting.LastUpdatedBy = userId;
        setting.LastUpdated = now;
        await operations.SaveAsync(cancellationToken);

        return new(new AiProviderSelection(sharedProfile, selectedSharedModel, true), selections);
    }

    async ValueTask<AgentAutomationSetting> GetOrCreateAsync(string userId, CancellationToken cancellationToken)
    {
        AgentAutomationSetting setting = await operations.RetrieveAutomationSettings(userId)
            .SingleOrDefaultAsync(cancellationToken);
        if (setting is not null) return setting;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        setting = new AgentAutomationSetting
        {
            Id = Guid.NewGuid(), UserId = userId, CreatedBy = userId, LastUpdatedBy = userId,
            CreatedOn = now, LastUpdated = now
        };
        operations.Add(setting);
        return setting;
    }

    AiProviderProfile FindProfile(string key) => profiles.FirstOrDefault(profile =>
        string.Equals(profile.Key, key, StringComparison.OrdinalIgnoreCase));

    AiWorkLaneSelection BuildLane(
        AgentWorkLane lane,
        string selectedKey,
        string selectedModel,
        int concurrency,
        AiProviderProfile fallback)
    {
        bool enabled = !string.Equals(selectedKey, "none", StringComparison.OrdinalIgnoreCase);
        AiProviderProfile profile = enabled ? FindProfile(selectedKey) ?? fallback : null;
        enabled = enabled && profile.IsConfigured;
        return new(
            lane,
            profile,
            enabled ? ResolveModel(profile, selectedModel) : string.Empty,
            enabled,
            enabled ? ClampConcurrency(concurrency, profile) : 1);
    }

    static void SetLane(
        AgentAutomationSetting setting,
        AgentWorkLane lane,
        string profileKey,
        string model,
        int concurrency)
    {
        switch (lane)
        {
            case AgentWorkLane.Lead:
                setting.LeadAiProfileKey = profileKey;
                setting.LeadAiModel = model;
                setting.LeadAgentConcurrency = concurrency;
                break;
            case AgentWorkLane.Opportunity:
                setting.OpportunityAiProfileKey = profileKey;
                setting.OpportunityAiModel = model;
                setting.OpportunityAgentConcurrency = concurrency;
                break;
            case AgentWorkLane.Client:
                setting.ClientAiProfileKey = profileKey;
                setting.ClientAiModel = model;
                setting.ClientAgentConcurrency = concurrency;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(lane), lane, "Unsupported AI work lane.");
        }
    }

    static IReadOnlyList<AiProviderProfile> BuildProfiles(
        AIConfiguration aiConfiguration,
        AiRoutingOptions options)
    {
        List<AiProviderProfile> configuredProfiles = [];
        foreach ((string key, AiRoutingProfileOptions profile) in options.Profiles)
        {
            if (!aiConfiguration.Providers.TryGetValue(key, out AIProviderConfiguration provider))
                continue;

            configuredProfiles.Add(new AiProviderProfile(
                key,
                string.IsNullOrWhiteSpace(profile.DisplayName) ? key : profile.DisplayName.Trim(),
                profile.Description?.Trim() ?? string.Empty,
                key,
                provider.CompletionProvider.DefaultModel,
                provider.CompletionProvider.Endpoint,
                provider.MaxConcurrency,
                IsConfigured(provider)));
        }

        return configuredProfiles;
    }

    static bool IsConfigured(AIProviderConfiguration provider)
    {
        bool requiresKey = provider.CompletionProvider.Mode is
            cCoder.AI.Models.Enums.AIProviderMode.OpenAICompatible
            or cCoder.AI.Models.Enums.AIProviderMode.AzureFoundry;
        return !string.IsNullOrWhiteSpace(provider.CompletionProvider.Endpoint)
            && !string.IsNullOrWhiteSpace(provider.CompletionProvider.DefaultModel)
            && (!requiresKey || !string.IsNullOrWhiteSpace(provider.CompletionProvider.ApiKey));
    }

    static void EnsureAvailable(AiProviderProfile profile)
    {
        if (!profile.IsConfigured)
            throw new ArgumentException($"AI profile '{profile.DisplayName}' is not fully configured.");
    }

    static string ResolveModel(AiProviderProfile profile, string requestedModel) =>
        string.IsNullOrWhiteSpace(requestedModel)
            ? profile.Model
            : requestedModel.Trim();

    static int ClampConcurrency(int concurrency, AiProviderProfile profile) =>
        Math.Clamp(concurrency, 1, Math.Max(1, profile.MaxConcurrency));
}
