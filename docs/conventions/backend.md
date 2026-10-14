# Backend conventions: .NET 10 + PostgreSQL, Onion × Vertical Slice

Hybrid: **the core follows Onion** (domain at the center, dependencies
pointing inward), **features are vertical slices** (all of a use case's
logic in one folder, not smeared across layers).

## Solution structure

```
src/backend/
  Mustask.Shared/                # cross-cutting primitives with no dependency
                             # of their own (incl. no ASP.NET): the
                             # MustaskException hierarchy
                             # (Mustask.Shared.Exceptions). See ADR-0003.
  Mustask.Domain/                # pure domain: entities, value objects,
                             # domain rules, domain events. No
                             # dependencies besides Mustask.Shared.
  Mustask.Application/           # vertical slices of use cases
    Features/
      <Module>/              # module = business area (e.g. Orders)
        <UseCase>/           # slice: everything in one folder
          <UseCase>Command.cs / Query.cs   # request + validator
          <UseCase>Handler.cs              # handler
          <UseCase>Response.cs             # response DTO
    Abstractions/            # infrastructure interfaces (repositories, bus, clock)
  Mustask.Infrastructure/        # implementations: EF Core + Npgsql, migrations,
                             # integrations with external systems, background jobs
  Mustask.Api/                   # minimal API: endpoint → slice mapping, auth, DI
  tests/
    Mustask.Domain.Tests/          # unit: domain rules
    Mustask.Application.Tests/     # unit: handlers (infrastructure — fakes)
    Mustask.Infrastructure.Tests/  # unit + integration: EF conventions,
                               # migrations (Testcontainers+Postgres) —
                               # Infrastructure's own behavior, not
                               # through an endpoint
    Mustask.Api.IntegrationTests/  # integration: endpoints, Testcontainers+Postgres
```

## Dependency rules (the Onion part)

- Domain ← Application ← Api; Infrastructure implements the
  Application/Domain abstractions. Arrows point inward only: Domain knows
  about no one.
- Domain rules (invariant validation, status transitions, calculations) —
  live in Domain, not in handlers. The handler orchestrates, the domain
  decides.

## Slice rules (the VSA part)

- One use case = one folder = one endpoint. No "shared services" between
  slices in Application — reusable logic goes into Domain or into
  Abstractions.
- Slices do not reference each other. Communication between modules goes
  through domain events or explicit interfaces.
- Reads (Query) can go to the DB directly (EF/Dapper, projecting straight
  into Response) bypassing the domain model; writes (Command) — only
  through the domain.

## API

