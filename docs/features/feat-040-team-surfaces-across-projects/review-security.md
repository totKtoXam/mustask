# Security pass: feat-040-team-surfaces-across-projects

## Round 1 — security (L, stage 06)

- **Session.** Clean context, read-only. The checkout is this reader's own worktree, detached at `8b3d083f` (the local
  `feat/040-team-surfaces-across-projects`). The diff is `git diff 5de279f7..HEAD`, with 5de279f7 the merge base with `origin/main`.
  Nothing was built or run, because the QA pass runs the suite at the same time. No background process was started.
- **Read in full.**
  - API: `BoardEndpoints.cs`.
  - Handlers: `GetBacklog`, `GetBoard`, `ReorderBacklog`, `GetOrganizationTeams`, plus the call-site changes in `GetProjectBacklog` and
    `GetProjectBoard`.
  - Abstractions: `CallerAccess`, `IOrganizationAccess`, `IOrganizationStructure` (`FindTeamAsync`, `ListLinkedProjectsAsync`).
  - Domain: `WorkItemFilter`, `StackRank`, `OrganizationRoleBundles`.
  - Infrastructure: `BacklogMembership`, `BacklogReadModel`, `BacklogRepository` (all raw SQL), `OrganizationStructure`,
    `ProjectMembers`, `TeamReadModel`, `WorkItemFilterPredicate`, the owner and parent keys in `WorkItemConfiguration`, and the
    migration.
  - Tests:
    - `TeamViewContainmentTests`, `RetiredTeamViewRoutesEndpointTests`, `TeamProjectFilterEndpointTests` (its invalid-value cases);
    - the test names of `TeamCardAndMovesEndpointTests` and `CreateFromTeamAcrossProjectsEndpointTests`;
    - the inventory rows, the surface-matrix rows and the cross-project parity test.
  - Frontend:
    - `boardsApi.ts`, `paths.ts`, `addressFilter.ts`, `useViewFilter.ts`, `ProjectFilter`/`ViewFilterRow`;
    - `WorkItemCardDialog.tsx`, `identifier.ts`, the dialog's project preselection, `mutations.ts`, `useNewWorkItem.tsx`;
    - `resolveSelection.ts`, `addressContext.ts`, `storage.ts`, the organization queries;
    - the `BR-TS-4` panel tests, and the in-memory scoped-check table `testMembership.ts`.
- **Verdict: approve.** Two minors (S-1, S-2). No blocker and no major. Nothing waits on a decision, and nothing is for the human.

### S-1 · minor · frontend tests: the panel's in-memory scoped check still lists the retired routes and lacks the new ones

**Where.** `src/frontend/src/features/organizations/testMembership.ts:100-102` still holds the three rules
`/projects/{id}/teams/{id}/backlogs|boards/…`. The table has no rule for `/teams/{id}/backlogs/{level}`, `…/reorder` or
`/teams/{id}/boards/{level}`. For any route the table lacks, `routeRuleOf` (`:105-108`) returns `undefined`, and the guard lets the
request through. `testMembership.test.ts:167-169` still pins the code of the retired
`POST /projects/p/teams/t/backlogs/stories/reorder`. This branch does not touch either file.

**Impact.** No runtime effect: the server holds every route (the inventory test, `TeamViewContainmentTests`). But the file's header
claim (`:1-4`, "so a test of a page by role meets the same answers in the same order the server gives") is now false for the three
moved routes. A panel test of a non-member, or of a «Читатель» reorder, at the team's address served behind `serveMemberships` would
get a 200 or 204 where the server gives 404 or 403. It would pass for the wrong reason. Today no test drives the team views through
this guard, so the defect is latent.

**Fix.**
- Replace the three rules with
  - `rule('GET', '/teams/${ID}/backlogs/${ID}', 'workitems.workitem.read')`;
  - `rule('POST', '/teams/${ID}/backlogs/${ID}/reorder', 'boards.backlog.reorder')`;
  - `rule('GET', '/teams/${ID}/boards/${ID}', 'workitems.workitem.read')`.
- Point the unit test at the new reorder path.
- Add one assertion that the retired path has no rule.

### S-2 · minor · backend and docs: "the team-view read model takes the caller" covers two of its four cross-project reads

**Where.**
- `IBacklogReadModel.cs:27,36`: `ReadTeamAsync` and `ReadTeamTreeAsync` take `CallerAccess`.
- `:66,75`: `ReadChecklistsAsync` and `FindLiveParentsAsync` return rows of several projects on a team board or backlog, but take a
  bare `Guid organizationId`. Their callers are `GetBoardHandler.cs:75` and `GetBacklogHandler.cs:77`.
