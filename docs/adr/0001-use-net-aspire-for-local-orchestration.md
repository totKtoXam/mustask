# ADR-0001: Use .NET Aspire for local orchestration

- Status: accepted (2026-08-22, confirmed by the human in chat at the
  gate-3 approve of the project-bootstrap feature)
- Date: 2026-08-21
- Context: the project bootstrap — the .NET solution scaffold (stage 4,
  backend)

## Problem

The backend consists of at least an API application and PostgreSQL, and
going forward will add integrations with external services (identity,
notifications, source control, chat) and possibly background processors. The developer and CI need a
single, repeatable way to bring up the whole stack locally: the database,
the service, and the connection between them via a connection
string/service discovery, without manual port setup or docker run
commands. The choice was set by the human in the project-bootstrap task
statement: use .NET Aspire.

## Options considered

### Option 1: .NET Aspire (AppHost + ServiceDefaults)

- Pros: native integration with the .NET SDK and `dotnet run`; orchestration
  is described in C# (`Mustask.AppHost`), not in a separate YAML — one
  language, one configuration repository; automatic passthrough of
  connection string/service discovery to dependent projects via
  `WithReference`; OpenTelemetry, health checks, and a dashboard for local
  debugging out of the box; the same primitives will be reused for
  integration tests (`Aspire.Hosting.Testing`) and for future integrations
  with external services (modeled as AppHost resources).
- Cons: younger than docker compose, the ecosystem and practices are still
  forming; an extra project (`Mustask.AppHost`) and SDK
  (`Aspire.AppHost.Sdk`) in the solution; requires Docker just like
  compose does (PostgreSQL still comes up in a container either way) —
  i.e. it does not remove this dependency, it just changes how containers
  are managed.

### Option 2: docker compose

- Pros: a widely known tool, independent of .NET; declarative YAML that
  everyone understands, including frontend developers and DevOps; not
  tied to a specific .NET SDK/version.
- Cons: the connection string and ports need to be synced manually between
  `docker-compose.yml` and the application configuration (`appsettings`/
  env); no unified dashboard/tracing out of the box — OpenTelemetry would
  have to be set up separately; two parallel places of configuration
  (compose + .NET DI) increase the risk of drift when adding new
  services.

## Decision

We use .NET Aspire: `Mustask.AppHost` orchestrates `Mustask.Api` and a PostgreSQL
container, `Mustask.ServiceDefaults` provides shared health checks/OpenTelemetry
for every service in the solution. A separate docker compose is not set
up — Aspire covers the local environment's needs and reuses the same API
for integration tests (`Aspire.Hosting.Testing`, where applicable) and as
the base for subsequent external integrations (ШЭП etc.) in the form of
separate AppHost resources. The decision was set by the human in the
project-bootstrap task statement; it is recorded here as an ADR for
subsequent features that will add new resources (queues, cache, external
services) to `Mustask.AppHost`.

## Consequences

- Local startup is via `dotnet run --project src/backend/Mustask.AppHost`, not
  `docker compose up`; `AGENTS.md`/README needs to describe this
  explicitly (this feature updates the "Commands" section of `AGENTS.md`).
- New external dependencies (Redis, queues, a second service) are added as
  `Mustask.AppHost` resources, not as lines in a separate compose file.
- CI, which does not need the Aspire dashboard, keeps running
  `dotnet test` directly against `Mustask.sln` — AppHost is used only for
  local development, integration tests bring up Postgres via
  Testcontainers independently of AppHost.
- The decision is worth revisiting if Aspire stops being developed/is
  superseded by another orchestration standard in the .NET ecosystem, or
  if orchestration not tied to .NET becomes necessary (for example, if
  services on a different stack appear that have no place in AppHost).
