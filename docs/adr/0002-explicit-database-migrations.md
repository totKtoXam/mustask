# ADR-0002: Apply EF migrations as an explicit step, not at Api startup

- Status: accepted
- Date: 2026-08-21; accepted by the human in chat on 2026-08-23
- Human decision at acceptance: a separate migrator is not set up now — the
  task stays in the backlog (a CI migrator step), migrations are
  applied manually until stage/prod environments appear.
- Context: the PostgreSQL migrations task — the DB layer (stage 4,
  backend)

## Problem

A mechanism is needed to bring the PostgreSQL schema in line with the EF
model: who runs `Database.Migrate()`, and when. There are three options:
auto-migration on every `Mustask.Api` startup, a separate migrator (a
deployment/CI step), or only a manual `dotnet ef database update` run by
the developer. MUSTASK is a multi-user system whose data outlives releases: DB
schema changes must be a predictable, review-able, reproducible action,
not a side effect of starting yet another instance of the application. The
decision is made now, while there are no migrations or stage/prod
environments yet, so it does not have to be rewritten retroactively once
the first business migration appears.

## Options considered

### Option 1: auto-migration at `Mustask.Api` startup (`Database.Migrate()` in `Program.cs`)

- Pros: zero manual step for the developer; in the Aspire scenario
  everything comes up with a single command
  (`dotnet run --project src/backend/Mustask.AppHost`).
- Cons: in prod with multiple Api instances — a migration race on
  simultaneous startup; the schema changes as an invisible side effect of
  a deploy/restart rather than a separate, deliberate action — bad for
  audit and incident analysis ("who changed the schema and when" gets
  lost among the application's startup logs); the Api's DB role needs DDL
  rights (`CREATE`/`ALTER TABLE`) permanently, not only at migration
  time — excess privileges for a runtime process; rolling back a
  migration after a failed deploy gets harder, since it is tied to the
  application process's lifecycle.

### Option 2: a separate migrator (a dedicated CI/CD step or console project)

- Pros: an explicit, reproducible, audit-able action; the Api's DB role
  can have no DDL rights; the migration and the Api deploy are separated
  in time and can be checked/rolled back independently; fits well with a
  future stage/prod pipeline.
- Cons: for the current stage (local dev only, `stage`/`prod` out of
  scope) a separate console project is an extra entity in the solution
  for environments that don't exist yet.

### Option 3: only a manual `dotnet ef database update`

- Pros: minimal code; the developer has full control over when it is
  applied.
- Cons: gives no reusable programmatic mechanism for tests and a future
  CI/ops step — tests and the pipeline should not call an interactive
  CLI; easy to forget to run before checking behavior.

## Decision

A combination: the mechanism is `MustaskDbContext.Database.MigrateAsync()`,
called **explicitly**, not automatically in `Program.cs` at `Mustask.Api`
startup. For local development — `dotnet ef database update` (a
design-time factory already exists, AppHost is not needed). For tests and
future CI/ops scenarios — the same `Database.MigrateAsync()` is called
directly from code that is explicitly responsible for it (currently — the
`Mustask.Infrastructure.Tests` integration tests), not from `Mustask.Api`'s
constructor/startup. This way `Mustask.Api` does not get the privileges and
responsibility to change the DB schema just by virtue of starting up.

A separate console migrator project (option 2) is not set up now — it
would be premature with no stage/prod, but the decision leaves a ready
building block for it: `Database.MigrateAsync()` is already separated
from Api and reusable; wrapping it in a CLI/CI job will be possible
without revisiting this ADR.

## Consequences

- `Mustask.Api/Program.cs` must not get `Database.Migrate()` without
  revisiting this ADR — if it becomes necessary, that is a separate
  decision (superseding ADR).
- Local workflow: after `dotnet run --project src/backend/Mustask.AppHost`
  (brings up an empty DB) the developer runs
  `dotnet ef database update --project src/backend/Mustask.Infrastructure`,
  passing the current `ConnectionStrings__mustask` (Aspire assigns the
  Postgres container a random port and password on every run — there is
  no fixed value). The full procedure for obtaining this string and the
  command are documented in `AGENTS.md`.
  (Previously `--startup-project src/backend/Mustask.Api` was specified here —
  it does not work: `Mustask.Api` does not reference
  `Microsoft.EntityFrameworkCore.Design`, the command fails with a
  missing package error; found by that task's QA, D-12.)
- The DB role used by `Mustask.Api`'s runtime may, in future environments, be
  narrowed to DML without DDL — migrations are applied by a separate,
  more privileged role/step.
- A future task (backlog, once the first stage/prod environment appears):
  set up a dedicated migration-application step in CI/CD (a console
  migrator project or a job calling `dotnet ef database update` / the
  same `MigrateAsync()`) — see `docs/backlog.md`.
- The decision is worth revisiting if a justified need for a zero manual
  step in local development appears (for example, through an explicit
  Aspire lifecycle hook in `Mustask.AppHost` that calls `MigrateAsync()` once
  at orchestration startup, rather than from `Mustask.Api` itself) — that is
  a separate decision, not covered by the current ADR.
