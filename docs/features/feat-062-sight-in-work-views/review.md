# feat-062-sight-in-work-views: code review

## Round 1

Diff `41a017f4..04e099f9` (branch `feat/062-sight-in-work-views`, HEAD confirmed). General pass and the L security pass in one
session (economy mode: one Opus review). No tests run: the reading settled every doubt; the developer's and QA's runs are in
the Log.

**Verdict: changes-requested** — blockers 0 · majors 2 (R-1, R-2) · minors 4 (R-3..R-6).

What holds: every multi-project read named in api-contract § 1 narrows inside its statement through one helper
(`VisibleProjectsQueries.WhereVisible`, a parameterized array) applied in `ProjectsInScope`, the directory, the rank and
change-time candidates, the team backlog/board membership and every sprint read (live and snapshot) — before grouping, count,
skip/take and cursor. The set comes from feat-041's `AccessSet` (`SeenProjects()` = `SeesProject` over the organization's
projects); body ids use `SeesAsync`, the same set. No second sight rule. The administrator short-circuits to `All` everywhere
(`HttpContextOrganizationAccess.cs:62-70`, `AccessSet.cs:80-81`). Hidden ids answer with the exact exception an unknown id gets
(reorder 409 `reorder_stale`, group 404 `project.not_found`, team 404, sprint 404). Twin tests really compare: every surface's
shape against a twin, plus `DoesNotContain` for the hidden project's name, key, id and assignee, which catches a directory or
`projects` list that the twin would otherwise share.

### Findings

**R-1 · major · security / scope (R-5 remainder): two body team ids still answer 409 for a hidden team.**
`SetDefaultTeamHandler.cs:58-70` (`PUT /projects/{p}/default-team`) and `UnlinkTeamHandler.cs:118-170` (`POST
/projects/{p}/teams/{t}/unlink`, `destination.teamId`): a team the caller does not see and that is not linked to the project gets
409 `organizations.team.not_in_project`; an unknown id gets 404 `organizations.team.not_found`. That tells a caller whether a hidden
team's id exists, which is exactly what this task closes for create/save. Both routes are reachable by non-administrators:
`project.write` and `project.link` are in the project and department «Менеджер» bundles (`ScopedRoleBundles.cs:34-63`), and
`team.link` in the team «Менеджер» bundle (`:72-77`). Fix: in both handlers, after the id parses and the team is found, `if
(!await access.SeesAsync(ResourceScope.Team, id, ct)) throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);`
(SetDefaultTeam needs `IOrganizationAccess` injected); a twin-style test per route (hidden vs unknown refusal equal, visible unlinked
still 409); add both rows to api-contract § 4.

**R-2 · major · prose: api-contract § 6 claims an audit that the code contradicts.** `api-contract.md:72` says "Default team
(`SetDefaultTeam`), `MoveTeam`: admin-only organization codes" — `SetDefaultTeam` is not admin-only (R-1). `:79` "Link routes: done in
feat-041 R-5" — feat-041 fixed `LinkProjectsToTeam`/`LinkTeamsToProject` only, not the unlink destination. A reader of § 6 relies on
"every other body id is safe". Stale source of the first error: `SetDefaultTeamHandler.cs:20` ("only an administrator"). Fix: with
R-1, move both routes to § 4 and correct the handler's remark; keep `MoveTeam` in § 6 (its route declares `team.create`,
`OrganizationEndpoints.cs:425-426`, admin-only — verified).

**R-3 · minor · fail-closed defaults (QA D-1), judged.** `IOrganizationAccess.cs:62` defaults `VisibleProjectsAsync` to `All`, and
`BacklogMembership.cs:50,77` read `visible = null` as `All`. Today it is safe: the one production implementation overrides the
method, and the one production caller of `Members`/`TreeMembers` (`BacklogReadModel.cs:36,47`) passes `caller.Projects`, which
throws for a non-admin with no sight. The risk is a future caller, so it does not have to be fixed in this round. Fix (debt): make
`visible` a required parameter of `Members`/`TreeMembers` (the tests pass `VisibleProjects.All`). Also, either drop the interface
default or pin the override with a test that a non-admin `HttpContextOrganizationAccess` returns `Only(...)`.

**R-4 · minor · test: the removal twin does not exercise the items rule.**
`SightInSprintsTests.cs:182-194` deletes `real.Active`, which is refused by state (`RemoveSprintHandler.cs:38-41`, not Planned)
before items are counted, so it proves nothing about "holding hidden items". Fix: remove the Planned `Next` sprint after giving
it one visible item in both worlds plus a hidden one in the real world, and compare the refusals.

