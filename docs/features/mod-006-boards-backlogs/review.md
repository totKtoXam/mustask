# Code review: mod-006-boards-backlogs

## Round 1 — whole task

Reviewed at `3d9260f` (tip of `mod/006-boards-backlogs`) against `main`
(merge base `44e3bb3`), 22 commits, 123 files. Per D-1 this is the one round
over backend and frontend, authorization and IDOR included, no separate
security pass and no delta round. D-2's cut (no concurrency token, no
hierarchy, no filter bar, no customization, no deleted items, no iterations),
D-3 (every signed-in user may do everything inside an active organization),
D-5 and D-6 are treated as binding. Known and decided, not reported:
`BacklogVolumeTests` timing under the full suite, E2E not yet run, touch
input not starting a native drag.

**Verdict: changes-requested.** No blocker, one major (F1), five minors
(m1–m5), each a debt with its proposed fix.

### What was checked and holds

- **Conformance.** The three routes, their shapes, statuses and codes
  match api-contract.md § 0–§ 5 (`BoardEndpoints.cs:36-62`,
  `BoardViews.cs`, `BoardResponse.cs`, `contracts/errors/boards.json`). The
  answer order of § 0.4 holds: address outside in, then 400 on the
  position, then team-again and stale under the locks
  (`ReorderBacklogHandler.cs:43-109`). Level resolution goes by kind and
  rank, never by name (`BoardLevel.cs:55-67`; seeded ranks Эпики 4, Фичи 3,
  Истории 2). Columns per BR-M4-2, headers per § 3.3, Removed without a
  column (`BoardLayout.cs:65-94`). Screen copy, entries, icons, tabs, frames
  and notices match spec § Screens and copy. Nothing beyond D-2's cut.
- **Authorization and IDOR.** All three routes carry `AuthenticatedOnly`
  and `RequiresOrganizationContext` and are in the route inventory
  (`OrganizationAddressedRoutes.cs:48-51`). Each handler re-checks that the
  project is reachable and of the addressed organization, and the team of
  the project (`GetBacklogHandler.cs:34-46`, `GetBoardHandler.cs:34-46`,
  `ReorderBacklogHandler.cs:49-61`). The reorder can only place items that
  the membership query returns for that project, team and level
  (`BacklogMembership.cs:57-77`, `BacklogRepository.cs:48-55`), so no foreign
  item id can be written. The create's `teamId` is resolved inside the
  addressed project only (`WorkItemRepository.cs:41-53`). Every SQL is
  parameterized (`ExecuteSqlInterpolatedAsync`, `unnest({ids})`). No new
  logging; error bodies carry catalog texts only.
- **Reorder and concurrency.** Team `FOR SHARE` with an active re-check in
  a new statement, then `backlog_orders` insert-if-missing and
  `FOR UPDATE` (`BacklogRepository.cs:20-46`). Reorders of one backlog
  serialize, and each reads the committed order of the one before
  (`ReorderBacklogEndpointTests.cs:118-160`, real overlap with the commit
  gate). A team deleted while the reorder waits answers not found, and
  nothing is placed (`:304-326`). The stale refusal throws before any
  write, and the lock row's insert rolls back with it
  (`StackRank.cs:86-96`, `:164-180`). The departed places are deleted by
  key, re-checked on team and level. The upsert skips unchanged rows
  (`BacklogRepository.cs:57-92`). For the cross-backlog case see m1.
- **Create with `teamId`.** It comes after the type, the counter lock and
  the replay (`CreateWorkItemHandler.cs:86-107`). The default is read under
  the team's `FOR SHARE`. A default change takes `FOR NO KEY UPDATE`
  (`SetTeamDefaultAreaHandler.cs:68`) and a deletion `FOR UPDATE`
  (`TeamRepository.cs:17`), and a default area cannot be deleted or
  re-owned (mod-002 BR-M1-14, `DeleteAreaHandler.cs:88-92`). So the item's
  area cannot be pulled away under it. A replay after the team's deletion
  answers 200 (`CreateFromTeamEndpointTests.cs:183-196`). The lock order
  is counter, then team. No other path takes team then counter.
