# Code review, round 1

- Reviewed: `feat/040-team-surfaces-across-projects` at `8b3d083f`, the whole diff from the merge base `5de279f7`
  (backend 76 files, frontend 130, docs 23), in a clean context, read-only. Nothing was built and no tests were run
  (the QA pass runs them in parallel); every anchor below is a reading of the code and the test that pins it.
- Against: spec (BR-TS-1..-14, OQ-1..3 A), design (D-1..D-7 A), api-contract, plan-backend § 14 and plan-frontend § 5
  (as built), ADR-0024 § 2 as amended by ADR-0028 § 7, ADR-0028 rules 3, 8, 9, ADR-0029 § 7, ADR-0030 rule 2,
  `docs/conventions/backend.md`, `frontend.md`, `ui-kit.md`.
- **Verdict: approve.** Four findings, all minor: debts for the developer or the next line, none blocks gate-3.

## Findings

**CR-1 · minor · backend** — `src/backend/Mustask.Infrastructure/Boards/BacklogRepository.cs:145-151` (and plan-backend
PD-9, line 61). The comment says the departed drop is "decided by a predicate over facts the reorder holds locked or that
cannot change under it". That holds for the members, but not for a deleted item that still holds a place on T/L: the
locking statements (`:80-92`, `:120-135`) take only members, so the deleted item is not locked, and Restore takes the item `FOR UPDATE`
and nothing else (`RestoreWorkItemHandler.cs:22-27`). If a Restore commits between the locking read and the `DELETE`
(`:157-169`), the item is a member by then. The `DELETE` keeps its pre-deletion place, and the upsert of the order (`:176-186`) does not
rewrite it. Neither serial order gives that result: reorder-then-restore drops the place (the BR-DR-10 intent at
`:155-156`), and restore-then-reorder makes the reorder read the item. The impact is cosmetic: the restored item sits at
its old position among the new 1..n, and the BR-TS-6 tie-break orders the tie. **Fix:** either lock the place holders
too (`FOR KEY SHARE OF item` over the items with a T/L place, which conflicts with Restore's `FOR UPDATE`; the pinned
reorder statement count then needs a look), or narrow the comment and PD-9 to name this window and its outcome.

**CR-2 · minor · backend** — the same `DELETE` (`BacklogRepository.cs:159-167`) is a fourth spelling of the team
membership (owner T, type in L, state in the shown states, an active project of the organization). It sits outside
ADR-0030 rule 2's parity test: `DeletedStateParityTests.cs:218` pins only `BacklogMembership` and the locking read.
`TeamBacklogMemberLockTests.cs:184` covers one Removed item and one item of another level, not every state of every
type. If the membership later gains or loses a condition, the drop can silently drift: it would remove a member's place
or keep a departed one. **Fix:** a parity case that seeds a T/L place for an item in every state of every type, runs
`SaveOrderAsync` with the membership as the order, and asserts that the surviving places of active projects are exactly
the membership.

**CR-3 · minor · frontend** — `src/frontend/src/features/boards/components/TeamHead.tsx:156` draws «Команды» whenever
`teams.length > 0`. Design § 3.2 (`design.md:65`) says it is shown when the organization's teams are read "(T is among
them)". When T is not in the shell's list (deleted after the read, or an address of a team the list does not hold),
the menu opens with no entry selected and `currentId` points at nothing. **Fix:** also require
`teams.some((t) => t.id === teamId)`, with a test beside the head's tests; or drop the parenthesis from the design if
the case cannot reach the head.

**CR-4 · minor · docs** — `docs/features/mod-015-app-shell-design-system/status.md` has no P-10 note. `spec.md:145` lists
"mod-015 BR-11's reset and hiding of the team level, and BR-18's team-level prefix" among the breaks owed "a note in each
owner's status.md". `status.md:80` lists the notes actually added: mod-003, -006, -016, -017, feat-027, -028, -032 and
-039. The switcher's three-level model has no owner note either, though ADR-0028 § 7 carries it. **Fix:** one `## Log`
line in mod-015's status.md, in the shape of the mod-006 one.

## Behavioural claims checked (direction 1: prose added or changed)

