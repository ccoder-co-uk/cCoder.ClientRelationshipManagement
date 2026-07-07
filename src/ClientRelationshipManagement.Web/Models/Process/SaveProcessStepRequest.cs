using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace ClientRelationshipManagement.Web.Models.Process;

public sealed class SaveProcessStepRequest
{
    public Guid? Id { get; set; }
    public Guid ProcessDefinitionId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public bool IsEntryPoint { get; set; }
    public bool IsActive { get; set; } = true;
    public ProcessActionType ActionType { get; set; }
    public RelationshipStatus? RelationshipStatusOnActivate { get; set; }
    public SalesPipelineStage? SalesStageOnActivate { get; set; }
    public ClientAccountStatus? ClientAccountStatusOnActivate { get; set; }
    public int DueAfterDays { get; set; }
    public int DueAfterHours { get; set; }
    public string TaskTitleTemplate { get; set; } = string.Empty;
    public string TaskInstructionsTemplate { get; set; } = string.Empty;
    public string EmailSubjectTemplate { get; set; } = string.Empty;
    public string EmailBodyTemplate { get; set; } = string.Empty;
    public string CallScriptTemplate { get; set; } = string.Empty;
    public string QuestionSetTemplate { get; set; } = string.Empty;
}