- **Migration `20261005134049_AddBoards`.** PKs `(team_id,
  backlog_level_id)` and `work_item_id`, `ck_backlog_places_position`. The
  place→work item FK cascades and the others restrict (nothing is purged,
  ADR-0026). Indexes serve the FKs, the departed-places read and the
  membership (`ix_work_items_project_id_type_id_area_id`). Down drops the
  places, then the orders, then the index (`AddBoards.cs:84-95`).
- **Query shape at 5 000.** A fixed number of statements: the area tree,
  one membership statement on the new index with the place joined by its
  PK, and one directory lookup (`BacklogReadModel.cs:19-66`). The order is
  applied in memory by the same `StackRank.Order` the reorder uses. The
  2 000 items of another level in the same area stay out of the read
  (`BacklogVolumeTests.cs:36-43`).
- **Frontend move and rollback.** `useMoveView` (`500ebd5`): the optimistic
  view while saving. Then the cache's read. Then the component's own data
  once it is no longer the data the move started from. A refusal and a
  failed read both end on the cache's data, so the row or card goes back
  to the read. The reorder's read runs in `onSettled` and is awaited by
  `mutateAsync` (TanStack Query 5), and the card move awaits `readAgain()`.
  So on `reorder_stale` the read lands before the notice
  (`mutations.ts:18-22`, `BacklogTable.tsx:121-149`,
  `BoardColumns.tsx:131-157`). One move at a time: `inFlight`,
  `aria-disabled` handles and menu buttons, `draggable={!saving}`, and a
  `dragstart` cancelled while saving. Retries are off app-wide
  (`App.tsx:14`), so a failed read cannot hold a page in `saving`.
- **Keyboard path and focus.** The handle is an `IconButton` as `span`
  (role button, Enter and Space open the menu). Entries are disabled per
  mod-015 BR-9 (`reorder.ts:27-36`). Focus goes back to the handle or to
  «Ещё действия» in the new column when a redraw drops it to `body`
  (`BacklogTable.tsx:106-119`, `BoardColumns.tsx:115-129`). Announcements
  go to a page-level `role="status"` (but see m5).
- **Shell changes.** The team-level address sync waits for the address
  project's teams and sets the team only when the active list holds it
  (`resolveSelection.ts:91-114`, `useOrganizationsContextSource.ts:147-180`).
  A deleted or foreign team sets nothing. Optional `views` make the level
  tabs `page` with no trail (`visibility.ts:113-123`). A team choice on
  either page opens the new team's «Истории» landing
  (`routes.boards.test.tsx:190-225`). For project and organization choices
  see F1.
- **mod-003 changes.** Additive and backward compatible. `teamId` is
  optional, and absent or `null` keeps the root
  (`CreateFromTeamEndpointTests.cs:198-213`,
  `CreateWorkItemDialog.test.tsx:97-99`). The dialog takes
  `CreatableType` and an optional `team`, and skips the area-tree read only
  then. `notFoundLevel` gains `team`, so the dialog's refusal closes into
  the page's frame. The barrel exports what the boards slice uses and
  nothing of its internals. Contract change recorded in both contracts and
  in mod-003's Log.
- **Tests.** Every backend-side Gherkin scenario of US-1..US-7 is named and
  asserts what it names (read: the reorder, board, create and US-7 classes).
  The frontend scenarios are matched by name, and `BacklogPage.reorder.test`
  was read in full: optimistic order, focus, announcement, busy state,
  stale-read-first, rollback, team-gone frame. For the gaps see F1, m3.

### Findings

