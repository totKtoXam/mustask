# Review: feat-041-project-visibility

## Round 1

Reviewer: code-reviewer (clean context, one Opus pass with the L security pass). Diff `66727b0e..61acb1fa` (81 files). Bound:
host DESKTOP-2S17CA7, `git rev-parse --show-toplevel` = the `workflow-debts-c5f842` worktree, branch `feat/041-project-visibility`.
No tests run (the reasons are in the diff and the code).

**Verdict: changes-requested** — blockers 0 · majors 2 (R-1, R-2) · minors 6 (R-3..R-8).

The sight rule (`SightRules`, `AccessSet.SeesProject/SeesTeam/SeesArea`), the gate in `OrganizationPermissionHandler`, the visibility
route, the assignee rule and the migration match BR-TD-12..-16, ADR-0040 § 4 and the contract. Grant reach with the node switch on and
off, linked-team membership, the flag, the administrator, team sight (member, grant on team or department, linked visible project),
department sight (grant, visible team or project inside) and node or sub-area sight (follows the project) are all correct. Hidden,
unknown, foreign and malformed path and query ids give the same 404 bytes before any 403. Nothing in the evaluator changes the
union of codes. The two majors are a test that lost its proof and an obligation that was deferred to this line and not delivered.

### Findings

**R-1 · major · tests.** `tests/Mustask.Api.IntegrationTests/Features/Authorization/OrganizationAccessInventoryTests.cs:429-441`
(BR-M1-33 · 6, "the behavioural proof"). For Member and Viewer, every route whose template names `{projectId}`, `{teamId}` or
`{areaId}` now asserts only "some not-found" and `return`s. That is 78 of the 108 routes. The 403-iff-the-bundle-lacks-the-code check
no longer runs for any of them, so a project, team or area route that enforces the wrong code for a non-administrator passes. The
test's name still claims the proof. `ScopedAccessTests.cs:168-178` (`MemberAndViewer_OnAWriteRoute_Get403…`) has the same pattern on
the team-member routes. mod-019 US-6 says feat-041 *adds* the "not found" cells by crossing the matrix with visibility. It does not
say feat-041 replaces the role cells.
Fix: build the inventory's organization with a visible project (flag on), a team linked to it and its department, and substitute those
real ids into the templates so the role assertions run again. Keep the fake-id 404 as a separate assertion (the new cell). Do the
same in `ScopedAccessTests`.

