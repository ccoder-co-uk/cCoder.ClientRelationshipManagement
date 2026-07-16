using System.Text;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.Web.Services.Agents;

public interface IProcessHealthReviewService
{
    ValueTask<int> CreateDailyReviewsAsync(string createdBy, CancellationToken cancellationToken = default);
}

public sealed class ProcessHealthReviewService(IPlatformDbContextFactory dbContextFactory) : IProcessHealthReviewService
{
    public async ValueTask<int> CreateDailyReviewsAsync(string createdBy, CancellationToken cancellationToken = default)
    {
        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateOnly reportDate = DateOnly.FromDateTime(now.UtcDateTime);
        var definitions = await context.ProcessDefinitions
            .AsNoTracking()
            .Include(item => item.Steps)
            .Where(item => item.LifecycleState == ProcessDefinitionLifecycleState.Active || item.IsActive)
            .OrderBy(item => item.TenantId).ThenBy(item => item.ScopeType).ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);

        int created = 0;
        foreach (var tenantGroup in definitions.GroupBy(item => item.TenantId))
        {
            string correlationKey = $"daily-process-health:{tenantGroup.Key}:{reportDate:yyyy-MM-dd}";
            if (await context.AgentMessages.AnyAsync(item => item.CorrelationKey == correlationKey, cancellationToken))
                continue;

            StringBuilder report = new();
            report.AppendLine($"Tenant-wide process health snapshot for {tenantGroup.Key} on {reportDate:dd MMM yyyy}.");
            report.AppendLine("Counts are unfiltered and cover every active process step. They are evidence for review, not authority to change a process.");

            foreach (ProcessDefinition definition in tenantGroup)
            {
                report.AppendLine();
                report.AppendLine($"{definition.Name} ({definition.ScopeType}, version {definition.VersionNumber})");
                foreach (ProcessStep step in definition.Steps.Where(item => item.IsActive).OrderBy(item => item.Sequence))
                {
                    var counts = await context.ProcessTasks
                        .AsNoTracking()
                        .Where(task => task.ProcessStepId == step.Id)
                        .GroupBy(_ => 1)
                        .Select(group => new
                        {
                            Total = group.Count(),
                            Pending = group.Count(task => task.State == ProcessTaskState.Pending),
                            Completed = group.Count(task => task.State == ProcessTaskState.Completed),
                            Cancelled = group.Count(task => task.State == ProcessTaskState.Cancelled),
                            SentEmails = group.Count(task => task.EmailId.HasValue && task.Email.State == EmailState.Sent),
                            RejectedEmails = group.Count(task => task.EmailId.HasValue && task.Email.State == EmailState.Rejected),
                            NoResponse = group.Count(task => task.CompletionOutcomeKey == "no-reply")
                        })
                        .FirstOrDefaultAsync(cancellationToken);
                    report.AppendLine(counts is null
                        ? $"- {step.Sequence}. {step.Name}: total 0"
                        : $"- {step.Sequence}. {step.Name}: total {counts.Total}; pending {counts.Pending}; completed {counts.Completed}; cancelled {counts.Cancelled}; sent emails {counts.SentEmails}; rejected emails {counts.RejectedEmails}; no-response outcomes {counts.NoResponse}");
                }
            }

            AgentMessage message = new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantGroup.Key,
                Kind = AgentMessageKind.FeedbackRequest,
                State = AgentMessageState.Pending,
                CorrelationKey = correlationKey,
                Title = $"Daily process health review — {tenantGroup.Key}",
                Body = "The scheduled tenant-wide process snapshot is ready for the Approval Agent to review for meaningful failure patterns.",
                AgentName = "Approval Agent",
                CreatedBy = createdBy,
                LastUpdatedBy = createdBy,
                CreatedOn = now,
                LastUpdated = now
            };
            context.AgentMessages.Add(message);
            context.AgentMessageEntries.Add(new AgentMessageEntry
            {
                Id = Guid.NewGuid(),
                AgentMessageId = message.Id,
                Role = "System",
                Body = report.ToString().Trim(),
                CreatedBy = createdBy,
                LastUpdatedBy = createdBy,
                CreatedOn = now,
                LastUpdated = now
            });
            created++;
        }

        await context.SaveChangesAsync(cancellationToken);
        return created;
    }
}
