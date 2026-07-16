using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using ClientRelationshipManagement.Web.Brokers.Loggings;
using ClientRelationshipManagement.Web.Configuration;
using ClientRelationshipManagement.Web.Services.Agents;
using ClientRelationshipManagement.Web.Services.Processes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClientRelationshipManagement.Web.Services.Leads;

public sealed class LeadWorkIntakeService(
    IPlatformDbContextFactory dbContextFactory,
    IWorkflowAutomationService workflowAutomationService,
    IOptions<AuthorityDataOptions> authorityOptions,
    IOptions<AgentWorkflowOptions> workflowOptions,
    ILoggingBroker<LeadWorkIntakeService> loggingBroker)
    : ILeadWorkIntakeService
{
    public async ValueTask<LeadWorkIntakeResult> EnsureCapacityAsync(
        CancellationToken cancellationToken = default)
    {
        AuthorityDataOptions options = authorityOptions.Value;
        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        IQueryable<ProcessTask> pending = context.ProcessTasks.Where(task =>
            task.State == ProcessTaskState.Pending
            && (task.LeadId.HasValue
                || task.OpportunityId.HasValue
                || task.ClientAccountId.HasValue
                || task.TenantCompanyRelationshipId.HasValue));

        int activeWorkItems = await pending.CountAsync(cancellationToken);
        int approvalBlockedWorkItems = await pending.CountAsync(task =>
            task.ActionType == ProcessActionType.Approval
            || context.AgentMessages.Any(message =>
                message.ProcessTaskId == task.Id
                && message.State == AgentMessageState.Pending
                && (message.Kind == AgentMessageKind.ApprovalRequest
                    || message.Kind == AgentMessageKind.FeedbackRequest)
                && !((task.ActionType == ProcessActionType.Call
                        || task.ActionType == ProcessActionType.Meeting)
                    && context.ProcessTransitions.Any(transition =>
                        transition.ProcessStepId == task.ProcessStepId
                        && transition.OutcomeKey == "await-response")))
            || (task.ActionType == ProcessActionType.Email
                && task.EmailId.HasValue
                && task.Email.State == EmailState.Draft),
            cancellationToken);

        if (activeWorkItems > 0 && approvalBlockedWorkItems == activeWorkItems)
            return new(activeWorkItems, 0, 0);

        IQueryable<ProcessTask> runnable = WorkflowTaskQueue.BuildImmediatelyAvailableQuery(context, now);
        int runnableWorkItems = await runnable.CountAsync(cancellationToken);
        runnableWorkItems += await pending.CountAsync(task =>
            task.AgentClaimId.HasValue && task.AgentClaimExpiresOn > now,
            cancellationToken);
        int minimumRunnable = Math.Max(1, options.MinimumRunnableWorkItems);
        int capacity = Math.Max(0, minimumRunnable - runnableWorkItems);

        if (capacity == 0)
            return new(activeWorkItems, runnableWorkItems, 0);

        string sourceSystem = options.SourceSystem?.Trim() ?? "CompaniesHouse";
        List<Company> candidates = await context.Companies
            .Include(company => company.RegisteredAddress)
            .Where(company => company.SourceSystem == sourceSystem
                && !company.IsProspectingSuppressed
                && (company.CompanyStatus == null || company.CompanyStatus.ToLower() == "active")
                && !context.Leads.Any(lead => lead.CompanyId == company.Id)
                && !context.TenantCompanyRelationships.Any(relationship => relationship.CompanyId == company.Id))
            .OrderByDescending(company => company.RankingScore)
            .ThenByDescending(company => company.AnnualRevenue)
            .ThenByDescending(company => company.EmployeeCount)
            .ThenBy(company => company.IncorporatedOn)
            .ThenBy(company => company.Id)
            .Take(capacity)
            .ToListAsync(cancellationToken);

        string executionUserId = string.IsNullOrWhiteSpace(workflowOptions.Value.ExecutionUserId)
            ? "system"
            : workflowOptions.Value.ExecutionUserId.Trim();
        List<Guid> leadIds = [];

        foreach (Company company in candidates)
        {
            Lead lead = new()
            {
                Id = Guid.NewGuid(),
                SourceSystem = company.SourceSystem,
                SourceRecordId = company.SourceRecordId ?? company.CompanyNumber,
                SourceFileName = "Bounded company intake",
                TenantId = options.DefaultTenantId,
                Status = LeadStatus.Imported,
                RawCompanyName = company.OfficialName,
                RawTradingName = company.TradingName,
                RawCompanyNumber = company.CompanyNumber,
                RawVatNumber = company.VatNumber,
                RawWebsiteUrl = company.WebsiteUrl,
                RawContactEmailAddress = company.ContactEmailAddress,
                RawContactPhoneNumber = company.ContactPhoneNumber,
                QualificationNotes = BuildQualificationNotes(company),
                RankingScore = company.RankingScore,
                RankingRationale = company.RankingRationale,
                CompanyId = company.Id,
                CreatedBy = executionUserId,
                LastUpdatedBy = executionUserId,
                CreatedOn = now,
                LastUpdated = now
            };

            context.Leads.Add(lead);
            leadIds.Add(lead.Id);
        }

        await context.SaveChangesAsync(cancellationToken);

        foreach (Guid leadId in leadIds)
            await workflowAutomationService.EnsureCoverageAsync(
                leadId: leadId,
                forceCreate: true,
                cancellationToken: cancellationToken);

        if (leadIds.Count > 0)
        {
            loggingBroker.LogInformation(
                "Promoted {PromotedCompanyCount} ranked compan(ies) into the bounded lead work queue. Active: {ActiveWorkItems}; runnable: {RunnableWorkItems}.",
                leadIds.Count,
                activeWorkItems,
                runnableWorkItems);
        }

        return new(activeWorkItems + leadIds.Count, runnableWorkItems + leadIds.Count, leadIds.Count);
    }

    static string BuildQualificationNotes(Company company) => string.Join(
        Environment.NewLine,
        new[]
        {
            $"Authority source status: {company.CompanyStatus}",
            $"Category: {company.CompanyCategory}",
            $"Origin: {company.CountryOfOrigin}",
            $"SIC: {company.PrimarySicCodes}",
            company.RegistryUri,
            company.RankingRationale
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
}
