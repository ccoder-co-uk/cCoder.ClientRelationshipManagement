# CRM controller contract wave B

Wave B covers the following controllers without changing entity types, workflow activity types, or namespaces:

- `ProcessController`: 13 web actions, each with explicit classified exception handling;
- `ProcessInstancesController`;
- `ProcessStepsController`;
- `ProcessTasksController`;
- `ProcessTransitionsController`;
- `ProcessStepTaskAttemptsController`;
- `ProcessStepTaskRunsController`;
- `ProcessStepTasksController`;
- `ActivitiesController`;
- `SourcesController`;
- `EmailsController`.

The ten API controllers each expose six actions (`Get` collection, `Get` by key, `Post`, `Put`, `Patch`, and `Delete`), for 60 API actions. `ProcessDefinitionsController` is intentionally excluded because PR #19 already owns it.

`CrmWaveBControllerContractTests` provides executable inventory protection and deterministic runtime coverage for every API action. It verifies successful completion, validation, authentication, authorization, unexpected dependency failures, and direct not-found rejection behavior. `ProcessControllerContractTests` directly invokes every web action and proves that each action catches and classifies validation, authentication, authorization, and unexpected dependency failures.

The response and logging contract is:

- successful completion logs Information;
- safe validation failures return 400 and log Warning;
- safe authentication failures return 401 and log Warning;
- authorization and security failures return 403 and log Warning;
- unexpected failures return a detail-free 500 and log Error;
- direct 4xx results log exactly one Warning and no success log.
