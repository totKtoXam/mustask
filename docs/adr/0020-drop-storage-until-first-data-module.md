# ADR-0020: Remove MUSTASK's storage from the system until the first module with data appears

- Status: superseded by [ADR-0021](0021-localized-dictionaries-pattern.md)
  (was: accepted by the human in chat, 2026-09-08)
- Date: 2026-09-07
- Context: the architecture stage of the series that moved the system onto
  Keycloak — a consequence of [ADR-0018](0018-keycloak-as-identity-provider.md)
  (dismantling the in-house identity contour), uncovered during review

## Problem

After moving authentication to Keycloak and tearing down identity, nothing
is left in the MUSTASK database. This is not a figure of speech: `MustaskDbContext`
has exactly four `DbSet`s — `Users`, `Roles`, `Permissions`,
`RefreshSessions` — and all of them leave; the single `InitialCreate`
migration creates six identity tables. Nothing else the system did at the
time stored data of its own: what remained either called an external
service or kept a small inbox in process memory.

The question: keep PostgreSQL, EF Core, migrations and Testcontainers for
the sake of a schema with not a single table — until the first module
appears that actually stores something?

## Options considered

### Option 1: keep the empty database and all its harness

- Pros: bringing back a module with data requires restoring nothing;
  ADR-0002, 0009, 0010, 0011, 0012, 0013 keep their subject and continue to
  be checked by convention tests.
- Cons: a container in the stand, Testcontainers on every integration test
  run, and a migration runbook — all of it for the sake of emptiness;
  "database schema is not created" at startup (ADR-0002) stays a ritual
  with no content.

### Option 2: remove it until the first module with data (chosen)

- Pros: the stand is lighter by one container, integration tests stop
  requiring Docker and get faster, the documentation drops what does not
  exist; the system's composition honestly reflects what it currently
  does — store nothing of its own.
- Cons: bringing storage back is size-L work (an infrastructure project, a
  context, a baseline migration from scratch, Testcontainers again); the
  risk of the temptation "I'll just keep it in memory for now" for state
  that deserves a table (an in-memory inbox already lives that way).

### Option 3: keep the database but remove Testcontainers

- Cons: a half-measure — the container stays in the stand, while
  integration tests lose the ability to verify data access, i.e. exactly
  the part that is hard to restore disappears.

## Decision

**Option 2** is chosen (decision by the human, 2026-09-07). Together with
identity, the following leave the system: `MustaskDbContext` and its
migrations, the EF harness of `Mustask.Infrastructure`, the `postgres` resource
in AppHost together with the `mustask-db-password` secret, the Testcontainers
infrastructure of the integration tests, and the database health check.

What this does **not** repeal: ADR-0002 (explicit migrations), ADR-0009
(EF Core), ADR-0010 (`xmin`), ADR-0011 (READ COMMITTED with pessimistic
locks), ADR-0012 (no retry), ADR-0013 (outbox). These decisions stay in
force — they simply have no subject while there is no storage; when the
database comes back they apply as-is and are not discussed again.

`docs/runbooks/local-migrations.md` is kept with a note "stale while the
system has no storage" — it returns together with the database.

## Consequences

- **Simpler:** the stand is one container lighter; `dotnet test` stops
  depending on Docker being up; a fresh clone needs one secret less.
- **Harder:** the first module with data starts not with a migration but
  with restoring the data-access layer; this must be factored into its
  estimate.
- **A risk named explicitly:** without a database at hand, state starts
  being kept in process memory. An in-memory inbox is already such a case,
  and it loses data on restart; it must disappear, not settle in as an
  example to follow.
- **When to revisit:** any module that stores data. Then the infrastructure project, the context and the baseline
  migration come back — as a separate size-L task.
