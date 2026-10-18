# Code review, round 1

feat-028-org-level-teams (L, with UI, breaking): one round over `git diff 92942e14..HEAD` (HEAD `429cd672`), branch
`feat/028-org-level-teams`, clean context. Read against spec.md and the stories, design.md, api-contract.md, both
plans, doc-012 (BR-MX-4..-6, -12, § Existing data, its Decomposition row), ADR-0028, ADR-0029 and mod-017
plan-backend § 11. Gates 1 and 2 are taken as approved: OQ-1 → C, OQ-2..OQ-5 A, D-1..D-5 A. The review was
read-only. Nothing was built or run, because QA shares this checkout. The L security pass is a separate session.

**Verdict: changes-requested.** Three majors (CR-1..CR-3) go back to the developer. CR-3 is prose only, so it
gets a delta read and no QA. CR-4..CR-6 are minors and are debt.

## Findings

### CR-1 · major · backend (tests, doc-012 P-1) — two tests the spec retires by name survive and assert BR-M1-9

- **Where:**
  - `src/backend/tests/Mustask.Api.IntegrationTests/Features/Organizations/GetTeamsEndpointTests.cs:240-254`,
    `Get_NewProject_ListsItsOnlyTeamAlone_MarkedOnlyTeam`. Its comment reads "A project is created ready to use: its
    one team, named after it".
  - `…/Organizations/DeleteOwnedItemsEndpointTests.cs:160-171`, `Delete_TheTeamCreatedWithTheProject_IsAllowedWhenAnotherRemains`,
    on "the team the project was created with".
- **Essence:**
  - spec.md:89 retires the first by name (BR-M1-9 → BR-OL-2), and spec.md:91 retires the second
    (BR-OT-11 → BR-OL-10).
  - The handler twin of the first is gone. These two stayed, and their names and comments still state the retired
    rule. `CreateProjectEndpointTests` in the same diff disproves that rule: a project is created with no team.
  - They pass only because `OrganizationsTestData.CreateProjectAsync` still adds a linked team named after the
    project. The tests therefore check the fixture, not the system.
- **Fix:**
  - Delete both, with a comment `retired by name, BR-OL-2 / BR-OL-10 (P-1)` at the place.
  - If the cases are still wanted, rewrite them under names that say what they check. For example, a project with
    one linked team lists it as `onlyTeam`, and any team may be deleted while another is linked.
  - Consider renaming `CreateProjectAsync` to `CreateProjectWithTeamAsync`, so the next reader is not misled the
    same way.

### CR-2 · major · frontend — the unlink dialog swallows a refusal when the team owns nothing

- **Where:** `src/frontend/src/features/organizations/components/UnlinkDialog.tsx:90`, with the no-field branch at
  :160-166.
- **Essence:**
  - `placeRefusal(error, ['destination'])` is passed whatever `owned` is.
  - When `owned === 0`, no field is rendered. `team.not_found` maps to `destination` (`refusals.ts:57`), so a 404
    for the address team goes into a field that is not on screen. That happens when the team was deleted while the
    dialog was open (UnlinkTeamHandler step 3).
  - The result: `alert` is `null`, `contextGone` stays `false`, the dialog stays open with «Отключить» enabled and
    nothing on screen changes. Each further click fails the same way.
  - Design § 6.2 (row "409, 404 team … under the field") assumes a field. This branch has none, and the code turns
    that into a silent failure of a write the user confirmed.
  - `TeamsPage.test.tsx:577-606` covers refusals only for `owned > 0`.
- **Fix:**
  - Pass `owned > 0 ? ['destination'] : []`. The refusal then reaches the alert, and `contextGone` follows from its
    not-found level.
  - Add a test: owns none, then 404 `team.not_found`, then the alert «Команда не найдена.», then on close the page
    reads itself again.

### CR-3 · major · backend (prose, same-diff contradiction) — lock and refusal comments still describe mod-017

Each sentence below is disproved by code or prose in this diff. Stage 06 makes that major regardless.

| Where | Says | Disproved by |
|---|---|---|
| `Mustask.Application/Features/WorkItems/CreateWorkItem/CreateWorkItemHandler.cs:133-134` | "locks the default as it stands, or every active team when the project has none" | the same file's class doc :42-50 and the call at :147 (`LockProjectLinksAsync`: every link, default read once) |
| same file :158-159 | "one of a deactivated project answers 404, another active project's team 409" | `ProjectTeams.EnsureBindable` (not linked → 409); api-contract § 0.3: "mod-017's row … is gone" |
| `…/WorkItems/SaveWorkItem/SaveWorkItemHandler.cs:247-250` | "an active team of this project … a team of a deactivated project 404 … another active project 409" | as above; `DecideTeamAsync` :269-273 |
| `…/Organizations/GetTeamOwnedItemCount/GetTeamOwnedItemCountQuery.cs:3-6` | the route is "`…/owned-items/count`" and the number is "what a team's deletion would reassign" | its handler's doc (`…/unlink-count`, the unlink's count in one project); `RetiredTeamRoutesEndpointTests.cs:23` |
| `tests/Mustask.Infrastructure.Tests/WorkItems/OwningTeamStatementCountTests.cs:243-245` | "every active team is locked … and the default is read again" | `ChildCreationStatements` 22 → 21 in the same file (read once) |
| `tests/Mustask.Infrastructure.Tests/Organizations/RepositoriesTests.cs:12-13` | "the deletion's lock takes every active team of the project in id order" | the class's tests now lock links (`LockLinks_OfAPair_…`, `LockTeamLinks_…`) |
| `tests/…/Organizations/DeleteOwnedItemsEndpointTests.cs:509` | the deletion "holds every active team" | `DeleteTeamHandler.cs:67,82`: the team row and its links |
| `tests/…/WorkItems/OwningTeamQaEndpointTests.cs:206-207` | "It now locks every active team when no default resolves" | `WorkItemRepository.cs:63-75` |

**Fix:** reword each sentence to the link model. The fix is prose only.

### CR-4 · minor · backend — "no unlink or deletion can commit before this creation" holds only for the links that existed at the lock