| # | Severity | Where | Finding | Proposed fix |
|---|---|---|---|---|
| F1 | major | `src/frontend/src/features/organizations/context/useOrganizationsContextSource.ts:224-269` (`:230`, `:247-266`), `src/frontend/src/app/navigation/switchTarget.ts:39`, `src/frontend/src/app/navigation/visibility.ts:41-42`; test `src/frontend/src/app/navigation/switchTarget.test.ts:194-202` | **On «Бэклоги» or «Доски», choosing another project or organization in the switcher opens «Главная».** It should open the same entry's landing in the new context. Cause: `select` builds `next` with no `teamId` (`{ organizationId, projectId: id }` for a project, `{ organizationId, projectId: firstProject }` for an organization). `switchTarget` then finds the backlogs or boards entry by the address, and `isEntryVisible(entry, …, { selection: next })` rejects it, because a `team`-scoped entry needs `selection.teamId`, so it returns `HOME_PATH`. mod-015 BR-18, which spec § Screens binds ("Both pages are team-level (mod-015 BR-18)"), says a choice at a level the address carries opens "the landing page of the same navigation entry in the new context … when that entry is visible there". After a project choice the team level takes the project's first team (mod-002 design § 3.5), so the entry is visible there. mod-002 design § 2.2 already does the same one level up: an organization choice on a project page goes to "the same entry for the new organization's first project", and `select` fetches that project first. The unit test titled "a project chosen there opens the new project's first team's backlog" passes only because it hands the resolver a `next` with `teamId: 't2'`, which the real `select` never builds. The test claims a behaviour the product does not have. `routes.boards.test.tsx` covers the team choice only. | In `select`, when `carried.teamId !== undefined` and the level is `project`, `fetchQuery` the new project's teams (`organizationsKeys.teams(org, id)`) and resolve with `next.teamId` = the list's first entry. For `organization`, fetch the first project, then its teams. On a failed read, go to «Главная», as the organization branch does. Rewrite the unit test so its `next` is the one `select` builds, and add a routes test that chooses a project on a backlog page and lands on `…/teams/<first team>/backlogs/stories`. |
| m1 | minor | `src/backend/Mustask.Infrastructure/Boards/BacklogRepository.cs:8-17`, `:81-91`; `docs/features/mod-006-boards-backlogs/status.md` Plan backend 5 | The class comment overstates: "a reorder of another backlog that took or dropped the same item's place meanwhile is never undone by a stale read". The delete re-evaluates team and level and is safe. The upsert is not. Its `DO UPDATE … WHERE … IS DISTINCT FROM` re-checks only the row's values, not whether the item is still on this backlog. Sequence: reorder A reads x as a member; x's Область moves it to B; a reorder of B places x and commits; A's upsert then moves x's row back to A. x drops to B's unplaced bottom. Also, two such upserts that overlap on two or more rows lock them in ordinal order, not id order, so they can deadlock (40P01 → 500). Both need an area change and a whole second reorder inside one reorder's window, so the harm is small. | Weaken the comment and the Plan sentence to what holds: the delete re-evaluates, and the upsert is last writer wins on a row. Feed the upsert in `work_item_id` order (`… WITH ORDINALITY AS moved(id, ordinal) ORDER BY moved.id`, keeping `ordinal` as the position) so overlapping upserts lock rows in one order. |
| m2 | minor | `src/backend/Mustask.Infrastructure/Boards/BacklogReadModel.cs:63` | `tree.PreOrder().Single(node => node.DefaultOfTeamId == scope.TeamId)` throws if the team is deleted between the handler's address check and the tree read. Deletion clears `default_of_team_id` (`TeamRepository.cs:63`). Both reads (`GetBacklog`, `GetBoard`) then answer 500, and the page shows a generic `ErrorBanner` instead of «Команда не найдена». The window is milliseconds. | Use `SingleOrDefault`, and throw `NotFoundException(OrganizationErrorCodes.TeamNotFound)` on `null`, the answer § 0.3 gives a deleted team. |
| m3 | minor | `src/backend/tests/Mustask.Api.IntegrationTests/Features/Boards/GetBacklogEndpointTests.cs:166-215` (shared by the board and reorder theories) | No test pins the organization→project boundary on the three routes. If `project.OrganizationId != organizationId` were dropped from `GetBacklogHandler.cs:36`, `GetBoardHandler.cs:36` or `ReorderBacklogHandler.cs:51`, an organization's address could read or reorder another organization's backlog, and no test would fail. Spec § Not-found answers lists "one of another organization" for «Проект не найден». The code is correct today. | Add a `project-of-another-organization` case to `NotFoundAddressAsync`: a second organization's project and its first team under the first organization's id, expecting `organizations.project.not_found`. It then runs for the backlog, board and reorder theories. |
| m4 | minor | `docs/features/mod-002-organizations-projects-teams/plan-frontend.md:934-937` (outside the diff) | Item 7 says the source "does not sync a team from" a team-level address. That was true at mod-002, and this branch makes it false (`useOrganizationsContextSource.ts:147-180`). The sentence anticipated the change ("the first page that does adds the teams read"), so nobody acting on it is misled into a failure. It just reads stale next to the code. | Append "(mod-006 added the team sync: `useOrganizationsContextSource.ts`, `resolveSelection.ts` `addressSync`)" to item 7. |
| m5 | minor | `src/frontend/src/features/boards/components/StatusRegion.tsx:9-15`; `BacklogPage.tsx:67`, `BoardPage.tsx:72`, `BacklogTable.tsx:132`, `BoardColumns.tsx:140` | The announcement is a `useState` string. A move whose text equals the previous one (for example, a retry of the same «В начало» after a `reorder_stale` refusal) leaves the region's DOM unchanged, so a screen reader says nothing. That falls short of "every move … is announced" (BR-M4-8, spec § NFR Accessibility). | Clear the region before setting the new text (set `''`, then the text on the next frame), or key the region's text node by a move counter. Add one test that repeats the same move and expects the region to change. |

