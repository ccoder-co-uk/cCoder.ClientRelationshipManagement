using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using cCoder.ClientRelationshipManagement.Runtime.Configuration;

namespace cCoder.ClientRelationshipManagement.Runtime.Services.Agents;

public static class WorkflowTaskQueue
{
    public static IOrderedQueryable<ProcessTask> OrderByCommercialProgress(
        IQueryable<ProcessTask> tasks) =>
        tasks.OrderBy(task =>
                task.OpportunityId.HasValue
                    ? 0
                    : task.LeadId.HasValue
                        ? 1
                        : task.TenantCompanyRelationshipId.HasValue && !task.ClientAccountId.HasValue
                        ? 1
                        : task.ClientAccountId.HasValue
                            ? 2
                            : 3)
            // Finish work already furthest through its process before admitting
            // another record at an earlier step. This lets relationship tip-in
            // self-propagate and prevents high-value pool records from starving
            // resource, fit, contact, and qualification work.
            .ThenByDescending(task => task.ProcessStep.Sequence)
            // Intake/research records the commercial priority on the lead once.
            // Reuse it here instead of joining the full company table and
            // recalculating turnover, sector, headcount, and contact heuristics
            // every time a worker claims a task.
            .ThenByDescending(task => task.LeadId.HasValue
                ? task.Lead.RankingScore
                : null)
            .ThenBy(task => task.DueOn)
            .ThenBy(task => task.ActionType == ProcessActionType.Email
                || task.ActionType == ProcessActionType.Call
                || task.ActionType == ProcessActionType.Meeting
                    ? 1
                    : 0)
            .ThenBy(task => task.RenderedTitle)
            .ThenBy(task => task.Id);

    public static IQueryable<ProcessTask> ForLane(
        IQueryable<ProcessTask> tasks,
        AgentWorkLane? lane) => lane switch
        {
            AgentWorkLane.Lead => tasks.Where(task => task.LeadId.HasValue),
            AgentWorkLane.Opportunity => tasks.Where(task =>
                task.OpportunityId.HasValue
                || (!task.LeadId.HasValue
                    && !task.ClientAccountId.HasValue
                    && task.TenantCompanyRelationshipId.HasValue)),
            AgentWorkLane.Client => tasks.Where(task => task.ClientAccountId.HasValue),
            _ => tasks
        };

}