- **Where:** `src/backend/Mustask.Infrastructure/WorkItems/WorkItemRepository.cs:73-75`.
- **Essence:** a link can commit between the `FOR SHARE` statement (:63-71) and `DefaultTeams.ResolveAsync`. One
  case is the first link of a team-less project, which then resolves as its only team. Such a link is resolved as
  the default without being locked. Two cases follow:
  - An unlink or deletion of that link that runs before the insert reassigns nothing. The insert then fails its
    composite FK.
  - One that runs after the insert reaches its raw `DELETE FROM project_teams`
    (`ProjectTeamRepository.cs:128,133`) with an item still owned. That raises `23503`, which is unmapped and
    answers 500.

  It needs three concurrent requests on a project that is gaining its first team. Nothing is corrupted, because the
  transaction rolls back.
- **Fix:** after resolving, lock the resolved team's link with `FOR SHARE` (`LockTeamLinkAsync`), and re-resolve
  if it is gone. Otherwise narrow the comment to "the links present at the lock". Optionally map that `23503` to
  a retryable 409.
- **Status: closed in delta read 3** (6c9720eb, by another fix than this one: plan-backend A-9).

### CR-5 · minor · backend — the reorder's link lock carries no organization

- **Where:** `src/backend/Mustask.Infrastructure/Boards/BacklogRepository.cs:62`, which runs
  `SELECT 1 FROM project_teams WHERE project_id = … AND team_id = … FOR SHARE`.
- **Essence:** plan-backend § 8 says every raw statement carries the organization (ADR-0028 § 4). Every other
  `project_teams` statement in the diff does. The risk is low, because the project is already resolved under the
  organization of the address.
- **Fix:** add `organization_id = {organizationId}`, as `WorkItemRepository.cs:53` does.

### CR-6 · minor · docs — a row the spec retires is kept, and the deviation is not recorded

- **Where:** `src/frontend/src/shared/api/errorCodes.organizations.test.ts:45` keeps the
  `last_team_cannot_be_deleted` row. spec.md:91 lists "`errorCodes.organizations.test.ts` rows for the code" as
  retired.
- **Essence:** the row is kept so the list stays one to one with the catalog, where the code is `deprecated`
  (ADR-0005), as mod-017's deprecated codes were. That is sound, but neither plan-frontend nor spec.md records it.
- **Fix:** add one line to plan-frontend § 6 ("kept: the catalog keeps the deprecated code"), or remove the file
  from that spec row.

## For the human

None of these blocks the round. Each one is already recorded in the plans.

1. **plan-backend D-3.** The unlink and the deletion lock links `FOR NO KEY UPDATE`, where ADR-0028 rule 8 reads
   `FOR UPDATE`. Every writer that binds a team takes a `FOR SHARE` on the link first. I found no path that
   inserts an owner without one, apart from the window in CR-4. **Recommended: accept, and amend the ADR wording
   in feat-028's follow-up.**
2. **plan-frontend § 6.** Two deviations:
   - The create-team form filters `GET …/members` locally, because no organization-scoped search exists.
     **Recommended: accept.**
   - «Отключить» is natively disabled (`ConfirmDialog.confirmDisabled`), where design § 6.2 says aria-disabled and
     focusable. **Recommended: accept now, and put the aria-disabled state into the kit's `ConfirmDialog` as a kit
     task.**

## Behavioural claims (direction 1)

- UnlinkTeamHandler: the second code is checked before any id → `UnlinkTeamHandler.cs:61`. ✓
- Unlink: the pair is locked in team-id order and both teams are read fresh under the lock →
  `UnlinkTeamHandler.cs:116-131`, `ProjectTeamRepository.cs:30-33`. ✓
- Unlink: an unlinked pair is a success → `UnlinkTeamHandler.cs:142`. ✓
- Unlink: it reassigns, drops the team's places in that project only, clears a default naming the team and
  removes the link, in one transaction → `UnlinkTeamHandler.cs:155-159`, `TeamBacklogPlaces.cs:29`. ✓
- Deletion: team row, then its links by project id, then items, places, reassignment, defaults, links,
  memberships and status → `DeleteTeamHandler.cs:67-91`, `ProjectTeamRepository.cs:61-64`. ✓
- Link: a pair that is already linked keeps its row → `ProjectTeamRepository.cs:119-121` (`ON CONFLICT DO NOTHING`). ✓
- Descriptions: empty, `null` and absent mean none; over 2 000 UTF-16 units is a 400 → `EntityDescription.cs:26,32`. ✓
- The `23503` backstops answer `team.not_found` → `OrganizationsSaveChanges.cs:74,81`. ✓
- Migration: drops every team (OQ-1 C) and logs the counts with a notice → `20261017200252_OrganizationLevelTeams.cs:55`.
  ✓ Its Down writes one team per project → :357, plan A-1. ✓
- The CreateWorkItemHandler class doc (every link `FOR SHARE`, one statement, the default read once) →
  `WorkItemRepository.cs:63-75`. ✓ Its guarantee "no unlink … can commit before" → CR-4.
- The CreateWorkItemHandler and SaveWorkItemHandler inline comments, and the query doc → CR-3.
- GetTeamOwnedItemCountHandler: the second code first, and a team not linked answers 404 → its handler's
  `access.Holds` and `FindLinkedAsync` calls. ✓
- plan-backend § 8, "every statement carries the organization" → CR-5.
- Design § 5.3 and § 6.2, "403: the codes are read again" → `access/useAccessWatcher.ts:105-113,141-145` (mutation
  failures re-read access). ✓
- Design § 6.2, "409/404 team under the field" → `UnlinkDialog.tsx:90` when `owned > 0`. ✓ When `owned = 0` → CR-2.
- Test comments asserting "a project is created with its team" → CR-1.
- `OwningTeamStatementCountTests` comments and the deletion comment → CR-3.
- spec.md:91 retires the `errorCodes.organizations.test.ts` row → CR-6.
- The pinned statement counts (unlink 11, delete 11, link 4, create team 5, create project 6), no N+1 → the
  statement-count tests' constants. The handlers issue set-based statements (`= ANY`, `unnest`, `GROUP BY`) and
  were read, not run.

