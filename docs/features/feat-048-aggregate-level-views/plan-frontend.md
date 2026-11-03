# feat-048 — frontend plan (stage 6)

Spec: `spec.md` (BR-AV-1..12, US-6, US-10, US-11). Design: `design.md`. Contract: `api-contract.md`.
Status at this writing: the aggregate views are built and tested at the unit and page level; see
"Left" for what is deliberately not done.

## 1. Slices touched

- `features/boards` owns everything new: `api/aggregateApi.ts`, `lib/grouping.ts`,
  `lib/openGroups.ts`, `lib/aggregateColumns.ts`, `lib/aggregateMove.ts`, `lib/aggregateTexts.ts`,
  `hooks/readQueue.ts`, `hooks/useGroupRead.ts`, `hooks/useAggregateView.ts`, the components
  `ViewGroup` (the design's `ProjectGroup`, renamed: a group is a department, a project or a team),
  `GroupBody`, `GroupingSwitch`, `AggregateHeader`, `AggregateRows`, `AggregateBacklogView`,
  `AggregateBoardView`, `rowColumns`, and the pages `AggregateViewPages`, `AggregateWorkItemsPage`.
- `features/workitems` gains the organization-wide list read (`getOrganizationWorkItems`,
  `useOrganizationWorkItems`) and a nullable project in the assignee options.
- `shared/api/httpClient` reads `Retry-After` into `ApiError.retryAfterMs`.
- `app/` registers the routes (`routeTree.tsx`) and the menu rows (`navigation/addressModel.ts`).

## 2. Decisions

- The switch is the kit's `MenuButton` (radio) in the `PageHeader` `viewsEnd` slot, not a
  `Command`: a command cannot host a menu. Offered options per level follow `groupingsOffered`.
- Default and memory: `defaultGrouping`, `resolveGrouping`; stored per user, organization, view and
  level in `localStorage` (`mustask.grouping.v1…`); open groups the same way, ten at most
  (`mustask.open-groups.v1…`). Never in the address.
- One `useGroupRead` per group (rows) or per lane cell (cards): a closed group reads nothing; a
  per-tab slot queue (`readQueue`) caps reads in flight at 4; a 429 retries by `Retry-After`
  (base 1 s doubling when absent) up to three times, then «Повторить»; a 400 on the cursor reloads
  the headers silently; a 404 `organizations.project.not_found` hides the group.
- «Без департамента» / «Без команды» groups come first (the server orders them so).
- Flat «Рабочие элементы» uses the page-enveloped list with the pager; grouped uses the cursor
  reads, no pager, no sorting (BR-AV-8). Rows carry no indent outside by-project (D-6 A).
- The move menu (backlog, by project only) reuses `reorderProjectBacklog`; the part-loaded rules
  are `aggregateMove` (OQ-6 A).
- The board moves a card through «Ещё действия» / «Переместить в колонку» with the shipped
  state write; the target cell is re-read from its first page (D-3 A).

## 3. Deviations from design.md

1. The group heading is MUI's own `Accordion` heading (an `h3`), not an `h2`: the kit guard (KIT-21)
   forbids a heading element of our own and the allow-list only shrinks.
2. Loading shapes are the kit's `TableSkeleton` inside our named status region (KIT-10 forbids a
   bare `Skeleton`).
3. The part-loaded «Ниже» secondary line is folded into the item's label.

## 4. Contract gap, escalated (not patched)

The groups read of a board gives column keys (`<Category>-<k>`) and counts only, with no header
text and no target state. The frontend therefore derives the columns from the process definition
by the server's `BoardLayout.Derive` rule (`lib/aggregateColumns.ts`, pinned by its test). If the
rule or the process changes, the two drift. Asked of the orchestrator: add `header` to the
column entries of the groups read (an additive change to `api-contract.md` with a note in
`status.md`), then the derivation can go.

## 5. Tests

- Unit: `lib/grouping.test.ts`, `lib/aggregateColumns.test.ts`, `lib/aggregateMove.test.ts`,
  `hooks/useGroupRead.test.tsx` (queue cap, retry, refusal, cursor reload, gone),
  `shared/api/httpClient.test.ts` (Retry-After).
- Page (MSW): `pages/AggregateViews.test.tsx` — the four options, default, first group open only,
  chosen grouping re-read and remembered, silent fallback, «Показать ещё», 429 retry then
  «Повторить», cursor-400 reload, headers error, empty, flat versus grouped «Рабочие элементы»,
  the team level offer, the board's cell reads.
- App: menu table, visibility, and the retired-address test updated (the seven feat-048
  addresses are served now); the kit guard passes with no allow-list change.

## 6. Left

- Drag and drop inside and between groups (the keyboard path «Переместить» is done).
- The project list's «Без команды» control, and the grouping on the shipped team-with-department
  and project pages (those pages keep feat-040/039 as they are; only the three new addresses use
  the switch).
- Sorting of the flat list by column (the list is shown in the server's order).
- Page tests for the board's card move, the department-level backlog and the move menu.
- The contract gap of section 4.
- A whole-suite run: due before the MR (`VERIFY_FULL=1`), not run in this lane.
