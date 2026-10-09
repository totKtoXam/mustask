# Code review: feat-026-backlog-hierarchy-ui (with feat-001-work-item-links)

## Round 1 — whole hierarchy

Reviewed at `fab7252`, the tip of `feat/026-backlog-hierarchy-ui`, against
`cd4b07f~1` (`68ca742`). The range is `git diff cd4b07f~1..fab7252`: 127
files. It holds three things:

- feat-001's first delivery, squashed into main as `cd4b07f` without a review
  (feat-001 D-10);
- the queue commit `a67c8e0`;
- feat-026's commits `9e76261`..`fab7252` (`65d0883`, `d5b1086`, `323ab12`,
  `15d861c` carry the code).

This is the one round over both tasks, backend and frontend, with security
in its scope (feat-026 D-1). There is no delta round and no separate security
pass. These decisions are binding:

- feat-001: D-2..D-9. D-8 OQ-1: the deleted-item rules are enforced on the
  server through the Removed category. D-9: a plain table with Tab stops, not
  a treegrid.
- feat-026: D-2..D-5.
- D-5 and D-3 of both tasks: every signed-in user may do everything inside an
  active organization.

Known and decided, not reported:

- the timing of `BacklogVolumeTests`;
- the E2E run in parallel;
- the pointer drag exercised in jsdom only;
- the short route without QA, design or security sessions;
- feat-001 merged before review.

Each finding says whether its code is feat-001's (already in main) or
feat-026's.

**Verdict: changes-requested.** No blocker, one major (F1), four minors
(m1–m4). The minors are debts, each with its proposed fix.

### What was checked and holds

- **Storage and migration (feat-001).** `parent_id` is one nullable column,
  so BR-H-1's "one parent at most" is a property of the schema. A replace is
  one UPDATE of one row.
  - The composite FK `(project_id, parent_id)` → alternate key
    `(project_id, id)` has `Restrict`. A null `parent_id` is not checked
    (MATCH SIMPLE). So a parent of another project cannot be stored at all
    (`WorkItemConfiguration.cs`, `20261006055330_AddWorkItemLinks.cs:14-59`).
  - `ck_work_items_parent_not_self` refuses the self-link.
  - The index `(project_id, parent_id)` serves the children read and the FK.
  - Down drops, in order: the FK, the lock table, the alternate key, the
    index, the check and the column. The FK goes before the key it needs.
  - `MigrationsTests.cs` rolls back to the migration before and re-applies.
- **Authorization and IDOR (feat-001).** The two new routes carry
  `AuthenticatedOnly` and `RequiresOrganizationContext`
  (`WorkItemEndpoints.cs:90-106`). They are in the route inventory
  (`OrganizationAddressedRoutes.cs:49-51`), and the PATCH keeps both.
  - Every handler re-checks that the project is reachable and belongs to the
    addressed organization. It then checks the identifier against the
    project key and the item's type key (`SetWorkItemParentHandler.cs:119-150`,
    `GetParentCandidatesHandler.cs:29-55`, `SaveWorkItemHandler.cs:55-89`).
  - The new and stored parents are loaded only `WHERE id = … AND project_id =
    child.project_id` (`WorkItemRepository.cs:130-137`).
  - A malformed id, an unknown id, an id of another project and an id of
    another organization all answer the same 404 `parent_not_found`
    (`ParentChange.cs:23-31`; test `SetWorkItemParentEndpointTests.cs:210`).
  - A foreign `seenParentId` only mismatches, so it reveals nothing.
  - The cycle path names only ancestors of the new parent, all in the same
    project.
  - The candidates are filtered to the project, the allowed types, not the
    item itself and not Removed (`WorkItemReadModel.cs:59-67`).
  - Every SQL statement is interpolated into parameters, and the text search
    is EF-parameterized. There are no secrets and no new logging.
  - Error bodies carry catalog texts only. The `cycle` params are
    identifiers of the same project.