## Direction 2

- **Swept**, grepping `docs/` (outside `docs/features/`), `.claude/`, `src/`, `contracts/` and both nested
  `AGENTS.md` files:
  - **The team address moved to the organization.** Identifiers: `settings/teams/:t`,
    `projects/{projectId}/teams/{teamId}`, `TeamPath`. Hits:
    - ADR-0028:167 and :236 are consistent with the move.
    - `routes.tsx:165`, `paths.ts:20-25` and `switchTarget.test.ts:132` are consistent.
    - The Boards queries (`GetBoardQuery.cs:4`, `GetBacklogQuery.cs:4`, `ReorderBacklogCommand.cs:4`) are
      team-view routes, which stay project-nested until feat-040. Consistent.
  - **The owned count became the unlink count.** Identifier: `owned-items/count`. Hits:
    - `GetTeamOwnedItemCountQuery.cs:4` → CR-3.
    - `OrganizationAddressedRoutes.cs:44` is a history line and correct.
    - `RetiredTeamRoutesEndpointTests.cs:23` is correct.
  - **The last-team guard.** Identifier: `last_team_cannot_be_deleted`. Hits:
    - `OrganizationErrorCodes.cs:82`, `OrganizationErrorCatalogTests.cs:85`, `contracts/errors/organizations.json`
      and `docs/error-codes.md` mark it deprecated. Correct.
    - `errorCodes.organizations.test.ts:45` → CR-6.
  - **A project created with a team.** Identifiers: `project comes with a team`, `created with a team`,
    `its first team`. Hits: the two tests → CR-1.
  - **The default lock.** Identifiers: `LockDefaultTeam`, `every active team`, `ListActiveTeamsAsync`. Hits: the
    CR-3 table. `GetTeamsEndpointTests.cs:31` describes the list and is correct.
  - **Nested `AGENTS.md` and `.claude/`:** no hits.
- **Not swept:**
  - The closed feature records of mod-002, mod-016 and mod-017 under `docs/features/`. They are history.
  - `src/frontend/e2e/**` specs and support. They are known to be broken by the move and are listed in
    plan-backend § 11. E2E is paused, and they were not read for edits.

## Delta read 1

The one delta read of the light route, in a clean context. Checkout confirmed (`feat/028-org-level-teams`, HEAD `52083b2b`). It
covers `git diff a5d76c0e..52083b2b`: commits 5f896fb7, cbc4d92b and 52083b2b, 11 files, +80/−54. It is read against the
majors still open from round 1 (CR-1..CR-3) and from QA (BD-2 = CR-1, FD-1 = CR-2), and nothing else.

**Verdict: approve.** CR-1, CR-2 and CR-3 (with BD-2 and FD-1) are resolved, and nothing nearby broke. Only minors remain,
and they are gate-3 debt. There is no further round.

### Resolution

- **CR-1 / BD-2: resolved** (5f896fb7).
  - Both tests are deleted. Each leaves a retire-by-name comment that cites the rule and P-1:
    `GetTeamsEndpointTests.cs:240-243` (BR-M1-9 → BR-OL-2) and `DeleteOwnedItemsEndpointTests.cs:160-163`
    (BR-OT-11 → BR-OL-10). This matches spec.md:89 and :91.
  - The tests those comments cite exist and assert what they are cited for:
    - `Get_DefaultMark_NamedOnlyTeamNone` (:79) and `Get_DefaultMark_FollowsTheLinks_NamedOnlyTeamNone` (:152);
    - `CreateProjectEndpointTests.Create_Returns201_WithTheRootAndNoTeam` (:46, with no links at :74-75);
    - `Delete_NamedTeam_ClearsTheSetting_AnotherTeamKeepsIt` (:167) and `Delete_TheProjectsLastTeam_IsAllowed_ProjectWorksOn` (:182).
  - Outside this feature's own records, nothing else refers to the two names.
  - The optional rename of `CreateProjectAsync` was not taken. It is not owed.
- **CR-2 / FD-1: resolved** (52083b2b).
  - `UnlinkDialog.tsx:92` now passes `owned > 0 ? ['destination'] : []`. With no field, `placeRefusal` falls through to
    `alertOnly` (`refusals.ts:104-110`, :66-68). The alert shows, and `contextGone` follows from the not-found level.
  - `TeamsPage.test.tsx:538` checks the project's end:
    - the alert appears, no combobox is drawn and the dialog stays open;
    - after «Отмена» the list is read again without the team. That assertion pins `contextGone` (`TeamsPage.tsx:85-90`).
  - `TeamPage.test.tsx:387` checks the team's end. The test double answers a deleted team with 404 (`testApi.ts:169-179`).
  - Both tests fail on the old line, because with `['destination']` no `alert` role appears.
  - The `owned > 0` path is unchanged, and its two refusal tests pass.
- **CR-3: resolved** (cbc4d92b). All eight rows are reworded to the link model. Each new sentence is anchored below
  (direction 1).

### Runs

- `npx vitest run` on `TeamsPage.test.tsx` and `TeamPage.test.tsx`: 67 passed. `eslint` and `prettier --check` on the three
  changed files: clean.
- `dotnet test Mustask.Api.IntegrationTests --filter "…Organizations.GetTeamsEndpointTests|…Organizations.DeleteOwnedItemsEndpointTests"`:
  40 passed, 0 failed.
- `dotnet build Mustask.Infrastructure.Tests`: 0 warnings, 0 errors. Warnings are errors here, so the reworded XML doc
  comments compile.
- There was no full verify, and no stamp exists for tree `5fdabb5d`. Against QA's green tree (`a5d76c0e`), this tree differs
  only in these 11 files.

### New

- **DR-1 · minor · design (direction 2).** design.md:260, the § 6.2 row "409, 404 team → under the field", is wrong for a
  dialog where the team owns nothing. That dialog has no field. The code puts the 404 in the alert and reads the page again
  on close (`UnlinkDialog.tsx:90-92`). Nothing an agent would do differs, because the code comment and the two tests pin the
  behaviour. **Fix:** add to the row "owns none: no field, so the alert, and on close the page reads itself again".

