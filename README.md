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

Before running locally, set:

- `ConnectionStrings__CRM`
- `ConnectionStrings__CRMAdmin`
- `ConnectionStrings__SSO`
- `Settings__DecryptionKey`

## Local AI Dependency

`ClientRelationshipManagement.Web` currently depends on a local sibling checkout of `cCoder.AI` at:

- `..\..\..\cCoder.AI\src\cCoder.AI\cCoder.AI.csproj`

This keeps the repository aligned with the current local code while `cCoder.AI` is reviewed and brought up to date for normal reuse. If that dependency shape changes later, this repo should be updated to consume it through the agreed package or repo flow instead of a machine-specific path.

## Package

The main package produced by this repository is:

- `cCoder.ClientRelationshipManagement`