- api-contract § 0, BR-TS-1: team views at `/api/organizations/{o}/teams/{t}/…` → `Mustask.Api/Features/Boards/BoardEndpoints.cs:35`.
- The project-nested team routes are not served and have no alias → `RetiredTeamViewRoutesEndpointTests.cs:37,66,78`.
- BR-TS-6: placed first, by position, then createdAt, the project key (ordinal), the number → `Mustask.Domain/Boards/StackRank.cs:78-82`; `TeamOrderAcrossProjectsEndpointTests`.
- BR-TS-8: `projectId` is repeated, an invalid value is ignored, and a set filter with no usable value selects nothing; "absent or empty" means no values, the same as mod-017's `teamId` (`OfTeams`) → `Mustask.Domain/WorkItems/WorkItemFilter.cs:42-67`; `WorkItemFilterTests` `OfProjects_MalformedValue_IsIgnored_ButTheFilterIsSet`.
- `projects` is not narrowed by the filter → `GetBacklogHandler.cs:65`, `GetBoardHandler.cs:63` (read from the links, not from the filtered set).
- tech-011 m2, the team read again after the reads → `GetBacklogHandler.cs:80`, `GetBoardHandler.cs:81`; `TeamViewDeletionRaceTests`.
- tech-011 m3 and mod-016's answer order on every team-view route → `TeamViewContainmentTests.cs:51,76,111,148,181`.
- NFR volumes: the same statement count for one project as for fifty, filtered too → `TeamViewStatementCountTests.cs:54,78,97`. No N+1 found on the read path: the parents and checklists are read by organization, once per view.
- ADR-0028 rule 8 lock order, team `FOR SHARE` then members `FOR KEY SHARE OF item`, no project row and no link → `BacklogRepository.cs:66,80-92`; `TeamBacklogMemberLockTests.cs:31,70`.
- PD-9, "a predicate over locked facts" → CR-1.
- PD-11, a 23503 on either place key maps to `ReorderStale` and never to a 500 → `BacklogRepository.cs:186-192`; `TeamBacklogMemberLockTests.cs:227`.
- ADR-0030 rule 2: the membership and the locking read agree across the deleted states and across projects → `DeletedStateParityTests.cs:218`. The surface matrix has row 3 across two projects, and its read routes moved to the team's address → `DeletedItemSurfaceMatrixTests.cs:96,274-283`. The departed drop is outside the parity test → CR-2.
- BR-TS-13, index `ix_work_items_owning_team_id_type_id` → `Migrations/20261018144102_AddTeamLedWorkItemIndex.cs`; `AddTeamLedWorkItemIndexMigrationTests.cs`.
- The route inventory stays at 59 → `OrganizationAddressedRoutes.cs:29`.
- ADR-0029 § 7, `CallerAccess` is a required argument of the team reads → `IBacklogReadModel.cs` (`ReadTeamAsync`, `ReadTeamTreeAsync`).
- Retired tests are named with rule ids (spec § Existing rules, doc-012 P-1):
  - backend: `TeamAcrossProjectsEndpointTests` and `OwningTeamStatementCountTests` (comments cite BR-TS-13, BR-OL-11/D-13), and plan-backend § 10.3;
  - frontend: `ProjectBacklogPage.test.tsx:262-272`, `ProjectBoardPage.test.tsx:546-549`, `routes.boards.test.tsx:319-323`, `routes.projectBoards.test.tsx:223`, `switchTarget.test.ts:208`, `useOrganizationsContextSource.test.tsx:189`; `TeamsPage.test.tsx` rewrites «shows neither view once a team is linked».
  - One retirement beyond the spec's list («choosing a team opens that team's page at the same level…») is named with its reason at `ProjectBacklogPage.test.tsx:267-269`, as the spec allows ("the plans name the rest").
