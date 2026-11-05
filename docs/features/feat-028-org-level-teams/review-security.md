# Security pass: feat-028-org-level-teams

## Round 1: security (L, stage 06)

- **Session.** Clean context, read-only. The checkout is the task worktree on `feat/028-org-level-teams` at `429cd672`. The diff is
  `git diff 92942e14..HEAD`. Nothing was built or run, because the QA pass runs the suite in this checkout at the same time. No
  background process was started.
- **Read in full.**
  - The endpoint files: `OrganizationEndpoints.cs` and `MembershipEndpoints.cs`.
  - Handlers: every new or changed Organizations slice (UnlinkTeam, Link×2, AddTeam, CreateProject, DeleteTeam, GetTeamDeletionCounts,
    SetDefaultTeam, the four member slices, Rename/SetTeamDescription, SetProjectDescription, GetTeam(s), GetTeamProjects,
    GetOrganizationTeams, GetTeamOwnedItemCount), plus the Boards and WorkItems structure call sites.
  - Domain: `ProjectTeams`, `ProjectTeam`, `Project`, `PermissionCatalog`, `OrganizationRoleBundles`.
  - Infrastructure: `ProjectTeamRepository`, `TeamRepository`, `ProjectRepository`, `OrganizationStructure`, `DefaultTeams`,
    `Reachability`, `TeamReadModel`, `MemberReadModel`, `WorkItemRepository`, `WorkItemTeamOwnership`, `BacklogRepository`,
    `TeamBacklogPlaces`, the four configurations, `OrganizationsSaveChanges` and the migration (`Up` and `Down`).
  - Tests: the route inventory, `OrganizationContainmentTests`, the second-code handler tests, and the race, schema and migration test
    names.
  - Frontend: the API client paths, query keys, permission codes and description rendering.
- **Verdict: approve.** Four minors (S-1..S-4); no blocker or major. None of them waits on a decision.

### S-1 · minor · backend: a binder's link lock and its "linked" read are two statements

**Where.**