**R-5 · minor · prose: api-contract § 2 and a dangling cref.** `api-contract.md:41-42` says a selector id naming a hidden project
"is gated the same way". Read after the 404 sentence before it, that means 404. The code and the QA test give 200 with nothing
selected, the same as an unknown id (`AggregateViewReadModel.cs:241-245`, `SightInWorkViewsTests.cs:222-245`). Fix: "selects
nothing, as an unknown id does (200)". `VisibleProjects.cs:5` cites `AccessSet.VisibleProjects`, which does not exist; it should
cite `AccessSet.SeenProjects`.

**R-6 · minor · prose, direction 2: older contracts still describe the old refusals.** `mod-007 api-contract.md:81-83` says
`close_destination_invalid` for a destination that "exists but is not a Planned sprint of this team", and `:129-130` says
`other_team` for a save. `mod-017 api-contract.md:73` says 409 `not_in_project` for "an active team of another active project". For
a hidden team or a hidden team's sprint, these are now 404 (feat-062 § 4). The mod-007 stories still mark the clauses "owed by
feat-041" (US-2:47, US-3:25, US-5:49,81, US-9:66, US-10:86). The status notes in feat-041 and mod-007 point to feat-062, but the
contract tables do not. Fix: add one line under each table: "a team (sprint of a team) the caller does not see: 404, feat-062 § 4".
Change "owed by feat-041" to "delivered by feat-062" in the five stories, leaving US-6/US-7 to feat-057/058.

### Security pass (L)

- Authorization of every endpoint: no route or declaration changed; the inventory is untouched. Pass.
- IDOR / non-disclosure: reads pass (see above). Body ids: R-1. Path ids: feat-041's gate, unchanged.
- Personal data in logs and responses: nothing new is logged, and the assignee options narrow by the same predicate. Pass.
- Audit trail: no significant action added. N/A.
- Injection: `WhereVisible` builds an expression over a captured array, so EF sends one parameter. `BacklogRepository`'s
  `project.id` column is added inside the existing interpolated `FormattableString`. Pass.
- Secrets: none. Pass.

### Behavioural claims checked

Direction 1 (prose added or changed by the diff):

- contract § 1 narrowing table → `AggregateViewReadModel.cs:41,59,90,121,178,239`; `BacklogReadModel.cs:36,47`; `SprintReadModel.cs:812,830,852,864,878,892,904`; holds.
- contract § 1 "directory lists omit hidden projects" → `AggregateViewReadModel.cs:59`, `GetBacklogHandler.cs:82`, `GetBoardHandler.cs:97`; holds.
- contract § 1 "overview, selector offers untouched" → `GetOverviewHandler.cs:22`, `GetSelectorOffersHandler.cs:23` (no sight); holds.
- contract § 2 hidden project group = 404 → `AggregateViewReadModel.cs:88-96` + `GetBacklogGroupRowsHandler.cs:35-37`; holds. Selector sentence → R-5.
- contract § 3 reorder → `ReorderBacklogHandler.cs:94-114`; holds, test `SightInWorkViewsTests.cs:249-283`.
- contract § 4 four rows → `CreateWorkItemHandler.cs:236-240`, `SaveWorkItemHandler.cs:358-362,439-443`, `CloseSprintHandler.cs:58-62`; holds.
- contract § 4 "visible unlinked still 409" → `ProjectTeams.EnsureBindable`; test `SightInSprintsTests.cs:240-243`; holds.
- contract § 5 close moves all, removal generic → `CloseSprintHandler` (no narrowing of the move), `RemoveSprintHandler.cs:38-46` (one code); holds (test weakness R-4).
- contract § 6 audit list → R-1, R-2 (default team, unlink destination); the other bullets hold (parents same-project: `BacklogReadModel.cs:154-155`).
- contract § 7 "no statement added, pins unchanged, member = admin + set" → `SprintStatementCountTests` new fact, `AggregateViewStatementCountTests` new fact; holds.
- plan § 2 "`Projects` fails closed" → `CallerAccess.cs:103-106`, `AccessSetSightTests` new fact; holds.
- `VisibleProjects`/`VisibleProjectsQueries` doc "in the statement, before paging; one array parameter" → `VisibleProjectsQueries.cs:34-44`; holds (cref → R-5).
- qa.md D-1 → R-3. status Log "1 minor carried" → R-3.
- feat-041 / mod-007 status notes (new lines) → match contract § 4/§ 5; hold.

