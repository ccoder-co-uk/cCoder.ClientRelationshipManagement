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

The list contains runnable work only. Explicit Approval tasks and contact tasks already awaiting approval are withheld. It is prioritised for automation:

1. non-contact research, enrichment, review, question, and wait tasks,
2. opportunity and client work,
3. relationship work,
4. lead qualification work,
5. outbound email, call, and meeting preparation.

Use `limit=1` when processing a single task safely.

### `GET /Api/AgentWorkflow/Tasks/{processTaskId}/EmailEvidence`

Returns the latest sent email context, mailbox freshness, and deterministically matched inbound reply candidates for a task.

- `hasMatchingEvidence=true` means the LLM must read the candidate body and decide whether it is genuinely positive, negative, a question, an automated message, or unrelated. A match is not itself proof of interest.
- `noEvidenceConfirmed=true` means the task is due or overdue, a sent outbound email exists, and a fresh mailbox sync completed after both the send and due time without finding a matching reply.
- if both values are false, do not infer a reply or no-reply outcome; the mailbox is stale or there is no sent message to assess.

Use `Get-TaskEmailEvidence.ps1` before completing response-review tasks and when an overdue contact task needs a truthful no-contact progress outcome.

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

For response-review tasks, the API enforces evidence: positive/negative response outcomes require a matched inbound candidate, while `no-reply` requires `noEvidenceConfirmed=true`.

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

### `GET /Api/AgentWorkflow/Messages`

Returns open Approval Agent conversations, including their human and agent entries, source process/task/email identifiers, and any proposed process version.

### `POST /Api/AgentWorkflow/Messages/{messageId}/Entries`

Adds the Approval Agent's next question, diagnosis, or proposed resolution to a conversation. Never claim that a live process changed merely by adding a message.

### `POST /Api/AgentWorkflow/Messages/{messageId}/ReplacementEmailDraft`

Creates a corrected replacement for the rejected email attached to a conversation. CRM preserves the rejected source as audit evidence, infers and stores an unambiguous opportunity and active process definition when old provenance is missing, and creates a separate human approval item for the replacement. The submitted body must be recipient-ready; CRM refuses internal headings such as `Lead with:` and `Avoid leading with:`.

Use `Create-ReplacementEmailDraft.ps1` after diagnosing an email rejection and agreeing the corrected copy with the user. Add a final conversation entry explaining what was repaired, then leave the conversation pending for the user to approve the solution with the Resolve control. A replacement remains a draft until a human approves it.

### `GET /Api/AgentWorkflow/Messages/{messageId}/RelatedDraftEmails`

Resolves and stores the rejected email's source process step, then returns every pending, unsent email produced by that same step across the process family. The response includes the live step key and templates plus each affected draft's id, company, recipient, state, and internal-guidance validation result.

Use `Get-RelatedDraftEmails.ps1 -ConversationId <agent-conversation-id>` when a human asks whether the correction should apply to other drafts from the same source. The value is the Agent Message/conversation `id`, not its `emailId`. Do not guess from subject similarity or search unrelated tenant mail.

The response includes an `approvedCorrection` when CRM can find a human-approved, sending, or sent replacement created from this rejection. It also includes the current live template rendered for the source company and `liveTemplateMatchesApprovedCorrection`. That boolean compares the approved reference copy with what the live process would generate now; matching peer drafts alone does not mean the defect is fixed.

### `POST /Api/AgentWorkflow/Messages/{messageId}/RefreshRelatedDraftEmails`

Re-renders eligible Draft, Approved, or Failed emails from the current live source-step template. Only changed emails are updated, every updated email is reset to Draft, and sent or currently-sending emails are never touched. CRM records the batch result as a System entry in the conversation.

Use `Refresh-RelatedDraftEmails.ps1 -ConversationId <agent-conversation-id>` only after the human explicitly asks to apply an already-approved correction and `liveTemplateMatchesApprovedCorrection` is `true`. The API returns `409 Conflict` if an approved correction differs from the live template. In that case create a process draft proposal for the returned exact step instead. Human approval and activation of that proposal will migrate active work and recreate its unsent drafts from the approved template.

### `GET /Api/AgentWorkflow/Processes/Metrics`

Returns process totals plus an ordered `steps` collection for each process. Step metrics include pending, overdue, completed, cancelled, completions without evidence, average turnaround minutes, and the oldest pending timestamp. Use these figures to identify a specific bottleneck; do not infer a process change from one anomalous task.

Returns conservative performance metrics for active process definitions.

### `GET /Api/AgentWorkflow/Mailbox/Sent/Reconciliation`

Returns CRM emails marked Sent with only credible Microsoft 365 Sent Items candidates, match scores and reasons, plus opportunities belonging to the same company relationship. It deliberately does not expose unrelated mailbox traffic.

Use `Get-SentEmailReconciliation.ps1` for one-off data consistency reviews. A score of 80 or more is required before CRM accepts a reconciliation.

### `POST /Api/AgentWorkflow/Emails/{emailId}/ReconcileSent`

Confirms a CRM email against a real Sent Items message and optionally links it to an opportunity belonging to the same company relationship. CRM re-fetches the mailbox evidence and validates the match rather than trusting the agent's assertion.

Use `Confirm-SentEmail.ps1` with the mailbox `externalId` returned by the reconciliation report.

### `POST /Api/AgentWorkflow/Emails/{emailId}/CancelUnverified`

Cancels a CRM email incorrectly marked Sent only when a fresh Sent Items query finds no credible match. The record is retained with an audit reason rather than deleted. If credible evidence exists the endpoint refuses cancellation.

Use `Cancel-UnverifiedEmail.ps1` only after reviewing the reconciliation report.

Use when:

- evaluating whether a live process appears ineffective,
- deciding whether to draft a small process improvement.

### `POST /Api/AgentWorkflow/Processes/{processDefinitionId}/DraftProposal`

Creates a draft version of a live process definition and a pending approval message for review.

Supply `agentMessageId` when the proposal concludes an existing Approval Agent conversation. The draft will be attached to that conversation for explicit human approval and activation.

Use for small changes only:

- template improvement,
- tone adjustment,
- instruction refinement,
- question-set improvement,
- conservative sequencing improvements.
