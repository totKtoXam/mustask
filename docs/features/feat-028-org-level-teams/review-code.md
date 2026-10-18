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
