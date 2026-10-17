# Security pass: mod-017-work-item-owning-team

## Round 1: security (L, stage 06)

- **Session.** Clean context, read-only. The checkout is the task worktree on `mod/017-work-item-owning-team` at `cd15dca1`. The diff is `git diff 8b1be5de..HEAD`, mod-017 only (feat-039 is below `8b1be5de`). Nothing was built or run: the QA pass runs the suite in this checkout at the same time. No background process was started.
- **Read in full.** The three endpoint files. Handlers: SetDefaultTeam, GetTeamOwnedItemCount, DeleteTeam, SaveWorkItem, CreateWorkItem, ReorderBacklog, GetTeam, GetTeams, RenameTeam. The diffs of AddTeam, CreateArea and DeleteArea. Domain: `ProjectTeams`, `ProjectDefaultTeam`, `WorkItemFilter`, the `Team`, `Project` and `WorkItem.Save` diffs. Infrastructure: `WorkItemRepository`, `TeamRepository`, `ProjectRepository`, `BacklogRepository`, `WorkItemTeamOwnership`, `TeamBacklogPlaces`, `DefaultTeams`, `OrganizationStructure`, `WorkItemFilterPredicate`, both read models' team joins, `OrganizationsSaveChanges`, both migrations, and `OrganizationRoleBundles`. Tests: the route inventory, `OrganizationContainmentTests` and the race-test names. Frontend: the API clients, the filter query and the permission gating of the teams pages.
- **Verdict: changes-requested.** One major (S-1) and one minor (S-2).

### S-1 · major · backend: a creation whose project default resolves to none takes no team lock, so it does not serialize with a team deletion

**Where.** `src/backend/Mustask.Infrastructure/WorkItems/WorkItemRepository.cs:65-67`. `LockDefaultTeamAsync` returns before taking any lock when `DefaultTeams.ResolveAsync` gives none. That is every project with two or more active teams and no named default, which after the upgrade means every multi-team project (BR-OT-14). The child path relies on this lock (`CreateWorkItemHandler.cs:144`, `:185`). The deletion locks the owned items in one statement over its snapshot (`WorkItemTeamOwnership.cs:16`) and releases them in another (`:21`).

BR-OT-13 and api-contract § 2.7 say two outcomes never happen. This gap allows both:

1. **An item stays owned by a deleted team.**
   - Deletion D of team T holds every active team `FOR NO KEY UPDATE`.
   - Child C1 is created beneath P, which T owns, and holds P. D's `LockOwnedAsync` waits on P with a snapshot that predates C1.
   - C1 commits. D locks P but not C1.
   - Grandchild C2 is created beneath C1. With a none default it takes no team lock. It locks C1, which D does not hold, reads T and inserts. The owner key takes only a key share of T, which D's `NO KEY UPDATE` admits (D-3).
   - If C2 is still in flight when D's release statement starts, the release waits for C2 on C1, then updates C1. It cannot see C2's row, so C2 is still owned by T after D commits.
   - Nothing catches this. Teams are never row-deleted, so no `23503` fires. The team joins have no status filter (`WorkItemReadModel.cs:165`, `BacklogReadModel.cs:141`), so the card shows the deleted team's name.
2. **A child gets the wrong default after a deletion.**
   - Take a two-team project with no named default.
   - A child beneath an item of T waits on the parent that D holds. After D commits, it reads the parent released.
   - It then takes the default it resolved before waiting, which is none. The project's only remaining team is what BR-OT-13 (2) requires: "the project default as it stands after the deletion".

**The tests miss both.** `DeleteOwnedItemsEndpointTests.Delete_VersusChildCreation_BothOrders` uses `OwningTeamTestData.CreateWorldAsync`, which has three active teams, and tests only one level of children.