### Behavioural claims in prose (item 6)

Direction 1: claims in prose the diff adds, read against the code.

- spec § Model "the board shows the same items in the same order as the
  backlog" → `GetBoardHandler.cs:64-67` (same `ReadAsync`, same
  `InOrder`). Holds.
- spec § Model "reordering changes no field and not the item's «Изменён»
  time" → `BacklogRepository.cs:57-92` writes `backlog_places` only; test
  `ReorderBacklogEndpointTests.cs:88-100`. Holds.
- spec § Model "Places are given by a reorder only … An item holds at most
  one place" → `backlog_places` PK `work_item_id`
  (`AddBoards.cs:51`); the only writer is `SaveOrderAsync`. Holds.
- spec § Model / BR-M4-25 "reorders of one backlog apply one at a time, in
  arrival order" → `BacklogRepository.cs:36-43`; tests `:118-160`. Holds.
- BR-M4-26 "refused … and nothing changes" → `StackRank.cs:86-96` before
  `SaveOrderAsync`; test `:164-180`. Holds.
- BR-M4-27 codes declared → `BoardEndpoints.cs:24-32,40,48,58`,
  `OrganizationPermissionCatalog.cs:23-24`. Holds.
- BR-M4-28 "read again when it opens, after each write of its user, and
  when the tab regains focus" → default `staleTime` 0 (`App.tsx:10-17`),
  `useReadTeamAgain` after reorder, move and creation, and
  `refetchOnWindowFocus: true` (`queries.ts:15`). Holds.
- spec § Screens "the switcher follows the address, and choosing another
  team opens the same entry's landing page there" → `addressSync`
  (`resolveSelection.ts:91-114`), `select` (`useOrganizationsContextSource.ts:231,268`),
  `routes.boards.test.tsx:190-225`. Holds for the team. The spec's own
  reference to BR-18 for project and organization choices → **F1**.
- spec § Notices "the moved row or card returns to where it was",
  "`reorder_stale` … the backlog is read again first" → `useMoveView.ts`,
  `mutations.ts:21`, `BacklogTable.tsx:134-148`; test
  `BacklogPage.reorder.test.tsx:360-390`. Holds.
- spec § Model "the columns are derived from the organization's process on
  every read, never stored" → `GetBoardHandler.cs:58-62`. Holds.
