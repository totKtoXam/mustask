# ADR-0015: Use Gridify for pagination, sorting and filtering of list reads

- Status: accepted (2026-09-01, decision by the human in chat)
- Date: 2026-09-01
- Context: bootstrap of a new project. ADR-0014 fixed a hand-rolled envelope
  (`PagedResponse<T>` + `PageQuery`/`PageRequestDefaults`/`PageRequestValidation`
  in `Mustask.Shared`); the human decided to replace the hand-rolled mechanics
  with the [Gridify](https://alirezanet.github.io/Gridify/) library before
  the first registry slice appears — there is nothing to rewrite, there are
  no live list endpoints in the scaffold.

## Problem

Every registry needs the same four things: page, page size, sorting and
filtering — applied server-side, on an `IQueryable`, before materialization.
The hand-rolled variant (ADR-0014) covered pagination and sorting, but every
slice assembled filtering by hand: its own query parameters, its own
parsing, its own expressions. This is the bulkiest and the most repetitive
part of every registry slice.

## Options considered

### Option 1: Gridify + Gridify.EntityFramework

- Pros: a single `filter` string in Gridify syntax covers arbitrary
  filtering, `orderBy` covers sorting (including by several fields), the
  standard `IGridifyQuery` (`page`, `pageSize`, `orderBy`, `filter`) binds
  from the query string without a DTO of our own; field whitelisting is via
  the slice's `GridifyMapper` (an unmapped field is an error, not a silent
  pass-through to an arbitrary column); `GridifyQueryableAsync` builds a
  single EF query with `COUNT` and the page; actively maintained, MIT.
- Cons: an external dependency in the signatures of Application abstractions
  (`IGridifyQuery`/`Paging<T>`); the filter syntax is a mini-language the
  frontend has to generate correctly (value escaping); there is no built-in
  ceiling on `pageSize` — the ADR-0014 principle has to be kept alive by the
  slice's own validation.

### Option 2: keep the ADR-0014 hand-rolled envelope and add filtering

- Pros: zero dependencies, full control of the shape.
- Cons: hand-written filtering in every slice — exactly the work the
  library does declaratively; our own "mini-Gridify" grows on its own,
  but without the library's tests and documentation.

### Option 3: OData

- Pros: a standard, the most expressive query language.
- Cons: a heavy runtime and its own metadata model for the sake of four
  operations; the expressiveness of `$expand`/`$select` is not needed by the
  scaffold and opens more surface than we want to control.

## Decision

Option 1. Packages `Gridify` (core) and `Gridify.EntityFramework` (v2.19.1,
versions centrally in `Directory.Packages.props`).

Distribution across layers (Onion, `docs/conventions/backend.md`):

- **Mustask.Api** — binds `GridifyQuery` from the query string
  (`page`, `pageSize`, `orderBy`, `filter`) and passes it to the slice;
  global configuration is set once at startup (`Program.cs`):
  `GridifyGlobalConfiguration.EnableEntityFrameworkCompatibilityLayer()`
  (required for EF: parameterization and a compatible expression shape) and
  `DefaultPageSize = 10` (Gridify's default is 20, our registry canon is 10).
- **Mustask.Application** — reader abstractions accept `IGridifyQuery`, the
  response of a list read is `Gridify.Paging<T>` (`{ count, data }`). It
  references only the core package (no EF) — the `OnionDependencyRules`
  architecture test guarantees this.
- **Mustask.Infrastructure** — applies the query to an `IQueryable` via
  `GridifyQueryableAsync`/`ApplyFiltering`/`ApplyOrdering`/`ApplyPaging`
  (`Gridify.EntityFramework`).
- **Mustask.Shared** — Gridify does not go in there: Shared stays without
  dependencies (ADR-0003). The hand-rolled `PagedResponse<T>`, `PageQuery`,
  `PageRequestDefaults`, `PageRequestValidation` are removed.

Rules that survive the mechanics swap (principles from ADR-0014):

1. **The `pageSize` ceiling is hard and shared: 100.** Gridify has no
   ceiling of its own — the slice validates `pageSize` and responds 400 per
   the error catalog (`<module>.invalid_page_size`), it does not silently
   truncate.
2. **Field whitelisting is mandatory.** Every registry slice declares a
   `GridifyMapper` with an explicit list of fields for `filter`/`orderBy`;
   `IgnoreNotMappedFields` is not enabled — an unmapped field must be an
   error (400 via mapping `GridifyException` in the slice), not a silent
   skip and not access to an arbitrary column.
3. **An unknown sort/filter field is 400, not a silent ignore** (the same
   "explicit error instead of implicit different behavior" rule).

Response shape — Gridify's `Paging<T>`, serialized camelCase:
`{ "count": 1000, "data": [ ... ] }`. `count` is the total number of records
in the set after filtering, not the length of `data`. The server does not
echo the applied `page`/`pageSize` (unlike the ADR-0014 envelope) — the
client knows them from its own request; the shared frontend adapter
(`src/frontend/src/shared/api/paging.ts`, `useServerRegistryQuery`) already
works this way.

## Consequences

- A new registry slice invents neither an envelope nor filter parsing:
  `GridifyQuery` on input, a `GridifyMapper` with whitelisting, `Paging<T>`
  on output.
- The frontend sends `page`/`pageSize`/`orderBy` (`field` / `field desc`)
  and `filter` in Gridify syntax; the shared part is `appendPagingParams`,
  the filter expression is assembled by the slice's api layer (it is the
  only one that knows its own fields).
- A dependency on the library's type appears in Application signatures — a
  conscious price: `IGridifyQuery` is an interface without EF, and the
  alternative (a translator wrapper over our own DTO) brings back exactly
  the hand-rolled layer we are moving away from.
- Worth reconsidering if a query appears that Gridify cannot express
  (cursor pagination, full-text search) — then a departure for that
  specific slice is recorded in a new ADR.
