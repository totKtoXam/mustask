# Audit 5-iterations — Iterations and sprints: what exists and what is planned

Read-only audit of branch `doc/012-matrix-organization-model` (main `ee54131a` plus the task commit). "The brief" is
`input-brief.md`: §§ 1-3 the principle, §§ 5-6 and 8 the team, item and sprint rules.

## As-is (what exists, with repository-relative file paths and line numbers where it matters)

**1. Shipped code: no iteration or sprint concept at all.** `grep -i "iteration|sprint"` over `src/` hits only the empty card
slot (`src/frontend/src/features/workitems/components/card/cardSlots.ts:28`, `WorkItemCard.tsx:411`, probe in
`WorkItemCard.slots.test.tsx:34,103`, "Owner: mod-007"), a naming-test string (`SnakeCaseConverterTests.cs:23`) and an
unrelated `Capacity` property. None of the 10 migrations (`src/backend/Mustask.Infrastructure/Migrations/`, InitialCreate
to `20261011214251`) has an iteration, sprint, capacity or snapshot table; `WorkItem` has no iteration pointer
(`src/backend/Mustask.Domain/WorkItems/WorkItem.cs:51-130`); `PermissionCatalog.All`
(`Mustask.Domain/Identity/PermissionCatalog.cs`) holds 20 codes, none `iterations.*`; no `docs/features/mod-007-*` folder
exists. The subsystem is specification plus queue lines (`docs/backlog.md:40,47-54`, all under "Later").

**2. Code the plan attaches to (what an iteration rule would read today):**
- Team is a child of a project: `Team.cs:30` `ProjectId`, `:39` `IsFirstTeam`, `:75` `Delete()`; `TeamConfiguration.cs` has a
  restrict FK to the project, a name unique per project among active teams, and the alternate key `(project_id, id)`.
- "Owning team" of an item exists only as a derivation: `WorkItem.AreaId` (`WorkItem.cs:65`) -> `AreaTree.ResolveOwner`
  (`Mustask.Domain/Organizations/AreaTree.cs:145`) over `Area.OwnerTeamId` (`Area.cs:46`). `BacklogMembership.cs:46`
  (`TeamAreas`) is the single definition of what a team's backlog/board holds (project, level, area resolves to team,
  state outside Removed). No `OwningTeamId` column exists; a sprint-compatibility check has nothing explicit to compare.
- Team backlog order is per (team, level): `backlog_orders` / `backlog_places` (`Migrations/20261005134049_AddBoards.cs:14-60`).
- Team surfaces are project-addressed: `Mustask.Api/Features/Boards/BoardEndpoints.cs:30` group
  `/api/organizations/{o}/projects/{p}/teams/{t}` (`backlogs/{level}` :33, `reorder` :41, `boards/{level}` :51); the panel
  mirrors it in `src/frontend/src/features/boards/paths.ts:7`.
- `TeamMembership` (`Domain/Organizations/TeamMembership.cs`) is user x team keyed by team id, no role, grants no code: the
  only "who is on the team" source a capacity feature could read.
- Estimation fields for burndown/velocity exist (`WorkItem.cs:97,118,121-127`; feat-022 absorbed the withdrawn feat-014,
  `docs/backlog/withdrawn.md:20`).
- No history store exists (no `history`/`revision` in Domain, Application, Migrations); feat-015 is queued (`docs/backlog.md:60`).
  `PATCH` saves are last-write-wins (`docs/features/mod-003-work-item-core/api-contract.md:226`); the token is feat-021 (`:64`).

**3. Planned ownership of iterations (doc-004 `spec.md`, doc-001 D-11): a project-owned tree plus team selection.**
- An iteration belongs to exactly one project, is a node of that project's tree of «Релиз»/«Спринт» nodes (BR-M5-1,
  ER `spec.md:80`); sprint numbers come from one per-project sequence (BR-M5-2); names are unique among a parent's active
  children in the project (BR-M5-3). "Cross-project iterations" are out of scope (`spec.md:203`).
- A team owns nothing: `TEAM }o--o{ ITERATION` "selects, node by node" (BR-M5-5), chosen by the organization
  administrator; several teams may select the same node; one selected node may be the team's default (BR-M5-6).
