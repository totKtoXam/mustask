# ADR-0014: Shared paged-response envelope for list reads in the API

- Status: superseded by ADR-0015 (2026-09-01, human decision in chat:
  pagination/sorting/filtering move to Gridify); before that — accepted
  (accepted by the human at gate-3, 2026-08-28; proposed 2026-08-27,
  backend-developer)
- Date: 2026-08-27
- Context: moving list endpoints from a bare array to server-side
  pagination/sorting/filtering.

## Problem

List reads that return a whole `TResponse[]`, when moving to server-side
pagination, need some response shape that carries both the page of data and
the slice's metadata (total count, the applied `page`/`pageSize`). The
shape is not a detail of one slice: if every endpoint picks its own (say,
`{items, total}` for one and `{data, count, page}` for another), rewriting
response shapes that are already merged costs more than fixing a convention
once for every future list read in the API.

## Options considered

### Option 1: a single envelope `{ items, page, pageSize, total }`

- Pros: one shape for the whole API — the frontend writes one thin adapter
  (`page.items`/`page.total`) instead of one adapter per screen;
  `page`/`pageSize` in the response echo the applied values, so the client
  does not need to recompute them from its own request state on a mismatch
  (e.g. the server clamped `page` — the envelope is already ready for
  that); the type lives in `Mustask.Shared` (already home to `MustaskException` —
  a shared primitive with no dependency on a specific slice, ADR-0003), so
  it does not violate the "slices don't reference each other" rule: every
  slice references `Mustask.Shared`, not each other.
- Cons: any number of fields in the envelope is already a decision that is
  harder to change later (every client depends on the shape).

### Option 2: pagination headers (`X-Total-Count` etc.), the body stays an array

- Pros: the response body's shape does not change at all — in theory fewer
  changes for existing consumers, if they did not read the body directly as
  an array (but they do).
- Cons: headers are not visible in the generated OpenAPI schema of the
  response body as clearly as JSON fields are; `fetch`/`axios` need a
  separate read of headers, which is not actually simpler than reading
  `body.total`; it breaks the same number of clients, with no payoff for
  keeping the body's shape.

### Option 3: every slice has its own shape

- Pros: zero work now, a slice depends on nothing shared.
- Cons: several registries within the same application, using different
  names for the same concept — a cost on the frontend (an adapter per
  screen instead of one) and confusion during onboarding.

## Decision

Option 1. `Mustask.Shared.Paging.PagedResponse<T>(IReadOnlyList<T> Items, int
Page, int PageSize, int Total)` — the single response shape for a list read
across the whole API. Serialized camelCase, like every other DTO:
`{ "items": [...], "page": 1, "pageSize": 10, "total": 1000 }`.

Companion decisions at the same level (apply identically to all list
slices):

- **The page-size cap is hard and shared.** `PageRequestDefaults`:
  `DefaultPage = 1`, `DefaultPageSize = 10`, `MaxPageSize = 100`. A value
  outside the `[1, MaxPageSize]` range is HTTP 400 per the error catalogue
  (`<module>.invalid_page`/`invalid_page_size`), not a silent clamp to the
  boundary. The explicit alternative ("clamp `pageSize` to 100 and return
  200") is rejected: it turns a client error into silently different
  behavior (not the row count the client expected, with no signal about
  it), and on a public endpoint it lets a caller request an arbitrarily
  large page simply by passing a deliberately huge `pageSize` and getting
  not an error but a clamped, accepted response. A hard rejection is
  cheaper for the server (a guaranteed upper bound on response size with no
  computation) and more honest for the client.
- **An unknown sort field or an invalid filter value is also 400, not a
  silent ignore.** The same "explicit error instead of implicit different
  behavior" principle, applied not only to the page but also to sorting
  (`<module>.invalid_sort_field`, `invalid_sort_direction`) and to
  closed-set filter values (`<module>.invalid_status_filter` etc.). Open
  (not from a closed set) parameters with no special meaning — e.g. a
  stray unrecognized query parameter — are still silently ignored by the
  standard `[AsParameters]` binding, and that is not a contradiction: the
  distinction is whether the server knows the meaning of the parameter (a
  field value from a closed set is known and must be correct) or not (the
  parameter itself is unknown to the server).

## Consequences

- A single point for future list reads: a new data list uses
  `PagedResponse<T>` by default instead of reinventing a shape.
- Clients read a list read's response body as an object with
  `items`/`total`, not as an array.
- Worth revisiting if a list appears for which the envelope genuinely does
  not fit (streaming output, cursor-based pagination); a deviation for that
  specific case is then acceptable with a new ADR, not a silent divergence.
