# ADR-0011: Keep READ COMMITTED and serialize concurrent scenarios with pessimistic locks

- Status: accepted (2026-08-28, accepted by the human in chat)
- Date: 2026-08-28
- Context: retroactive record. The model took shape in
  the identity feature (refresh-session rotation, its backend plan §3.3).

## Problem

Optimistic concurrency (ADR-0010) protects against a lost update of *one
row*, but does not cover concurrent "check-then-act" scenarios over a *set*
of rows: two operators simultaneously pick a "free" record from a pool —
both find the same one; two records with overlapping ranges pass the
overlap check at the same time — both get inserted; two requests
simultaneously rotate the same refresh session. A model is needed: an
isolation level plus a way to serialize such scenarios, the same for every
slice.

## Options considered

### Option 1: SERIALIZABLE + transaction retry on serialization failure (40001)

- Pros: declarative — the RDBMS finds conflicts itself, no explicit locks
  in the code; correctness does not depend on whether the slice's author
  remembered the race.
- Cons: every use case must be a wholly retriable delegate (error 40001 is
  a normal outcome, not a failure), i.e. retry wrapping with all the
  problems of re-executing Application logic; under load, the rate of
  serialization failures is unpredictable; for the "hand each competitor
  their own row from the pool" scenario, SERIALIZABLE produces aborts where
  `SKIP LOCKED` hands out different rows with no conflict at all.

### Option 2: READ COMMITTED + targeted pessimistic locks + DB constraints

- Pros: Postgres's default level — no global cost for isolation;
  serialization happens only where there really is a race, and with
  exactly the primitive that matches it; `FOR UPDATE SKIP LOCKED` hands
  competitors different rows instead of waiting/aborting; invariants
  expressible as a DB constraint are held by the DB itself — the last line
  of defense does not depend on code at all.
- Cons: requires the slice's author to *see* the race and pick the right
  primitive — the model must be known (this ADR is exactly that record);
  locks are not expressible in LINQ — raw-SQL escape hatches are needed
  (ADR-0009).

### Option 3: optimistic only (`xmin`), no locks

- Pros: nothing extra is added.
- Cons: does not solve the problem — the "both picked the same free row"
  race turns into a conflict on `SaveChanges` for the loser at best, and
  into a double issue at worst (when competitors write *different* rows,
  as with two overlapping ranges); retrying it would have to happen at the
  level of the whole use case.

## Decision

Option 2. Echelons, from cheap to heavy — each next one applies only if the
previous one cannot express the invariant:

1. **A DB constraint** — for invariants expressible declaratively: an
   exclusion constraint on overlapping ranges, unique indexes. A violation
   is mapped by SQLSTATE into a business exception
   (`23P01`/`23505` → `ConflictException`).
2. **Optimistic `xmin`** (ADR-0010) — for ordinary editing of a single
   entity; works everywhere by default, nothing to do.
3. **`SELECT … FOR UPDATE`** — when competitors must queue up for the same
   row (refresh-session rotation: `Identity/RefreshSessionStore`).
4. **`SELECT … FOR UPDATE SKIP LOCKED`** — when competitors pick from a
   pool and must get *different* rows without waiting on each other.

The isolation level is READ COMMITTED (Postgres's default); slices are
forbidden from raising it without a new ADR. Transactions are explicit and
short: `IUnitOfWork.ExecuteInTransactionAsync`, or a repository that opens
a transaction with `BeginTransactionAsync` and commits it within the same
HTTP request (Scoped lifetime; `DisposeAsync` rolls back anything
uncommitted, so a lock never outlives the request). A transaction never
crosses an HTTP request boundary and never contains external I/O
(ADR-0013).

## Consequences

- Locks are not expressible in LINQ — the sanctioned escape hatch is
  parameterized raw SQL (ADR-0009); for `xmin` in manual SELECTs, see
  ADR-0010, "Consequences".
- Explicit (user-initiated) transactions are incompatible with EF's
  retrying execution strategy — this is the direct basis of ADR-0012.
- At the plan stage, the slice's author must name the concurrent scenario
  and pick the echelon — "which races are possible and which of the four
  echelons closes them" is part of the backend-stage plan and of the code
  review checklist.
- Worth revisiting if a class of scenarios appears where targeted locks
  spread across many tables of one transaction (deadlock risk and, in
  effect, a "manual SERIALIZABLE") — then compare against option 1 again,
  on a real scenario.