- An item holds one optional pointer; setting it needs the iteration active, in the item's project and selected by the
  item's owning team = the area owner (BR-M5-7, doc-001 BR-3, BR-7). "Current" is derived per team (BR-M5-8, doc-001 BR-9).
- Sub-slices: filter (feat-008, BR-M5-9), planning pane (feat-009, BR-M5-13), move unfinished (feat-010, BR-M5-12); MS-4
  "MVP complete" = mod-007 + feat-008..010 (doc-010 `spec.md:68`).
- Queue anomalies: mod-011 deps `feat-004,feat-014` (`docs/backlog.md:50`) are both withdrawn (`withdrawn.md:20-21`); mod-007
  deps (`:40`) omit feat-015 and feat-021 though BR-M5-7/-10 need pointer history and a revision-checked write.

**4. Planned features (release 2/3) and which ownership they would follow:**
- Sprint backlog/taskboard (mod-011, US-5/6): "team iteration, items owned by the team" -> follows the sprint's team and
  becomes cross-project; the taskboard rides M4's BOARD (team, Tasks level), changing BR-M4-1.
- Capacity (feat-011, BR-M5-15): per (team, iteration, member) + the team's working days -> follows the sprint's team;
  members come from team membership, but the brief lets assignees come from outside the team (§ 6).
- Burndown/velocity (mod-012, BR-M5-16): "over the iteration's dates and the team's area ownership" -> follows the sprint's
  team, sums estimates across the projects it spans, reads pointer history (feat-015).
- Forecast (feat-012, US-9): team velocity over the team's ordered backlog (`backlog_places`, per team+level) -> the team.
- Delivery plans (mod-013, BR-M5-17, US-10): organization-owned, one row per team, "teams of any of its projects" -> already
  team-keyed; only the project wording and cards of Restricted projects change.

## Responsibilities the current model carries (one duty per bullet; "MULTI" marks one entity carrying several duties)

- Iteration = release grouping and sprint time-box in one entity, told apart by an immutable `kind` flag (BR-M5-2, E-11): MULTI.
- Iteration = tree path label for pickers, the subtree filter (BR-M5-9, doc-001 BR-15) and the saved-query "Under" clause
  (doc-010 D-16): a structural duty. Also numbering and name-uniqueness scope per project (BR-M5-2/3): an identity duty.
- Project = owner of every iteration, the picker domain and the item-to-iteration compatibility boundary.
- Team selection (join) = "which nodes a team plans in"; team default = fallback for "current" only (BR-M5-6): usage is
  already separate from ownership.
- "Current iteration" = derived per team from dates, never stored (BR-M5-8, -11); lifecycle is only Active/Removed (ADR-0026 rule 9).
- Area = classification, team ownership and scoping at once (MULTI); iteration rules rely on its ownership duty alone
  (BR-M5-7, -9, -12, -13, -16, US-5 via doc-001 BR-3).
- Work item pointer = scheduling, plus the unit counted by the removal prompt (BR-M5-10) and the bulk move (BR-M5-12).
- Org administrator = creates/dates/moves/removes iterations AND sets every team's selection and default; any member
  schedules any team's items; team membership carries no access (`doc-004 spec.md:19-34`): one org-wide role covers all.
- Delivery plan = read-only timeline that writes through the item pointer (BR-M5-17).

## Rules and contracts the matrix model touches (by id) — what in the brief collides

- doc-001 D-11 / BR-7 / BR-9, ER: "Iterations belong to the project; a team selects" vs brief § 1, § 8 "Sprint belongs to Team".
  BR-9 tie-breaks ("lower number" between sprints) assume one numbering; per-project numbers are not comparable if a team
  uses sprints of several projects.
- doc-004 E-5, E-7, E-10, E-11, E-12 (human decisions): several teams per project, a mixed Release/Sprint tree, per-project
  sprint counter, editable names. E-7/E-11 (tree of mixed kinds) collide with brief § 8.9 "do not mix releases/milestones
  with sprints"; E-10/E-11 scope the counter to a project, the brief's example "Sprint 12" of team MARS is team-scoped.
