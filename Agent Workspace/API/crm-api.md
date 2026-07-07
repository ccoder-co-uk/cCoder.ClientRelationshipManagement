# CRM Agent Workflow API

Base URL comes from the runtime environment variable:

- `CRM_AGENT_API_BASE_URL`

Authentication comes from runtime environment variables:

- `CRM_AGENT_EXECUTION_TOKEN`
- `CRM_AGENT_EXECUTION_USER_ID`

The helper scripts automatically send:

- `Authorization: Bearer {CRM_AGENT_EXECUTION_TOKEN}`

## Endpoints

### `GET /Api/AgentWorkflow/Tasks/Due?limit=25`

Returns due pending workflow tasks with the relevant company, contact, email template, current email state information, and the legal workflow outcomes for the current step.

The list is prioritised for automation:

1. opportunity and client work,
2. relationship work,
3. lead qualification work.

Use `limit=1` when processing a single task safely.

### `POST /Api/AgentWorkflow/Tasks/{processTaskId}/Complete`

Completes a pending workflow task using one of the legal outcome keys returned by `GET /Api/AgentWorkflow/Tasks/Due`.

Payload:

```json
{
  "outcomeKey": "researched",
  "completionNote": "Summary of what was done and why."
}
```

Use when:

- the task can be safely progressed without human approval,
- you have enough evidence to choose one of the legal outcomes,
- you want the workflow engine to create the next step.

### `POST /Api/AgentWorkflow/Tasks/{processTaskId}/DraftEmail`

Creates or updates a draft email linked to a process task and creates a pending approval message for the user.

Use when:

- the process task is an outreach email task,
- the draft does not yet exist,
- the draft needs refining before the user reviews it.

### `POST /Api/AgentWorkflow/Leads/{leadId}/Research`

Updates the researched lead details before you complete a lead task.

Payload fields are optional and may include:

- `rawCompanyName`
- `rawTradingName`
- `rawCompanyNumber`
- `rawVatNumber`
- `rawWebsiteUrl`
- `rawContactEmailAddress`
- `rawContactPhoneNumber`
- `rawAddressText`
- `qualificationNotes`
- `contactName`
- `contactPosition`
- `contactEmailAddress`
- `contactPhoneNumber`
- `contactLinkedInUrl`

Use when:

- lead qualification research discovered better company details,
- you found a useful contact,
- you want those findings stored before the workflow advances.

### `POST /Api/AgentWorkflow/Messages`

Creates or updates a user-facing agent message.

Use when:

- more information is needed,
- a decision is needed,
- you want to explain why you did or did not progress a task,
- you want to ask the user for clarification.

### `GET /Api/AgentWorkflow/Processes/Metrics`

Returns conservative performance metrics for active process definitions.

Use when:

- evaluating whether a live process appears ineffective,
- deciding whether to draft a small process improvement.

### `POST /Api/AgentWorkflow/Processes/{processDefinitionId}/DraftProposal`

Creates a draft version of a live process definition and a pending approval message for review.

Use for small changes only:

- template improvement,
- tone adjustment,
- instruction refinement,
- question-set improvement,
- conservative sequencing improvements.
