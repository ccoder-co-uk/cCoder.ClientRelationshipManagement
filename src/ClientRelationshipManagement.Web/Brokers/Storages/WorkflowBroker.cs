using System.Data.Common;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
namespace ClientRelationshipManagement.Web.Brokers.Storages;
public sealed class WorkflowBroker(IClientRelationshipDbContextFactory factory) : IWorkflowBroker, IDisposable
{
    readonly ClientRelationshipDbContext context = factory.CreateDbContext(useAdminConnection: true);
    public IQueryable<Activity> Activities => context.Activities; public IQueryable<AgentMessageEntry> AgentMessageEntries => context.AgentMessageEntries;
    public IQueryable<AgentMessage> AgentMessages => context.AgentMessages; public IQueryable<ClientAccount> ClientAccounts => context.ClientAccounts;
    public IQueryable<Company> Companies => context.Companies; public IQueryable<CompanyContact> CompanyContacts => context.CompanyContacts;
    public IQueryable<CompanyHistoryItem> CompanyHistory => context.CompanyHistory; public IQueryable<EmailRecipient> EmailRecipients => context.EmailRecipients;
    public IQueryable<Email> Emails => context.Emails; public IQueryable<LeadContact> LeadContacts => context.LeadContacts; public IQueryable<Lead> Leads => context.Leads;
    public IQueryable<Material> Materials => context.Materials; public IQueryable<Opportunity> Opportunities => context.Opportunities;
    public IQueryable<ProcessDefinition> ProcessDefinitions => context.ProcessDefinitions; public IQueryable<ProcessInstance> ProcessInstances => context.ProcessInstances;
    public IQueryable<ProcessStep> ProcessSteps => context.ProcessSteps; public IQueryable<ProcessStepTask> ProcessStepTasks => context.ProcessStepTasks;
    public IQueryable<ProcessStepTaskRun> ProcessStepTaskRuns => context.ProcessStepTaskRuns; public IQueryable<ProcessStepTaskAttempt> ProcessStepTaskAttempts => context.ProcessStepTaskAttempts;
    public IQueryable<ProcessTask> ProcessTasks => context.ProcessTasks;
    public IQueryable<ProcessTransition> ProcessTransitions => context.ProcessTransitions; public IQueryable<RelationshipContact> RelationshipContacts => context.RelationshipContacts;
    public IQueryable<TenantCompanyRelationship> TenantCompanyRelationships => context.TenantCompanyRelationships;
    public void Add(object entity) => context.Add(entity); public void AddRange(params object[] entities) => context.AddRange(entities);
    public void RemoveRange(IEnumerable<object> entities) => context.RemoveRange(entities); public bool IsAdded(object entity) => context.Entry(entity).State == EntityState.Added;
    public ValueTask ReloadAsync(object entity, CancellationToken token = default) => new(context.Entry(entity).ReloadAsync(token));
    public ValueTask SaveAsync(CancellationToken token = default) => new(context.SaveChangesAsync(token));
    public ValueTask<IDbContextTransaction> BeginTransactionAsync(CancellationToken token = default) => new(context.Database.BeginTransactionAsync(token));
    public Task<int> ExecuteSqlRawAsync(string sql, CancellationToken token = default) => context.Database.ExecuteSqlRawAsync(sql, token);
    public DbConnection GetDbConnection() => context.Database.GetDbConnection(); public void Dispose() => context.Dispose();
}
