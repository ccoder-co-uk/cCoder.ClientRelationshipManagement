# Process step tasks

A process remains the business lifecycle and a step remains one visible business outcome. A step may contain an ordered task sequence when achieving that outcome requires several operations. Steps without explicit tasks retain the existing behaviour and are handled as one implicit inference task.

The initial task types are `Inference`, `Operation`, `Validation`, and `Flow`. Operation and validation tasks reference registered handler keys; they do not store arbitrary executable API calls. Tasks declare required and produced context keys, success and failure routing, and a bounded attempt limit.

Each company-level `ProcessTask` owns `ProcessStepTaskRun` records. Every execution is recorded as a `ProcessStepTaskAttempt`, including context, output, validation errors, and disposition. Inference retries are bounded. Exhaustion creates one correlated Approval Agent bottleneck conversation for the process step and task.

Email steps use the standard sequence:

1. Resolve and verify recipient.
2. Generate grounded recipient-facing content.
3. Validate recipient, subject, body, template tokens, placeholders, and internal guidance.
4. Persist the approval draft only after validation succeeds.

The process designer keeps tasks subordinate to their step. The normal view shows a task count; expanding a step shows task type, registered handler, retry limit, and routing. Proposal reviews identify the affected step and its task count so reviewers can see the execution context without turning internal tasks into top-level workflow stages.
