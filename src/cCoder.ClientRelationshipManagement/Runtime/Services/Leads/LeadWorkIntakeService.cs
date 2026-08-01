using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using cCoder.ClientRelationshipManagement.Services.Foundations.Platform;
using cCoder.ClientRelationshipManagement.Runtime.Brokers.Loggings;
using cCoder.ClientRelationshipManagement.Runtime.Configuration;
using cCoder.ClientRelationshipManagement.Runtime.Services.Agents;
using cCoder.ClientRelationshipManagement.Runtime.Services.Processes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace cCoder.ClientRelationshipManagement.Runtime.Services.Leads;

public sealed class LeadWorkIntakeService(
    IProcessCoordinationService processes,
    ISalesCoordinationService sales,
    cCoder.ClientRelationshipManagement.Services.Entities.IAgentMessageOrchestrationService messages,
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
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string[] priorityCompanyNumbers = options.PriorityCompanyNumbers
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        bool priorityDiscoveryOnly = options.PriorityDiscoveryOnly && priorityCompanyNumbers.Length > 0;

        IQueryable<ProcessTask> pending = processes.RetrieveTasks().Where(task =>
            task.State == ProcessTaskState.Pending
            && (task.LeadId.HasValue
                || task.OpportunityId.HasValue
                || task.ClientAccountId.HasValue
                || task.TenantCompanyRelationshipId.HasValue));

        int activeWorkItems = await pending.CountAsync(cancellationToken);
        Guid[] approvalMessageTaskIds = await messages.RetrieveAll()
            .Where(message => message.ProcessTaskId.HasValue
                && message.State == AgentMessageState.Pending
                && (message.Kind == AgentMessageKind.ApprovalRequest
                    || message.Kind == AgentMessageKind.FeedbackRequest))
            .Select(message => message.ProcessTaskId!.Value)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        Guid[] awaitResponseStepIds = await processes.RetrieveTransitions()
            .Where(transition => transition.OutcomeKey == "await-response")
            .Select(transition => transition.ProcessStepId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        int approvalBlockedWorkItems = await pending.CountAsync(task =>
            task.ActionType == ProcessActionType.Approval
            || (approvalMessageTaskIds.Contains(task.Id)
                && !((task.ActionType == ProcessActionType.Call
                        || task.ActionType == ProcessActionType.Meeting)
                    && awaitResponseStepIds.Contains(task.ProcessStepId)))
            || (task.ActionType == ProcessActionType.Email
                && task.EmailId.HasValue
                && task.Email.State == EmailState.Draft),
            cancellationToken);

        if (activeWorkItems > 0 && approvalBlockedWorkItems == activeWorkItems)
            return new(activeWorkItems, 0, 0);

        IQueryable<ProcessTask> runnable = sales.RetrieveRunnableProcessTasks(now);
        int runnableWorkItems = await runnable.CountAsync(cancellationToken);
        runnableWorkItems += await pending.CountAsync(task =>
            task.AgentClaimId.HasValue && task.AgentClaimExpiresOn > now,
            cancellationToken);
        int minimumRunnable = Math.Max(1, options.MinimumRunnableWorkItems);
        int capacity = Math.Max(0, minimumRunnable - runnableWorkItems);

        if (capacity == 0)
            return new(activeWorkItems, runnableWorkItems, 0);

        string sourceSystem = options.SourceSystem?.Trim() ?? "CompaniesHouse";
        IQueryable<Company> candidateQuery = sales.RetrieveCompanies()
            .Include(company => company.RegisteredAddress)
            .Where(company => company.SourceSystem == sourceSystem
                && company.IsVerified
                && !company.IsProspectingSuppressed
                && (company.CompanyStatus == null || company.CompanyStatus.ToLower() == "active")
                && !sales.RetrieveLeads().Any(lead => lead.CompanyId == company.Id)
                && !sales.RetrieveRelationships().Any(relationship => relationship.CompanyId == company.Id));
        List<Company> candidates;
        if (priorityDiscoveryOnly)
        {
            List<Company> availablePriorityCompanies = await candidateQuery
                .Where(company => priorityCompanyNumbers.Contains(company.CompanyNumber))
                .Take(priorityCompanyNumbers.Length)
                .ToListAsync(cancellationToken);
            if (availablePriorityCompanies.Count > 0)
            {
                candidates = availablePriorityCompanies
                    .OrderBy(company => Array.FindIndex(
                        priorityCompanyNumbers,
                        number => string.Equals(number, company.CompanyNumber, StringComparison.OrdinalIgnoreCase)))
                    .Take(capacity)
                    .ToList();
            }
            else
            {
                // The named frontier is exhausted. Related-company discoveries
                // already occupy runnable capacity, so this fallback is reached
                // only when both the priority and discovered frontiers are empty.
                candidates = await candidateQuery
                    .OrderByDescending(company => company.RankingScore)
                    .ThenByDescending(company => company.AnnualRevenue)
                    .ThenByDescending(company => company.EmployeeCount)
                    .ThenBy(company => company.IncorporatedOn)
                    .ThenBy(company => company.Id)
                    .Take(capacity)
                    .ToListAsync(cancellationToken);
            }
        }
        else
        {
            candidates = await candidateQuery
                .OrderByDescending(company => priorityCompanyNumbers.Contains(company.CompanyNumber))
                .ThenByDescending(company => company.RankingScore)
                .ThenByDescending(company => company.AnnualRevenue)
                .ThenByDescending(company => company.EmployeeCount)
                .ThenBy(company => company.IncorporatedOn)
                .ThenBy(company => company.Id)
                .Take(capacity)
                .ToListAsync(cancellationToken);
        }

        string executionUserId = string.IsNullOrWhiteSpace(workflowOptions.Value.ExecutionUserId)
            ? "system"
            : workflowOptions.Value.ExecutionUserId.Trim();
        List<Guid> leadIds = [];

        foreach (Company company in candidates)
        {
            bool isPrioritySeed = priorityCompanyNumbers.Contains(company.CompanyNumber);
            Lead lead = new()
            {
                Id = Guid.NewGuid(),
                SourceSystem = company.SourceSystem,
                SourceRecordId = company.SourceRecordId ?? company.CompanyNumber,
                SourceFileName = isPrioritySeed
                    ? "Target discovery: known-company seed"
                    : "Bounded company intake",
                TenantId = options.DefaultTenantId,
                Status = LeadStatus.Imported,
                RawCompanyName = company.OfficialName,
                RawTradingName = company.TradingName,
                RawCompanyNumber = company.CompanyNumber,
                RawVatNumber = company.VatNumber,
                RawWebsiteUrl = company.WebsiteUrl,
                RawContactEmailAddress = company.ContactEmailAddress,
                RawContactPhoneNumber = company.ContactPhoneNumber,
                QualificationNotes = BuildQualificationNotes(company, isPrioritySeed),
                RankingScore = isPrioritySeed ? Math.Max(100, company.RankingScore ?? 0) : company.RankingScore,
                RankingRationale = isPrioritySeed
                    ? "Target discovery seed: known substantial company; exact scale facts are optional for the initial pitch-and-contact pass."
                    : company.RankingRationale,
                CompanyId = company.Id,
                CreatedBy = executionUserId,
                LastUpdatedBy = executionUserId,
                CreatedOn = now,
                LastUpdated = now
            };

            sales.Add(lead);
            leadIds.Add(lead.Id);
        }

        await sales.SaveAsync(cancellationToken);

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

    static string BuildQualificationNotes(Company company, bool isPrioritySeed) => string.Join(
        Environment.NewLine,
        new[]
        {
            isPrioritySeed ? "Target discovery seed: known substantial company" : null,
            $"Authority source status: {company.CompanyStatus}",
            $"Category: {company.CompanyCategory}",
            $"Origin: {company.CountryOfOrigin}",
            $"SIC: {company.PrimarySicCodes}",
            company.RegistryUri,
            company.RankingRationale
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
}