**Fix.**
- When the default resolves to none, lock the project's active teams `FOR SHARE` in id order: one statement, at step 2 of the lock order, before the number row. Then resolve again under that lock.
- A creation then always queues against a deletion, because the deletion locks every active team (D-4). Either it commits before the deletion reads the owned items, or it reads its parent and the default after the deletion commits. The same fix closes outcome 2.
- Tests: add a two-team case that expects the remaining team, and a grandchild created during the deletion.
- Correct the prose that claims the guarantee:
  - `CreateWorkItemHandler.cs:41-47`: "every other creation that may default … locks the project default team … or waits for it";
  - `DeleteTeamHandler.cs:38-40`: "a child created beneath one of its items … is reassigned all the same";
  - plan-backend § 7.3 (2) and D-14.
- A narrower alternative closes outcome 1 only: the deletion takes the project's hierarchy row before `LockOwnedAsync`.

### S-2 · minor · backend: the structure contract's by-id reads still cross the tenant boundary

**Where.** `src/backend/Mustask.Application/Features/Organizations/IOrganizationStructure.cs:28` and `:34`. `FindProjectAsync(projectId)` and `FindTeamAsync(teamId)` read an object of any organization.

**Why it is a finding.**
- ADR-0028 § 4 says "No by-id read is cross-tenant any more".
- ADR-0028 § 8 says "every method takes the organization id, and an object of another organization reads as null".
- The ADR's delivery table gives mod-017 "the contract changes of rule 8 except the area reads".
- api-contract § 7 keeps the old signatures and gives no reason.

**Today.** Containment holds only because every caller compares the organization itself:
- `SaveWorkItemHandler` and `CreateWorkItemHandler` compare `project.OrganizationId`;
- `ProjectTeams.EnsureBindable` (`ProjectTeams.cs:32`) compares `team.OrganizationId`.

A future caller that skips the comparison would read another tenant's team. Nothing leaks now.

**Fix.** Give both reads the organization id and answer null for a foreign object. Alternatively, record the deferral to `feat-028-org-level-teams` in api-contract § 7, with a Log line in `status.md`.

### Checked, no finding

- **Authorization.**
  - Both new routes declare `organizations.team.write`: `OrganizationEndpoints.cs:173` and `:217`. Both are rows in `OrganizationAddressedRoutes.All`, and the two removed routes are gone from the map and from the inventory.
  - The bundles are unchanged. `organizations.team.write` is only in the Admin bundle, because `Member` (`OrganizationRoleBundles.cs:26-31`) does not hold it.
  - The `team` field on create and save stays under `workitems.workitem.write`. A Member already moved an item between teams through its area, so nothing is widened.
  - The panel gates the admin-only controls on `organizations.team.write` (`TeamsPage.tsx:52`, `TeamPage.tsx:47`). The server enforces it in any case.
- **Tenant containment and IDOR.**
  - Every new or changed handler resolves the project under the organization before touching a team: SetDefaultTeam through `IsReachableAsync(org, project)`, the count through `FindReachableAsync(org, project[, team])`, and DeleteTeam by locking only the addressed project's teams, with the addressed team required to be among them.
  - A body team is locked through `WorkItemRepository.cs:45`, which joins the project on the organization, so a foreign id locks nothing. It is then refused 404 with a body identical to an unknown id's (`ProjectTeams.cs:32`). `OrganizationContainmentTests` compares status, code and detail. Its state snapshot now includes the owning teams and the default.
  - A foreign filter value is inert. The predicate runs over the project's own items, and the owner key keeps every owner inside the item's project.
- **An owning team is a team of the item's project.** Three keys hold it:
  - `fk_work_items_teams_project_id_owning_team_id`, `(project_id, owning_team_id)` onto `teams (project_id, id)`;
  - the default key `(id, default_team_id)` onto `teams (project_id, id)`;
  - the place key `(work_item_id, team_id)` onto `work_items (id, owning_team_id)`.

  Whether the team is active rests on locks; S-1 is the one hole found.
- **Concurrency, both orders read against the code.**
  - Save × deletion: the team share comes first, and the team is judged on a fresh read after the item lock.
  - Save × deletion of the item's old team, explicit create × deletion, default naming × deletion (team, then project row), reorder × deletion, and two deletions (all teams by id): no hole found.
  - Reorder × team change: the members' `FOR KEY SHARE` against the save's `FOR UPDATE`, then the locking re-read. No hole found.
  - Every path keeps one order: teams, then the number and hierarchy rows, then items, then order rows. No cycle was found in plan § 7.4's edges.
  - D-3 (`FOR NO KEY UPDATE` where ADR-0028 rule 8 says `FOR UPDATE`) is already open for the human. S-1's fix does not depend on it.
