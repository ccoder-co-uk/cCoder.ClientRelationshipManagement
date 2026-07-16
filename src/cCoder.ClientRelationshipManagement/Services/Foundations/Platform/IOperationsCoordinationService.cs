using cCoder.ClientRelationshipManagement.Platform.Models.Entities;

namespace cCoder.ClientRelationshipManagement.Services.Foundations.Platform;

public interface IOperationsCoordinationService
{
    IQueryable<Email> RetrieveAllEmails();
    IQueryable<AgentRun> RetrieveAllAgentRuns();
    IQueryable<ProcessDefinition> RetrieveAllProcessDefinitions();
    IQueryable<AgentAutomationSetting> RetrieveAutomationSettings(string userId);
    IQueryable<MailboxMessageRecord> RetrieveMailboxMessages();
    void Add(AgentAutomationSetting entity);
    void Add(AgentRun entity);
    void Add(MailboxMessageRecord entity);
    ValueTask SaveAsync(CancellationToken cancellationToken = default);
}
