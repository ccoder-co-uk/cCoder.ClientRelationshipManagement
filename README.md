# cCoder.ClientRelationshipManagement

`cCoder.ClientRelationshipManagement` contains the Client Relationship Management domain for the cCoder platform.

[View the latest main-branch code coverage report](https://ccoder-co-uk.github.io/cCoder.ClientRelationshipManagement/)

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

The Web and HostedServices executables bind the complete configuration root to
their own `AppConfiguration`. Each root contains the domain configurations the
host requires and is populated from appsettings plus environment-variable
overrides.

Composition is explicit at the app boundary. `CRMData` owns CRM persistence,
`SecurityData` owns SSO persistence, and the `CRM`, `Security`, and `AI`
sections configure business behavior. The CRM domain no longer accepts raw
connection strings or silently registers Security persistence.

Environment-variable paths use the standard double-underscore mapping:

- `CRMData__ConnectionString`
- `CRMData__AdminConnectionString`
- `CRM__AgentWorkflows__ExecutionUserId`
- `AI__Providers__open-ai__CompletionProvider__ApiKey`
- `AI__DefaultProvider`
- `SecurityData__ConnectionString`
- `SecurityData__AdminConnectionString`
- `Security__DecryptionKey`

`CRMData__AdminConnectionString` and
`SecurityData__AdminConnectionString` are optional migration-only overrides.
When omitted, startup migration uses the corresponding regular connection;
normal runtime operations always use the regular connection. CRM-owned
workflow, routing, import, authority-data, and mail settings remain beneath
`CRM`; former flat and `ConnectionStrings` aliases are not part of the current
configuration contract.

## Local AI Dependency

The domain library consumes the published `cCoder.AI` package. CRM owns agent
workflow selection and named-routing profiles; `cCoder.AI` owns provider composition
and the `AI` configuration section.

## Package

The main package produced by this repository is:

- `cCoder.ClientRelationshipManagement`