- Three texts state the wider rule:
  - spec BR-TS-13: "the team-view read model … takes the caller as a required argument … with no new signature";
  - the `CallerAccess.cs:7-11` remark;
  - ADR-0029 § 7: "every such read model takes the caller's access".

**What holds.** Nothing leaks, today or under feat-041:
- Both reads carry the organization (`BacklogReadModel.cs:84`, `:141`).
- Their ids come only from the caller-scoped rows.
- The `(project_id, parent_id)` foreign key (`WorkItemConfiguration.cs:129`) keeps a parent and its children in the row's own project.
  A visibility filter on the rows therefore filters these two reads as well.

**Why minor.** The sentence overstates. An implementer of feat-041 who takes it at its word would do nothing different, but the
interface would accept arbitrary parent ids under the organization alone. A related sentence, api-contract § 8.2 ("every statement
of a team view carries the caller's organization"), has one benign exception: the user-directory lookups go by user id, are not
tenant data, and their ids come from the organization's rows.

**Fix.** Either:
- pass `CallerAccess` to both methods on the team path (a signature change only); or
- narrow BR-TS-13, the `CallerAccess` remark and § 8.2 to "the team rows' read; checklists and parents follow by the parent key".

### Checked, holds (no finding)

- **Authorization per route.**
  - The routes and their codes:
    - `BoardEndpoints.cs:39` GET backlogs: `workitems.workitem.read`;
    - `:47` POST reorder: `boards.backlog.reorder`;
    - `:57` GET boards: `workitems.workitem.read`.
  - The scoped check runs before the body. The order (non-member 404, then 403 before the team) is held by
    `TeamViewContainmentTests.NonMember_…`, `TheAnswerOrder_…`.
  - The inventory swaps rows 35-37 (`OrganizationAddressedRoutes.cs:108-110`). `OrganizationAccessInventoryTests` compares the
    inventory against the served endpoints.
  - The «Читатель» bundle holds both read codes (`OrganizationRoleBundles.cs:17,20`).
- **Nothing is widened by taking the project out of the address.**
  - The project check is replaced by team-by-(organization, id) (`OrganizationStructure.cs:95`).
  - The project's reachability moves into every membership statement (`BacklogMembership.cs:60,86`; `BacklogRepository.cs:87,130,165`).
  - A deactivated project's items, places and name stay off the view, the `projects` list (`ReachableProjects`,
    `OrganizationStructure.cs:139-146`) and the filter.
- **Tenant containment.**
  - The team: a foreign, unknown, malformed or deleted team gives the same `organizations.team.not_found`.
  - The reorder's lock is on (id, organization) (`BacklogRepository.cs:66,71`).
  - Members, the locking read, the departed `DELETE` and the upsert ids are all members under the organization.
  - The parity test pins the predicate: a random organization id yields no member (`DeletedStateParityTests`, cross-project case).
  - The database already forbids cross-organization ownership: the owner key `(project_id, owning_team_id)` references
    `project_teams`, and `project_teams` carries the organization.
- **The project filter.**
  - Values are canonical ids or ignored (`WorkItemFilter.OfProjects`).
  - A set filter of nothing matches nothing. The predicate (`WorkItemFilterPredicate.cs:38`) is in the same statement.
  - Unknown, malformed, all-zero, foreign, unlinked and no-team projects each give a 200 that is identical apart from the empty items,
    with `projects` unchanged (`TeamProjectFilterEndpointTests.cs:161`).
  - The panel names no project and no reason for an invalid value (`ProjectFilter.tsx`).
- **IDOR in the reorder.** A foreign, unknown or other-team item gives 409 `reorder_stale`, whether it is the moved item or the anchor,
  and is alike in status and body (`StackRank.cs:99,104`; `TeamViewContainmentTests.AnotherOrganizationsIds_…`). Foreign places are
  unchanged.
- **The card, and writes from a team view.**
  - Every write goes to the row's `project.id` (`mutations.ts`, `BacklogTable.tsx`), and the server re-checks the project, the
    identifier and the parent foreign key.
  - By address:
    - the identifier's key is matched only against the organization's active projects (`WorkItemCardDialog.tsx:113-125`);
    - an unknown, malformed or deactivated key, or one of another organization, sends no request and shows one not-found frame
      (`BacklogPage.projects.test.tsx:390-426`);
    - the identifier is encoded (`workItemsApi.ts:336`).
