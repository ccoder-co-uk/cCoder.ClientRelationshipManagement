# CRM standardisation roadmap

## Quality contract

The current MVC routes, views, forms, and interaction design are compatibility requirements. Standardisation changes dependency direction, not the user experience. Every slice must retain its existing acceptance tests and add a contract test proving the UI and authenticated API observe the same operation and result.

The target dependency direction is:

`MVC or OData exposer -> orchestration/processing/foundation service -> broker -> resource`

MVC is a human exposer and OData is an agent/programmatic exposer. Neither owns persistence queries, authorization rules, audit behavior, workflow transitions, or data repair logic.

## Implemented architecture

Persistence now uses `ClientRelationshipDbContext` with one partial model file per entity. MVC, OData, hosted services, and coordination services consume entity orchestrations; only single-entity storage brokers access the scoped context. The obsolete generic controllers/services/brokers, audit base class, and grouped storage brokers have been removed.

`ArchitectureBoundaryTests` enforces these boundaries and requires a concrete controller, broker, foundation, processing service, orchestration, and CRUD lifecycle event path for every operational entity.

## Migration order

1. Approval conversations — complete. UI and OData share query, reply, response, and state-change operations.
2. Workflow tasks and process definitions — highest agent leverage and the largest source of repair work.
3. Emails and email evidence — approval and delivery must use semantic actions.
4. Leads and company discovery — shared query specifications plus lead lifecycle commands.
5. Opportunities and clients — shared pipeline transitions, activities, materials, and handoff commands.
6. Imports and sources — resumable import commands and observable job state.
7. Dashboard, agent runs, and automation settings — shared operational read models and configuration commands.
8. Background services — replace web-owned persistence with the same domain services and system execution context.

## Completion criteria

- No MVC, OData, or hosted exposer references `PlatformDbContext` or its factory.
- Every operational entity is either exposed through authorised CRUD or explicitly documented as command-only/read-only.
- Relationship-derived tenant access is enforced in services, never inferred by an agent.
- UI operations have equivalent documented OData CRUD/actions where appropriate.
- The transitional `/Api/AgentData` controller and repair-script guidance are removed.
- Duplicate legacy CRM models and dormant layers are migrated or deleted.
- Architecture, authorization, metadata/OpenAPI, UI/API parity, and regression suites pass.
