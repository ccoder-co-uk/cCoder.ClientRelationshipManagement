# Entity Class Diagram

This diagram reflects the current classes in `Models/Entities` and the relationships expressed by their foreign keys and navigation properties.

```mermaid
classDiagram
direction LR

class Address {
    +Guid Id
    +string PoBox
    +string Line1
    +string Line2
    +string ZipOrPostalCode
    +string TownOrCity
    +string StateOrProvince
    +string CountryId
    +bool IsActive
    +DateTimeOffset CreatedOn
    +DateTimeOffset LastUpdated
}

class Client {
    +Guid Id
    +string TenantId
    +string AccountOwner
    +RelationshipStatus Status
    +PipelineStage CurrentStage
    +ClientPriority Priority
    +string LeadSource
    +string InitialRoute
    +decimal? FitScore
    +string OpportunitySummary
    +string PreferredOpeningAngle
    +string NextAction
    +DateTimeOffset? NextActionDueOn
    +string CreatedBy
    +string LastUpdatedBy
    +DateTimeOffset CreatedOn
    +DateTimeOffset LastUpdated
    +bool IsArchived
}

class Company {
    +Guid Id
    +Guid ClientId
    +string Name
    +string LegalEntityName
    +string TradingName
    +string CompanyNumber
    +string VatNumber
    +string ContactEmailAddress
    +string ContactPhoneNumber
    +string WebsiteUrl
    +string RegisteredOfficeText
    +bool IsActive
    +bool IsVerified
    +Guid? RegisteredAddressId
}

class ClientContact {
    +Guid Id
    +Guid ClientId
    +string Name
    +string Position
    +string EmailAddress
    +string PhoneNumber
    +string LinkedInUrl
    +string Source
    +string RelationshipRoute
    +ClientContactStatus Status
    +bool IsPrimary
    +string Notes
}

class ClientOpportunity {
    +Guid Id
    +Guid ClientId
    +Guid? PrimaryContactId
    +ClientOpportunityType Type
    +PipelineStage Stage
    +decimal? EstimatedAnnualValue
    +decimal? Probability
    +string PainSummary
    +string ValueHypothesis
    +string DecisionProcess
    +string NextAction
    +DateTimeOffset? NextActionDueOn
}

class ClientActivity {
    +Guid Id
    +Guid ClientId
    +Guid? ClientContactId
    +Guid? ClientOpportunityId
    +Guid? ClientMaterialId
    +DateTimeOffset ActivityOn
    +ClientActivityType Type
    +ClientActivityDirection Direction
    +string Summary
    +string Outcome
    +string NextAction
    +DateTimeOffset? NextActionDueOn
}

class ClientMaterial {
    +Guid Id
    +Guid ClientId
    +Guid? SentToContactId
    +string Name
    +string FilePath
    +ClientMaterialType Type
    +ClientMaterialStatus Status
    +DateTimeOffset? SentOn
    +string Notes
}

class Email {
    +Guid Id
    +Guid ClientId
    +Guid? ClientMaterialId
    +Guid? SentToContactId
    +string SenderUserId
    +string FromDisplayName
    +string FromEmailAddress
    +string ReplyToAddresses
    +string ToAddresses
    +string CcAddresses
    +string BccAddresses
    +string Subject
    +string BodyHtml
    +string BodyText
    +bool IsBodyHtml
    +EmailState State
    +DateTimeOffset? ApprovedOn
    +string ApprovedBy
    +DateTimeOffset? ScheduledSendTimeUtc
    +DateTimeOffset? LastSendAttemptOn
    +DateTimeOffset? SentOn
    +string ExternalMessageId
    +string LastError
    +int SendFailureCount
}

class ClientHandoffPack {
    +Guid Id
    +Guid ClientId
    +Guid ClientOpportunityId
    +string SignedContractPath
    +string LegalEntity
    +string PrimaryCommercialContact
    +string PrimaryOperationalContact
    +string PrimaryTechnicalContact
    +string AgreedScope
    +string CommercialTermsSummary
    +string PromisedOutcomes
    +string KnownRisks
    +string OnboardingOwner
    +ClientHandoffStatus Status
    +DateTimeOffset? HandedOffOn
}

class ClientProcessDefinition {
    +Guid Id
    +string TenantId
    +string Name
    +string Description
    +bool IsDefault
    +bool IsActive
    +string CreatedBy
    +string LastUpdatedBy
    +DateTimeOffset CreatedOn
    +DateTimeOffset LastUpdated
}

class ClientProcessStep {
    +Guid Id
    +Guid ClientProcessDefinitionId
    +string Key
    +string Name
    +int Sequence
    +bool IsEntryPoint
    +ClientProcessActionType ActionType
    +RelationshipStatus? StatusOnActivate
    +PipelineStage? StageOnActivate
    +int DueAfterDays
    +int DueAfterHours
    +string TaskTitleTemplate
    +string TaskInstructionsTemplate
    +string EmailSubjectTemplate
    +string EmailBodyTemplate
    +string CallScriptTemplate
    +string QuestionSetTemplate
    +bool IsActive
}

class ClientProcessTransition {
    +Guid Id
    +Guid ClientProcessStepId
    +Guid? NextClientProcessStepId
    +string OutcomeKey
    +string OutcomeLabel
    +bool IsDefaultOutcome
    +bool IsTerminal
    +RelationshipStatus? TerminalStatus
    +PipelineStage? TerminalStage
}

class ClientProcessInstance {
    +Guid Id
    +Guid ClientId
    +Guid ClientProcessDefinitionId
    +Guid? CurrentClientProcessStepId
    +Guid? CurrentClientProcessTaskId
    +ClientProcessInstanceState State
    +string CompletionOutcomeKey
    +DateTimeOffset StartedOn
    +DateTimeOffset? CompletedOn
}

class ClientProcessTask {
    +Guid Id
    +Guid ClientId
    +Guid ClientProcessInstanceId
    +Guid ClientProcessStepId
    +Guid? EmailId
    +ClientProcessActionType ActionType
    +ClientProcessTaskState State
    +DateTimeOffset DueOn
    +string RenderedTitle
    +string RenderedInstructions
    +string RenderedEmailSubject
    +string RenderedEmailBody
    +string RenderedCallScript
    +string RenderedQuestionSet
    +string CompletionOutcomeKey
    +string CompletionNotes
    +DateTimeOffset? CompletedOn
    +string CompletedBy
}

Address "1" <-- "0..*" Company : RegisteredAddress

Client "1" --> "0..1" Company : Company
Client "1" --> "0..*" ClientContact : Contacts
Client "1" --> "0..*" ClientOpportunity : Opportunities
Client "1" --> "0..*" ClientActivity : Activities
Client "1" --> "0..*" ClientMaterial : Materials
Client "1" --> "0..*" Email : Emails
Client "1" --> "0..*" ClientHandoffPack : HandoffPacks
Client "1" --> "0..*" ClientProcessInstance : ProcessInstances
Client "1" --> "0..*" ClientProcessTask : ProcessTasks

ClientContact "1" <-- "0..*" ClientActivity : ClientContact
ClientContact "1" <-- "0..*" Email : SentToContact
ClientContact "1" <-- "0..*" ClientMaterial : SentToContact
ClientContact "1" <-- "0..*" ClientOpportunity : PrimaryContact

ClientOpportunity "1" <-- "0..*" ClientActivity : ClientOpportunity
ClientOpportunity "1" <-- "0..*" ClientHandoffPack : ClientOpportunity

ClientMaterial "1" <-- "0..*" ClientActivity : ClientMaterial
ClientMaterial "1" <-- "0..1" Email : Email

ClientProcessDefinition "1" --> "0..*" ClientProcessStep : Steps
ClientProcessDefinition "1" --> "0..*" ClientProcessInstance : Instances

ClientProcessStep "1" <-- "0..*" ClientProcessTransition : FromStep
ClientProcessStep "1" <-- "0..*" ClientProcessTransition : NextStep
ClientProcessStep "1" <-- "0..*" ClientProcessTask : Tasks
ClientProcessStep "1" <-- "0..*" ClientProcessInstance : CurrentStep

ClientProcessInstance "1" --> "0..*" ClientProcessTask : Tasks
ClientProcessInstance "1" --> "0..1" ClientProcessTask : CurrentTask

Email "1" <-- "0..*" ClientProcessTask : Email
```

## Reading Notes

- `Client` is currently the main aggregate root around which most of the model hangs.
- `Company` is effectively a one-to-one extension of `Client`.
- `ClientOpportunity` is a child collection under `Client`, not a separate aggregate root.
- `ClientActivity`, `ClientMaterial`, and `Email` together form most of the communication and collateral history.
- The newer process automation layer sits alongside the core relationship model:
  - `ClientProcessDefinition` -> `ClientProcessStep` -> `ClientProcessTransition`
  - `ClientProcessInstance` and `ClientProcessTask` attach that process to a `Client`
- `ClientProcessTask` can optionally point at an `Email`, which is how scheduled process-driven outreach is currently connected into the email workflow.
