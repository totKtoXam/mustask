# ADR-0009: Use EF Core + Npgsql as the primary data access

- Status: accepted (2026-08-28, accepted by the human in chat)
- Date: 2026-08-28
- Context: retroactive record (following the pattern of ADR-0007). The
  decision effectively took shape in the project bootstrap and the
  PostgreSQL migrations task, and was exercised in the identity feature;
  this records its boundaries and cost.

## Problem

The .NET 10 backend works with PostgreSQL; it needs to be fixed exactly
what with — a full ORM, a micro-ORM, or bare ADO.NET — and where the
boundaries of the chosen tool lie. Forces: an Onion domain (aggregates with
invariants, writes only through the domain), mandatory schema migrations
(ADR-0002), optimistic concurrency as a baseline rule
(`docs/conventions/backend.md`), concurrent scenarios with row locks
(`FOR UPDATE [SKIP LOCKED]`), a nullable + warnings-as-errors culture. An
unrecorded choice raises the same question in every new slice — "can I use
Dapper here?" — producing inconsistency instead of a canon.

## Options considered

### Option 1: EF Core + Npgsql, with agreed escape hatches into raw SQL

- Pros: change tracking expresses the aggregate cycle "load → domain
  transition → SaveChanges" without hand-written SQL for every invariant;
  LINQ projections for reads; EF migrations is already the accepted schema
  mechanism (ADR-0002); a single metadata model that cross-cutting
  conventions (snake_case, `xmin` — ADR-0010) attach to automatically for
  every future entity; typed queries fail at compile time on a property
  rename (warnings-as-errors works on the data layer too).
- Cons: EF has areas it either cannot express at all (`FOR UPDATE SKIP
  LOCKED`) or expresses more expensively than hand-written SQL (bulk
  operations); it demands discipline on reads (projections, filtering in
  SQL) — without it, it quietly degrades into "load everything and filter
  in memory".

### Option 2: Dapper (+ a separate migration tool)

- Pros: full control over SQL, a minimal runtime layer, predictable read
  performance.
- Cons: aggregate invariants would have to be maintained with hand-written
  SQL for every transition (an UPDATE set for every field) — exactly the
  mechanics that is the reason writes in Onion go through the domain; no
  automatic conventions, no `xmin` tracking, no typed projections; a
  separate migration stack instead of the already-accepted ADR-0002.

### Option 3: EF Core for writes + Dapper for reads (two stacks at once)

- Pros: the "ideal" tool for each task.
- Cons: two mapping models for the same set of tables that must be kept in
  sync (snake_case names, enum→string conversions — twice); double the
  learning curve for every developer and agent; the benefit is
  hypothetical — there is no measured bottleneck in EF reads, and
  projections close off the main source of overhead (entity
  materialization).

## Decision

Option 1. EF Core + Npgsql is the sole data access stack:

- **writes** — through the domain model and change tracking (Infrastructure
  repositories), atomicity via `SaveChangesAsync` / explicit transactions
  (ADR-0011);
- **reads** — LINQ projections straight into the slice's Response rows,
  without entity materialization (rules in
  `docs/conventions/backend.md`, "EF Core");
- **escape hatches into raw SQL** — legal, but only through EF mechanisms
  on the same connection/transaction (`FromSqlInterpolated`, `SqlQuery`,
  `ExecuteSqlInterpolated`), only in Infrastructure and only where LINQ
  cannot express what is needed: row locks, bulk operations. This is not
  "EF everywhere" but "EF with agreed escape hatches" — the hatches are
  listed in the conventions.

Dapper (or a second mapper) is not introduced while the EF escape hatches
suffice: a second stack means a second mapping model, not just a second
library.

Tying to the PostgreSQL dialect is an acceptable, deliberate choice:
Postgres is part of the platform (`xmin`, exclusion constraints, `SKIP
LOCKED`), not a detail hidden behind an abstraction. "Portability to
another RDBMS" is not a requirement.

## Consequences

- Cross-cutting model rules live as code in one place — the
  `Mustask.Infrastructure/Conventions` conventions apply to every new entity
  with no per-entity setup; this underpins ADR-0010.
- Read discipline (projections, filtering before materialization, no lazy
  loading) is the mandatory price of option 1; it is recorded in
  `docs/conventions/backend.md`, "EF Core", and checked at code review.
- A second access stack (a targeted Dapper for a measured bottleneck) is a
  separate decision: a new ADR with a benchmark showing that an EF
  projection is actually the bottleneck, not merely assumed to be one.
- Worth revisiting if the system's profile shifts toward bulk
  reads/exports (reporting, registries with hundreds of thousands of rows)
  where the overhead of the EF pipeline becomes measurable — measure first,
  then write the ADR.