| Binder | The lock (no rows returned) | The fresh read that decides |
|---|---|---|
| Item create or save | `WorkItemRepository.cs:45` `LockTeamLinkAsync` | `SaveWorkItemHandler.cs:269`, `CreateWorkItemHandler.cs:222` |
| Default naming | `ProjectTeamRepository.cs:38` (the bool is discarded at `SetDefaultTeamHandler.cs:65`) | `SetDefaultTeamHandler.cs:67` |
| Creation that may default | `WorkItemRepository.cs:58` (all of the project's links `FOR SHARE`) | `DefaultTeams.ResolveAsync` at `:75` |

Under READ COMMITTED a link committed between the two statements is judged linked (or becomes the default) although the binder
never locked it. Three requests have to meet in that gap:

1. A link (P, T) commits.
2. An unlink or a deletion of T takes the link `NO KEY UPDATE` before the binder writes.
3. The binder's owner (or default) key takes only a `KEY SHARE` of the link, which `NO KEY UPDATE` admits.

The unlink's or the deletion's reads miss the uncommitted row (`LockOwned`, `Reassign`, `ClearDefault`). Its final `DELETE`
(`UnlinkTeamHandler.cs:159`, `DeleteTeamHandler.cs:88`) then waits on that key share. Once the binder commits, the Restrict check
raises `23503`, which nothing maps, so the answer is a 500.

**What holds.** Nothing is corrupted. The transaction rolls back, and the keys still guarantee that every owner and every default is
linked.

**What it breaks.** It refutes plan-backend § 7.4 ("each already holds the row `SHARE`") and the remark at
`UnlinkTeamHandler.cs:35-37`. It also breaks the spec NFR "no server error" in this narrow window.

**Fix.** Judge "linked", and resolve the default, from the rows the lock statement itself returns
(`SqlQuery<Guid>($"SELECT team_id … FOR SHARE")`). Alternatively, lock again when the fresh read finds a link the lock did not hold.
Add a race test: link, then a binder in the gap, then an unlink.

### S-2 · minor · backend: list fields have no upper bound, and AddTeam looks members up in the directory first

**Where.**
- `AddTeamHandler.cs:121-122`: every distinct `memberIds` entry goes to `UserDirectoryBatches.LookupAllAsync`, one statement per 500
  ids, before `FilterMembersAsync`, the one-statement membership filter that would reject them.
- No handler bounds `memberIds`, `projectIds` or `teamIds`; the request body limit is the only cap.

**Impact.** An administrator's single request with 10^5 fabricated user ids runs about 200 directory statements before its 409.
api-contract § 0.2 (`:71-73`) makes two claims the code does not meet:
- "bounded only by the organization's own volumes" — nothing enforces it;
- "checked in a number of statements that does not grow with its length" — false for `memberIds` past 500.

It is admin-only, so the finding is minor.

**Fix.** Run `FilterMembersAsync` first, and look up only the ids it returns. Optionally refuse a list longer than the organization's
volume (2 000) with 400. Either way, align the contract sentence with the code.

### S-3 · minor · backend: TeamRepository's "every statement carries the organization" has an exception

**Where.** The class remark at `TeamRepository.cs:10` is new in this diff. `FindNameAsync(teamId)` at `:64` reads a team of any
organization. It is pre-existing, and its only callers are tests (`RenameProjectEndpointTests.cs:42`, `RepositoriesTests.cs:87`).
Nothing leaks today. A future caller trusting the remark would read another tenant's team name.

**Fix.** Give it the organization, or drop it and let the two tests read through the context.

### S-4 · minor · tests: the second-code tests cannot tell which code is asked

**Where.**
- `FakeOrganizationAccess` is role-based (`FakeMemberReadModel.cs:79-84`).
- The D-7 tests all use «Участник», who holds none of the codes involved:
  - `UnlinkTeamHandlerTests:236`;
  - both `Link*HandlerTests` (`:149`, `:151`);
  - `GetTeamOwnedItemCountHandlerTests:111`;
  - `AddTeamHandlerTests:225`;
  - `CreateProjectHandlerTests:466`.

**What survives.** These mutations pass every test today:
- a handler that asks the route's own code instead of the other end's;
- dropping either half of `!Holds(TeamLink) || !Holds(ProjectLink)` (`AddTeamHandler.cs:53`, `CreateProjectHandler.cs:53`).

That costs nothing under today's bundles. It is what `mod-019-scoped-access` will rely on.

**Fix.** Let the fake take an explicit code set. Add one case per second code: route code held, second code missing → 403 and no
call. All codes held → passes.

### Checked, no finding

- **Authorization.**
  - Every new route declares an organization code, and the declared codes match `OrganizationAddressedRoutes` (55 rows).
  - The team-member routes moved with `team.read`/`team.write`.
  - The four new codes are in `PermissionCatalog` with `PermissionScope.Organization`. They reach «Администратор» only through
    `Admin = OrganizationCodes` (`OrganizationRoleBundles.cs:23`); Member and Viewer are explicit lists that were not changed.
  - `team.write` no longer guards create, delete, the deletion counts or the default team: `team.create`, `team.delete` and
    `project.write` do.
  - Nothing is widened. `boards.backlog.reorder` keeps its holders.
  - The second code is asked before the transaction and before any id is parsed: `UnlinkTeamHandler.cs:61`,
    `LinkTeamsToProjectHandler.cs:44`, `LinkProjectsToTeamHandler.cs:40`, `GetTeamOwnedItemCountHandler.cs:31`,
    `AddTeamHandler.cs:52-61`, `CreateProjectHandler.cs:52-56`.
  - A deletion needs no code on the projects it reaches (ADR-0029 § 6).
- **Tenant containment (S-2 of mod-017).**
  - `FindProjectAsync`, `FindTeamAsync` and `FindProjectTeamAsync` filter on `(id, organization_id)` (`OrganizationStructure.cs:34`,
    `:95`, `:106`). The list reads filter `link.organization_id`.
  - No caller still compares organizations itself (grep of Application: none).
  - Every raw statement in `ProjectTeamRepository`, `TeamRepository` (S-3 aside) and `ProjectRepository` carries the organization.
    The unscoped ones (`DropForTeamInProjectAsync`, `LockOwned`/`ReassignOwned`, the reorder's link lock, `EndAllAsync`,
    `DefaultTeams`) take only ids resolved under the organization earlier in the same request (ADR-0028 rule 4).
  - Keys:
    - a link is keyed `(organization_id, project_id)` → projects and `(organization_id, team_id)` → teams (migration `:165`, `:171`);
    - a membership is keyed `(organization_id, team_id)` → teams;
    - an owner is keyed `(project_id, owning_team_id)` → `project_teams` PK, and so is a default `(id, default_team_id)`.

    So the database refuses a cross-organization link or membership, and an owner or default that is not linked to its project.
  - Foreign ids in bodies answer byte-for-byte as unknown ones, and nothing changes in either organization
    (`OrganizationContainmentTests.AForeignIdInAnOrganizationLevelBody…`, plus the path-id theory over all 55 routes).
- **Injection.**
  - Every raw statement is `ExecuteSqlInterpolatedAsync` with values only; arrays are bound as parameters (`= ANY`, `unnest`).
  - No `SqlRaw`, string concatenation or dynamic identifiers.
  - Descriptions are stored as typed, bounded by `ck_*_description_length`, and rendered as React text. There is no
    `dangerouslySetInnerHTML` in the diff.
  - API paths use `segment()` encoding, and the query keys include the organization.
- **Concurrency.** These races end in one order with the invariants held (C-1..C-18):
  - an unlink against a save, a create, a child, a default naming or a reorder;
  - two unlinks that name each other;
  - an unlink against the deletion of either team;
  - a link against a deletion or another link;
  - a deletion against a save or a creation.

  Every link lock is in (project, team) order: unlink by team, deletion by project, creation by team in one project. Team rows always
  come before links. mod-017 S-1's none-default gap is closed: a creation that may default locks every link of the project. S-1 is the
  only gap found.
- **Migration.**
  - One atomic migration. The drop runs before the reshape, and `teams.organization_id` has no default.
  - The dropped data is as BR-OL-13 lists it. `changed_at` is not written.
  - The `NOTICE` logs counts only, with no names. `Down` empties and restores the shape (A-1).
- **Logging, personal data, secrets.**
  - No logger call is added.
  - Error bodies carry fixed texts. `linked_by` is stored but never returned.
  - No secret is in the diff. Realm and Keycloak are untouched.
- **Audit.** Links record `linked_by` and `linked_at`. The history of reassignments is owed to `feat-015` (spec § Owed by). Team
  creation and deletion record no actor, as before this line; that is out of scope.
- **Deletion counts.** The counts show deactivated projects' names to `team.delete` holders («Администратор»), as plan § 11 says;
  `feat-041` will filter them.

### Behavioural claims (security-related; both directions)

- api-contract § 0.1, "the handler asks the other … first of all": anchored to the six handler lines above.
- api-contract § 5 and `ProjectTeamConfiguration` summary, "the database refuses a link across organizations": anchored to migration
  `:165`, `:171` and `OrganizationLevelTeamsSchemaTests:24`.
- `IOrganizationStructure` remarks, "every by-id read … answers null for a foreign object": anchored to `OrganizationStructure.cs:34`,
  `:95`, `:106`.
- `ProjectTeamRepository` remarks, "every statement carries the organization": anchored to all seven statements.
- `TeamRepository.cs:10`, the same claim: finding **S-3**.
- `IWorkItemTeamOwnership` remarks, "the caller passes ids it resolved under the organization": anchored to `UnlinkTeamHandler.cs:83-147`
  and `DeleteTeamHandler.cs:63-80`.
- api-contract § 0.1, "`GET …/access` lists the four codes for «Администратор» and no one else": anchored to
  `OrganizationRoleBundles.cs:23-33`.
- BR-OL-13, "the migration logs, per kind, how many it dropped": anchored to migration `:55` (counts only).
- plan-backend § 7.4 and `UnlinkTeamHandler.cs:35-37`, "every key-share taker already holds the link": finding **S-1**.
- api-contract § 0.2, list size and statement count: finding **S-2**.
- **Direction 2.**
  - Behaviours changed: the team routes left the project's address, and `team.write` was narrowed.
  - Grepped: `organizations.team.write`, `teams/{teamId}/members` and `owned-items/count` under `docs/` (feature folders excluded),
    `.claude/` and the `AGENTS.md` files.
  - Hits: ADR-0029 `:107`, `:118`, `:242`, all consistent with the code.
  - Not swept: the E2E specs. They are paused, and plan § 11 lists them as broken.
