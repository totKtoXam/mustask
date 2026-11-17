# Review — feat-061-sight-in-structure-lists

## Round 1

Scope: `git diff 9e0c9262..HEAD` (HEAD 6c0957ec): one Opus pass, with the L security pass folded in (status § Plan). Inputs:
doc-016 spec BR-TD-8, -12..-17, -21, US-6; this feature's plan-backend, api-contract, plan-frontend, qa; feat-041 review R-4, R-7.
Targeted runs (foreground): Application `AccessSet*` + `NavigationReadsHandlerTests` 17 passed; vitest `structureFilter`,
`AreasPage`, `AggregateViews.gone` 42 passed. No verify.sh (economy mode, CI runs it).

**Verdict: approved** — blockers 0 · majors 0 · minors 5 (R-1..R-5), plus QA's m-1..m-3 confirmed. All are debts for the gate-3 summary.

### Findings

**R-1 · minor · test proves nothing.** `StructureListsSightTests.cs:397-411` `TheLinkPickers_NeverOfferAHiddenObject_EvenToAHolderOfTheLinkCode`:
Olga is a «Участник» with only a VENUS membership (`:92-98`); the Member bundle holds no `*.link` code and she has no grant, so all three
`linkableFor` lists are `[]` and the `Assert.All` lines pass vacuously. The name ("a holder of the link code") and qa.md's row "Pickers
(`linkableFor`) narrowed before the link filter — pass" overstate. (For a non-administrator a link code comes only from a grant, and a
grant reaching an object makes it visible, so the narrowing is belt-and-braces; the test still should not claim more.) Fix: give the
caller a grant that yields the link code (e.g. a department grant on «Логистика») and assert the exact non-empty sets; or rename the
test and the qa.md row to what it checks.