- **Injection.** Every raw statement in the diff is `ExecuteSqlInterpolatedAsync` or `SqlQuery(FormattableString)`, so values and uuid arrays are bound as parameters and nothing is spliced. Filter values go through `AddressedId.TryParse`. Their number is bounded by Kestrel's request line. The panel encodes them with `encodeURIComponent` (`workItemsApi.ts:306`).
- **Logging and hidden names.**
  - The three `RAISE NOTICE`s log counts only.
  - The diff adds no logger call.
  - The new refusals carry a code and no parameters. The one code with a team-name parameter, `organizations.area.is_team_default`, is retired.
  - Responses carry only names of teams in the item's own project, which every role can read through `organizations.team.read`.
- **Audit trail.** A team change and a deletion's reassignment move only `changed_at`. The history belongs to `feat-015-work-item-history` (spec § Out of scope), so this is not a finding.
- **Secrets.** None in the diff.

### Claims checked (security prose, both directions)

**Direction 1: claims the diff adds.**

- api-contract § 0.1: "only «Администратор» holds `organizations.team.write`". Anchor: `OrganizationRoleBundles.cs:23-31`.
- api-contract § 0.2 and BR-OT-16: a foreign team gets an "identical status and body". Anchor: `ProjectTeams.cs:32`, plus the detail comparisons in `OrganizationContainmentTests`.
- api-contract § 3: "a team of another organization … answer alike … nothing is disclosed". Anchors: `WorkItemFilter.OfTeams` and `WorkItemFilterPredicate.Apply`, over the project's items only.
- api-contract § 2.9: the count reveals the number to a holder of `organizations.team.write` only. Anchors: `OrganizationEndpoints.cs:217-218` and `GetTeamOwnedItemCountHandler` (organization, project and team reachability).
- api-contract § 2.5: the reorder's lock order and the stale rule. Anchor: `BacklogRepository.LockAsync` and `ReadEntriesAsync`.
- api-contract § 2.7, last paragraph: a child beneath an item of the team "ends reassigned or on the project default". **S-1.**
- `CreateWorkItemHandler.cs:41-47`: a deletion "waits for it". **S-1.**
- `DeleteTeamHandler.cs:38-40`: "reassigned all the same". **S-1.**
- plan-backend § 7.4: "no race above reaches [`23503`]". True, but it is not an anchor for the invariant. S-1 never produces a `23503`: it produces a wrong owner, silently.
- api-contract § 6: "each migration logs its counts". Anchor: the `RAISE NOTICE`s in `20261017063134_AddWorkItemOwningTeam.cs` and `20261017075902_RetireAreaOwnership.cs`.
- plan-backend § 11, the five security bullets: every new route declares a code, ids are parsed and bound, the team lock is tenant-scoped, a foreign id answers as unknown, and the count reveals only the number. Each is anchored in the points above.

**Direction 2: behaviour the diff changes, in prose it does not touch.**

- **Identifiers grepped:** `default-area`, `areas/{areaId}/owner`, `SetAreaOwner` and `SetTeamDefaultArea`, over `docs/`, `src/` and `.claude/`, excluding mod-017, the doc-0xx analyses and the migrations.
- **Hits:** the artifacts of merged lines (mod-002, mod-003, mod-006, mod-016, feat-027) and `docs/adr/0026` line 176.
- **Why they stand:** they are superseded through the P-10 notes (`mod-002/status.md:56`, `mod-016/status.md:58`) and ADR-0028's amendment of ADR-0026 rule 8. No live document tells a client to call a removed route.
- **Not swept:** the general round's full item-6 pass (it belongs to `review.md`), and frontend components beyond the API clients, the filter query and the gating.

### For the orchestrator

- **S-1** needs one fix and one delta re-check.
- **S-2** is a debt: record it in api-contract § 7, or fold it into feat-028.
- **No decision is required:** S-1's fix stays inside the plan's lock order.