Direction 2 (behaviour changed: refusals of hidden team/sprint ids; sight on sprint and aggregate reads). Searched for
`not_in_project`, `close_destination_invalid`, `other_team` and "owed by feat-041" under `docs/`. Hits read: error-codes.md
(generic text, holds), feat-028 and feat-040 contracts (link/unlink, no hidden case, consistent with R-1), mod-017 contract :73 and
mod-007 contract :81, :129 and the stories, all covered by R-6. Not swept: the frontend's handling of 409 vs 404 on these routes;
the contract states no shape or copy change, so the panel shows the existing not-found copy.

## Round 2 (delta)

Delta `eb0ed643..5629996e` against the Round 1 findings. No tests run: the developer's run of the affected classes is in the Log.

**Verdict: approved** — blockers 0 · majors 0 · minors open 0. Every Round 1 finding is closed.

- **R-1 · closed.** `SetDefaultTeamHandler.cs:71-75` checks `SeesAsync(Team)` after the id parses and before the link lock; an unknown
  id fails the same check (`AccessSet.SeesTeam` is false for a team not in the organization), so both answer the same 404.
  `UnlinkTeamHandler.cs:127-131` does the same after the destination's existence check, so a hidden destination is 404 before the
  linked-pair branch, as an unknown one already was. The administrator passes through (`SeesAsync` is true). The test
  `SightInSprintsTests.ADefaultTeamOrAnUnlinkDestination_…` uses a non-admin project «Менеджер» of «Альфа» plus team «Менеджер»
  of MARS, and a hidden team linked to «Гамма» only. Before the fix that hidden team answered 409, so the comparison with an unknown
  id can fail. The visible, unlinked JUPITER stays 409 on both routes as the control.
- **R-2 · closed.** Contract § 4 has both rows, § 6 keeps only `MoveTeam` as admin-only and narrows the link-routes bullet to the
  routes feat-041 fixed. `SetDefaultTeamHandler.cs:21-23` now names the managers as holders.
- **R-3 · closed.** `IOrganizationAccess.VisibleProjectsAsync` has no default (`IOrganizationAccess.cs:62`), so a new implementation
  must state its sight; the one production implementation already did. `BacklogMembership.Members/TreeMembers` take a required
  non-null `visible` (`:50,77`). The test doubles state `All` explicitly.
- **R-4 · closed.** The removal twin now removes the Planned `Next` sprint, holding a visible item in both worlds and a hidden one
  in the real world, as a team «Менеджер». It asserts the twin's 409 (refused for items, not for state or rights) and equal
  refusals.
- **R-5 · closed.** Contract § 2 says "selects nothing, as an unknown id does (200)", and `VisibleProjects.cs:5` cites
  `AccessSet.SeenProjects`.
- **R-6 · closed.** There are pointer lines under mod-007 api-contract (close destination, sprint in a save) and mod-017
  api-contract (the owning-team table, plus default team and unlink), each with a status note. The five story markers say "delivered
  by feat-062", US-6 says feat-057 and US-7 says feat-058. Left as is: mod-007 `spec.md:46-50` still says the clauses are "owed by
  feat-041". That is a planning statement, not a behavioural claim, and the mod-007 status note records the delivery.

New leaks: none. Both new checks only turn a 409 into the unknown id's 404, and neither runs when no team is named (clearing the
default, «Без команды»).

### Behavioural claims checked (delta)

Direction 1:
- contract § 4 new rows → `SetDefaultTeamHandler.cs:71-75`, `UnlinkTeamHandler.cs:127-131`; test above; holds.
- contract § 6 `MoveTeam` admin-only → `OrganizationEndpoints.cs:425-426` (`team.create`); holds.
- contract § 2 selector sentence → `AggregateViewReadModel.cs:241-245`; holds.
- `IOrganizationAccess.cs:59-60` "no default … fails closed" → the member has no body; a new implementation does not compile without it; holds.
- mod-007/mod-017 pointer lines → feat-062 § 4 rows and the handlers above; hold.
- status Log `[backend]` line → matches the diff (13 doubles: 13 added `VisibleProjectsAsync` bodies); holds.

Direction 2: behaviour changed is the default-team and unlink-destination refusal for a hidden team. I searched `docs/` for
`default-team` and `not_in_project`. feat-028 api-contract and spec describe 409 for "an active team not linked", with no hidden
case; they remain true for a visible team, and the mod-017 pointer covers the general rule. Not swept: the frontend's copy for
these two routes, since the 404 copy already exists.