### Behavioural claims (delta)

Direction 1, the prose the delta adds:
- `UnlinkDialog.tsx:90-91`, "with none, every refusal is the alert's" → `placeRefusal(…, [])`, `refusals.ts:98-100` and :104-110. ✓
- `CreateWorkItemHandler.cs:131-136` → :139-149 and `WorkItemRepository.cs:45-77`. ✓ Its "never in between" now names
  the unlink as well. That is the same window as CR-4 (minor, open).
- `CreateWorkItemHandler.cs:158-162` and `SaveWorkItemHandler.cs:247-250` → the api-contract § 0.3 table (:88-92) and
  `DecideTeamAsync` (:252). ✓
- `GetTeamOwnedItemCountQuery.cs:4-7` → `OrganizationEndpoints.cs:230` (`/{teamId}/unlink-count`), api-contract § 2.4 (:360)
  and `RetiredTeamRoutesEndpointTests.cs:23`. ✓
- `RepositoriesTests.cs:11-13` → `ProjectTeamRepository.cs:32-33` (a pair, `ORDER BY team_id`), :63-64 (a team's links,
  `ORDER BY project_id`) and plan-backend D-3. ✓
- `OwningTeamStatementCountTests.cs:243-245` → `ChildCreationStatements = 21` (:60), and `LockProjectLinksAsync` takes its
  lock in one statement. ✓
- `DeleteOwnedItemsEndpointTests.cs:500-502` → `DeleteTeamHandler.cs:66-67` (the team row, `FOR NO KEY UPDATE`) and :82
  (`LockTeamLinksAsync`). ✓
- `OwningTeamQaEndpointTests.cs:205-208` → `LockProjectLinksAsync` and spec.md:73 (BR-OL-7). ✓
- The two retire comments' "pinned by" clauses → the five tests under CR-1. ✓

