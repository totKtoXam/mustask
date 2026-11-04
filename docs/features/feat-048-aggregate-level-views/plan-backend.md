# Backend plan: Aggregate level views (feat-048)

Stage 4a. Contract: [api-contract.md](api-contract.md). Decisions behind it: spec BR-AV-1..12, [design.md](design.md) §§ 7-8 (D-2, D-5), [ADR-0036](../../adr/0036-cursor-paged-group-reads-and-read-limiter.md) (proposed; the cursor, the limiter). No further ADR is owed.

## 1. What is built

| Part | Where |
|---|---|
| Grouping rules (BR-AV-2 offers, parse) | `Mustask.Domain/Boards/AggregateGrouping.cs` |
| Request parsing shared by the five reads (grouping, selectors, feat-003 filters, group, limit, cursor) | `Application/Abstractions/Boards/AggregateParameters.cs` (an abstraction, not a slice-to-slice reference) |
| Cursor: opaque, forward only, two order keys, fingerprinted | `Application/Abstractions/Boards/GroupCursor.cs` |
| Read side | `Application/Abstractions/Boards/IAggregateViewReadModel.cs` (every method takes `CallerAccess` first, BR-NV-15), `Infrastructure/Boards/AggregateViewReadModel.cs` |
| Slices (one folder each) | `Features/Boards/GetViewGroups`, `GetBacklogGroupRows`, `GetBoardCells`, `GetWorkItemGroupRows`; `Features/WorkItems/GetOrganizationWorkItems`, `GetOrganizationAssigneeOptions` |
| Limiter (ADR-0036 § 3) | `Mustask.Api/Features/Boards/AggregateReadLimiter.cs`, an endpoint filter on the group and cell reads only (OQ-2 A) |
| Endpoints | `Mustask.Api/Features/Boards/AggregateViewEndpoints.cs` (all eight routes mapped in full) |
| Migration `AggregateViewIndexes` (D-5 A) | four new `work_items` indexes, one replaced |
| Catalogue | `http.too_many_requests` (429, platform), `boards.invalid_cursor`, `boards.invalid_grouping`, `boards.invalid_parameter` (field reasons) in `contracts/errors/*.json` and `docs/error-codes.md` |

## 2. Read strategy

- **Source of rows:** `WorkItemSets.Live` (or `InAnyState` narrowed by the state filter for «Рабочие элементы», as the project list's `Listing`), the level's types for backlog/board, the shared `WorkItemFilterPredicate`. No raw `context.WorkItems`.
- **Counts (headers, columns):** one statement `GROUP BY (project, owning team, type, state)` over the active projects in scope, folded in memory (≤ 200 projects) into the chosen grouping, a department being the project node's parent; a second statement, unnarrowed, gives `total`; one statement for the names (projects, departments, teams).
- **Change-time group read:** one statement. `CROSS JOIN LATERAL` per active project in scope (EF `SelectMany` with a correlated `Take`), top `limit + 1` after the cursor in `(changed_at desc, number desc)`, an outer top `limit + 1` on `(changed_at desc, number desc, project key)`; then the rows of those ids in a second statement, the assignees, and the live parents. Statement count is constant for any volume or number of groups.
- **Rank group read (by project, backlog and board):** the project's items joined to `project_backlog_places`, keyset on `(unplaced, place, created_at, number)`; one project, a few thousand rows, no new index.
- **Flat list:** `count` plus the same lateral with `Take(skip + size)`.

## 3. Departures to be seen at gate-3 (reported to the orchestrator)

1. **By-project backlog rows are flat.** A group read returns the level's own items in the project's rank order, each with its `parent`; it does not nest the lower levels' items under them as the project's own «Бэклог» does (feat-027). A tree is not pageable by a cursor without a recursive read per page; D-6 already makes every other grouping flat. Nesting would be a further read (children of the page's rows) and a contract change.
2. **Cell read takes the board's column `key`** (`Proposed-1`, from the groups read), not a `stateId`: a column maps one state per type, and the key is what the board already speaks.
3. **Reasons beyond the two the task names:** `boards.invalid_grouping` for `grouping`, `boards.invalid_parameter` for `group`, `limit`, `column`.

## 4. Tests

| Test | Covers |
|---|---|
| `Mustask.Domain.Tests` `AggregateGroupingTests` | BR-AV-2 offers per level |
| `Mustask.Application.Tests` `GroupCursorTests`, `AggregateParametersTests` | round trip, malformed, foreign (fingerprint), limit bounds |
| `Mustask.Api.IntegrationTests` `AggregateViewEndpointTests` (the groups, rows, cells and flat list; `AggregateViewQaTests` adds the QA cases) | US-11 scenarios on the cast: departments with «Без департамента» first, teams with «Без команды» first, project order, counts and `total`, board column counts, none, deactivated project absent, other organization absent |
| (in `AggregateViewEndpointTests`) | change-time order, ties on number by project key, cursor paging with no row twice and none lost when a change time moves, rank order and a move seen at the next read, 400s on cursor/grouping/group/limit/column, 404 for a deactivated project's group |
| (in `AggregateViewEndpointTests`) | the flat list: order, total, keyword (title, identifier, number), state filter reaching «Удалена», selectors |
| `AggregateReadLimiterTests` (fake `TimeProvider`) | two tabs = 8 reads, 6 run, 2 wait, none refused; 13th refused; wait 3 s then 429 with `Retry-After: 2`; global 40+20; the key is the subject across organizations |
| `Mustask.Infrastructure.Tests` `AggregateViewStatementCountTests` | statement counts of the groups read, a group's first page (change-time and rank order), a cell and the flat list (shallow, deep, keyword), equal at 5 and at 50 projects |
| `AggregateViewVolumeTests` | BR-AV-11 at 200 projects and 1 000 000 items: counts, first page and a later page of every grouping and of the stream, within 2 s |
| route inventory, `DeletedItemSurfaceMatrixTests`, `OrganizationAddressedRoutes` | the eight new routes classified; the pinned count 73 becomes 81 |
| `RulesAreNotVacuous` style signature test | BR-NV-15 on `IAggregateViewReadModel` |

## 5. Lanes

Eight routes but one shared read model and one request parser: not four disjoint use cases. No lanes; done in one pass.

## D-7. The deep page of the flat list, and the no-team index

- `ReadFlatPageAsync` keeps the BR-NV-12 envelope (count, page numbers, the order) and picks its cut by depth: while `skip + size` is at
  most 250 it reads that many rows per project through the LATERAL (one index range each); deeper, the LATERAL would read `skip + size`
  rows in every project (page 100 at 200 projects is the whole table), so one ordered pass over the join with a top-N sort cuts the page
  instead — a heap of `skip + size` rows whatever the depth, planned by Postgres from the statistics (a small tenant gets the project-led
  index, a large one a scan). The 1M test measures page 100.
- `ix_work_items_no_team_changed_at` was `(changed_at desc, number desc)` partial: it has no `project_id`, so the per-project LATERAL of the
  no-team group could not use it. It is now `(project_id, type_id, changed_at desc, number desc) WHERE owning_team_id IS NULL`, the shape
  of the LATERAL's range with the team filter built in.
