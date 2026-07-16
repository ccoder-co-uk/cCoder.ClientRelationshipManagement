using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using ClientRelationshipManagement.Web.Configuration;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.Web.Services.Agents;

internal static class WorkflowTaskQueue
{
    public static IQueryable<ProcessTask> BuildRunnableQuery(
        PlatformDbContext context,
        DateTimeOffset now) =>
        BuildImmediatelyAvailableQuery(context, now);

    public static IQueryable<ProcessTask> BuildImmediatelyAvailableQuery(
        PlatformDbContext context,
        DateTimeOffset now) =>
        context.ProcessTasks.Where(task =>
            task.State == ProcessTaskState.Pending
            && task.DueOn <= now
            && (!task.AgentClaimExpiresOn.HasValue || task.AgentClaimExpiresOn <= now)
            && task.ActionType != ProcessActionType.Approval
            && !context.AgentMessages.Any(message =>
                message.ProcessTaskId == task.Id
                && message.State == AgentMessageState.Pending
                && (message.Kind == AgentMessageKind.ApprovalRequest
                    || message.Kind == AgentMessageKind.FeedbackRequest)
                && !((task.ActionType == ProcessActionType.Call
                        || task.ActionType == ProcessActionType.Meeting)
                    && context.ProcessTransitions.Any(transition =>
                        transition.ProcessStepId == task.ProcessStepId
                        && transition.OutcomeKey == "await-response")))
            && !(task.ActionType == ProcessActionType.Email
                && task.EmailId.HasValue
                && (task.Email.State == EmailState.Draft
                    || task.Email.State == EmailState.Approved
                    || task.Email.State == EmailState.Sending
                    || task.Email.State == EmailState.Sent)));

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