- Minimal API, endpoints grouped by module; routes `/api/<module>/...`.
- Errors — ProblemDetails. Business errors — descendants of `MustaskException`
  (`Mustask.Shared.Exceptions`), which Domain/Application throw deliberately;
  the centralized mapping to ProblemDetails lives in `Mustask.Api/ErrorHandling`
  (three `IExceptionHandler`s: cancel → business → global). The `Result`
  type is not used. The decision and its consequences — ADR-0004.

  | Descendant | HTTP status |
  |---|---|
  | `NotFoundException` | 404 |
  | `ValidationException` | 400 |
  | `UnauthorizedException` | 401 |
  | `ForbiddenException` | 403 |
  | `ConflictException` | 409 |

  The error code (`MustaskException.Code`) — format `<module>.<reason>`, e.g.
  `orders.not_found`; it goes to the client in `errorCode` and is used as
  a structured log field. Codes are a stable registry (never renamed and
  never reused, declared as a constant next to the slice's exception) —
  rules and rationale in ADR-0005. Each slice lists its error codes in
  `api-contract.md` and attaches `.ProducesProblem(...)` to the endpoint —
  otherwise failure modes are visible nowhere, since the handler's
  signature does not show them.
- **[CHANGE vs ADR-0004, see ADR-0006] `MustaskException.Message` is not text
  for display.** `BusinessExceptionHandler` does **not** hand `Message` to
  the client: the descendant's constructor takes a `code` and an optional
  `ErrorParams` (`Mustask.Shared.Errors`), not free-form text —
  `throw new ConflictException(OrdersErrorCodes.LimitExhausted,
  new ErrorParams { ["used"] = ErrorParam.Number(50), ["limit"] = ErrorParam.Number(50) });`
  `Message` is built automatically as a diagnostic trace (code +
  parameters, invariant culture) — it is printed wherever the exception
  surfaces outside our handler, but is never shown to the user. The
  displayed `detail` is built by `BusinessExceptionHandler` from
  `ErrorCatalog` (`Mustask.Api/ErrorHandling/Catalog`) from the code and
  parameters — the single source of error text (in both languages — ru and
  kk) is the files `contracts/errors/<module>.json` (see
  `contracts/README.md`), not code. The security contract is not weakened:
  neither `detail` nor `params` may ever contain messages from underlying
  exceptions, SQL queries, file system paths, or identifiers of someone
  else's entities — that's what `innerException` is for (safe, never
  rendered outward, goes only into the structured log at `Warning` level
  together with its own stack trace and the rendered Russian text, see
  `BusinessExceptionHandler`). `MustaskException`'s own stack is not logged:
  for an exception thrown as normal control flow, it carries no
  diagnostic value.
- **`params` in the response** — raw values plus a closed type hint
  (`{value, type}`, types `string`/`number`/`currency`/`date`/`datetime`),
  with no locale formatting: the client formats it itself. The `params`
  key is entirely absent for a code with no parameters. The full response
  shape, the set of platform codes, and the contract-change rule —
  ADR-0006, `contracts/README.md` and `docs/error-codes.md`.
- **`ValidationException.Errors`** — `IReadOnlyDictionary<string,
  IReadOnlyList<FieldError>>`: the key is the request field's technical
  identifier (not a display label), the value is a list of reasons, each
  with its own registry code (`<module>.<reason>`, the same rules as the
  top-level code) and its own `ErrorParams`. There is no second identifier
  family for field reasons.
- **Error text catalog** — `contracts/errors/<module>.json`, one file per
  module, outside `src/backend/` and outside `src/frontend/` (accessible
  without knowing React and without building the backend). Text in both
  Russian and Kazakh is mandatory for every code — a blocking check
  (`src/backend/tests/Mustask.Contracts.Tests`), not a warning; a mismatch
  between `{name}` placeholders and the declared `params` also fails the
  build. The human-readable projection is `docs/error-codes.md`
  (generated, committed, a mismatch with the catalog fails the build).
  Details — `contracts/README.md`, ADR-0006.
- **A class holding code constants is marked `[ErrorCodes]`**
  (`Mustask.Shared.Errors`) — otherwise the slice's code-catalog completeness
  check simply won't see it: it collects "application codes" by reflecting
  over classes carrying this attribute. A forgotten attribute = a green
  build and, at runtime, a faceless 500 instead of the expected
  business-error status (the handler won't render a code that has no
  catalog entry). A mechanical ban on an unnoticed code is a backlog item
  (ADR-0005); until then the attribute is set by hand.
- Utility (non-business) surfaces — the API explorer, diagnostic
  dashboards, metadata exports — are turned on by the `<Feature>:Enabled`
  configuration flag. Semantics (set by the API explorer task): an explicit
  value wins over the environment in both directions (`true` opens the
  surface in any environment, `false` closes it even in Development); a missing,
  empty, or unrecognized value = "off" (fail-closed); the environment
  decides only when the flag is not set.
- Input validation — at the boundary (the slice's validator);
  authorization — explicit on every endpoint (policy), "forgot it means
  closed." Two kinds of right (ADR-0027): an **instance** code comes from the
  token (`.RequiresPermission(code)`); an **organization** code is derived from
  the caller's membership in the organization of the address
  (`.RequiresOrganizationPermission(code)`), and a route with
  `{organizationId}` that declares none fails `scripts/verify.sh` (the inventory
  test, `OrganizationAccessInventoryTests`). The scoped check runs before the
  body is read and answers 401, then 404 «Организация не найдена» for a
  non-member (the same bytes as for an organization that does not exist), then
  403 for a role without the code; a handler that needs a second code asks
  `IOrganizationAccess`.

## Database

- PostgreSQL, EF Core migrations in Infrastructure; snake_case names.
- Every migration is reversible. Reference data — seeded in migrations.
- Concurrency: optimistic concurrency (xmin) for editable entities —
  applied automatically by model convention, nothing needs configuring
  per-entity (ADR-0010); concurrent scenarios on top of it — the
  transactional model of ADR-0011.
- **A business code is unique by convention.** An entity whose identity
  inside its own registry is a `Code` property (dictionaries, catalogs,
  roles) gets a unique index on it automatically —
  `CodeUniqueIndexConvention` adds it at model-finalizing time, the same
  way `xmin` is added by ADR-0010's convention; nothing is configured per
  entity and no one has to remember it. The convention skips the property
  when it is already the single-column primary key or is already covered by
  a unique index or an alternate key, so an explicit
  `HasIndex(x => x.Code).IsUnique()` stays valid and produces no duplicate.
  A model test over `MustaskDbContext` asserts the resulting rule: every entity
  type with a `Code` property has a unique constraint covering exactly it.
  Why a convention rather than a rule in review: a missing unique index on
  a code does not break anything visibly — it lets a duplicate in, and the
  duplicate is discovered later by whatever reads the dictionary by code
  and gets two rows. (Introduced by the dictionaries feature, ADR-0021; the
  human deliberately declined a separate ADR for it.)
- Parallel branches with migrations: before a PR — rebase onto main; if
  main has brought in its own migrations, the feature's migrations are
  deleted and regenerated on top of the fresh model snapshot (see stage 9
  in ai-workflow). A migration that has landed in main is never edited
  again — only a new one on top.

### EF Core

There is one data-access stack — EF Core + Npgsql (ADR-0009): writes go
through the domain model and change tracking, reads go through
projections. Automatic retry of transient DB failures is deliberately
disabled (ADR-0012). Rules mandatory for every slice:

- **Reads use `AsNoTracking` + a projection into Response.** The reader
  builds the query and projects with `.Select(...)` straight into the
  slice's Response row — only columns actually needed go into the
  `SELECT`. The entity → row mapping lives in one static `Project(...)`
  helper per slice, not smeared across methods. Domain entities are never
  returned from readers and never serialized in the API: the entity ↔
  Response boundary runs inside Infrastructure.
- **Filtering happens before materialization.** Everything that narrows
  the result set — `Where`, `OrderBy`, `Skip`/`Take`, `Count`, aggregates
  — is composed on `IQueryable` and goes into SQL; client-side
  post-filtering after `ToListAsync()` is forbidden. Using
  `AsEnumerable()`/`ToList()` mid-query as a "bridge" for a
  non-translatable predicate is a forbidden move: rewrite the query,
  don't move where it's computed. Counting — use `CountAsync`/`AnyAsync`,
  not `.Count()` on a list. Reads run with a fixed number of queries, no
  per-row cascade (the lesson of an early N+1 bug). Caveat: finishing off, in
  memory, a selection *already narrowed to a page* (stitching rows
  together, grouping already-loaded child records) is assembling a
  projection, not a violation; the ban is on reading extra rows.
- **Mapping is done by hand.** Reflection-based mappers (AutoMapper and
  kin) are not adopted: `ProjectTo` adds non-translatable surprises and
  hidden configuration, and with our flat Response rows it saves lines
  but not complexity; a manual projection fails to compile when a field
  is renamed — warnings-as-errors keeps working.
- **Lazy loading is not enabled.** `UseLazyLoadingProxies` is not added;
  relations are loaded explicitly — via a projection or `Include`. Hidden
  queries and N+1 are incompatible with the fixed-number-of-queries rule.
- **Raw SQL — only parameterized and only where LINQ can't express it.**
  `FromSqlInterpolated` / `SqlQuery` / `ExecuteSqlInterpolated`
  (interpolation = parameters, string concatenation is forbidden), only
  inside Infrastructure, for reasons like: row locking
  (`FOR UPDATE [SKIP LOCKED]`, ADR-0011), bulk operations. A manual
  `SELECT` of an entity must explicitly list `xmin` — or use the "lock by
  id with raw SQL, load with the regular tracking query" technique
  (ADR-0010, "Consequences").
- **External I/O inside a DB transaction is forbidden.** Inside
  `ExecuteInTransactionAsync` and between `BeginTransactionAsync`/`Commit`
  — no HTTP calls; consistency with external services is achieved through
  intent statuses and an outbox (ADR-0013).
- **Time — UTC only, through `IClock`.** Type — `DateTimeOffset`, source —
  `IClock.UtcNow`; `DateTime.Now`/`DateTimeOffset.Now` are never called in
  code. Npgsql maps time to `timestamptz` and requires UTC — local time
  fails at runtime.
- **Deferred deliberately, not forgotten:** DbContext pooling
  (`AddDbContextPool`) and compiled queries are not enabled — they get
  enabled based on profiling data, not preemptively; pooling additionally
  requires the context to stay stateless.

### Soft removal

Organizations, projects and teams are removed softly, and nothing is
purged ([ADR-0026](../adr/0026-soft-deactivation-by-status.md)).

- **A status on the object's own row.** Each removable entity has a
  closed status enum, stored as text. A removal or a restore writes that
  one row, and nothing beneath it changes status. A container's removal
  hides what is beneath it through reachability, not through a cascade.
- **No global query filter.** Every read says what it wants. Lists and
  pickers want reachable, active objects. A deactivated list wants the
  deactivated objects of a reachable container. A by-id read for another
  module wants the object with its status, reachable or not.
- **Reachability is checked in one place per module.** For M1 that is
  `Mustask.Infrastructure/Organizations/Reachability.cs`, and at the
  boundary the scoped authorization requirement with its handler
  (`OrganizationPermissionHandler`, [ADR-0027](../adr/0027-organization-access-from-membership.md);
  it replaced mod-002's endpoint filter). An unreachable
  object answers its level's not-found code, never "deactivated".
- **Reservation is a unique index.** A name that stays reserved gets a
  unique index over all rows. A name freed by removal gets a partial
  unique index over active rows. Both index the normalized value.
- **Other modules read the status** through the owning module's read
  interface, at read time, and apply it to their own data. They never
  copy it and never subscribe to it.

## Named values

A literal in code is acceptable only if it carries no domain meaning (a
loop index, a buffer size, an HTTP status from the framework). For
everything else — a decision tree, step by step:

1. **Does the value change the business without a release** (limits,
   deadlines, regulations, distribution percentages)? → this is **data**:
   settings or a reference table in the DB (`app-settings`,
   `reference-data`). A constant in code for such a value is a defect: it
   cancels out the very possibility of configuring it.
2. **A closed set that logic branches on** (statuses, types,
   discriminators)? → an `enum` in `Mustask.Domain`. In the DB and the API —
   **as a string** (`HasConversion<string>()`, `JsonStringEnumConverter`):
   a number turns reordering the enum's members into silent data
   corruption.
3. **A single value with meaning** (max attempts, timeout)? → `const` /
   `static readonly` **next to the concept that owns it**. Name it by
   meaning (`MaxSendAttempts`), not by value (`Three`). There is no
   shared `Constants.cs`: a dumping ground for constants violates the
   vertical slice the same way a dumping ground for handlers does.
4. **Does the value cross the API boundary** (an error code, a status in
   a DTO)? → it is additionally recorded in the slice's
   `api-contract.md`. Error codes — per the registry rules, ADR-0005.
5. **Two values that happen to be equal but come from different
   concepts** — do NOT merge them. A shared constant is acceptable only
   for a shared concept: merging "coincidentally identical" trios means
   changing one silently changes the other.

Point 1 outranks the rest: before adding a constant, ask "who owns the
change — the developer or a ministry employee?"

## Tests

- Domain rules — unit tests without mocks.
- Every endpoint — an integration test through WebApplicationFactory +
  Testcontainers (a real Postgres). External integrations — stubs.
- Naming: `Method_Scenario_ExpectedResult`.

## Miscellaneous

- Nullable reference types enabled, warnings as errors.
- Logging — structured (Serilog), request correlation.
- Secrets and connection strings — configuration/env only.