**R-2 · major · conformance (ADR-0029 § 8 item 4; mod-019 BR-SA-9).** `OrganizationPermissionHandler.cs:65-133` gates only the
project, team and area ids. ADR-0029 § 8 item 4 puts "the sprint's («Спринт не найден.») or the work item's not-found: every other id
in the path, outside in" before the 403. § 7 makes the handler resolve `sprintId` and the work item's identifier. mod-019 BR-SA-9
keeps the old order (403 before a nested id's not-found) only "until `feat-041-project-visibility`". As shipped, a viewer's PATCH to
`…/projects/{A}/workitems/A-999` (and a sprint write naming an unknown `sprintId` on a visible team) still answers 403 before 404.
api-contract § 1 narrows to project, team and area without saying so. Nothing hidden leaks this way (the container is visible), so
this is a conformance gap, not a disclosure.
Fix: resolve `sprintId` (inside the team) and `identifier` (inside the project) in the gate after the team or area check and answer
their not-found before item 5. Alternatively, if the orchestrator records a deferral, write it into api-contract § 1, add a note in
mod-019's status.md and queue the work.

**R-3 · minor · dead code / latent oracle.** `AccessSet.cs:115-122`: `KnowsTeam`, `KnowsProject` and `KnowsArea` have no callers.
They are the leftover of deviation (a), which QA's D-1 reversed. They answer "exists but hidden", which is exactly the distinction the
gate must not draw. Fix: delete them.

**R-4 · minor · prose overstates (frontend).** `plan-frontend.md:27-29` and the status `[frontend]` log line say a hidden object
"already shows the not-found frame … No new screen." That holds for object pages. It does not hold for the aggregate views: the
header selection feeds `projectId`/`teamId`/`departmentId` (`boards/api/aggregateApi.ts:112-114`). The selectors are not narrowed
until feat-061, so they still offer hidden objects. Choosing one (or a remembered one after the upgrade) gets the 404, and
`AggregateWorkItemsPage.tsx:167-175` shows the generic error with «Повторить», which never succeeds. This is the known interim of
US-6 "shippable: no". Fix: narrow the sentence, and name this interim in status `## Plan` "Out of scope" and in the gate-3 summary
(optionally treat a filter 404 as a gone selection).

**R-5 · minor · security: body ids.** Body ids that name a hidden object are outside the gate. Example:
`LinkProjectsToTeamHandler` (`POST /teams/{t}/projects`) answers 403 for an existing project the caller does not see and 404 for an
unknown one. For a `team.link` holder who has the GUID, that tells them whether the id exists. The same applies to item ids from
hidden linked projects on a visible team's backlog reorder. QA recorded the first. ADR-0029 § 7 asks for "identical for a foreign and
an unknown id", and under ADR-0040 hidden ≡ missing. Fix: queue with feat-062 (the team views), or judge body project and team ids with
`SeesProject`/`SeesTeam` in `ScopedCodes`.

**R-6 · minor · security: audit.** `SetProjectVisibilityHandler.cs:46-49`: turning a project visible to the whole organization
leaves no trace of who did it or when. Grant writes log the actor (`GiveGrantHandler.cs:88`). Fix: an `ILogger` information line like
the grants' (project, value, actor).

**R-7 · minor · performance (deviation e).** On a non-administrator's object-addressed request the gate loads the full set (8
statements: the tree, 3 grant reads, all projects, teams and links of the organization, and the caller's memberships). `HoldsAsync`
(`OrganizationPermissionHandler.cs:38-62`) then re-reads the reach with `ReachingGrantsAsync` (3-4 more), although the loaded set
already answers `Holds`. Nothing pins the per-request total. Within ADR-0029 § 7's "200 projects once per request", so this is not
blocking. Fix: answer `HoldsAsync` from the cached set when it is loaded.

**R-8 · minor · prose.** `qa.md:26` cites `ProjectVisibleToOrganizationMigrationTests` "(4)". The class has 2 facts. Fix: "(2)".

### Security pass (L)

- Authorization of every endpoint: `PUT /projects/{id}/visibility` declares `organizations.visibility.write` with scope Project
  (`OrganizationEndpoints.cs:227`). It is held by the project and department «Менеджер» (`ScopedRoleBundles.cs:46,60`) and the
  administrator only (`OrganizationRoleBundlesTests`). The gate runs before the code. A cut department manager gets 404. Pass.
- IDOR / non-disclosure: path ids and query ids are handled correctly (`HiddenObjectAsync`; QA probe byte-identical). Every route
  carries at least one of the gated ids or none, and the 30 id-less routes are organization-level. Body ids: R-5. Work item and sprint
  ids: R-2 (no disclosure). Lists and counts are not narrowed, by design (feat-061/062).
- Personal data in logs and responses: `RefusalLog` writes kind, organization, code and scope. The candidates return user ids only
  to callers who already pass the gate. Pass.
- Audit: R-6.
- Injection: the migration SQL is static. The gate parses ids with `AddressedId.TryParse`. Pass.
- Secrets: none in the diff. Pass.
- Migration on populated data: additive (column `DEFAULT false`, a new log table). The log predicate matches the rule: role is
  `'admin'`, `type = 2`, and only a department grant through an inheriting node counts. Tested on a cast with Down. Pass.

### Behavioural claims checked

Direction 1 (prose added or changed by the diff):
- status Log [backend]: "set pin 4 -> 8 (four sight statements, still fixed)" → `ScopedAccessReadModel.cs` `LoadSightAsync` (4 fixed queries). OK.
- status Log [backend]: "migration additive … the log … in the migration" → `20261116104040_ProjectVisibleToOrganization.cs:20-66`. OK.
- status Log [backend] D-1 fix: "hidden and unknown query ids are indistinguishable; `none`/empty pass; admin unchanged" → `OrganizationPermissionHandler.cs:108-133,135-136,174`. OK.
- status Log [frontend] / plan-frontend § 3: "a hidden object already shows the not-found frame; no new screen" → R-4.
- api-contract § 1 rows 1-3 → `OrganizationPermissionHandler.cs:91-131`. OK. Row 4 and ADR-0029 § 8 item 4 (sprint, work item) → R-2.
- api-contract § 1: "The administrator sees everything" → `OrganizationPermissionHandler.cs:174`, `AccessSet.SeesProject` admin short-circuit. OK.
- api-contract § 1: "Sight changes apply on the next request" → set per request, no cache (`LoadSetAsync`; `HttpContext.Items`). OK.
- api-contract § 2.1: holders, body, order, idempotent 200 → `ScopedRoleBundles.cs`, `SetProjectVisibilityHandler.cs:26-52`, `Project.SetVisibleToOrganization`. OK.
- api-contract § 3: `visibleToOrganization` on GET, `codes` listing the code → `GetProjectHandler.cs:63`, `ProjectReadModel.cs:22`. OK.
- api-contract § 3: candidates = those who see the project; without `projectId`, every active member → `GetAssigneeCandidatesHandler.cs:45-50`, `ScopedAccessReadModel.UsersSeeingProjectAsync`. OK.
- api-contract § 3: a newly set assignee who does not see the project is refused, an existing one stays → `SaveWorkItemHandler.cs:474-477` + domain `AssigneeEdit` (refuses newly set only). OK.
- api-contract § 4 / plan: log per project and a notice → migration + `ProjectVisibleToOrganizationMigrationTests`. OK.
- api-contract § 5 / status Plan "Out of scope": lists, selectors, counts not narrowed → no read model changed. OK. Consequence unstated → R-4.
- mod-016 status.md P-10 note: order change, § 3.4 reworded, new code, inventory 108 → `OrganizationAddressedRoutes.cs:67`, `TeamViewContainmentTests.cs:232`. OK.
- plan-backend: "`KnowsProject/KnowsTeam/KnowsArea`" → no callers (R-3).
- plan-backend: "The set read is kept in the request for the handlers" → `OrganizationPermissionHandler.cs:85`, `HttpContextOrganizationAccess.cs:49`. OK.
- qa.md: migration tests "(4)" → R-8.
- `IScopedAccessReadModel.UsersSeeingProjectAsync` doc (the inverse form) → parity test `UsersSeeingProjectParityTests`. OK.
- `contracts/access/role-bundles.json` "regenerated" → diff adds the code in 3 bundles and the catalog. OK.

Direction 2 (prose outside the diff describing the changed behaviour):
- Answer order: grepped `Is403Not404`, `Order_AViewersWriteToAMissingProject`, "403 comes before", "before any id is looked at", "until `feat-041`". Hits:
  - ADR-0029 § 8 (consistent; its item 4 → R-2) and mod-019 spec BR-SA-9 / § Non-disclosure / US-3 (these defer to this line → R-2).
  - feat-028 plan-backend D-7 and its test table (`Viewer_…_Is403Not404`; history of a merged line, and the test is restated in the diff).
  - mod-018 review (history).
  - feat-028 api-contract:52 ("second codes first of all, before any id is looked at": still true of body ids; the addressed id now comes first, which the mod-016 note covers).
- Visibility code: grepped `organizations.visibility.write`. mod-019 spec BR-SA-16 and US-8 say "stays the project role's (feat-041)", but the diff also gives it to the department «Менеджер». doc-016 spec line 97 (decided at gate-2) supersedes that, so not a finding.
- Assignee: ADR-0029 § 11's «нет доступа к проекту» marker for an assignee who lost access is not built. Neither this diff nor its contract claims it. Not swept further.
- Not swept: every story of mod-007, feat-040, feat-048 and feat-057/058 that describes team or aggregate views as organization-wide (owed to feat-062 by the decomposition).

## Round 2 (delta)

Delta read of `ccb67b2d..4ff58635` (8b725eb2, developer: R-1, R-2, R-3, R-5 in part, R-6, R-8; 4ff58635, orchestrator: R-4) against the
findings still open from Round 1. No tests run: the developer's full run is in the status Log.

**Verdict: approved** — blockers 0 · majors 0 · minors open: R-5 (rest), R-7, R-9 (new). These are debts for the gate-3 summary.

- **R-1 · closed.** For a non-administrator, `OrganizationAccessInventoryTests` now builds a world with a project visible to the
  organization, its linked team, the department, a sprint and an item (`OrganizationAddressedRoutes.PathOf`), so the role cell is
  judged again on every object route. The cells for codes a role lacks must be exactly 403, so they also prove the ids are visible.
  The not-found cells are a separate theory
  (`EveryObjectRoute_ForANonAdministrator_NamingAnObjectThatDoesNotExist_Is404OfItsKind_BeforeAny403`). `ScopedAccessTests.cs:177-183`
  runs the role cell after the 404 cell.
- **R-2 · closed.** `OrganizationPermissionHandler.cs:114-135` resolves `sprintId` inside the team and `identifier` inside the project.
  Identifier resolution uses the key compared ordinally, as `WorkItemAddress.cs:40` does, and covers any state. Both run after the
  project, team and area checks and before the 403, for non-administrators only. This is pinned by
  `ProjectSightNestedIdsTests.ASprintOrAnItemThatDoesNotExist_…` and the four restated legacy tests. api-contract § 1 item 3a records
  it, including the type-key gap the handler still judges, and there is a P-10 note in mod-019's status.md.
- **R-3 · closed.** `AccessSet.Knows*` is deleted.
- **R-4 · closed.** status.md § Plan names the interim.
- **R-5 · partly closed.** `ScopedCodes.SeesAllAsync` runs before the second code on both link routes
  (`LinkProjectsToTeamHandler.cs:89`, `LinkTeamsToProjectHandler.cs:92`), and `HttpContextOrganizationAccess.SeesAsync` reuses the
  request's set. Still open, recorded in api-contract § 1 item 4: the item ids of a team backlog reorder and the other routes' body
  ids. These go to feat-062.
- **R-6 · closed.** `SetProjectVisibilityHandler.cs:52-54` logs the project, the value and the actor (an id, no personal data).
- **R-7 · open (debt).** Unchanged. The nested-id gate adds 1-2 statements to sprint and item routes on top of it.
- **R-8 · closed.** `qa.md:26` now says (2).

New in the delta:

**R-9 · minor · tests.** R-5's sight check on `POST /projects/{p}/teams` (`LinkTeamsToProjectHandler.cs:92`) has no test of its own.
`ProjectSightNestedIdsTests` covers only `POST /teams/{t}/projects`. Fix: a mirror fact asserting that a hidden team in the body gets
the same bytes as an unknown one.

### Behavioural claims checked (delta)

Direction 1:
- api-contract § 1 item 3a (sprint and identifier not-found before 403; type key left to the handler) → `OrganizationPermissionHandler.cs:114-135`. OK.
- api-contract § 1 item 4 (body ids on the two link routes; reorder item ids not done) → `ScopedCodes.SeesAllAsync` + the two handlers. OK.
- api-contract § 1 "the visibility write logs … the actor" → `SetProjectVisibilityHandler.cs:52`. OK.
- mod-019 status.md P-10 note → same anchors. OK.
- status.md § Plan interim (aggregate views 404 with «Повторить») → `AggregateWorkItemsPage.tsx:167-175`. OK.
- status Log [backend]: "full Api.IntegrationTests … 4 restated and re-run green" → the four restated files are in the diff. Not re-run here.

Direction 2: the changed behaviour is sprint and item not-found before 403. I grepped `Спринт не найден`, `workitem.not_found` and
"403 before a nested id" under docs/. ADR-0029 § 8 item 4 now matches. mod-019 BR-SA-9's "until feat-041" is fulfilled, with the P-10
note. No other hit describes the old order as current.
