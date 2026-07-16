using cCoder.ClientRelationshipManagement.Brokers.Transactions;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Entities;

namespace cCoder.ClientRelationshipManagement.Services.Foundations.Platform;

internal sealed class ProcessCoordinationService(
    IProcessDefinitionOrchestrationService definitions,
    IProcessStepOrchestrationService steps,
    IProcessTransitionOrchestrationService transitions,
    IProcessInstanceOrchestrationService instances,
    IProcessTaskOrchestrationService tasks, ICRMTransactionBroker transaction) : IProcessCoordinationService
{
    readonly List<ProcessDefinition> addedDefinitions = [];
    readonly List<ProcessStep> addedSteps = [];
    readonly List<ProcessTransition> addedTransitions = [];
    readonly List<ProcessDefinition> deletedDefinitions = [];
    readonly List<ProcessStep> deletedSteps = [];
    readonly List<ProcessTransition> deletedTransitions = [];

    public IQueryable<ProcessDefinition> RetrieveDefinitions() => definitions.RetrieveAll();
    public IQueryable<ProcessDefinition> RetrieveWritableDefinitions() => definitions.RetrieveWriteable();
    public IQueryable<ProcessStep> RetrieveSteps() => steps.RetrieveAll();
    public IQueryable<ProcessTransition> RetrieveTransitions() => transitions.RetrieveAll();
    public IQueryable<ProcessInstance> RetrieveInstances() => instances.RetrieveAll();
    public IQueryable<ProcessTask> RetrieveTasks() => tasks.RetrieveAll();
    public void Add(ProcessDefinition entity) => addedDefinitions.Add(entity);
    public void Add(ProcessStep entity) => addedSteps.Add(entity);
    public void Add(ProcessTransition entity) => addedTransitions.Add(entity);
    public void Delete(ProcessDefinition entity) => deletedDefinitions.Add(entity);
    public void Delete(IEnumerable<ProcessStep> entities) => deletedSteps.AddRange(entities);
    public void Delete(ProcessStep entity) => deletedSteps.Add(entity);
    public void Delete(IEnumerable<ProcessTransition> entities) => deletedTransitions.AddRange(entities);
    public void Delete(ProcessTransition entity) => deletedTransitions.Add(entity);

    public async ValueTask SaveAsync(CancellationToken cancellationToken = default)
    {
        foreach (ProcessTransition entity in deletedTransitions) await transitions.RemoveAsync(entity, cancellationToken);
        foreach (ProcessStep entity in deletedSteps) await steps.RemoveAsync(entity, cancellationToken);
        foreach (ProcessDefinition entity in deletedDefinitions) await definitions.RemoveAsync(entity, cancellationToken);
        foreach (ProcessDefinition entity in addedDefinitions) await definitions.AddAsync(entity, cancellationToken);
        foreach (ProcessStep entity in addedSteps) await steps.AddAsync(entity, cancellationToken);
        foreach (ProcessTransition entity in addedTransitions) await transitions.AddAsync(entity, cancellationToken);
        addedDefinitions.Clear(); addedSteps.Clear(); addedTransitions.Clear();
        deletedDefinitions.Clear(); deletedSteps.Clear(); deletedTransitions.Clear();
        await transaction.CommitAsync(cancellationToken);
    }
}