Direction 2:
- **Behaviour changed:** the unlink dialog's refusal placement when the team owns nothing.
- **Grepped** in design.md, US-4 and `refusals.ts`: `placeRefusal`, «Команда не найдена», "under the field", "owns none".
- **Hits:**
  - design.md:260 → DR-1.
  - design.md:181 (the deletion dialog's 404 alert) is consistent.
  - US-4:80 (the destination table) is consistent.
  - The class doc at `UnlinkDialog.tsx:22-30` is consistent.
- The CR-1 and CR-3 commits change no runtime behaviour, so there was nothing more to sweep. The only identifiers were the
  two retired names, grepped above. Nothing was left unswept.

### Still open, for the human (no further round)

- **Minors, gate-3 debt:**
  - CR-4, CR-5 and CR-6 (round 1);
  - DR-1 (this read);
  - QA's BD-1, BD-3 and FD-2;
  - security S-1..S-4.
- **Carried from round 1's "For the human":**
  - plan-backend D-3, `FOR NO KEY UPDATE` where ADR-0028 rule 8 reads `FOR UPDATE`. **Recommended: accept.**
  - plan-frontend § 6's two deviations. **Recommended: accept**, and make the `ConfirmDialog` aria-disabled state a kit task.

## Delta read 2 (gate-3 change: server member search)

The second and last delta read of the L route, in a clean context. Checkout confirmed: branch `feat/028-org-level-teams`, HEAD
`185c8b49`, toplevel the feat-028 worktree. It covers `git diff 9b97df16..HEAD` only:
- ad241e04 (backend), 63c61a68 (contract and plan prose), 185c8b49 (frontend);
- 27 files, +902/−102.

It is read against:
- api-contract § 0.1, § 0.5, § 2.3 and § 2.3a;
- mod-016 § 0.5, § 1.3 and § 2.2;
- design § 5.1 and plan-frontend § 6.

**Verdict: approve.** The route and the hook do what § 2.3a and design § 5.1 say. The read adds two minors, which are gate-3
debt. Nothing goes back to the developer.

### Checked

- **Search rules (mod-016 § 0.5, § 1.3, § 2.2).**
  - There is one definition, `TeamMemberCandidateSearch.cs:27-28`: the member ids, then
    `UserDirectoryBatches.SearchAmongAsync` in batches of 500 ids (`UserDirectoryBatches.cs:65-70`).
  - Both handlers run it: `SearchTeamMemberCandidatesHandler.cs:47` and `SearchNewTeamMemberCandidatesHandler.cs:48`.
  - The text rules come from mod-014 unchanged: trimming, the two-character floor, the cut at 20 with `moreMatched`, matching
    on name or e-mail with letter case not deciding, and the 400 over 100 characters.
  - Endpoint tests pin them at :45, :73, :93, :108 and :129. Handler tests pin them at :37–:111, including 501 members with
    every batch at most 500.
- **Routing.**
  - The literal `…/teams/member-candidates` (`MembershipEndpoints.cs:65`) outranks `GET` and `DELETE …/teams/{teamId}`. No
    other team route has as few segments, so nothing else can collide with it.
  - `Route_MemberCandidates_NeverBindsAsATeamId_…` (:211) pins every case:
    - the search, also spelled in upper case;
    - the team read by an id in either letter case;
    - `…/member-candidates/members` and `…/member-candidates/member-candidates` answer `team.not_found`;
    - `DELETE` answers `team.not_found`, not 405.
- **Codes and containment.**
  - The route declares `team.create` (:66).
  - The handler asks `team.write` first of all, before it reads the address (handler :36). Handler test :125 pins this with
    a malformed id. The order matches § 0.5.
  - The inventory row (`OrganizationAddressedRoutes.cs:72`) puts the route into the every-route theories:
    - per role (`OrganizationAccessInventoryTests.cs:270`) and for a non-member (:304);
    - a deactivated organization (`ScopedAccessTests.cs:332`);
    - a malformed or never-existing organization (`ScopedAccessTests.cs:112`);
    - no token.
  - The endpoint tests add two cases: an administrator of another organization (:162), and an administrator of two
    organizations, who is offered only the members of the one addressed (:185).
- **Statement count.**
  - 2 for 2 members and for 200, all of them matching (`TeamStatementCountTests.cs:165`; `NewTeamMemberCandidatesStatements = 2`
    at :50).
  - The test calls the handler directly, so the scoped check is outside the count, as on every other row.
- **Inventory and OpenAPI.**
  - Routes 55 → 56 (`OrganizationAccessInventoryTests.cs:237`), OpenAPI operations 44 → 45 (`OrganizationsOpenApiTests.cs:48`).
  - `ContractOperations` is derived from `OrganizationAddressedRoutes.All`, so the inventory row and the operation cannot drift
    apart.
- **Hook, states and texts (design § 5.1).**
  - The chain: `CreateTeamDialog.tsx:71` → `useUserSearch({ kind: 'newTeam' })` (`useUserSearch.ts:44-50`) →
    `searchNewTeamMemberCandidates` (`membershipApi.ts:121-127`).
  - The states and texts are the team page's search's:
    - under two characters, «Введите не менее 2 символов…» and no request;
    - «Поиск…»;
    - «Пользователи не найдены»;
    - 20 users and then «Показаны первые 20. Уточните запрос.»;
    - «Не удалось выполнить поиск…», retried on reopening the list (`onOpen={retry}`, :183).
  - `OrganizationTeamsPage.members.test.tsx` :43–:150 pins each state. It also pins that `GET …/members` is never read (:43),
    and that members chosen in two searches all go into `memberIds` (:150).
  - The hint «Среди активных участников организации.» is unchanged (`teamTexts.ts:53`).
- **Dead code.** `useMemberSearch.ts` is deleted, and a grep finds no reference left under `src/`. `useMembers` stays, because
  `MembersPage.tsx` uses it.
- **MSW double.**
  - The handler is at `testMembers.ts:334`. The guard row is at `testMembership.ts:61`, before `/teams/${ID}`, because `find()`
    takes the first match.
  - Both are pinned at `testMembership.test.ts:153-155`.

### Runs

- `dotnet test`:
  - `Mustask.Application.Tests` `~SearchTeamMemberCandidates`: 19 passed;
  - `Mustask.Architecture.Tests` `~VerticalSliceRules`: 2 passed;
  - `Mustask.Infrastructure.Tests` `~TeamStatementCountTests.NewTeamMemberCandidates`: 1 passed;
  - `Mustask.Api.IntegrationTests`, 506 passed: `SearchNewTeamMemberCandidatesEndpointTests`,
    `Organizations.SearchTeamMemberCandidatesEndpointTests`, `OrganizationAccessInventoryTests`, `OrganizationsOpenApiTests`
    and `ScopedAccessTests`;
  - `EndpointCategoryInventoryTests` and `OrganizationContainmentTests`: 58 passed.
- `npx vitest run` on `OrganizationTeamsPage.members.test.tsx`, `OrganizationTeamsPage.test.tsx`, `UserSearchField.test.tsx`
  and `testMembership.test.ts`: 62 passed.
- On the 10 changed frontend files, `eslint` and `prettier --check` are clean. `tsc -p tsconfig.app.json --noEmit` is clean.
- No `verify.sh`: that run is the orchestrator's.

### New

- **DR2-1 · minor · backend (architecture, backend.md:50).**
  - The convention is "one use case = one folder = one endpoint". `Features/Organizations/SearchTeamMemberCandidates/` now
    holds two handlers for two routes. No other slice folder in Application does.
  - Plan § 12 A-8 records this, with the reason "a slice may not use another slice". The convention's answer to that problem
    is Abstractions, not a shared folder; `UserDirectoryBatches` exists for exactly this case.
  - Nothing breaks. `VerticalSliceRules` passes because it sees the folder as one slice.
  - **Fix:**
    - move `SearchNewTeamMemberCandidates{Handler,Query}` to their own folder;
    - move `TeamMemberCandidateSearch` to `Abstractions/Organizations`, beside `IMemberReadModel`, returning `UserSearchResult`;
    - give each slice its own response record of the same shape;
    - update A-8's sentence.
- **DR2-2 · minor · frontend (tests).**
  - Two tests duplicate each other: `OrganizationTeamsPage.test.tsx:226` ("offers only active members…: not an inactive one,
    not a user of no organization") and the new `OrganizationTeamsPage.members.test.tsx:85`. Both type the same «Ол» and «Мар»
    into the same field and assert the same lines.
  - Since this delta, neither tests the panel's own filtering, because that filtering is gone. Both test the MSW double's
    `candidatesFor` (`testMembers.ts:98-121`). The server rule itself is pinned by the endpoint test at :45.
  - **Fix:** delete the older test and leave a retire comment that points to the new file. Or drop "offers only active
    members" from both titles.

### Behavioural claims (delta)

Direction 1:
- api-contract § 2.3a, "Query and body" and "Rules" → `TeamMemberCandidateSearch.cs:27-31`; endpoint tests :45, :73, :93,
  :108, :129. ✓
- § 2.3a, "Who is offered" → endpoint test :45 covers all four excluded kinds: removed, disabled, a member of nothing, a member
  of another organization only. ✓
  - "The creation still judges every `memberIds` entry" → the create handler is unchanged; round 1 anchored its refusal
    tests. ✓
- § 2.3a, "Statements: two … one more per further 500" → `TeamStatementCountTests.cs:165`, `UserDirectoryBatches.cs:65-68`,
  handler test :87. ✓
- § 2.3a, "Codes … first of all … a role without either learns nothing" → handler :36; handler test :125; endpoint test
  :145. ✓
- § 2.3a, "Containment" → endpoint test :162 covers a non-member, an instance administrator, a member of another organization
  only and an unknown organization; `ScopedAccessTests.cs:112`, :134 and :332 cover malformed and deactivated, over all
  routes. ✓
- § 2.3a, "Routing", every sentence → endpoint test :211. ✓
- § 2.3a, "Errors, in order" → handler :36, then :42, then :48 (the 400 comes from the directory). ✓
- The § 0.1 table row, the § 4 row and the count "47 → 56 … 17 in … OpenAPI 45" → `OrganizationAddressedRoutes.cs:72`,
  inventory test :237, OpenAPI test :48. ✓
- plan-backend § 12 A-8, § 12.2 ("member search 2"), § 12.3 and § 12.4 → the anchors above and `CreateTeamDialog.tsx:71`. ✓
  A-8's slice rationale is DR2-1.
- plan-frontend § 4 and § 6 bullet 1 → `useUserSearch.ts:52-69`, `OrganizationTeamsPage.members.test.tsx` :43–:150, and the
  `members` field at `CreateTeamDialog.tsx:110`. ✓ The bullet's claims: the states, the texts, the retry, `useMemberSearch`
  deleted, the 409 on «Участники», both codes held by «Администратор» alone.
- status.md Log lines 72–73 → the three commits. ✓ Line 72's "still reads the local filter" is history, and line 73 closes it.
- Code comments, each against the code it sits on: ✓
  - `MembershipEndpoints.cs:62-64`;
  - the handler's remarks :16-27 ("only «Администратор» holds both");
  - the `TeamMemberCandidateSearch` summary;
  - `useUserSearch.ts:9-24` and `CreateTeamDialog.tsx:33-42`;
  - `testMembership.ts:60`;
  - `OrganizationAddressedRoutes.cs:27-28` and :70-71.

Direction 2:
- **Behaviour changed:**
  - the new route `…/teams/member-candidates`;
  - the route count;
  - the creation form's search, which moved from a local filter of `GET …/members` to the server.
- **Grepped** across the repository, without `node_modules`, `bin`, `obj` and `dist`: `member-candidates`, `useMemberSearch`,
  "filtered locally", "local filter", "55", "sixteen", "16 new", "47 → 55".
- **Hits:**
  - plan-backend.md :542, :546 and :557 ("55 rows", "16 new routes"). These are the § 9 test plan of stage 4a, which § 12 A-8
    supersedes. Nothing an agent would do differs, because the tests say 56.
  - qa.md:37, review-security.md:101 and :126 ("55"). These record their own sessions and are history.
  - review-code.md:119 is round 1's question, now answered.
  - design.md:134 is now true. design.md:379 is about the team-address routes and makes no claim about the creation search.
  - `RetiredTeamRoutesEndpointTests.cs:27` and :101 test the project-nested path and are unaffected.
  - The e2e files `27-organization-members.spec.ts`, `membersApi.ts` and `membersUi.ts`. E2E is paused (plan-backend § 11),
    and they were not read for edits.
  - mod-016's contract needs no change: feat-028 § 2.3a adds a route and supersedes nothing.
- Nothing was left unswept.

### For the human

- **The security pass (review-security.md, 925e0517) ran before this route existed.** This read covered its checklist for the
  one new route:
  - authorization: both codes, and the per-role theory;
  - IDOR and containment: the theories over all 56 routes, and the two cross-organization tests;
  - personal data: e-mail goes to «Администратор» only, as in § 2.3's search;
  - injection: the text goes through mod-014's parameterized search;
  - secrets: none.

  **Recommended: no separate security delta.**
- **Gate-3 debt now:**
  - CR-4..CR-6 (round 1), DR-1 (delta read 1), DR2-1 and DR2-2 (this read);
  - QA's BD-1, BD-3 and FD-2;
  - security S-1..S-4.

## Delta read 3 — the CI race fix

Ordered by the orchestrator after PR #17's CI went red on the CR-4 / S-1 race, so it is past the L cap of two delta reads.
Clean context; checkout confirmed: branch `feat/028-org-level-teams`, HEAD `d2b0a3e1`, toplevel the feat-028 worktree. It
covers `git diff 5abfeef0..HEAD` only: 6c9720eb (code and tests), b814b5f0 (plan A-9) and d2b0a3e1 (status Log), 20 files,
+771/−143. It is read against CR-4, review-security S-1, ADR-0028 rule 8, and plan-backend § 6, § 7.2, § 7.4, § 12.1 and § 12.2.

**Verdict: approve.** CR-4 and S-1 are closed. The four new findings, DR3-1..DR3-4, are all minor. They are gate-3 debt and
are owed no round.

### Resolution

- **CR-4: closed** (6c9720eb).
  - `LockProjectLinksAsync` takes the held teams back from its own lock (`WorkItemRepository.cs:58`, `ProjectTeamLocks.cs:47-62`).
  - It resolves the default over those teams only (`:63`; `DefaultTeams.cs:23`, `:38`, `:45`, `:51`).
  - So a resolved owner's link is held `FOR SHARE`, and an unlink or a deletion of it waits for the creation. Both of
    CR-4's cases, the insert's `23503` and the final `DELETE`'s failure, are gone.
  - CR-4's own fix was not built, and rightly so (A-9). It would lock one more link after the others, out of team-id order,
    and that forms a cycle with an unlink of the pair.
- **S-1: closed** (6c9720eb). Every row of its table now judges "linked" by the rows its own lock statement returned:
  - item create: `CreateWorkItemHandler.cs:150` → `:237`;
  - item save: `SaveWorkItemHandler.cs:113` → `:279`;
  - default naming: `SetDefaultTeamHandler.cs:68` → `:73`, now one statement (`ProjectTeamRepository.cs:32-35`);
  - the creation that may default: as in CR-4.
  - The unlink's own pair is outside the table, and it is the path CI hit: `UnlinkTeamHandler.cs:124`, `:147`, `:150`.
  - No writer reads `ProjectTeamInfo.IsLinked` any more. Only `GetBacklog`, `GetBoard` and the reorder's address check read
    it, and none of them writes a key onto a link.
- review-security.md S-1 and qa.md A-5 still read as open. They are their authors' records, left to the orchestrator.

### What holds (lock reasoning, READ COMMITTED)

- **A lock statement returns exactly the rows it holds.**
  - It takes the rows of its own snapshot, waits behind an incompatible holder, and then re-checks the committed version
    (EvalPlanQual): it locks the row if it still matches and skips it if it was deleted.
  - A row inserted after its snapshot is neither returned nor locked. So "held" equals "returned".
  - The command log shows each lock sent as one bare statement (DR3-1).
- **Modes.**
  - A binder's `FOR SHARE` conflicts with the unlink's and the deletion's `NO KEY UPDATE`. The owner and default keys take
    `FOR KEY SHARE`, which `NO KEY UPDATE` admits and the final `DELETE` does not.
  - So the `DELETE` can fail only on a key share taken with no `FOR SHARE` held. After A-9 there are three such takers,
    and none of them can make it fail:
    1. **A binder** holds its link `FOR SHARE`. The unlink waits for it, and its `Reassign` and `ClearDefault` are new
       statements that see what the binder wrote (`WorkItemTeamOwnership.cs:63-69`, `ProjectRepository.cs:52-53`).
    2. **The unlink's destination** is held `NO KEY UPDATE` by the unlink itself.
    3. **A child takes its parent's team** (`CreateWorkItemHandler.cs:194-197`) and may hold no link at all. Its parent is
       held `NO KEY UPDATE` (`WorkItemRepository.cs:185-191`), so the unlink's or the deletion's `LockOwned … FOR UPDATE`
       (`WorkItemTeamOwnership.cs:46-48`) waits for the creation, and the `Reassign` after it sees the child. If the
       unlink took the parent first, the child reads the parent's new team instead.
  - A link that the unlink's own lock did not return gives 204 or 409, and no `DELETE` runs.
- **The deletion.**
  - Every creator of a link holds the team row `FOR SHARE`: `LinkProjectsToTeamHandler.cs:56`,
    `LinkTeamsToProjectHandler.cs:86` and `CreateProjectHandler.cs:111`. AddTeam's team is new.
  - The deletion takes the team row `NO KEY UPDATE` first (`DeleteTeamHandler.cs:67`). So its lock-then-read of the links
    (`ProjectTeamRepository.cs:37-56`) cannot see a link it does not hold.
- **No new cycle.** No lock is added and none moves. The three statements take the same rows, in the same modes and the
  same order (`ProjectTeamLocks.cs:56`, `:77`). The naming loses a read, not a lock, and `ResolveAsync` takes no lock.
- **Organization.** All three statements carry it (`ProjectTeamLocks.cs:34`, `:55`, `:76`). `ResolveAsync` is LINQ, run
  over ids that came from a statement scoped to the organization.
- **Pins.** No constant moved: unlink and deletion 11 (`TeamStatementCountTests.cs:52-53`), child creation 21
  (`OwningTeamStatementCountTests.cs:60`). § 12.2's "one statement fewer" is `DefaultTeams.cs:23`.
- **Contract and tests.**
  - `api-contract.md` is unchanged.
  - The race test's assertions and its 6 rounds are untouched. Its comment now matches the body ("for the team by name",
    not "as children").
  - Each `LinkJudgementWindowTests` case fails if its handler goes back to the fresh read's `IsLinked`.
  - On the pre-fix lock, which was a non-query, the interceptor would never fire, so these tests cannot replay the old
    defect. The hand check at :38-40 is the only evidence from before the fix. It is disclosed, so it is not a finding.

### Runs

- `dotnet build Mustask.sln`: 0 errors.
- `Mustask.Architecture.Tests`: 83 passed.
- `Mustask.Infrastructure.Tests`, filtered to `StatementCount`, `LinkJudgementWindowTests`, `RepositoriesTests` and the two
  `…AtomicityTests`: 58 passed.
- `TeamQaEndpointTests.TheFirstTeamOfATeamlessProject_…`: **25 consecutive runs, 25 passed, 0 failures** (6 rounds of 12 requests each).
- `Mustask.Api.IntegrationTests` `Features.Organizations.` + `Features.WorkItems.`: 1494 passed, 0 failed (4 m 19 s). This includes bug-005's
  `UnlinkRacesEndpointTests.Create_VersusTheDeletionOfTheNamedDefault_AndASecondDeletionOrAnUnlink_NoDeadlock` and the
  C-1..C-18 races (`UnlinkRaces`, `LinkRaces`, `DeleteOwnedItems`).
- `OwningTeamQaEndpointTests` once with EF command logging on (`Serilog__MinimumLevel__Override__Microsoft.EntityFrameworkCore`),
  to read the lock SQL: passed.
- Not run: `verify.sh` (the orchestrator's) and E2E (the standing NO).

### New

- **DR3-1 · minor · backend (prose)** — `ProjectTeamLocks.cs:15-17`.
  - The remark says EF composes the statement as `SELECT … FROM (<statement>) AS t`, "the same shape `ReadEntriesAsync`
    uses".
  - The command log shows it sent exactly as written, with no wrapper, and the developer's report says the same. EF wraps
    raw SQL only when a LINQ operator is composed on it.
  - The conclusion still holds: `ORDER BY` comes before the locks, and every row is locked.
  - **Fix:** say it is sent as written, and that composing a LINQ operator on it would wrap it in a subquery.
- **DR3-2 · minor · backend (prose)** — "as if the creation had run a moment before the link": `CreateWorkItemHandler.cs:51-55`,
  `DefaultTeams.cs:15-18` and `IWorkItemRepository.cs:37-38`. Plan § 7.4:321 says the same ("each holds the row `SHARE` …
  That is the whole claim"). Two cases break the sentence:
  - (i) A child takes its parent's team, whether or not its link is held. It is safe through the parent's lock, which § 7.4
    itself says one bullet later (:330).
  - (ii) `ResolveAsync` reads the setting afresh (`DefaultTeams.cs:38`). So a default naming of a team whose link committed
    after the lock, if it commits before the resolve, is seen while that link is not held.
    - The creation then falls back to the only held active team, or to none. Neither order of the requests gives that
      owner: run before the link, the creation would have taken the old setting.
    - There is no error and the owner is valid; it takes three concurrent requests.
  - `DefaultTeams.cs:9-10`, "cannot disagree", is the same kind of overstatement: the creation and the structure seam
    apply one rule to different sets of links.
  - **Fix:**
    - narrow the sentences to "an explicit team and the default's team", and name the child case and the setting case;
    - in § 7.4, say "under a held `SHARE` or under its parent's lock";
    - for `DefaultTeams.cs:9-10`, say "apply one rule".
- **DR3-3 · minor · backend** — the reorder still uses the S-1 pattern.
  - `BacklogRepository.cs:61-66` locks the link and then judges it by `AnyAsync`. Its comment at :58-60 ("either commits
    before the unlink … or reads the link gone") holds only for a link that existed at the lock.
  - This is harmless, as § 7.4:335-337 argues: a place keys the item, not the link, and the members' `FOR KEY SHARE`
    (:75-81) against `LockOwned`'s `FOR UPDATE` orders the reorder and the unlink either way. § 7.4 describes only one of
    those two orders.
  - **Fix:** fold this into CR-5, which is the same line. `ProjectTeamLocks.ShareAsync` returns the row and carries the
    organization, so it can replace the lock, and `AnyAsync` goes.
- **DR3-4 · minor · docs (direction 2)** — ADR-0028:144, rule 8.
  - Three phrases there can be read as a second statement: "re-reads what it checks after locking", the unlink case's
    "re-checking that the destination is still linked", and "re-reading under its lock". Read that way, they describe the
    pattern A-9 retires. For a row that can be inserted after the lock, that pattern reproduces S-1 whatever the lock mode.
  - The code and § 7.4 are right. The ADR is accepted, and the human's gate-3 answer already sends rule 8's clarification
    (D-3) to mod-018's amendment.
  - **Fix:** carry A-9's corollary into that amendment: "a locked row's presence is judged by the lock statement's own
    answer". A note on mod-018's queue line, or one in status.md Decisions, would do it.

### Behavioural claims (delta)

Direction 1:
- `IProjectTeamRepository.cs:14-29` → `ProjectTeamLocks.cs:26-83`, `ProjectTeamRepository.cs:16-35`. ✓
- `IWorkItemRepository.cs:20-31` (a foreign team answers false) → `ProjectTeamLocks.cs:34`. ✓
- `IWorkItemRepository.cs:33-43` → `WorkItemRepository.cs:58-63`, `DefaultTeams.cs:23-51`. ✓ "Ordered before it" → DR3-2.
- `UnlinkTeamHandler.cs:34-41` (204 or 409 for a pair it does not hold) → :124, :147, :150-161; window tests :150, :186. ✓
- `SetDefaultTeamHandler.cs:26-28`, :57-58 → :68-73; window test :129; handler test `Handle_TeamLinkedAfterTheLock_…`. ✓
- `CreateWorkItemHandler.cs:45-55` → :150, :155, :237; window tests :54, :79. ✓ "As if … before the link" → DR3-2.
- `SaveWorkItemHandler.cs:40-42`, :251-256 → :113, :279; window test :105. ✓
- `ProjectTeams.cs:8-10`, :56-59, and the comments in `WorkItemRepository.cs:46-49`, :54-62 and `ProjectTeamRepository.cs:26-34`
  → the anchors above. ✓
- `ProjectTeamLocks.cs:5-11`: "returns exactly what it locked", "carries the organization" → :34, :55, :76 ✓; the `23001`
  is the developer's trace (history). Remarks :15-17 → DR3-1.
- plan § 6 and § 7.2 (Unlink, SetDefaultTeam, Save/Create) → the handler anchors. ✓
- plan § 7.4, :317-340:
  - the `DELETE` bullet → DR3-2;
  - the child → the CR-4 chain above ✓;
  - the deletion → the four anchors in "What holds" ✓;
  - the reorder → DR3-3;
  - "no race reaches `23001`" → "What holds" and the 25 runs ✓.
- plan § 12.1, the A-3 note and A-9 → the code ✓. A-9's run counts are the developer's and were not re-run.
- plan § 12.2, the A-9 paragraph → the pins above, and `RepositoriesTests.LinkLocks_…` (3 statements). ✓
- `TeamQaEndpointTests.cs:100-108` → its body ✓. The run figures there are history.
- `LinkJudgementWindowTests.cs:24-41` → `AfterLock` :366-383 and the cases. ✓
- status.md, the fix's Log line → the commits and the runs. ✓ It is over the 300-character limit, which verify.sh notes.

Direction 2:
- **Behaviour changed:**
  - "linked" is judged by the lock's answer, by the binders and by the unlink;
  - the default is resolved over the held links;
  - a pair linked after the unlink's lock gives 204, and such a destination gives 409.
- **Grepped:** `LockLinksAsync`, `LockLinkForShareAsync`, `LockTeamLinkAsync`, `LockProjectLinksAsync`, `ResolveAsync`, `IsLinked`,
  "fresh read", "re-read", `23001`, `23503`. Searched under `docs/` (the ADRs, the conventions, error-codes.md, this feature's
  folder and mod-017's), under `.claude/`, in the `AGENTS.md` files, and in backend code outside the diff.
- **Hits:**
  - ADR-0028:144 → DR3-4. ADR-0028:108 (`23503` maps to not-found) covers the insert side and still holds.
  - `BacklogRepository.cs:58-66` → DR3-3.
  - The Boards handlers' `IsLinked` checks are address checks and write nothing.
  - api-contract.md:572 (`ProjectTeamInfo.IsLinked`) is still true: the structure still answers it.
  - CR-4, the S-1 table and qa.md A-5 are records of their own sessions, closed here. mod-017's reviews are history.
  - The `.claude/`, conventions and other-ADR hits concern files and caches, not locks.
- Nothing was left unswept.

### Still open, for the human (no further round)

- **Gate-3 debt:**
  - CR-5 and CR-6;
  - DR-1, DR2-1 and DR2-2;
  - DR3-1..DR3-4, where DR3-3 folds into CR-5;
  - QA's BD-1, BD-3 and FD-2;
  - security S-2..S-4.
- **DR3-4 is for mod-018's amendment**, together with D-3.
