# ADR-0012: Disable automatic retry of transient DB failures (EnableRetryOnFailure)

- Status: accepted (2026-08-28, accepted by the human in chat)
- Date: 2026-08-28
- Context: retroactive record (`DisableRetry = true` in
  `Mustask.Infrastructure/DependencyInjection.cs`); before it, Aspire's default
  with retries was in effect, and CR-24 was written in the identity
  feature as a wrapper around it.

## Problem

Aspire's Npgsql integration enables `EnableRetryOnFailure` by default: a
retrying execution strategy repeats a query or transaction that failed on a
transient error. But it **refuses to work with user-initiated
transactions**: `Database.BeginTransactionAsync()` outside
`CreateExecutionStrategy().ExecuteAsync(...)` throws an
`InvalidOperationException`. The model in ADR-0011 rests exactly on such
transactions: an explicit transaction wraps a lock (`FOR UPDATE [SKIP
LOCKED]`), the Application logic between the lock and the write, and the
commit — and it cannot be folded into one retriable delegate without
dragging the slice's domain logic into it. The default and the model
cannot coexist — one has to be chosen.

## Options considered

### Option 1: keep retry, wrap every scenario in `ExecuteAsync(delegate)`

- Pros: resilience to transient connection failures "out of the box" — a
  network drop to Postgres turns into an invisible retry instead of a 500.
- Cons: every use case with a transaction must be a wholly safely-retriable
  delegate, Application logic included; a retry after a rollback inherits
  the `ChangeTracker` state of the first attempt — this already needed the
  `ChangeTracker.Clear()` wrapper (CR-24 in `UnitOfWork`), with a scenario
  of silently taking the wrong branch analyzed there; every new slice
  carries this complexity tax, paying for a failure that has not been
  observed in the project's practice.

### Option 2: disable retry (`DisableRetry = true`)

- Pros: the explicit transactions of ADR-0011 work without ceremony; an
  entire class of "retry saw the first attempt's dirty state" bugs
  disappears; failure behavior is obvious — the query errors, an honest
  500, the client retries the action; no code in the project relies on
  auto-retry.
- Cons: a transient failure (a dropped connection, a Postgres restart)
  reaches the user as an error, though it could have been hidden by a
  retry; the decision is global for the whole `MustaskDbContext` — reads lose
  retry too, where it would have been safe.

### Option 3: two contexts — one with retry for reads, one without for writes

- Pros: formally "the best of both".
- Cons: a second context registration, a doubled connection pool, a "which
  context to use" rule in every slice — permanent complexity against a
  hypothetical benefit; premature with zero observed transient failures.

## Decision

Option 2: `settings.DisableRetry = true` when registering `MustaskDbContext` —
for the whole context, no exceptions. The concurrency model (ADR-0011)
matters more than automatic retry: it is needed for every concurrent
scenario right now, while retry protects against failures whose frequency,
under our conditions (DB and API on the same infrastructure, no unstable
network between them), is unmeasured and presumably low. Reliability of
external interactions is solved not by an EF retry but at the level of the
integration pattern (ADR-0013): its dispatcher retries delivery itself.

## Consequences

- The system deliberately lives without automatic resilience to transient
  DB failures: such a failure equals an error for that specific query;
  idempotency of a retried attempt is provided by the
  optimistic/pessimistic mechanisms of ADR-0010/0011, not by retry magic.
- `CreateExecutionStrategy()` in `UnitOfWork.ExecuteInTransactionAsync` now
  returns a non-retrying strategy — the CR-24 wrapper
  (`ChangeTracker.Clear()`) and its comment describe behavior that no
  longer exists. The runtime fix is not part of this docs task — a backlog
  line `tech-NNN-unitofwork-retry-comment` has been filed: remove the dead
  wrapper or rewrite the comment so it no longer claims retries exist.
- Bringing retries back requires a new ADR and data behind it: if
  structured logs show a noticeable rate of transient SQLSTATEs (57P01,
  08006 and relatives), consider the targeted option 3 for reads first,
  not a global re-enable.
- Tests that rely on "EF itself survives a container restart" must not be
  written — the behavior on a connection failure is always an error, and
  the test must pin exactly that.
