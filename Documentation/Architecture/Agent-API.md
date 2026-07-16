# CRM API architecture for user-delegated agents

## Purpose

The CRM API is the sole supported data and action boundary for an Approval Agent. An agent must not connect directly to SQL or create repair scripts. It sends the same SSO bearer token as the requesting user and therefore receives exactly that user's readable and writeable tenant scope.

## Dependency direction

The implementation follows The Standard used by the other cCoder domains:

`OData exposer -> entity orchestration -> entity processing -> entity foundation -> single-entity broker -> ClientRelationshipDbContext`

- Exposers translate HTTP/OData only.
- Services enforce authentication, tenant ownership, validation, audit fields, and workflow rules.
- Brokers contain thin persistence operations and no business decisions.
- No controller or hosted service may access `ClientRelationshipDbContext` directly.

The operational `Platform.Models.Entities` model is canonical. The obsolete parallel CRM entity, broker, service, migration, and singular-controller stack has been removed.

## Discovery

- OData service document: `/Api/ClientRelationshipManagement`
- OData schema: `/Api/ClientRelationshipManagement/$metadata`
- OpenAPI document: `/swagger/ClientRelationshipManagement/swagger.json`
- Interactive Swagger UI: `/swagger`

The EDM describes every table in the operational CRM model. Every set has a concrete OData controller and its own broker, foundation, processing, orchestration, and CRUD-event path. Relationship-owned creation remains fail-closed when an aggregate-level semantic operation is required.

Successful creates, updates, and deletes publish `<entity>_add`, `<entity>_update`, and `<entity>_delete` through `cCoder.Eventing`, carrying the requesting SSO identity. `AgentMessages` retains the semantic `Reply`, `Respond`, and `ChangeState` actions; workflow-sensitive callers must use those actions rather than emulate state transitions with raw PATCH requests.

## Authentication and authorization

Send `Authorization: Bearer <SSO token>` on every request. The security middleware resolves the SSO identity; `AuthorizationBroker` resolves `client_read` and `client_write` tenant grants. Services always add tenant/user predicates themselves. Client-provided `$filter`, `$expand`, keys, or body values can narrow this scope but can never broaden it.

Creation stamps ownership and audit values from authenticated context. Updates cannot move a record between tenants. Relationship-owned records must derive their access through the owning aggregate before their endpoint is enabled.

## Agent operating rules

1. Read `$metadata` and OpenAPI rather than guessing routes or fields.
2. Query the smallest useful projection with `$select`, `$filter`, and `$expand`.
3. Use normal POST, PUT/PATCH, and DELETE operations through the documented API.
4. Treat 401 as an invalid/missing SSO session and 403 as insufficient user access; never work around either result.
5. Prefer domain actions for state transitions once defined. Direct CRUD must not bypass a workflow invariant.
6. Report an unavailable capability as an API gap. Do not generate SQL or one-off PowerShell to mutate data.

## Implementation status

The typed OData context exposes query and CRUD routes for every operational entity set. `AgentMessages` additionally provides `Reply`, `Respond`, and `ChangeState`; these are the canonical equivalents of the UI's reply, approval/rejection/dismissal, and resolve/reopen operations. Other workflow-sensitive changes continue to use their documented semantic operations rather than bypassing invariants with raw creation.
