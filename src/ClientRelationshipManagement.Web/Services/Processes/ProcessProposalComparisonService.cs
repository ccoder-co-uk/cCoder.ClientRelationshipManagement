using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using ClientRelationshipManagement.Web.Models.Admin;

namespace ClientRelationshipManagement.Web.Services.Processes;

public static class ProcessProposalComparisonService
{
    public static ProcessProposalReviewViewModel Build(ProcessDefinition current, ProcessDefinition proposed)
    {
        ArgumentNullException.ThrowIfNull(current); ArgumentNullException.ThrowIfNull(proposed);
        List<ProcessProposalChangeViewModel> changes = [];
        Compare(changes, "process", "", "Process definition", "Definition", "Name", current.Name, proposed.Name);
        Compare(changes, "process", "", "Process definition", "Definition", "Description", current.Description, proposed.Description);

        Dictionary<string, ProcessStep> oldSteps = current.Steps.ToDictionary(step => step.Key, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, ProcessStep> newSteps = proposed.Steps.ToDictionary(step => step.Key, StringComparer.OrdinalIgnoreCase);
        foreach (string key in oldSteps.Keys.Union(newSteps.Keys, StringComparer.OrdinalIgnoreCase))
        {
            oldSteps.TryGetValue(key, out ProcessStep oldStep); newSteps.TryGetValue(key, out ProcessStep newStep);
            string name = newStep?.Name ?? oldStep?.Name ?? key;
            if (oldStep is null) { changes.Add(Change(key, name, "Structure", "Step", "", "Added", "added")); continue; }
            if (newStep is null) { changes.Add(Change(key, name, "Structure", "Step", "Removed", "", "removed")); continue; }
            foreach ((string category, string label, Func<ProcessStep, object> value) in StepFields())
                Compare(changes, key, key, name, category, label, Text(value(oldStep)), Text(value(newStep)));
            CompareTransitions(changes, oldStep, newStep, oldSteps, newSteps);
        }

        List<ProcessProposalStepViewModel> steps = newSteps.Values.OrderBy(step => step.Sequence).Select(step =>
        {
            int count = changes.Count(change => string.Equals(change.StepKey, step.Key, StringComparison.OrdinalIgnoreCase));
            return new ProcessProposalStepViewModel { Key = step.Key, Name = step.Name, Sequence = step.Sequence,
                ActionType = step.ActionType.ToString(), ChangeState = oldSteps.ContainsKey(step.Key) ? (count > 0 ? "modified" : "unchanged") : "added", ChangeCount = count,
                TaskCount = step.StepTasks.Count > 0 ? step.StepTasks.Count : oldSteps.GetValueOrDefault(step.Key)?.StepTasks.Count ?? 0 };
        }).ToList();
        foreach (ProcessStep removed in oldSteps.Values.Where(step => !newSteps.ContainsKey(step.Key)).OrderBy(step => step.Sequence))
            steps.Add(new ProcessProposalStepViewModel { Key = removed.Key, Name = removed.Name, Sequence = removed.Sequence,
                ActionType = removed.ActionType.ToString(), ChangeState = "removed", ChangeCount = 1, TaskCount = removed.StepTasks.Count });

        return new ProcessProposalReviewViewModel { Id = proposed.Id, Name = proposed.Name, ScopeType = proposed.ScopeType.ToString(),
            CurrentVersion = current.VersionNumber, ProposedVersion = proposed.VersionNumber, ChangeSummary = proposed.ChangeSummary ?? "No rationale supplied.",
            ProposedBy = proposed.ProposedByAgent ?? proposed.CreatedBy ?? "Unknown", CreatedOn = proposed.CreatedOn.LocalDateTime.ToString("dd MMM yyyy HH:mm"),
            Steps = steps.OrderBy(step => step.Sequence).ToList(), Changes = changes };
    }

    static IEnumerable<(string, string, Func<ProcessStep, object>)> StepFields() =>
    [
        ("Step", "Name", step => step.Name), ("Step", "Objective", step => step.Objective),
        ("Step", "Action", step => step.ActionType), ("Step", "Sequence", step => step.Sequence),
        ("Timing", "Due after days", step => step.DueAfterDays), ("Timing", "Due after hours", step => step.DueAfterHours),
        ("Task", "Task title", step => step.TaskTitleTemplate), ("Task", "Task instructions", step => step.TaskInstructionsTemplate),
        ("Email", "Recipient", step => step.EmailRecipientTarget), ("Email", "Email subject", step => step.EmailSubjectTemplate),
        ("Email", "Email body", step => step.EmailBodyTemplate), ("Call", "Call script", step => step.CallScriptTemplate),
        ("Review", "Question set", step => step.QuestionSetTemplate), ("Facts", "Required facts", step => step.RequiredFacts),
        ("Facts", "Produced facts", step => step.ProducedFacts), ("Lifecycle", "Relationship status", step => step.RelationshipStatusOnActivate),
        ("Lifecycle", "Sales stage", step => step.SalesStageOnActivate), ("Lifecycle", "Client status", step => step.ClientAccountStatusOnActivate),
        ("Step", "Entry point", step => step.IsEntryPoint), ("Step", "Active", step => step.IsActive)
    ];

    static void CompareTransitions(List<ProcessProposalChangeViewModel> changes, ProcessStep oldStep, ProcessStep newStep,
        Dictionary<string, ProcessStep> oldSteps, Dictionary<string, ProcessStep> newSteps)
    {
        var oldRoutes = oldStep.OutgoingTransitions.ToDictionary(route => route.OutcomeKey ?? "", StringComparer.OrdinalIgnoreCase);
        var newRoutes = newStep.OutgoingTransitions.ToDictionary(route => route.OutcomeKey ?? "", StringComparer.OrdinalIgnoreCase);
        foreach (string outcome in oldRoutes.Keys.Union(newRoutes.Keys, StringComparer.OrdinalIgnoreCase))
        {
            oldRoutes.TryGetValue(outcome, out ProcessTransition oldRoute); newRoutes.TryGetValue(outcome, out ProcessTransition newRoute);
            string Old(ProcessTransition route, Dictionary<string, ProcessStep> steps) => route is null ? "" : route.IsTerminal ? "Terminal" :
                steps.Values.FirstOrDefault(step => step.Id == route.NextProcessStepId)?.Name ?? "Unresolved";
            Compare(changes, oldStep.Key, oldStep.Key, newStep.Name, "Routing", $"Route: {outcome}", Old(oldRoute, oldSteps), Old(newRoute, newSteps));
        }
    }

    static void Compare(List<ProcessProposalChangeViewModel> changes, string anchor, string key, string name, string category, string property, string current, string proposed)
    { if (!string.Equals(current, proposed, StringComparison.Ordinal)) changes.Add(Change(anchor, name, category, property, current, proposed, string.IsNullOrEmpty(current) ? "added" : string.IsNullOrEmpty(proposed) ? "removed" : "modified", key)); }
    static ProcessProposalChangeViewModel Change(string anchor, string name, string category, string property, string current, string proposed, string type, string key = null) =>
        new() { Anchor = $"change-{Safe(anchor)}-{Safe(property)}", StepKey = key ?? anchor, StepName = name, Category = category, Property = property,
            CurrentValue = string.IsNullOrWhiteSpace(current) ? "Not set" : current, ProposedValue = string.IsNullOrWhiteSpace(proposed) ? "Not set" : proposed, ChangeType = type };
    static string Safe(string value) => new(value.ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '-').ToArray());
    static string Text(object value) => value?.ToString() ?? "";
}
