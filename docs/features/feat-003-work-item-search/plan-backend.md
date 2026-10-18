# Plan (backend): work item list filters (feat-003)

Stage 4a. Contract: [api-contract.md](api-contract.md). Rules BR-WS-n: [spec.md](spec.md). Base: main with mod-005 (ADR-0030), mod-017 and mod-020; feat-040 (in flight) shares the filter classes (§ 9). M with UI: plan and code in one pass, then the frontend stage.

## 1. What changes

- `GET …/workitems` takes three new query parameters beside `page` and `teamId`; its rows' `assignee` gains `isActive` (OQ-2 A).
- One new route, `GET …/workitems/assignee-options` (the «Исполнитель» options), behind `workitems.workitem.read` (ADR-0029 § 7: a read inside one project needs only the gate). No migration, no new table, no new error code, no new permission code, no new package.
- Slices: `Features/WorkItems/GetWorkItems` (changed), `Features/WorkItems/GetAssigneeOptions` (new; no slice references another). Domain: `WorkItemFilter` (+ `WorkItemKeyword`). Infrastructure: `WorkItemFilterPredicate`, `WorkItemSets`, `WorkItemReadModel`. Api: `WorkItemEndpoints`.

## 2. Request parameters (final names; the design's were provisional)

| Parameter | Shape | Value |
|---|---|---|
| `state` | repeated, one per chosen name | a state name, compared as stored, ordinally, letter case included (`StateNameRule`) |
| `assigneeId` | repeated | a user id as stored (opaque, ordinal), or `none` («Не назначен») |
| `search` | one | the keyword; given twice, the first non-blank value counts |

- **Blank left out.** An empty or whitespace-only `state`, `assigneeId` or `search` value is dropped; nothing left means the dimension is not set (BR-WS-11, review R-2). `teamId` keeps mod-017's server reading (a blank value is set and names nothing: pinned by `WorkItemFilterTests`); the panel never sends one.
- **A value that names nothing** leaves its dimension set and selects nothing, with the same status and body for every kind (BR-WS-10): an unknown state name or other letter case; a malformed, unknown, foreign or unassigned user id. A value holding U+0000 (PostgreSQL text cannot) or longer than its column (255) is dropped before SQL and counts as such a value, never an error. A value never beside a valid one adds anything.
- Dimensions combine with AND; values within `state`, `assigneeId`, `teamId` with OR (BR-WS-2). Order, 50 a page and `count` follow the matched set (BR-WS-12).

## 3. The keyword (BR-WS-9, review R-6)