- **Rules and refusal order (feat-001).** `WorkItemHierarchy.Write`
  (`WorkItemHierarchy.cs:131-185`) checks in the contract's order:
  1. deleted (the child, then the new parent);
  2. `parent_changed`, where having seen none matches a stored parent in
     Removed;
  3. self;
  4. level by rank (BR-H-2: `>=`, so same level and skipped levels are
     allowed);
  5. cycle.

  The stored parent named again is a no-op that keeps «Изменён». «Изменён»
  moves on the child, on the removed parent unless it is in Removed, and on
  the added parent (`:175-182`).

  The PATCH order follows api-contract § 3.3:
  1. the address;
  2. 400;
  3. area 404, then parent 404;
  4. state and assignee 409s, then the link 409s.

  The test is `SaveWorkItemParentEndpointTests.cs:158`. For the one gap in
  the deleted check on this path, see m1. `levelAbove` takes the lowest rank
  above the item's own (`WorkItemHierarchy.cs:56-69`). The seeded results
  are tested: FEAT for US/BUG, EPIC for FEAT, none for EPIC, US+BUG for TASK.
- **Concurrency and deadlocks (feat-001).** The lock order is the same in
  both writers. Each parent write locks:
  1. the project's `work_item_hierarchy_locks` row (insert-if-missing, then
     `FOR UPDATE`);
  2. the child `FOR UPDATE`;
  3. the area (PATCH only);
  4. the new and the stored parent `FOR NO KEY UPDATE`;
  5. then it walks the ancestry in one recursive CTE
     (`WorkItemRepository.cs:81-118`, `:148-165`).

  It runs under READ COMMITTED (`UnitOfWork.cs:31`), so each statement after
  the lock sees what the previous holder committed. Only parent writes change
  `parent_id`, so the stale check and the cycle check decide on current data.
  Two commit-gate tests prove it: a stale write (`:257`) and a cycle closed
  from both ends (`:283`). The CTE's `UNION` over whole rows ends even on a
  stored cycle.

  No lock cycle with the other writers. The mod-003 save without a parent
  locks one item. A backlog reorder's FK check takes `FOR KEY SHARE` on its
  items. That conflicts only with the child's `FOR UPDATE`, and the parent
  write then never waits on anything the reorder holds. The parents' `FOR NO
  KEY UPDATE` is compatible with it. Tests: a reorder against a mapping
  (`BacklogTreeEndpointTests.cs:265`) and a save against a PUT
  (`SetWorkItemParentEndpointTests.cs:311`).
- **Deleted items (feat-001 D-8 OQ-1).** `HierarchyTypes.IsDeleted` reads
  the type's Removed states (`ParentChange.cs:49-62`). The candidates and the
  form's children exclude Removed (`WorkItemReadModel.cs:66`, `:190-195`).
  The form's parent includes a Removed parent. The backlog's `parent` comes
  through `FindLiveParentsAsync`, which excludes Removed
  (`BacklogReadModel.cs:72-90`). No backlog holds a Removed item, so nothing
  nests under one. US-7 is tested by setting the state in SQL, through PUT,
  PATCH, the read, the candidates and the backlog.
- **The backlog tree (feat-001 pass B).** The levels are read one by one:
  epics → features → stories while the process has them
  (`GetBacklogHandler.cs:66-80`, `BoardLevel.cs` `Below`).
  - Each level comes through mod-006's membership and stack rank.
  - `BacklogTree.Nest` builds from the bottom up. It keys children by the
    parent's id among the level directly above only, so a parent two levels
    up, on another team's backlog or on its own level nests under nothing.
  - Children keep their own backlog's order.
  - «Порядок» is given to top-level rows only (`BoardViews.cs`).
  - The statement count is fixed: per level the area tree, the members and
    the assignees, then one live-parents query.
  - The board is unchanged, and its cards carry no links
    (`BacklogTreeEndpointTests.cs:156`).
- **Parent candidates.** The text is trimmed and compared lower-case
  against the title (folded under ICU collation) or the identifier built in
  SQL. A text longer than the 255-character title limit matches nothing. At
  most 20 come back, plus one more read to set `moreMatched`, by `changedAt`
  descending and then number descending. A non-boolean `levelAbove` is a 400.
- **Form section (feat-001 frontend A).** `changesOf` sends `parent` only
  when the chosen id differs from the loaded one, with the loaded parent as
  `seenParentId` (`draft.ts:72-77`).
  - On `parent_changed` the form reads the item outside the cache.
    `rebaseDraft` keeps the touched fields and takes the stored parent
    (`draft.ts:92-109`).
  - The fix `a151a4a` holds: a failed re-read leaves the cache entry and the
    form alone, shows the read's error in the `Alert` and keeps the refusal
    under «Родитель» (`queries.ts:42-53`, `WorkItemFormPage.tsx:223-238`).
    For a small window in it, see m4.
  - The `link.*` codes land on «Родитель», never on a frame (`refusals.ts:27-37`).
  - The picker asks nothing until the list first opens, debounces the text,
    keeps the chosen value among MUI's options and uses `describeChild`, so
    the «Родитель» link's name stays the identifier.
- **Backlog tree and mapping UI (feat-026).**
  - `treeLines` draws only expanded children (`tree.ts:29-41`).
  - The expanded set is table state, by item id (`BacklogTable.tsx:159`).
    The table is keyed by level and the page by team, so another level, team
    or a reload starts collapsed. For the failed re-read case, see m2.
  - Moves operate on top-level rows, and children travel inside the row's
    data. The drop gap is computed over a whole block (`:381-405`). Nested
    rows get no «Порядок», handle or «Ещё действия».
  - «Убрать родителя» shows «Нет» at once through `useMoveView`. It writes
    `seenParentId` = the row's live parent, reads again, then shows the
    notice. A refusal puts the cell back from the read (`:242-272`).
  - The dialog is `FormDialog`, `maxWidth="sm"`, and busy per § 9.1.
  - On `parent_changed` it reads again and preselects the stored parent
    (`:307-320`).
  - A row whose parent is in Removed reads «Нет», opens the dialog empty and
    sends `seenParentId: null` (test `BacklogPage.mapping.test.tsx:539`).
  - While a write is saved, handles and «Ещё действия» are `aria-disabled`
    and inert (`off`).
  - Focus returns to «Ещё действия» after the menu, the dialog and the write.
- **Level drop-down and rename (feat-026).**
  - `LevelMenu`: a `Button` «Уровень: <уровень>» with `ExpandMore`,
    `aria-haspopup`, `aria-expanded` and `aria-controls`. ArrowDown opens
    too. The `selectedMenu` variant opens on the current level, and each item
    is a `RouterLink` to its address with `aria-current="page"`.
  - The page component is keyed by team only, so the button stays mounted
    and keeps focus across levels.
  - The rename covers the entry, the `h1` and the tab, the region, «Показать
    канбан» and «Канбан пуст». The `boards` id and segment are unchanged.
- **Accessibility of the plain table (D-9 accepted).**
  - The expand control is a native button with «Развернуть/Свернуть <ID>»
    and `aria-expanded`. Focus stays on it.
  - The Tab order is: handle, expand, title link, parent link, actions.
  - The nesting is carried by the indent and the «Родитель» column, as the
    spec decides. No defect found beyond what D-9 accepts.
- **Conventions.**
  - Reusable logic sits in Domain (`WorkItemHierarchy`, `BacklogTree`) and
    Abstractions (`ParentChange`, `ProcessHierarchy`). No slice references
    another.
  - Reads are `AsNoTracking` projections with a fixed statement count. Raw
    SQL is parameterized and confined to Infrastructure.
  - The frontend imports across slices through barrels only
    (`../../workitems`).
  - The reference tracker is not named anywhere in the diff.
- **Tests.**
  - Every backend Gherkin scenario of US-1/2/3/4/5/7 is named in an
    integration class and asserts what it names. Read in full: the
    SetWorkItemParent concurrency and US-7 tests, the Save order test, the
    replace tests with their «Изменён» checks, the tree reorder and map
    tests, and the volume test.
  - Frontend scenarios were matched by title to US-1..US-7. Read in full:
    the tree tests that keep expanded state, reorder and drag.
  - There is no story scenario without a test, except US-6 "The board itself
    is unchanged", which rests on mod-006's existing board tests. For the
    gap behind F1, see F1.

### Findings

| # | Severity | Origin | Where | Finding | Proposed fix |
|---|---|---|---|---|---|
| F1 | major | feat-026 | `src/frontend/src/features/boards/components/BacklogTable.tsx:279`, `:292`, `:448`, `:464-466`; `src/frontend/src/features/boards/components/ChangeParentDialog.tsx:189` | **A backlog re-read while «Изменить родителя» is open lets the save silently overwrite another user's mapping.**<br><br>The dialog's choice is set once, at open (`chosen: actionsRow.parent`, `:448`). The row it compares against and writes from is the latest read (`rows.find(…) ?? dialog.row`, `:279`; `dialogRow`, `:464-466`). This page re-reads when the tab regains focus (`refetchOnWindowFocus: true`, `hooks/queries.ts:15`, BR-M4-28).<br><br>Sequence:<br>1. Борис opens the dialog on MSK-US-2. MSK-FEAT-6 is preselected.<br>2. Анна maps MSK-US-2 to MSK-FEAT-8.<br>3. Борис switches tabs and back. The re-read brings MSK-FEAT-8 into `dialogRow.parent`, but the field still shows MSK-FEAT-6.<br>4. «Сохранить» turns on without any choice (`value.id !== row.parent.id`).<br>5. Pressing it, or picking MSK-FEAT-23, sends `{ parentId, seenParentId: <MSK-FEAT-8> }`.<br>6. The server accepts. Анна's link is overwritten with no `parent_changed`.<br><br>Rules not met:<br>- US-5 "A parent changed meanwhile refuses the mapping" and BR-H-4, the one stale-view guard the backend's per-project lock exists to make reliable;<br>- the dialog's "«Сохранить» (disabled until a different parent is chosen)" (spec § Page «Бэклоги»). | Decide on the row as the dialog showed it.<br>- Use `dialog.row.parent` for both `seenParentId` (`:292`) and the dialog's `submitDisabled` (pass `dialog.row` or its parent instead of `dialogRow` for that check).<br>- `dialog.row` is already refreshed on the `parent_changed` path (`:314`), so a retry still sends the stored parent.<br>- A change that lands meanwhile is then refused with `parent_changed`, and the existing path reads again and preselects.<br><br>Add a component test: open the dialog, change the parent in the in-memory API, refocus, check «Сохранить» is still disabled, pick another feature, save, and expect the refusal under «Родитель» with the stored parent preselected. |
| m1 | minor | feat-001 | `src/backend/Mustask.Application/Features/WorkItems/SaveWorkItem/SaveWorkItemHandler.cs:112-117`; `src/backend/Mustask.Domain/WorkItems/WorkItemHierarchy.cs:140` | **On the PATCH path the deleted check reads the state after `item.Save` has applied the body's `stateId`.**<br><br>mod-003 § 2.4 allows any non-Removed state "from any other", so «Удалена» can be left too. A PATCH `{ stateId: <Активна>, parent: {…} }` on an item in «Удалена» therefore passes `item_deleted` and links it. BR-H-8 ("no link write touches an item in «Удалена»") does not hold on that path. PUT is not affected. It cannot be reached until mod-005 puts items in «Удалена». | Capture `IsDeleted(item)` on the stored state before `item.Save`, and pass it into the decision (or check it first, keeping the 409 order). Add an integration test that sets the state in SQL and PATCHes `stateId` + `parent`. Alternatively, note it on mod-005's queue line, which owns the read-only form of a deleted item. |
| m2 | minor | feat-026 (pattern from mod-006) | `src/frontend/src/features/boards/pages/BacklogPage.tsx:90`, `:111-112`; `src/frontend/src/features/boards/components/BacklogTable.tsx:159`, `:297` | **A re-read that fails unmounts `BacklogTable`.**<br><br>Any failed re-read does it: a focus refetch, or the read after a parent write when the server is unreachable. TanStack sets `isError` while it keeps the data, and the page swaps the table for `ErrorBanner`. Effects:<br>- the expanded set resets, though BR-H-12 says it is kept "across its re-reads";<br>- an open «Изменить родителя» disappears with the "no answer" `Alert` the spec puts in it. The component test passes only because its read succeeds after a failed PUT. | Keep the table mounted while `query.data` exists, and show a failed re-read as an `ErrorBanner` above it. Or keep the table's expanded set and dialog state in `TeamBacklog`. Add a test where both the PUT and the read after it fail. |
| m3 | minor | feat-026 (D-7, BR-H-15/16), prose outside the diff | `docs/features/mod-015-app-shell-design-system/design.md:444`, `:972`; `docs/features/mod-006-boards-backlogs/spec.md:225`, `:259` | **Prose outside the diff still describes «Доски» with the level tabs.** frontend.md cites mod-015's design.md as the shared-pattern catalogue every module's design fills. Its navigation row and its board page (`PageHeader «Доски», tabs «Эпики», «Фичи», «Истории»`) still read that way, and so do mod-006's § Page tables. Nothing marks them superseded by feat-001 BR-H-15/16. The API change was annotated in mod-003's and mod-006's contracts and Logs. The UI change was not. An agent designing the next board feature from mod-015's catalogue would rebuild the tabs. | Append "superseded by feat-001 BR-H-15, BR-H-16: «Канбан», level drop-down" at those rows, or add one Log line each to mod-006's and mod-015's `status.md`, as was done for the contract. |
| m4 | minor | feat-001 | `src/frontend/src/features/workitems/pages/WorkItemFormPage.tsx:185`, `:223-227` | **After `parent_changed`, edits typed during the re-read are lost.** `busy` is `save.isPending`, which is already false while `refresh()` runs, so the fields are editable. `rebaseDraft(item, fresh, draft)` then lays the save-time `draft` over the fresh read. The window is one GET. | Keep the form busy until the re-read settles. Or rebase from the latest edits (`setEdited((current) => rebaseDraft(item, fresh, current ?? draft))`). |

### Behavioural claims in prose (item 6)

Direction 1 covers claims in prose the diff adds, read against the code.

- feat-001 api-contract § 3.2 Concurrency, "Parent writes of one project run
  one after another: each takes the project's hierarchy lock before it reads
  anything it decides on". Anchors:
  - `WorkItemRepository.cs:81-91`
  - `SetWorkItemParentHandler.cs:142-153`
  - `SaveWorkItemHandler.cs:76-101`
  - tests `SetWorkItemParentEndpointTests.cs:257`, `:283`

  Holds.
- § 3.2, "a parent write and a backlog reorder of the same item both take
  effect", and "a parent write and a form save of other fields … both take
  effect". Anchors: `BacklogTreeEndpointTests.cs:265` and
  `SetWorkItemParentEndpointTests.cs:311`. Holds.
- § 3.2 checks 3–7 in order, "a parentId equal to the stored parent changes
  nothing, changedAt included", and the removed parent in «Удалена» "stays
  exactly as it was". Anchors:
  - `WorkItemHierarchy.cs:140-182`
  - `WorkItemHierarchyTests.cs:357`
  - `SetWorkItemParentEndpointTests.cs:105`
  - `SaveWorkItemParentEndpointTests.cs:181`

  Holds for PUT. For PATCH see **m1**.
- § 0.3 / BR-H-1, "the answer is the same in all four cases". Anchors:
  `ParentChange.cs:23-31`, `WorkItemRepository.cs:99-106` and test `:210`.
  Holds.
- § 3.3, "`"parent": null` or no `parent` leaves the parent unchanged". Anchors:
  `WorkItemEndpoints.cs` (`request?.Parent is { }`),
  `SaveWorkItemHandler.cs:76,98` and test `SaveWorkItemParentEndpointTests.cs:97`.
  Holds.
- § 3.3 order of answers. Anchors: `SaveWorkItemHandler.cs:92-117` and test
  `:158`. Holds, except that the deleted check is evaluated after the state
  is applied (**m1**).
- § 3.4, "never the item itself, never an item in «Удалена»", "A text longer
  than 255 characters matches nothing", "at most 20 … moreMatched", "Its
  descendants are offered", "Any other value fails binding: 400". Anchors:
  `WorkItemReadModel.cs:59-98`, `GetParentCandidatesHandler.cs:57-64` and the
  tests at `GetParentCandidatesEndpointTests.cs:119-199`. Holds. The 400 test
  asserts the status only, not `http.bad_request`. That is not a finding:
  the platform answers it.
- § 2.2 children, "those in «Удалена» excepted, by level rank descending, then
  by number". Anchors: `WorkItemReadModel.cs:187-207` and
  `GetWorkItemLinksEndpointTests.cs:39`, `:110`. Holds.
- § 4 `parent` "null when there is none or it is in «Удалена»", and `children`
  "in that backlog's own order". "«Эпики» answers up to three tiers". Anchors:
  - `BacklogReadModel.cs:72-90`
  - `BoardViews.cs` `ParentOf`
  - `BacklogTree.cs:36-67`
  - `GetBacklogHandler.cs:66-80`
  - tests `BacklogTreeEndpointTests.cs:38`, `:69`, `:290`

  Holds.
- feat-001 Plan A3, "a reorder's FK check (FOR KEY SHARE) is never waited on,
  so no deadlock". The statement is about the parents' `FOR NO KEY UPDATE`
  (`WorkItemRepository.cs:130-137`), and it holds there. The child's `FOR
  UPDATE` does make a reorder wait (the test at `BacklogTreeEndpointTests.cs:265`
  relies on it), but with no lock cycle. Holds as scoped. Not a finding.
- `IWorkItemRepository.cs:5-13` lock order, and "No holder of an area's lock
  waits on an item". Anchors: both handlers follow it, and mod-002's
  deletion was not re-read (see Not read). Holds as far as read.
- `WorkItemRepository.cs:139-147`, "the statement ends even if a defect ever
  stored a cycle". `UNION` over the full row tuple deduplicates a revisited
  row. Holds.
- `WorkItemReadModel.cs:17-18`, "One item adds one statement for its links".
  Anchor: `:187-195`. Holds.
- mod-003 api-contract § 1.1/§ 2.4 additions (in the diff), "its five 409s
  follow the assignee's". Anchor: `SaveWorkItemHandler.cs:112-117`. Holds,
  apart from m1.
- mod-006 api-contract § 3.1 addition, "nested row without `order`", "the
  board's cards do not". Anchors: `BoardViews.cs` (`BacklogChildView`) and
  `BacklogTreeEndpointTests.cs:48`, `:156`. Holds.
- feat-001 spec § Page «Бэклоги», dialog "«Сохранить» (disabled until a
  different parent is chosen)", and US-5 "A parent changed meanwhile refuses
  the mapping". Anchors: `ChangeParentDialog.tsx:189`,
  `BacklogTable.tsx:279-292`. Holds only while no re-read lands under the
  open dialog → **F1**.
- spec BR-H-12, "What is expanded is kept … across its re-reads", and the
  feat-026 Log, "the expanded set kept across re-reads". Anchors:
  `BacklogTable.tsx:159`, `BacklogPage.tsx:121-131` and
  `BacklogPage.tree.test.tsx:220`. Holds for successful re-reads. A failed
  one resets it → **m2**.
- spec § Page «Бэклоги», "Any other refusal, or no answer, is the dialog's
  `Alert`". Anchors: `BacklogTable.tsx:309-319`, `refusals.ts:82-95` and
  `BacklogPage.mapping.test.tsx:422`. Holds when the read after the write
  succeeds → **m2**.
- spec § Page «Бэклоги» «Убрать родителя», "«Нет» at once; while saved,
  every handle and «Ещё действия» is off … A refusal puts the cell back".
  Anchors: `BacklogTable.tsx:242-272`, `:175` and tests `:391-466`. Holds.
- spec § Screens and copy (form), "`parent_changed` also sets the field to the
  stored parent from a fresh read, other drafted fields kept". Anchors:
  `WorkItemFormPage.tsx:223-238`, `draft.ts:92-109` and
  `WorkItemFormPage.related.test.tsx:544`, `:578`. Holds, with the window
  in **m4**.
- spec § Level drop-down keyboard and focus. Anchors: `LevelMenu.tsx:41-80`
  and `BacklogPage.levels.test.tsx:71`, `:153`. Holds.
- spec § The rename table. Anchors:
  - `app/navigation/model.ts:76`
  - `BoardPage.tsx:101`, `:121`
  - `BoardColumns.tsx:244`
  - `BacklogPage.tsx:26`
  - `BacklogPage.levels.test.tsx:211`

  Holds.
- spec § NFR, "Nested rows are rendered only while shown". Anchor:
  `tree.ts:29-41`. Holds.
- `ParentPicker.tsx:27`, "Nothing is asked until the list first opens".
  Anchor: `:77-80` (`enabled: opened`). Holds.
- feat-026 Log, "handles and menus off while saved", "`parent_changed` read
  again and preselected", "No contract mismatch". Anchors:
  `BacklogTable.tsx:175`, `:307-320` and `boardsApi.ts`/`workItemsApi.ts`
  against § 2–§ 4. Holds.
- No `AGENTS.md` file is in the diff.

Direction 2 covers prose outside the diff about behaviour the diff changes.

- The board was renamed «Доски» → «Канбан», and the level tabs became a
  drop-down. I grepped «Доски», «Показать доску» and "tabs" under `docs/`,
  `.claude/`, `src/frontend/AGENTS.md` and `src/backend/AGENTS.md`. Hits:
  - mod-015 `design.md:58,78,294,322,444,972`, `spec.md:90,98` and
    `stories/US-3-navigation-model.md:56`;
  - mod-006 `spec.md:200,225,246,259,276` and its US-4 story;
  - doc-010 `spec.md` and its US-3 story (an analysis, by date and
    convention left as worded).

  The frontend comments that cite "spec § Page «Доски»" name mod-006's
  section titles and are accurate citations. The AGENTS files have no hits.
  mod-015's catalogue and mod-006's page tables are stale → **m3**.
- Backlog rows gain `parent`/`children`, `WorkItem` gains them, and the PATCH
  gains `parent`. I grepped `parent`, `children` and `BacklogItem` in mod-003's
  and mod-006's contracts and status files. Both were annotated in this diff,
  and nothing is stale.
- New routes `…/parent` and `…/parent-candidates`, and the table
  `work_item_hierarchy_locks`. Grep under `docs/` finds only feat-001's own
  artifacts, `error-codes.md` (generated) and the doc-002/003/005/010
  analyses, which use generic wording. None is stale.
- Not swept: the stand runbook and any E2E index for file 24. There is no
  `e2e/README`, and the run is the executor's.

### Not read

These were not read:

- the generated migration Designer and model snapshot;
- the frontend test doubles (`boards/testApi.ts`, `workitems/testApi.ts`,
  `testBacklog.ts`, `testFixtures.ts`, `testLevels.ts`). I skimmed them only
  for § 3.2's check order;
- the bodies of most frontend component tests. I read the names and matched
  them to US-1..US-7, and read the tree reorder and drag tests and the
  mapping refusal tests in part;
- `BoardPage.test.tsx` and `BoardPage.create.test.tsx` changes;
- e2e specs 19, 23 and 24, beyond a skim of 24's header and titles;
- `e2e/support/workItemsApi.ts`;
- the Kazakh texts in `contracts/errors/workitems.json`;
- `ChildrenList.tsx`, `Notice.tsx` beyond its diff, and `queryKeys.ts`
  beyond its diff;
- mod-002's area deletion, which the lock-order comment relies on: "no
  holder of an area's lock waits on an item".

No test was run.

Could not check by reading:

- the wire size of the «Эпики» read at 5 000 per level. It returns about
  15 000 rows, each nested row with a full `parent` `WorkItemRef`. The 2 s
  NFR is measured in-process (`BacklogTreeVolumeTests.cs`), without the
  network, the browser's JSON parse or the render of 5 000 top-level rows;
- how the plain table actually reads in a screen reader.
