using System.Data.Common;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore.Storage;
namespace ClientRelationshipManagement.Web.Brokers.Storages;
public interface IWorkflowBroker
{
    IQueryable<Activity> Activities { get; } IQueryable<AgentMessageEntry> AgentMessageEntries { get; } IQueryable<AgentMessage> AgentMessages { get; }
    IQueryable<ClientAccount> ClientAccounts { get; } IQueryable<Company> Companies { get; } IQueryable<CompanyContact> CompanyContacts { get; }
    IQueryable<CompanyHistoryItem> CompanyHistory { get; } IQueryable<EmailRecipient> EmailRecipients { get; } IQueryable<Email> Emails { get; }
    IQueryable<LeadContact> LeadContacts { get; } IQueryable<Lead> Leads { get; } IQueryable<Material> Materials { get; }
    IQueryable<Opportunity> Opportunities { get; } IQueryable<ProcessDefinition> ProcessDefinitions { get; } IQueryable<ProcessInstance> ProcessInstances { get; }
    IQueryable<ProcessStep> ProcessSteps { get; } IQueryable<ProcessStepTask> ProcessStepTasks { get; } IQueryable<ProcessStepTaskRun> ProcessStepTaskRuns { get; }
    IQueryable<ProcessStepTaskAttempt> ProcessStepTaskAttempts { get; } IQueryable<ProcessTask> ProcessTasks { get; } IQueryable<ProcessTransition> ProcessTransitions { get; }
    IQueryable<RelationshipContact> RelationshipContacts { get; } IQueryable<TenantCompanyRelationship> TenantCompanyRelationships { get; }
    void Add(object entity); void AddRange(params object[] entities); void RemoveRange(IEnumerable<object> entities);
    bool IsAdded(object entity); ValueTask ReloadAsync(object entity, CancellationToken token = default);
    ValueTask SaveAsync(CancellationToken token = default); ValueTask<IDbContextTransaction> BeginTransactionAsync(CancellationToken token = default);
    Task<int> ExecuteSqlRawAsync(string sql, CancellationToken token = default); DbConnection GetDbConnection();
}
