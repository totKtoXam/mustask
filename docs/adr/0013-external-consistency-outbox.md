# ADR-0013: Reconcile the DB and external services via intent statuses and an outbox, not distributed transactions

- Status: accepted (2026-08-28, accepted by the human in chat)
- Date: 2026-08-28
- Context: a **proactive** decision (unlike the retroactive ADR-0009–0012):
  there are no integrations in the code yet, but the very first task that
  exchanges data with an external system will hit the problem below. The
  pattern is fixed before every integration solves it its own way.

## Problem

The scenario of any integration: a local write → a call to an external
service → a local write of the result (`SaveChanges → RequestExternalService
→ SaveChanges`). Failure is possible at every point: the external call
fails — the first write is already committed and there is nothing to "roll
it back" with; the second `SaveChanges` fails — the external effect has
already happened but is not recorded on our side; the process itself
crashes — the system does not know which step it stopped at. The DB and the
external service are two independent stores; an atomic write to both does
not physically exist. A single pattern is needed for how every integration
slice survives these failures.

## Options considered

### Option 1: the external call inside a DB transaction

- Pros: a failed external call rolls back the local write — half the
  problem disappears syntactically.
- Cons: locks and the connection are held for the duration of a network
  call to an external service that answers in seconds — under the ADR-0011
  model this strangles concurrency; the other half of the problem remains,
  mirrored: the external call *succeeded*, but the commit after it fails —
  the counterparty has the effect, we have no record of it. The window is
  not closed, only hidden.

### Option 2: a distributed transaction (2PC)

- Pros: atomicity of both stores, in theory.
- Cons: practically unavailable — external services over HTTP/SOAP do not
  support the two-phase commit protocol; the coordinator becomes a single
  point of failure; the option is technically dead, recorded here for
  completeness.

### Option 3: compensation "head-on", without durable state

- Pros: minimal machinery — step 2 fails, step 1 is rolled back by hand
  with a second request.
- Cons: works until the first process crash between the failure and the
  compensation — after a restart the system has no idea what to fix: the
  fact "we are mid-operation" is recorded nowhere.

### Option 4: intent statuses + outbox + idempotency

- Pros: every point of failure leaves the system in a recorded, recoverable
  state; retries and compensations are managed and visible; the window of
  inconsistency is not denied but shown (a "sending" status); the entire
  mechanism is ordinary local transactions, already covered by ADR-0011.
- Cons: eventual consistency — there is a visible window between "accepted
  on our side" and "confirmed by the counterparty"; an outbox table, a
  background dispatcher, reconciliation of stuck records and idempotency
  keys are all needed — a cost paid in code.

## Decision

Option 4. An operation with an external effect is split into three steps,
each an ordinary local transaction:

1. **Intent.** A domain transition into an intermediate status
   (`Pending`/"sending") + a record in the outbox table (what to send, to
   whom, the idempotency key) — in one transaction, atomic by definition.
   The first write is not "half an operation" but full-fledged durable
   state.
2. **Delivery.** The external call is made by a background dispatcher that
   reads the outbox under `FOR UPDATE SKIP LOCKED` (echelon 4 of ADR-0011)
   — **outside** any transaction that wraps business data. A failure means
   a retry with backoff on the dispatcher's schedule; the idempotency key
   guarantees a retry does not double the effect at the counterparty.
3. **Result.** On the response — a transaction transitioning to `Confirmed`,
   or, once retries are exhausted, to `Failed` with a compensating domain
   transition (return the resource to the pool, release the reservation) —
   also an ordinary local transaction.

A hard rule that makes option 1 syntactically impossible: **external I/O
inside a DB transaction is forbidden** — no HTTP calls inside
`ExecuteInTransactionAsync` or between `BeginTransactionAsync`/`Commit`.
The rule is recorded in `docs/conventions/backend.md`, "EF Core", and
checked at code review.

Incoming calls from counterparties (callbacks, receipts) follow the mirror
rule: deduplication by the idempotency of their messages; redelivery of the
same event does not produce a second domain transition.

This ADR fixes the **pattern**, not the protocols: the outbox table format,
the concrete status models, retry schedules and each counterparty's
agreement are defined in the integration features that reference this ADR
— otherwise the ADR would go stale with the first exchange agreement.

## Consequences

- Eventual consistency becomes a property of the contract: the UI must be
  able to show intermediate statuses, and the integration feature's spec
  must describe them; there is no such thing as an "instant" integration,
  and it cannot be promised.
- The first integration feature sets up the outbox table, the dispatcher
  and reconciliation of stuck `Pending` records (a metric/report) — that is
  its scope, not "extra work".
- Delivery retries live in the dispatcher, not in EF — consistent with
  dropping `EnableRetryOnFailure` (ADR-0012); the dispatcher is a consumer
  of the ADR-0011 locking model.
- Compensating transitions are part of the domain model (entity state
  machines), designed together with the happy path, not added afterward.
- Worth revisiting if a counterparty appears with real transactional
  semantics on its own side (an exactly-once queue, a saga protocol) — for
  it, steps 2–3 might simplify; that would be a separate decision of the
  feature, not a repeal of the pattern.
