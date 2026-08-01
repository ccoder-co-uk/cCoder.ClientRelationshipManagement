# cCoder.ClientRelationshipManagement

`cCoder.ClientRelationshipManagement` contains the Client Relationship Management domain for the cCoder platform.

## Contents

- `src/cCoder.ClientRelationshipManagement`
  The main domain library package.
- `src/ClientRelationshipManagement.Web`
  The standalone web host for the domain.
- `src/ClientRelationshipManagement.HostedServices`
  Background and internal-hosted-service entry points for imports and agent workflows.
- `src/cCoder.ClientRelationshipManagement.Tests`
  Unit tests for the domain library.
- `src/ClientRelationshipManagement.AcceptanceTests`
  Acceptance tests for the standalone host.
- `Documentation`
  Business-process notes, pipeline guidance, and supporting marketing/domain material copied into the repo root.
- `Agent Workspace`
  Checked-in agent prompts and workspace assets. Runtime archives are intentionally ignored.

## Build

```powershell
dotnet build src/cCoder.ClientRelationshipManagement.sln -v minimal
```

## Test

```powershell
dotnet test src/cCoder.ClientRelationshipManagement.Tests/cCoder.ClientRelationshipManagement.Tests.csproj -v minimal
dotnet test src/ClientRelationshipManagement.AcceptanceTests/ClientRelationshipManagement.AcceptanceTests.csproj -v minimal
```

## Local Configuration

The web and hosted-services entry points read configuration from their local `appsettings.json` files, with secrets overridable through environment variables.

The CRM domain owns the top-level `CRM` section. AI provider configuration remains
under the separate top-level `AI` section owned by `cCoder.AI`. Environment-variable
paths use the standard double-underscore mapping, for example:

- `CRM__ConnectionString`
- `CRM__AdminConnectionString`
- `CRM__AgentWorkflows__ExecutionUserId`
- `AI__Providers__open-ai__CompletionProvider__ApiKey`
- `AI__DefaultProvider`
- `ConnectionStrings__SSO`
- `Settings__DecryptionKey`

The standalone hosts continue to accept `ConnectionStrings__CRM` and
`ConnectionStrings__CRMAdmin` as compatibility aliases for database tooling and
existing deployments. CRM-owned workflow, routing, import, authority-data, and mail
settings have moved from their former top-level sections beneath `CRM`; those former
flat paths are no longer bound.

## Local AI Dependency

The domain library consumes the published `cCoder.AI` package. CRM owns agent
workflow selection and named-routing profiles; `cCoder.AI` owns provider composition
and the `AI` configuration section.

## Package

The main package produced by this repository is:

- `cCoder.ClientRelationshipManagement`