**R-2 · minor · fail-closed is local, the doc says general.** `CallerAccess.cs:16-17`: `Sight` is an optional trailing parameter with
default `null`, and the doc says "A read that names or counts them applies it before paging and counting". Only `NavigationReadModel`
(`NavigationReadModel.cs:133-136`) applies it and fails closed; the eight other `new CallerAccess(...)` sites (Boards `GetBacklog`,
`GetBacklogGroupRows`, `GetBoard`, `GetBoardCells`, `GetViewGroups`, `GetWorkItemGroupRows`; WorkItems `GetOrganizationAssigneeOptions`,
`GetOrganizationWorkItems`) pass none, and their read models ignore it (feat-062's scope). The default makes forgetting it silent.
Fix: make `Sight` required (each site passes `set.Sighted()` or an explicit `null` for the administrator), or narrow the doc sentence to
"the navigation reads apply it; feat-062 brings the others".

**R-3 · minor · convention.** `docs/conventions/backend.md:199-210` forbids client-side post-filtering after `ToListAsync()`. The
handler-side `Sees*` filters (`GetProjectsHandler.cs:54`, `GetOrganizationTeamsHandler.cs:31-38`, `GetAreaTreeHandler.cs:50`,
`GetAreaTeamsHandler.cs:36`, `GetAreaDeletionHandler.cs:38`, `GetTeamProjectsHandler.cs:35`, `GetTeamsHandler.cs:53`,
`GetProjectTeamLinksHandler.cs:41`, `GetWritableAreaIdsHandler.cs:28`, `GetDeactivatedProjectsHandler.cs:44`) do exactly that. The harm
is nil today — the lists are unpaged, the rows read are those read before, the statement count is fixed, and mod-019's `linkableFor`
`Holds` filter set the precedent — but the deviation is unrecorded. Fix: one sentence in plan-backend § Paging and counts naming the
exception (unpaged whole-organization lists, sight decided in memory by the one rule); move to `Sighted()` ids in SQL when a list gains
paging.

**R-4 · minor · dead code, stale comment.** `src/frontend/src/features/organizations/lib/areaTree.ts:166-170`: `filterTree` has no
production caller left (only `areaTree.test.ts:154-160`), and its comment still cites «Найти область». Fix: delete it and its test, or
point the comment at `structureFilter.ts`.

**R-5 · minor · test comment wrong.** `StructureListsSightTests.cs:375` says the teams "stay visible by membership"; MARS is not
Olga's team, it stays through the deactivated A (m-1, as `:388` says). Fix: reword the summary line.

### QA's open minors

- **m-1 · confirmed minor.** `AccessSet.SeesTeam` (`AccessSet.cs:93`) and `SeesArea` (`:120`) count deactivated projects
  (`SightData.Projects` includes them), so a team or department can stand on a project no surface shows. No disclosure: the caller saw
  that project when it was active. Same family: a department whose only visible project is deactivated stays in offers and counts.
- **m-2 · confirmed minor.** BR-TD-21 "whichever view is open": the diagram tab has no field (`AreasPage.tsx:261`). Its own search
  reads the same narrowed tree, so no leak.
- **m-3 · confirmed minor, widened.** A sub-area name matches (`structureFilter.ts:43`), which BR-TD-21 does not list; and the key of a
  deactivated project node (shown in the tree) never matches, because only `useProjects(…, 'active')` is read (`AreasPage.tsx:85`).

### Security pass (L checklist)

- **Authorization / non-disclosure of every Organizations read that names or counts departments, teams, projects.** All `MapGet` of
  `OrganizationEndpoints.cs` checked. Narrowed here, before any cut or count: selector-offers and overview (ids inside each statement,
  `NavigationReadModel.cs:111-131`; the «Обзор» `Take(5)` and `CountAsync` run over the narrowed query), projects, projects/deactivated,
  teams (rows and nested projects), teams/{t}/projects, projects/{p}/teams|team-links, areas (+`linkableFor`), areas/{a} (children; a
  hidden id 404 at `GetAreaHandler.cs:58-61`), areas/{a}/teams|deletion, areas/writable-ids. Left unnarrowed, judged safe: teams/{t},
  projects/{p}, projects/{p}/areas (object gate; their department refs and paths name only ancestors of a visible object, visible by
  BR-TD-14), teams/{t}/deletion-counts and projects/deactivated (organization-scope codes, Admin bundle only,
  `OrganizationRoleBundles.cs:23-35`), unlink-count (gated by both ends), switch impact, grants, members (no structure names). Aggregate
  views: feat-062.
- **Derived disclosures.** A visible team's `departmentId/Name`, a visible project's department and `teamIds`, `myTeams`, tree paths: each
  names only objects BR-TD-13/-14 already make visible. Verified by reading the rule, not only by tests.
- **One rule.** Every filter calls `AccessSet.SeesProject/SeesTeam/SeesArea`; `Sighted()` is built from the same three. No second
  implementation found.
- **Fail-closed.** Navigation reads throw for a non-administrator without sight (`NavigationReadModel.cs:133-136`, tested
  `NavigationReadModelTests.cs:218-224`); other `CallerAccess` users — R-2.
- **Administrator.** `Sighted()` null and `Sees*` true, nothing narrowed (`Ann_...`).
- **IDOR / injection / secrets / personal data / audit.** No new route, body or write; the id arrays are bound parameters
  (`ids.Contains` → `= ANY(@p)`); no secrets, no personal data added to logs or responses; reads need no audit.

### Correctness and performance

- `AccessSet` memoization: `_seenProjects/_seenTeams` are filled only after the admin and not-in-organization early returns, the inputs are
  immutable for the set's life, the set lives one request (`HttpContextOrganizationAccess.cs:62-72`), so cached answers equal fresh ones.
  `SeesTeam → SeesProject` only; no cycle. The lookups give an empty sequence for a missing key, matching the old `Any` scans.
  `Sighted()` parity pinned by `AccessSetSightTests` (17 passed).
- Statements: the set read stays 8 per request (shared, cached); navigation reads 7 and 4 with ids as parameters. Routes that did not
  read the set before (`teams` without `linkableFor`, selector-offers, overview, areas/{a}/teams|deletion where the gate had not loaded
  it) now read it once: a fixed +8, no N+1. Whole-list decisions are linear.

### Frontend

- BR-TD-21: label, ~360 px, Esc, first character, case-insensitive, name + key + team name, ancestors kept, matching department keeps its
  descendants, `useState` only, no navigation (`structureFilter.ts`, `AreasPage.tsx:82-108,262-268`). Candidates are only server-narrowed
  reads. Gaps: m-2, m-3.
- R-4 of feat-041 closed: `goneSelectionLevel` → `NotFound keepNavigation` in backlog, board, grouped and flat «Рабочие элементы»; other
  failures keep «Повторить» (`AggregateViews.gone.test.tsx`). The flat list's early return sits after its hooks (`AggregateWorkItemsPage.tsx:100-103`).

### Behavioural claims in prose

Direction 1 (prose the diff adds):
- api-contract § The rule, rows of § Changed reads → the handler lines in the security pass above. OK.
- api-contract "before any cut or count … no `ещё`" → `NavigationReadModel.cs:20-48`. OK.
- api-contract "`myTeams` as before (visible by membership)" → `TeamsWithMembership(…, sight, onlyMine)`; membership ⇒ `SeesTeam`. OK.
- api-contract "a hidden `areaId` answers 404" → `GetAreaHandler.cs:58-61` + gate. OK.
- api-contract / plan "Statement counts unchanged: 8, 7, 4" → `ScopedAccessStatementCountTests` (untouched), `NavigationReadModelTests.cs:190-205`. OK.
- plan-backend "No second implementation" → all filters call `Sees*`. OK.
- plan-backend "memoizes … linear" → `AccessSet.cs:64-95,143-156`. OK (department answers not memoized; each node asked once).
- plan-backend "read model throws for a non-administrator without sight" → `NavigationReadModel.cs:133-136`. OK.
- plan-backend "a visible node's ancestors are visible by construction" → BR-TD-14 via `ProjectsOfDepartment`, `SeesArea` for sub-areas. OK.
- `CallerAccess.cs` doc "A read that names or counts them applies it" → R-2.
- plan-frontend § 1 filter behaviour → `structureFilter.ts:11-61`, `ListFilter.tsx`. OK; m-2/m-3 gaps disclosed.
- plan-frontend § 2 "the client builds nothing of its own" for offers → unchanged `useNavigationSource`; no hidden object source on the client. OK.
- plan-frontend § 3 "no paging; length over the narrowed list" → handlers return whole narrowed lists. OK.
- qa.md backend row "Pickers narrowed before link filter — pass" → R-1.
- qa.md "projects/deactivated … administrator role only" → `OrganizationRoleBundles.cs:23-35`. OK.
- status Log [backend] test counts and [frontend] counts → not re-run in full; targeted runs agree (AreasPage file holds 32 after QA's +3).
- Test comment `StructureListsSightTests.cs:375` → R-5.

Direction 2 (prose outside the diff on changed behaviour): grepped «Найти область», `feat-061`, `CallerAccess`, "not narrowed" over
`docs/`, `.claude/`, `src/**/AGENTS.md`, `src/frontend/src`. Hits: `areaTree.ts:168` → R-4; feat-041 api-contract § 5, plan, qa,
review R-4, status (all say "until feat-061" — historical, now true); feat-043 design § 5.2 and feat-049 artifacts («Найти область») —
merged features' design records, superseded by doc-016 BR-TD-21, no living doc repeats them. `docs/backlog.md:94` line matches scope.
Not swept: the Boards/WorkItems prose on aggregate headers (feat-062's behaviour, unchanged here).
