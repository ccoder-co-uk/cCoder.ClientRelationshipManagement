using cCoder.ClientRelationshipManagement.Brokers.Transactions;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Entities;

namespace cCoder.ClientRelationshipManagement.Services.Foundations.Platform;

internal sealed class OperationsCoordinationService(
    IEmailOrchestrationService emails,
    IAgentRunOrchestrationService agentRuns,
    IProcessDefinitionOrchestrationService processDefinitions,
    IAgentAutomationSettingOrchestrationService automationSettings,
    IMailboxMessageRecordOrchestrationService mailboxMessages, ICRMTransactionBroker transaction) : IOperationsCoordinationService
{
    readonly List<AgentAutomationSetting> pendingSettings = [];
    readonly List<AgentRun> pendingRuns = [];
    readonly List<MailboxMessageRecord> pendingMailboxMessages = [];

    public IQueryable<Email> RetrieveAllEmails() => emails.RetrieveAll();
    public IQueryable<AgentRun> RetrieveAllAgentRuns() => agentRuns.RetrieveAll();
    public IQueryable<ProcessDefinition> RetrieveAllProcessDefinitions() => processDefinitions.RetrieveAll();
    public IQueryable<AgentAutomationSetting> RetrieveAutomationSettings(string userId) =>
        automationSettings.RetrieveAll().Where(item => item.UserId == userId);
    public IQueryable<MailboxMessageRecord> RetrieveMailboxMessages() => mailboxMessages.RetrieveAll();
    public void Add(AgentAutomationSetting entity) => pendingSettings.Add(entity);
    public void Add(AgentRun entity) => pendingRuns.Add(entity);
    public void Add(MailboxMessageRecord entity) => pendingMailboxMessages.Add(entity);

    public async ValueTask SaveAsync(CancellationToken cancellationToken = default)
    {
        foreach (AgentAutomationSetting entity in pendingSettings) await automationSettings.AddAsync(entity, cancellationToken);
        foreach (AgentRun entity in pendingRuns) await agentRuns.AddAsync(entity, cancellationToken);
        foreach (MailboxMessageRecord entity in pendingMailboxMessages) await mailboxMessages.AddAsync(entity, cancellationToken);
        pendingSettings.Clear(); pendingRuns.Clear(); pendingMailboxMessages.Clear();
        await transaction.CommitAsync(cancellationToken);
    }
}