Domain `WorkItemKeyword`, parsed once from the first non-blank `search`: trimmed (`Trim()`, every Unicode white space); `MatchesNothing` when longer than 255 UTF-16 units after trimming, holding a line break (the title's set, `WorkItemText`) or U+0000. Otherwise an item matches on any of:
- (a) the title contains the text: `strpos(replace(lower(title COLLATE "ru-x-icu"), 'ё', 'е'), replace(lower(@text COLLATE "ru-x-icu"), 'ё', 'е')) > 0` (the parent picker's collation, `OrganizationsCollation.Russian`; `strpos` has no wildcards, so `%`, `_` and `\` are literal and nothing is escaped);
- (b) the text is the item's identifier: ASCII-only text, upper-cased, parsed by `WorkItemIdentifier.TryParse`; its project key must be the addressed project's (the handler passes `ProjectInfo.Key`), then `number = n AND type key = k` (the type key through a subquery on `work_item_types`: no join, no extra statement). A part of an identifier, another type's key, another project's key, `042` do not parse to an item;
- (c) the text is the item's number: `^[1-9][0-9]{0,9}$` and fits `int`; a longer or overflowing number is only text for (a), never an error.
The item is counted once however many of (a)-(c) hold (one predicate, no join row).

## 4. «Удалена» (BR-WS-4, ADR-0030 rule 2)

- No `state`: `WorkItemSets.Live` as today. `state` set: the new `WorkItemSets.Listing(context, projectId, statesSet)` = `Live` ∪ `Deleted` (each restricted to the project, one `UNION ALL`), and `WorkItemFilterPredicate` narrows by name: `EXISTS (state_definitions: id = item.state_id AND name = ANY(@names))`. A deleted item is listed only when a chosen name is its own state's name; "deleted" is never spelled again (the category is `WorkItemSets`'). No raw SQL, no `context.WorkItems` outside `WorkItemSets`.
- The predicate takes the context (`Apply(context, items, filter)`): the state and type-key subqueries need it. Its three call sites (the list, the project backlog's two memberships) change; those two pass a filter that sets only the team dimension, so they read as before.
- Rows: `DeletedItemSurfaceMatrixTests` row 1 gains the list under `state`, a new row the options; `DeletedStateParityTests` gains the list and the options over the every-state fixture.

## 5. The options read (BR-WS-6, -7)

`IWorkItemReadModel.GetAssigneeOptionsAsync(projectId, stateNames)`: the distinct assignee ids of the project's `Live` items, plus those of its `Deleted` items whose state name is among `stateNames` (so "while «Статус» names «Удалена»" is judged by data and category, the panel computes nothing). One statement for the ids; `UserDirectoryBatches.LookupAllAsync` (one statement per 500 ids, BR-UP-17); `ProjectMembers.MemberIdsAsync` (one statement); then `UserRefOrder.Order`: known by display name (primary strength), then id; unknown by id. Each option is `{ id, displayName | null, email | null, isActive, isMember }` (unknown: both null, `isActive` false). «Не назначен» is the panel's constant, last; the read returns people only. Answers 404 organization/project as the list does (`GetWorkItemsHandler`'s order of answers).

## 6. The list row's assignee (OQ-2 A)

`WorkItemListItemView.Assignee` becomes `WorkItemListAssigneeView(Id, DisplayName, Email, IsActive, IsMember)`; `isActive` is the directory's flag, false for an unknown id (as mod-005's deleter). The card's `WorkItemAssigneeView` is untouched (its shape is pinned by `SaveWorkItemEndpointTests`; feat-017 gives it the flag). Restated in api-contract § 1; note in mod-003's status.md.

## 7. Speed, shape, containment

- Statements: the list keeps its 7 (the filters are composed in the same two statements); the options read is 3 + ⌈people/500⌉ (project, ids, members, lookups). `FilterStatementCountTests` pins both for 50 and 5 000 items, 50 and 2 000 people, every dimension and all four at once.
- 2 s at 100 000 items in the organization, 5 000 in the project, 500 deleted, 2 000 people: `WorkItemFilterVolumeTests` (first page and count unfiltered, each dimension, all four with «Удалена»; the options).
- Containment: the routes sit under the organization and project (non-member 404 organization, foreign/deactivated project 404 project); no value is validated or disclosed; the keyword and ids are matched inside the project's items only. The new route gets its row in `OrganizationAddressedRoutes` (59 to 60) and `WorkItemsOpenApiTests`.

## 8. Tests to add

Domain: `WorkItemFilterTests` (blank, dedupe, NUL, long values, `none`, first search), `WorkItemKeywordTests` (trim, 255, line breaks, ASCII upper, identifier vs number vs overflow, other project's key). Infrastructure: `WorkItemSetsTests` (Listing), `DeletedStateParityTests` rows, `FilterStatementCountTests`. Api integration: `WorkItemStateFilterEndpointTests` (US-1), `WorkItemAssigneeFilterEndpointTests` (US-2, options, marks, order, containment), `WorkItemKeywordEndpointTests` (US-3, every table row), `WorkItemFilterNonDisclosureTests` (one status and body per kind), `WorkItemFilterVolumeTests`; `DeletedItemSurfaceMatrixTests` and `OrganizationAddressedRoutes` extended. Each test names its story and rule in a comment.

**Retired:** none by name (checked: no test pins the list row's assignee keys, a list `state`/`search`/`assigneeId` parameter, or "no way to list deleted items"; `List_Row_CarriesEveryColumn` is extended with `isActive`). **Superseded text, not tests:** mod-003 § 1.2, § 2.1; mod-017 § 2.1, § 3; mod-005 api-contract § 3.2 ("no parameter lists deleted items").
**E2E (standing NO; not run, not edited):** the backend change breaks no spec (additive). The frontend stage will meet the new band in `22-work-items.spec.ts` (list rows, trail back) and `27-work-item-card.spec.ts` ("from the list a title opens the full page"); both use exact or card-scoped locators, so none is expected to break.

## 9. Merge with feat-040

It adds `ProjectsSet/ProjectIds` (init members), `OfProjects` and a project branch in `Apply`, and two `Apply` call sites in `BacklogMembership`. This line adds init members and `Of(…)` at the end of `WorkItemFilter`, new files for the keyword, and the `context` argument: the second to merge adds `context,` to the call sites and notes it in its status.md.

## As built

(deviations are recorded here)