- **The creation path's team rule.**
  - The server rules are unchanged (mod-017/feat-028): not linked → 409, deleted → 404, deactivated → 404, foreign team as unknown
    (`CreateOwningTeamEndpointTests.cs:198`).
  - The dialog preselects only a project that is both in the read list and in the filter (`CreateWorkItemDialog.tsx:134-143`), so an
    address cannot point the creation at an unoffered project.
  - A «Читатель» gets no command (`useNewWorkItem.tsx` `canCreate`). The server answers 403 (`Roles_…ViewerCannotWrite`).
- **Ids in the address.**
  - The panel decodes and re-encodes ids and filter values (`paths.ts`, `boardsApi.ts` `projectFilterQuery`, `addressFilter.ts`
    `URLSearchParams`).
  - The API accepts only the canonical form.
  - A team address naming another organization's team: the switcher does not sync (`resolveSelection.ts` `addressSync` → `none`) and
    the read answers 404.
  - The old panel and API addresses: the API gives `http.not_found` for every method and every caller
    (`RetiredTeamViewRoutesEndpointTests`).
- **Injection.**
  - All new raw SQL is `ExecuteSqlInterpolatedAsync`/`SqlQuery` with parameters (`BacklogRepository.cs:65-180`). There is no
    `*Raw` call.
  - The `projectKeyOf` regex (`identifier.ts:8`) has no nested quantifier.
  - No `dangerouslySetInnerHTML`, `href` from data, or `eval` is added. The created notice and «Проект» render as text.
- **What the switcher's team level discloses.**
  - The level shows `GET /organizations/{o}/teams` (`organizations.team.read`, held by every role), unchanged.
  - `isMember` is the caller's own membership only (`TeamReadModel.cs:24`, keyed by the token `sub`).
  - The remembered team is stored per subject. The query cache is cleared at sign-out (`AuthContext.tsx:173`).
- **Personal data, audit and secrets.**
  - The rows' assignees are the organization members' as before. Membership is now asked of the caller's organization
    (`ProjectMembers.cs:41`).
  - No logging is added. The reorder has no audit trail, as in mod-006.
  - No secret is added.

### Behavioural claims (security prose; both directions)

- api-contract § 0.1, tenant isolation on every route → `TeamViewContainmentTests.cs:111` (all three routes, team, filter, item).
- § 0.2, canonical ids only, else "names nothing" → `GetBacklogHandler.cs:43`, `ReorderBacklogHandler.cs:53,67,97`,
  `WorkItemFilter.cs:60`.
- § 0.3/§ 0.4, the order of answers → `TeamViewContainmentTests.TheAnswerOrder_…`.
- § 3.2 step 3, foreign items alike as stale → `StackRank.cs:99,104`, the containment test.
- § 3.2, "no link and no project row is locked" → `BacklogRepository.cs:80-92` (`FOR KEY SHARE OF item`).
- § 3.4, retired routes are `http.not_found` for everyone, with no alias → `BoardEndpoints.cs:35`,
  `RetiredTeamViewRoutesEndpointTests.cs:37,66,78`.
- § 4.1, `isMember` is the caller's → `GetOrganizationTeamsHandler.cs:28`, `TeamReadModel.cs:24`.
- § 8.2, "every statement carries the caller's organization": anchored for the database reads. The directory lookups are by id
  → S-2.
- spec BR-TS-13, "the team-view read model takes the caller" → partly anchored → S-2.
- spec BR-TS-13, the codes per route → `BoardEndpoints.cs:39,47,57`, and `TeamViewContainmentTests.cs:219-231` (the inventory).
- spec BR-TS-2, a deactivation is never revealed → `ListLinkedProjectsAsync` returns only reachable projects. A reorder on a team with
  no active project gives 409, the same as JUPITER's.
- `CallerAccess.cs:7-11`, "its `OrganizationId` is the tenant predicate" → `BacklogMembership.cs:60,86` → S-2 for its scope.
- `testMembership.ts:1-4`, "the same answers … the server gives" → false for the three moved routes → S-1.
- **Direction 2.** I grepped `teams/{teamId}/backlogs|boards` and `teams/:teamId/…` outside this task's folder. The hits under `docs/`:
  - the merged contracts and specs of mod-006, mod-016, mod-017, feat-027, feat-028, feat-032, feat-039: they carry P-10 notes;
  - feat-001, feat-022, mod-015 design, doc-012 audit: no note, but they hold no access or containment claim on the moved routes
    (left to the general round's item 6).

  The hits in code: the frontend test double → S-1. Swept: only the routes and their access or containment prose. Not swept: the
  ordering, switcher and UI prose (general round).

### E2E

Not run (the standing answer is NO). The specs this line breaks are listed in plan-backend § 14 and in status.md's frontend Log line.
This pass has nothing to add to that list.