- BR-TS-12, OQ-1 A: the project's «Команды» always offers «Бэклог проекта» and «Доска проекта», to every role, from the first paint → `TeamsPage.tsx:101`; `TeamsPage.test.tsx` «shows both views once a team is linked too…», «draws them from the first paint…».
- BR-TS-11: the head's menu «Команды» lists the organization's teams, with no «Весь проект» and none on a project page → `TeamHead.tsx:44,156`; `ProjectBacklogPage.test.tsx:273`. The design's "(T is among them)" → CR-3.
- BR-TS-10, the switcher's team level:
  - it lists the organization's teams, own first, with or without a project → `resolveSelection.ts:41`, `ContextSwitcher.tsx:354-384` (`options={organization.teams}`);
  - a project choice keeps a chosen team → `routes.projectBoards.test.tsx` «choosing «Аналитика» at the project level keeps a chosen team»;
  - «Бэклоги» and «Канбан» need the organization and a team → `app/navigation/visibility.ts:51-52`, `model.ts:116-139`.
- D-1: «Проект» comes after «Команда», 120 px wide → `backlogColumns.ts:17,38`, `app/theme/tokens.ts:445`.
- D-2: «Проект» on the card sits in block A, row 2, under «Статус», read-only → `card/ProjectField.tsx`, `WorkItemCard.tsx:493`; `WorkItemCard.project.test.tsx`.
- BR-TS-3: every per-item write goes to the item's own project → `BacklogTable.tsx:538,579,676,852`, `BoardColumns.tsx:208`, `useChecklists.ts:276,376`, `CardChecklists.tsx:151`; `BacklogPage.projectRows.test.tsx`, `BoardPage.projects.test.tsx`.
- design § 4.9: the `project` drop refusal is judged first, with its copy → `lib/drop.ts:23`; `drop.test.ts`.
- design state 10(a): the hint «Команда не участвует ни в одном проекте.» → `lib/board.ts:74`.
- D-4, D-5: the dialog's «Команда» captions match `design.md:140,248` word for word → `workitems/lib/teams.ts:71,80`; `teamView.test.ts:76,82`.
- `status.md:80`, "P-10 notes added to …": true for the eight it names; spec.md:145 also owes mod-015 → CR-4.
- `status.md:82`, the E2E specs this line breaks: confirmed by grep, with the exact list below ("23-29" is loose, as 27 and 28 each have two specs).

## Direction 2 (behaviour changed → stale prose elsewhere)

- **Behaviour changed:** team addresses in the panel and the API (project-nested → organization), «Весь проект», the project's team menu, `PROJECT_SCOPE` and `ProjectFilterRow`, and the project-scoped team level of the switcher.
- **Grepped:** `projects/{projectId}/teams/{teamId}/(backlogs|boards)`, `projects/:projectId/teams/:teamId`, `PROJECT_SCOPE`, `ProjectFilterRow` and `Весь проект`.
- **Where:** `docs/conventions`, `docs/adr`, `docs/architecture`, `docs/ai-workflow`, `.claude`, `AGENTS.md`, `src/frontend/AGENTS.md`, `src/backend/AGENTS.md`, `docs/*.md`.
- **Hits:** one, `docs/adr/0024-app-shell-and-visual-language.md:129`. ADR-0028 § 7 (`0028:160-167`) amends it, and ADRs are not edited, so it is not stale.
- **Covered by P-10 notes rather than edits:** the owning features' specs (mod-006, feat-027, -028, -032, -039, mod-016, -017). mod-015's is missing → CR-4.
- **Not swept:** other features' spec, design and story prose. It is historical, and P-10 notes are the mechanism for it. `src/frontend/e2e` is not swept either: E2E is NO, and it is listed below.

## E2E specs this line breaks (not run, not edited; standing answer NO)

- **The team addresses:** `src/frontend/e2e/support/membersUi.ts:35-46` (`backlogAddress` and `boardAddress` build `…/projects/:p/teams/:t/…`), and through it `specs/28-team-members.spec.ts` and `specs/29-role-views.spec.ts`.
- **The team views:** `specs/23-backlogs-boards.spec.ts`, `24-work-item-hierarchy`, `25-work-item-fields`, `26-backlog-tree-editing` (also the menu «Команды проекта») and `27-work-item-card`.
- **The switcher's team level alone:** `specs/19-shell-in-a-browser.spec.ts` and `21-organizations.spec.ts`.
- **Not affected:** `27-organization-members` and `28-ui-kit-showcase` use no team-view address.
