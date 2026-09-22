# ADR-0016: Assemble frontend Gridify queries with the official gridify-client

- Status: accepted (2026-09-01, decision by the human in chat: "gridify is
  already added on the backend, add it on the frontend too")
- Date: 2026-09-01
- Context: continuation of ADR-0015 — the backend accepts `page`/`pageSize`/
  `orderBy`/`filter` in Gridify syntax and responds with `Paging<T>`. The
  frontend was left with the riskiest part of the contract: assembling the
  `filter` string correctly, with value escaping (a downside called out
  right in ADR-0015). The shared layer
  (`src/frontend/src/shared/api/paging.ts`) assembled `page`/`pageSize`/
  `orderBy` by hand and left building `filter` entirely to the slice's api
  layer.

## Problem

The `filter` expression is a mini-language with special characters (`(`,
`)`, `,`, `|`, `/i`). A value from user input ("ТОО (Астана), филиал")
without escaping breaks the expression or changes its meaning — and this
would have to be done correctly in every registry slice. Manual string
concatenation in every api layer is exactly the repetitive and
error-prone code the server moved away from with a library.

## Options considered

### Option 1: the official `gridify-client`

- Pros: the same author and repository as the server-side Gridify — client
  and server syntax evolve in step; `GridifyQueryBuilder` assembles
  `filter`/`orderBy` and escapes values itself (`addCondition`), validates
  the expression at `build()` (an unclosed group, two conditions in a row —
  a build error, not a 400 from the server); `and(optional)`/`or(optional)`
  remove the manual bookkeeping of "is this the first condition" when
  assembling a filter from optional form fields; it exports `Paging<T>` —
  the response envelope type matches the ADR-0015 shape and is not
  duplicated on our side; zero dependencies, TypeScript, MIT.
- Cons: one more frontend runtime dependency; the builder does not know the
  slice's fields — whitelisting stays on the server (`GridifyMapper`,
  ADR-0015), a client-side typo in a field name is caught only via a 400.

### Option 2: assemble filter strings by hand in every slice

- Pros: zero dependencies.
- Cons: escaping and the mini-language's grammar get smeared across
  slices; the first missed special character turns correct user input
  into an incorrect request.

### Option 3: our own shared builder in shared

- Pros: control over the API shape, zero external dependencies.
- Cons: this is rewriting `gridify-client` without its tests and without
  staying in sync with the evolution of the server syntax — the same
  argument by which ADR-0015 rejected "write our own mini-Gridify".

## Decision

Option 1. Package `gridify-client` (v2.2.x) in `src/frontend`.

Slices do not import the package directly — everything is re-exported from
the shared layer `src/frontend/src/shared/api/paging.ts`:

<!-- amended 2026-09-14, code review round 2 R-16: the re-adoption
     (D-10) wraps the package's `GridifyQueryBuilder` in a slice-typed
     `RegistryQueryBuilder<TField>` instead of re-exporting the class as-is,
     and there is no `useServerRegistryQuery` hook in this frontend — the
     list below is corrected accordingly. The decision itself (Option 1, the
     shared re-export point, the escaping approach) is unchanged. -->

- `Paging<T>`, `IGridifyQuery`, `ConditionalOperator` — re-exported from the
  package as-is;
- `RegistryQueryBuilder<TField>` — wraps the package's `GridifyQueryBuilder`
  rather than re-exporting it, narrowing every field-taking method
  (`addCondition`, and the `createRegistryQueryBuilder` helper's own
  `addOrderBy`) to the slice's own sort/filter field union, so an unlisted
  field is a compile error rather than a runtime
  `dictionaries.unknown_query_field` 400 — a property the package's own,
  bare-`string` API does not give for free;
- `createRegistryQueryBuilder(params)` — a builder preloaded with the shared
  part (`page`/`pageSize`/`orderBy` from `RegistryQueryParams`); filter
  conditions are added by the slice's api layer:
  `.and(true).addCondition(field, op, value)` per condition;
- `gridifyQueryToSearchParams(query)` — turns the `build()` result into the
  endpoint's query string (empty `orderBy`/`filter` are not written).

The hand-rolled `appendPagingParams` and the duplicate `GridifyPage<T>` type
are removed — their role passed to the builder and the package's `Paging<T>`.

Historical note: while the repository still had a second frontend (an empty
personal-cabinet scaffold), the package was not added there — a dependency
for use before the first registry would have been YAGNI. The scaffold was
removed later, the question is moot.

## Consequences

- The api layer of a registry slice contains no manual string assembly:
  `createRegistryQueryBuilder(params)` → conditions from filter fields →
  `gridifyQueryToSearchParams(builder.build())` → request.
- Escaping of user-supplied values is partial, not guaranteed: the builder
  escapes exactly `(`, `)`, `,`, `|` and `/i`, narrowing the "the frontend
  has to generate the syntax correctly" downside from ADR-0015 rather than
  closing it. A value that begins with an operator character (`>` measured)
  still reaches the server as syntax and is rejected with a 400, and `\` is
  not escaped, so a trailing backslash is silently swallowed instead of
  matched literally (code review round 3, R-18).
- The Gridify client and server are upgraded as a pair: bumping the server
  package on a syntax change means bumping `gridify-client` too.
- Worth reconsidering together with ADR-0015 — if a query appears that
  Gridify cannot express, a departure for that specific slice is recorded
  in a new ADR.