- api-contract § 3.2 "`afterItemId` … ignored with top and bottom; equal to
  `itemId` leaves the item where it is" → `StackRank.cs:32-45,98-102`.
  Holds.
- api-contract § 7 "a default change or a deletion of that team waits for
  the creation, or the creation waits for it" →
  `WorkItemRepository.cs:41-53`, `SetTeamDefaultAreaHandler.cs:68`,
  `TeamRepository.cs:17`; test `CreateFromTeamEndpointTests.cs:160-181`.
  Holds.
- status.md Plan backend 5 and `BacklogRepository.cs:8-17`, "never undone
  by a stale read" → **m1**.
- status.md Log (backend) "commit-gate races (two reorders of different
  items and of the same item; a reorder and a creation each against a team
  deletion)" → the four gated tests named above. Holds.
- status.md Log (frontend) "the switcher takes a team-level address's team,
  a navigation entry may name sibling `views`" →
  `useOrganizationsContextSource.ts:147-172`, `types.ts:25-32`,
  `visibility.ts:118-122`. Holds.
- `BacklogMembership.cs:48-56` "a join on a unique key is planned as one
  lookup per item whatever the statistics say". This overstates the planner,
  which may pick a hash join, but it is rationale for the query's shape and
  nothing an agent would do differs. Not a finding.
- No `AGENTS.md` file is in the diff.

Direction 2: prose outside the diff about behaviour the diff changes.

- Changed behaviour: mod-003's create takes `teamId`. Grepped "root area" /
  «корнев» / `teamId` under `docs/features/mod-003-work-item-core/` and
  mod-002 BR-M1-14. Hits: `api-contract.md:181-202` (updated in the diff);
  `spec.md:92,135` (BR-M3-20 already names mod-006); `spec.md:200` (the
  list's dialog text, unchanged and still true); `stories/US-6:55-57` ("in
  this task" a creation cannot race a deletion; still true, and the new
  path is covered by the default-area rule); mod-002 `spec.md:115` BR-M1-14
  (consistent). None is stale.
- Changed behaviour: the context source syncs the team from a team-level
  address. Grepped `addressContext`, "team-level", `teams/:t` under `docs/`.
  Hit `mod-002 plan-frontend.md:934-937` is stale → **m4**. Hits
  `mod-002 design.md:124-125` and `plan-frontend.md:103` are scoped to
  mod-002's own pages. ADR-0024 and `frontend.md:78-80` are consistent.
- Changed behaviour: navigation entries «Доски» and «Бэклоги» exist, and
  sibling `views` are `page`. Grepped `aria-current` and the entry names
  under mod-015's docs. `design.md:444-445` says "In this build: no
  (mod-006)", a column about mod-015's build, as mod-003's row is. No
  contradiction.
- Not swept: `src/frontend/AGENTS.md` and the stand runbook for any mention
  of E2E spec 23 (the E2E run is in flight and owned elsewhere).

### Not read

The generated migration Designer and model snapshot. The frontend test
doubles `testApi.ts`, `testFixtures.ts`, `testRender.tsx`, `testBoard.ts`,
`testDrag.ts`, and the backend `BoardsTestData.cs`. The bodies of most
frontend component tests (names read and matched to the stories;
`BacklogPage.reorder.test.tsx` read in full). The domain unit-test bodies
(names read). `BoardSkeleton.tsx` and `backlogColumns.ts`. The Kazakh texts.
`e2e/support/*`. mod-002's `AreaTree.ResolveOwner` (trusted as merged).
Whether `UnitOfWork.ExecuteInTransactionAsync` retries a 40P01 (bears on
m1's deadlock note). No test was run.

Could not check by reading:

- The pointer drag in real browsers. It never runs outside jsdom, because
  E2E moves through the menus by decision. Both `dragleave` handlers clear
  the target when `relatedTarget` is `null`, and browsers differ there.
- The panel's render cost at 5 000 rows. Only the server side has a volume
  test, and a move re-renders every row twice: optimistic renumbering,
  then the read.
