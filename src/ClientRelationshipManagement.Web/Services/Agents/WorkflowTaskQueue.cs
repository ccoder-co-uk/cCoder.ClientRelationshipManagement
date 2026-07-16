using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using ClientRelationshipManagement.Web.Configuration;

namespace ClientRelationshipManagement.Web.Services.Agents;

internal static class WorkflowTaskQueue
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
            .ThenByDescending(task => task.ProcessStep.Sequence)
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
