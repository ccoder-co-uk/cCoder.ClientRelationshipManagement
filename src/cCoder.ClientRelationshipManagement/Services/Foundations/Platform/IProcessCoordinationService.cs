using cCoder.ClientRelationshipManagement.Platform.Models.Entities;

namespace cCoder.ClientRelationshipManagement.Services.Foundations.Platform;

public interface IProcessCoordinationService
{
    IQueryable<ProcessDefinition> RetrieveDefinitions();
    IQueryable<ProcessDefinition> RetrieveWritableDefinitions();
    IQueryable<ProcessStep> RetrieveSteps();
    IQueryable<ProcessTransition> RetrieveTransitions();
    IQueryable<ProcessInstance> RetrieveInstances();
    IQueryable<ProcessTask> RetrieveTasks();
    void Add(ProcessDefinition definition);
    void Add(ProcessStep step);
    void Add(ProcessTransition transition);
    void Delete(ProcessDefinition definition);
    void Delete(IEnumerable<ProcessStep> steps);
    void Delete(ProcessStep step);
    void Delete(IEnumerable<ProcessTransition> transitions);
    void Delete(ProcessTransition transition);
    ValueTask SaveAsync(CancellationToken cancellationToken = default);
}