- BR-M5-1 (iteration in exactly one project, never across): collides with § 8.2 "a sprint may include tasks of different
  projects". BR-M5-2/3 (project number sequence; sibling-name uniqueness per project tree): scope has to move to the team.
- BR-M5-5 (N:M team selection; many teams share one node): collides with 1:N "Team has N sprints" / "belongs to Team" (§ 8.1);
  BR-M5-6 (default among the selected): "selected" and "owned" become one set, so the rule changes meaning.
- BR-M5-7 (set only if same project and selected by the area-derived owning team): collides with § 6.3-6.4 (explicit optional
  `OwningTeamId`), § 8.2-8.3, and with doc-001 BR-3. Its second half, "a stored pointer the owning team does not use stays,
  displayed as outside the selection, never rewritten" (also US-2/US-3 scenarios, team-deletion scenario), contradicts § 6.10
  "a task cannot stay in a sprint of a team no longer responsible" and § 8.4 "moving a task requires adjusting its sprint".
- BR-M5-8 / doc-001 BR-9 / E-2: "current" exists on team surfaces only, never on the project list; brief § 8.7-8.8 want a
  Project Board over sprints of several teams and an Area view of "active sprints of linked teams". BR-M5-9 / US-13 /
  feat-008: the project list offers "any active iteration of the project"; sprints stop being project data.
- BR-M5-10 / US-14 / doc-010 D-13 (soft removal, pointers cleared, never purged): vs § 8.5 "do not lose the history of
  completed sprints"; removal clears every pointer, so a removed sprint's contents survive only in item history.
- BR-M5-12 / US-12 (move "items owned by the team"): count and move span projects; the count in the confirmation can disclose
  items of projects the actor cannot read (§ 10.3-10.4 item 4).
- BR-M5-13 / US-11 (pane counts per team backlog level), BR-M4-5/-6/-20/-21 (BACKLOG_ENTRY team = area owner): the team
  backlog is project-addressed today; brief § 5 makes it cross-project.
- BR-M5-15 / BR-M5-16 (capacity, metrics keyed to the team and its area ownership), BR-M5-17 (teams "of any of its projects").
- doc-004 § Roles + doc-010 code table (`spec.md:275-279`): org-scope codes only, no sprint-level or team-level scope vs
  brief § 10 (`sprint.manage`, Team Lead, scopes Organization/Area/Project/Team). ADR-0027 chain "org -> project -> team or
  area in project -> item in project" and mod-016 spec:118 (iterations follow the same not-found rule) assume project-owned teams.
- doc-006 BR-M1-9, -11, -14, -18: first team, root area, default area, team deletion that "drops iteration selection and
  default" and re-owns areas to the first team (US-2 scenario "deleted team's settings dropped"). Brief § 5 gives teams their
  own sprints, so deletion must say what happens to the sprints, their items and the first-team re-ownership (P-1, P-7).
- ADR-0026 rule 9 (iterations reuse Active/Removed, number reserved) and rule 7 ("mod-007 ignores a deleted team's settings").
- Contracts: mod-003 api-contract § 1.1 (WorkItem shape has no sprint/owning team), § 2.4 (PATCH last-write-wins vs BR-M5-7's
  conflict), mod-006 api-contract § 1 and § 3.1-3.3 (team/project-addressed reads). doc-002 `Итерация` row (`spec.md:88`).

## Regression surface (existing behaviour that must keep working, with the proof that exists today)

No iteration behaviour ships, so doc-004's US-1..US-14 (all `shippable: no`, status pending) are specification only. Adjacent
behaviour with tests (suites under `src/backend/tests/` unless noted):
- Team backlog/board membership by area owner, order, re-scoping: `Api.IntegrationTests/Features/Boards/GetBacklogEndpointTests.cs`
  (`Get_HoldsTheTeamsItemsOfTheLevel_AndNothingElse`, `Get_AreaReOwned_TakesItsItemsAlong`,
  `Get_DeletedTeam_ItsItemsGoToTheFirstTeamByNumber`), `GetBoardEndpointTests`, `ReorderBacklogEndpointTests`, `BacklogVolumeTests`.
