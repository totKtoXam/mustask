# ADR-0010: Implement optimistic concurrency via `xmin` through an automatic convention

- Status: accepted (2026-08-28, accepted by the human in chat)
- Date: 2026-08-28
- Context: retroactive record. The decision is implemented in
  the PostgreSQL migrations task
  (`Mustask.Infrastructure/Conventions/OptimisticConcurrencyConvention.cs`),
  verified on the identity feature.

## Problem

The convention (`docs/conventions/backend.md`, "Database") requires
optimistic concurrency for editable entities — protection against lost
updates where "the last writer silently wins". Two questions are open:
**what** to store the version token in, and **how to guarantee** that
every new entity gets it, instead of depending on the memory of the
developer or agent setting up the slice.

## Options considered

### Option 1: an explicit version column on every table

- Pros: portable across RDBMSes; the version is an ordinary column that can
  be handed to the client (ETag/If-Match scenarios).
- Cons: DDL on every table and a configuration line for every entity; the
  version only "lives" if every write goes through it — a direct UPDATE
  bypassing EF does not increment it, and the protection silently
  disappears.

### Option 2: `xmin`, configured manually on each entity's configuration

- Pros: no DDL — Postgres maintains `xmin` on every row itself, and any
  write path (including raw SQL) updates it atomically with the write
  itself.
- Cons: "manually on every entity" is forgettable; a missing configuration
  line fails neither the build nor the tests — the entity is simply left
  silently unprotected. Exactly the class of defect we are protecting
  against.

### Option 3: `xmin` via an automatic model-finalizing convention

- Pros: everything from option 2 plus the impossibility of forgetting — the
  convention walks the finalized model and attaches a shadow token to every
  eligible entity type; a new slice gets protection without knowing it
  exists.
- Cons: the convention code must correctly skip special cases
  (view/function mappings, TPH, owned types on the owner's table) — this is
  complexity written once and covered by tests, in
  `Mustask.Infrastructure.Tests`.

## Decision

Option 3: the PostgreSQL system column `xmin` as the concurrency token,
wired in by `OptimisticConcurrencyConvention` — an
`IModelFinalizingConvention` registered in
`MustaskDbContext.ConfigureConventions`. The token is a shadow property (type
`uint`, column `xid`) that lands in SELECT projections and in the WHERE
clause of every UPDATE/DELETE through EF's regular pipeline. Skipped:
entities with no table (view/function/sql mappings — there is nowhere to
write, a view has no `xmin` of its own), non-root TPH types, and owned
types on the owner's table (a row has one `xmin`, the token is declared
once). The detailed reasoning for each skip is in the convention class's
comment.

The main reason for this choice: `xmin` is updated by Postgres itself,
atomically with any write — the protection cannot be bypassed by direct
SQL or by another process; and the convention mechanically closes the
second half of the problem — "how not to forget".

## Consequences

- Tying to PostgreSQL is an already-accepted cost (ADR-0009); this ADR is
  the first to be revisited if the RDBMS ever changes.
- **A manual `SELECT` of an entity must explicitly list `xmin`** — `SELECT
  *` does not include system columns, and materialization via `FromSql`
  without it fails. This has already bitten us:
  `Identity/RefreshSessionStore` lists `xmin` in its `FOR UPDATE` query.
  A working alternative pattern is to lock via raw SQL
  (`SqlQuery<Guid>` by id), then load the locked row with a regular
  tracking EF query (`EfRequestRepository.ReservePaperBlankAsync`): all the
  mappings, `xmin` included, arrive without listing columns by hand.
- `xmin` is never exposed externally (in the API) — it is an internal
  token, not a resource version. If an ETag/If-Match scenario is ever
  needed for clients, it needs an explicit version column on top; this ADR
  does not cover that — it would be a separate decision.
- A conflict surfaces as `DbUpdateConcurrencyException` on
  `SaveChangesAsync`; a slice for which the conflict is a business
  situation must catch it and throw a `ConflictException` with its own
  registry code (ADR-0004/0005); an uncaught conflict is an honest 500.
- The convention's behavior (skips included) is pinned by tests in
  `Mustask.Infrastructure.Tests` — the convention cannot change without a test
  failing.
