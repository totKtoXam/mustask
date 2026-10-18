# docs/adr/ — Architecture Decision Records

The project's log of architecture decisions. One file — one decision:
`NNNN-<kebab-slug>.md` (numbering is sequential: `0001-...`, `0002-...`;
a number is never reused, so a removed ADR leaves a gap — ADR-0017 is one),
template — `docs/templates/adr.md`. Language — English, like all artifacts.

## When to write an ADR

Mandatory if the decision:

- chooses a technology/library with long-term consequences (authentication,
  a message bus, API client generation, and the like);
- sets a pattern that every slice will repeat (error handling, audit,
  multi-language support, API versioning);
- departs from `docs/conventions/*` — the convention is then updated with a
  reference to the ADR;
- defines how to integrate with an external system;
- changes something irreversibly or expensively (the core data schema, the
  identifier format).

Not needed for: local decisions within a single slice, naming choices,
anything already fixed by the conventions.

## Process

1. The ADR is written by whoever makes the decision during the stage
   (usually the backend/frontend-developer at the plan stage, or the
   orchestrator). Model tier — deep: these are the "architectural
   decisions" from model routing.
2. A new ADR is created with status `proposed` and shown to the human at
   the nearest human gate (or immediately, if the decision blocks work).
   Only the human sets `accepted` — as with gates: either they edit it
   themselves, or they explicitly confirm in chat and the agent records it
   with a note.
3. An accepted ADR is not edited substantively. If the decision changes —
   a new ADR, and the old one gets `superseded by ADR-NNNN` with a link.
4. References to an ADR from code/docs — by number: `ADR-0001`. In a
   feature's `plan-*.md` and `design.md`, list the ADRs the implementation
   relies on.

## Index

Keep the list current (append-only, newest on top):

- ADR-0030 — Delete a work item into its Removed state with the way back on its row, hide it by named entry points, and purge it in one locked transaction · accepted (amends ADR-0029 § 4)
- ADR-0029 — Compute effective permissions once, from organization, area, project and team scopes over the membership-derived codes · accepted (amends ADR-0027); amended by ADR-0030
- ADR-0028 — Organize an organization as independent areas, projects and teams joined by links, with the organization as the only tenant boundary · accepted (amends ADR-0024, ADR-0026)
- ADR-0027 — Derive organization access from the membership of the resource's organization, and keep Keycloak to instance roles · accepted (amends ADR-0018)
- ADR-0024 — Build every panel screen on one shell and one theme, carry the work context in the address, and name type appearance by a closed token set · accepted
- ADR-0025 — Project Keycloak users through a read-only event-listener provider that pushes signed, ordered state into MUSTASK · proposed
- ADR-0025 — Project Keycloak users through a read-only event-listener provider that pushes signed, ordered state into MUSTASK · accepted
- ADR-0026 — Remove organizations, projects and teams softly by a status on the object itself, and decide reachability by containment · accepted
- ADR-0023 — A stand-only Keycloak client impersonates the E2E fixture user · accepted
- ADR-0022 — The panel's refresh token never reaches the browser: a token-mediating backend · proposed
- ADR-0021 — Store a dictionary as an entry plus a typed translation table per dictionary, and resolve the language on the server · proposed
- ADR-0020 — Remove the MUSTASK data store until the first module with data appears · superseded by ADR-0021
- ADR-0019 — Narrow the UI stack to what is actually used: MUI core instead of x-data-grid, controlled forms instead of react-hook-form · accepted
- ADR-0018 — Hand authentication to Keycloak with separate realms instead of our own issuer · accepted; amended by ADR-0027
- ADR-0016 — Build the frontend's Gridify requests with the official gridify-client · accepted
- ADR-0015 — Use Gridify for pagination, sorting and filtering of list reads · accepted
- ADR-0014 — A shared paginated response envelope for list reads of the API · superseded by ADR-0015
- ADR-0013 — Reconcile the DB and external services via intent statuses and an outbox, instead of distributed transactions · accepted
- ADR-0012 — Disable automatic retry of transient DB failures (EnableRetryOnFailure) · accepted
- ADR-0011 — Keep READ COMMITTED and serialize concurrent scenarios with pessimistic locks · accepted
- ADR-0010 — Implement optimistic concurrency via `xmin` by an automatic convention · accepted
- ADR-0009 — Use EF Core + Npgsql as the primary data access · accepted
- ADR-0008 — Issue JWTs ourselves (RS256) instead of plugging in an external IdP · superseded by ADR-0018
- ADR-0007 — Fix the frontend UI stack and the canonical screen states · accepted, partially narrowed by ADR-0019, amended by ADR-0024
- ADR-0006 — Error texts live in a shared catalog, not in server code · proposed
- ADR-0005 — Treat error codes as a stable registry — part of the API contract · accepted
- ADR-0004 — Business errors are exceptions, mapping to ProblemDetails is centralized · accepted, partially superseded by ADR-0006
- ADR-0003 — Set up Mustask.Shared for cross-cutting primitives with no dependencies · accepted
- ADR-0002 — Apply EF migrations as an explicit step, not at Api startup · accepted
- ADR-0001 — Use .NET Aspire for local orchestration · accepted
