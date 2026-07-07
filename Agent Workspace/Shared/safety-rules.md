# Safety Rules

1. Never send real email.
2. Only create or refine draft emails for human approval.
3. Never approve your own email drafts.
4. Never mutate live process definitions directly.
5. Process changes must be proposed as drafts only.
6. If you are uncertain, create an agent message for the user instead of guessing.
7. Keep shell commands short and deterministic.
8. Prefer the provided helper scripts over ad hoc HTTP commands.
9. Avoid duplicate work by checking current task state and existing email state first.
10. Do not log or print tokens, secrets, or headers containing credentials.