- Team deletion cascade that BR-M5-5 hooks onto: `Features/Organizations/DeleteTeamEndpointTests.cs`
  (`Delete_ReownsExplicitAreasToFirstTeam_InheritedFollow_NamesAndPlacesKept`), `Application.Tests/.../DeleteTeamHandlerTests`,
  `Domain.Tests/Organizations/TeamTests.cs`; area ownership: `AreaTreeTests.cs` (`ResolveOwner_InheritsFromTheNearestOwnedAncestor`).
- Work item save/field semantics a pointer write would extend: `Features/WorkItems/SaveWorkItemEndpointTests.cs`,
  `WorkItemFormFieldsEndpointTests.cs`, `Domain.Tests/WorkItems/WorkItemTests.cs`, `WorkItemFieldsTests.cs`; estimation check
  `ck_work_items_story_points` (migration `20261009055512`).
- Every route declares an organization code (`Features/Authorization/OrganizationAccessInventoryTests.cs`); non-member 404
  (`WorkItemsAccessEndpointTests.cs`); card keeps the iteration slot after the area field (`WorkItemCard.slots.test.tsx:103`);
  explicit migrations (`Infrastructure.Tests/MigrationsTests.cs`, `Architecture.Tests/AutomaticMigrationsAreForbidden.cs`).
  Per P-11, "green" = `scripts/verify.sh`, not unit suites.

## Risks and open questions for the analyst

- Does the Release/Sprint tree survive? § 8.9 separates releases from sprints, while the tree, "Under" queries (D-16), paths
  in every picker and filter, and the immutable kind (E-7, E-11) all assume one mixed tree. What owns releases/milestones?
- Numbering and name-uniqueness scope of a sprint: per team, organization or project of origin? doc-001 BR-9's tie-break needs
  numbers comparable across the sprints one team may hold. Exactly one team per sprint (1:N), or still shared (today's N:M
  selection)? If 1:N, what becomes of E-5's shared-node scenarios and the org-admin-only selection setting?
- May an item without an owning team (§ 6.2) be scheduled? The "no owner" branch left doc-004 because every area resolves to a
  team (`doc-004-iterations-planning/status.md` log 2026-09-25); the optional explicit owner reopens it.
- Stale pointers: keep-and-mark (US-2/US-3) or forbid/auto-clear (§ 6.10, § 8.4)? Unlinking a team from a project (SC-09) and
  deleting a team must say what happens to the sprint pointers of the affected items (P-7).
- Sprint lifecycle (P-8): today only Active/Removed plus date-derived "current" (BR-M5-8, BR-M5-11 "nothing happens
  automatically"). Does the matrix model need stored states (planned/active/closed) for § 8.8 "active sprints"? That would
  revise BR-M5-8/-11 and ADR-0026 rule 9.
- Removal vs "do not lose the history of completed sprints": may a completed sprint be removed at all, and do velocity and
  burndown of removed sprints stay readable (BR-M5-10 clears pointers; the history they read is feat-015's, unbuilt)?
- Permissions: who creates, dates and removes a sprint (org admin only today; the brief lists `sprint.manage` and a Team
  scope), and who schedules (any member today)? BR-M5-12's confirmation count, pane counts (BR-M5-13) and velocity must
  follow Restricted-project visibility without leaking numbers.
- Capacity members come from `TeamMembership`, but assignees may come from other teams (§ 6): is capacity per team member or
  per assignee, and does "team load across all its projects" (§ 5) count non-member assignees?
- Three "default" notions would coexist (team default area BR-M1-14, team default iteration BR-M5-6, the human-decided
  project default team); the spec should name them apart.
- BR-M5-12/-16 rely on state categories (doc-001 BR-2), which survives differing statuses (SC-10), but the process is one per
  organization today (doc-001 ER `ORGANIZATION ||--|| PROCESS`): confirm with the workflow audit.
- mod-007 and feat-008..010 are unbuilt (rewrite, not migrate); mod-011..013 and mod-008's "Under on Итерация" need
  re-pointing. The queue line names feat-038, absent from this checkout's queue and docs: confirm what it is.
