# QA — Organization-level teams (feat-028)

One pass over both layers (`stages/05-frontend.md` § 7), clean context, light route. The plan below was written from `spec.md`, the eight stories, `design.md`
and `api-contract.md` **before** any implementation file was read; the code and its tests were read afterwards and the gaps closed. **E2E: not asked, not
written, not run** (standing NO since 2026-10-12, `status.md` § Decisions); the specs this line breaks are in `plan-backend.md` § 11 and the MR names them.

✓ covered by the developer's tests (read, and green below) · ✓+ covered, and a QA test added · ✗ defect. `…E.T.` = `…EndpointTests` in
`Mustask.Api.IntegrationTests/Features/Organizations`; OMT/OSC/OST/TSC/UTA = `OrganizationLevelTeamsMigration`/`…Schema`/`OrganizationStructure`/`TeamStatementCount`/`UnlinkTeamAtomicity` tests.

## Backend

### Coverage matrix

| # | Criterion | Check | St. |
|---|---|---|---|
| B-1 | US-1 list and read, every role; non-member (instance admin too) 404 | `GetOrganizationTeamsE.T.` (10), `GetTeamE.T.`, `GetTeamsE.T.` | ✓ |
| B-2 | US-1 create with a name only makes no project, link or membership; Max, Vera 403 | `AddTeamE.T.` `Add_NameOnly_…`, `…_Return403_CreateNothing` | ✓ |
| B-3 | US-1 description, members, projects in one operation; link records caller and time; only team = default | `AddTeamE.T.` `…_AreOneOperation_…`, `Add_OnlyLinkedTeam_…` | ✓ |
| B-4 | US-1 a refused choice leaves no team and the name free | `AddTeamE.T.` `Add_Refusals_CreateNothing_NameStaysFree`, `…Over2000_…` | ✓ |
| B-5 | BR-OL-3 name unique in the organization: case, trim, deleted name free, other organization free, 0/101 | `AddTeamE.T.` (14), `RenameTeamE.T.` (14), OSC; **QA** `TeamName_IsComparedIgnoringCyrillicLetterCase_…` | ✓+ |
| B-6 | US-1 two creations / a creation and a rename / two renames of one name: one 409, no 5xx | `Add_TwoConcurrentSameName_OneTeam`; **QA** `Rename_VersusCreation…` (both orders), `Rename_TwoTeamsToTheSameName_…` | ✓+ |
| B-7 | US-1 rename; description as typed, cleared, 2 001; 403 | `RenameTeamE.T.`, `SetTeamDescriptionE.T.`; **QA** `Description_OfSpacesOnly_…` | ✓+ |
| B-8 | US-1 members at the team's address; removal from the organization empties that organization's teams only | the four member E.T., `RemoveMemberE.T.` `…_EndsTheUsersTeamMemberships_…`, `TeamAcrossProjectsE.T.` | ✓ |
| B-9 | US-1 page independent of projects; BR-OL-12 the eight project-nested routes are gone | `GetTeamE.T.`, `LinkProjectsToTeamE.T.`, `RetiredTeamRoutesE.T.` | ✓ |
| B-10 | US-2 a project is created with no team or membership; description; `teamIds`; refusals leave nothing; names/keys as mod-002 | `CreateProjectE.T.` (25) | ✓ |
| B-11 | US-2 a project without a team works: «Без команды», backlog/board read, a member reorders, a viewer cannot | `DeleteOwnedItemsE.T.` `…_TheProjectsLastTeam_IsAllowed_ProjectWorksOn`; **QA** `ATeamlessProject_Works_…` | ✓+ |
| B-12 | US-2 project description; survives deactivation; a rename keeps description and links | `SetProjectDescriptionE.T.` (7), `RenameProjectE.T.` | ✓ |
| B-13 | US-3 link from either end: once, caller and time, several, all or nothing, same pair keeps the first row; races; deactivated ends | `LinkTeamsToProjectE.T.` (10), `LinkProjectsToTeamE.T.` (11), `LinkRacesE.T.` (3) | ✓ |
| B-14 | US-3/BR-OL-6 an owning or default team must be linked: 409 not linked, 404 deleted/foreign/unknown alike; the mark follows the links | `SaveOwningTeamE.T.`, `CreateOwningTeamE.T.`, `SetDefaultTeamE.T.`, `GetTeamsE.T.` | ✓ |
| B-15 | US-4 unlink count; unlink to a team: owner, places, project place, «Изменён», other projects and teams untouched, default cleared; to nobody; the only team; owns none | `GetUnlinkCountE.T.` (7), `UnlinkTeamE.T.` (19); **QA** `Unlink_ReassignsItemsOfEveryStateAndLevel_…` (Removed, features) | ✓+ |
| B-16 | US-4 destination refusals, no destination, unlinked pair, deactivated project, roles, wrong JSON | `UnlinkTeamE.T.` | ✓ |
| B-17 | US-4 races (save, create, child, reorder, default; mutual unlinks; against deletions); 5 000 items in 5 s; all or nothing | `UnlinkRacesE.T.` (7), `UnlinkTeamVolumeTests`, UTA | ✓ |
| B-18 | US-5 counts per linked project; deletion across projects, defaults, links, memberships, name; last team; two deletions; races | `GetTeamDeletionCountsE.T.` (6), `DeleteOwnedItemsE.T.` (14), `DeleteTeamE.T.` (9), `DeleteTeamVolumeTests` | ✓ |
| B-19 | US-6 rename everywhere; `isMember` per project; views and reorder per project; default per project; unlinked address 404; filter by an unlinked team | `TeamAcrossProjectsE.T.` (9), `OwningTeamQaE.T.` `Filter_MixedValues_…` | ✓ |
| B-20 | US-7 the upgrade (OQ-1 C): every team, membership, place, order, default dropped; owners NULL, «Изменён» unmoved; nothing else; counts logged; empty databases; down and up again | OMT (9) | ✓ |
| B-21 | US-8 / S-2 the structure contract reads inside the organization; ten foreign-id rows answer as an unknown id | OST (10), `OrganizationContainmentTests` | ✓ |
| B-22 | US-8 every step by role; answer order; 55 routes each with a code; the four new codes for «Администратор» alone | `TeamPermissionMatrixE.T.`, `OrganizationAccessInventoryTests` (15), bundle tests; **QA** `Access_ListsTheFourDeliveryCodes_…` | ✓+ |
| B-23 | Schema keys, statement counts (200 teams, 50 projects), error catalog | OSC (6), TSC (10), `OrganizationErrorCatalogTests` | ✓ |

### Additional scenarios

| # | Scenario | Expectation | St. |
|---|---|---|---|
| A-1 | a list field of the wrong JSON type (`[1]`, `"x"`, `{}`, `[true]`) on the four list routes | 400 `http.bad_request`, nothing changes | ✓+ **QA** (6) |
| A-2 | description of spaces only; 1 000 vs 1 001 supplementary characters | kept as typed; 200 / 400 | ✓+ **QA** |
| A-3 | a NUL character in a description or a name | 400 | ✗ **BD-1** |
| A-4 | 24 writes naming a team (unlink both ways and to nobody, link, deletion, save, create, child, default) at once over one project, no gates | no 5xx | ✓+ **QA** `MixedTeamWrites_…`: 6 rounds committed; 40 rounds (960 requests) once, none |
| A-5 | first team of a team-less project linked and unlinked while items are created (CR-4, S-1) | no 5xx | not reproduced: 80 rounds (960 requests) once, 6 committed; the window needs a gate no test has |
| A-6 | malformed, upper-case, all-zero ids; the old `{ "name" }` body on the link route | as an id that names nothing; 204, nothing created | ✓ |
| A-7 | migrations on a clean database, rolled back and applied again | same result | ✓ every Testcontainers run, OMT `Rollback_…` |

### Runs

| Date | What was run | Result |
|---|---|---|
| 2026-10-18 | `BASE=92942e14 bash scripts/verify.sh`, once, background (QA tests of this pass in the tree) | `RESULT: green`, EXIT 0. Build ok; `Api.IntegrationTests` 2 607 passed, 17 skipped (pre-existing live-stand cases; none added), 0 failed, 9 m 37 s; `Infrastructure.Tests` 368, `Application.Tests` 739, `Domain.Tests` 690, `Contracts.Tests` 246, `Architecture.Tests` 83, `Shared.Tests` 53 |
| 2026-10-18 | `dotnet test … --filter "FullyQualifiedName~Organizations.TeamQaEndpointTests"` | 18 passed (the tests added after verify started included) |
| 2026-10-18 | hypothesis runs of A-4 (40 rounds) and A-5 (80 rounds), raised and put back | no 5xx |

### Defects

#### BD-1: a NUL character in any text field answers 500
- Severity: minor · Layer: backend · Status: open
- Steps: `PUT /api/organizations/{o}/teams/{t}/description` with `{"description":"до\u0000после"}`; the same on `PUT …/projects/{p}/description`, on `POST …/teams` with that `description`, on `PUT …/teams/{t}/name` and on `PUT …/projects/{p}/name`.
- Expected: 400 `organizations.validation_failed`. Actual: 500 `http.internal_error` (PostgreSQL `text` cannot hold U+0000). The names fail the same way on main (pre-existing); the two new descriptions inherit it. Nothing is stored. A probe test was written and not committed (it fails): the fix adds it.
- Fix: refuse U+0000 in `EntityName`/`EntityDescription` with the existing length reasons, or in one shared text rule. Debt for gate-3, a queue line if wanted.

#### BD-2: two tests the spec retires by name survive (same as review CR-1)
- Severity: major · Layer: backend tests (doc-012 P-1) · Status: open
- Steps: `GetTeamsE.T.` `Get_NewProject_ListsItsOnlyTeamAlone_MarkedOnlyTeam` (:243) and `DeleteOwnedItemsE.T.` `Delete_TheTeamCreatedWithTheProject_IsAllowedWhenAnotherRemains` (:162) are in spec § Existing rules, rows BR-M1-9 and BR-OT-11, as retired; both still exist and assert a project is created with a team.
- Actual: they pass only because `OrganizationsTestData.CreateProjectAsync` seeds a linked team; they test the fixture. Fix: delete with a comment, or rewrite under names that say what they check.

#### BD-3: the contract and a doc comment say what the code does not
- Severity: minor · Layer: docs · Status: open
- `api-contract.md` § 5 says `Down` "empties the teams again … recreates no dropped row"; it also inserts one team per project (plan A-1; the contract was left unchanged). `EntityDescription` and § 0.2 call the 2 000 limit "UTF-16 code units, as the database's `char_length` counts": `char_length` counts code points, so the database check is looser than the domain's (harmless; the domain refuses first).

## Frontend

### Coverage matrix

| # | Screen / state (design) | Check | St. |
|---|---|---|---|
| F-1 | § 3 organization «Команды»: skeleton, error + «Повторить», empty (holder / others), projects, description, row menu by code | `OrganizationTeamsPage.test.tsx` (31 tests, F-1..F-4) | ✓ |
| F-2 | § 5.1 «Новая команда»: fields, `Проекты` only with both link codes, validation before sending, refusals on their fields, one request per press, Escape order | same | ✓ |
| F-3 | § 5.2 rename and description dialogs | same, `ProjectGeneralPage.test.tsx` | ✓ |
| F-4 | § 5.3 deletion: counting, failed count, per-project list with zeros and «деактивирован», no project, last team, 404/403, cancel paths, focus | same | ✓ |
| F-5 | § 4 team page: commands by code, facts, line breaks, projects section (empty, failed), members, not found | `TeamPage.test.tsx` (22) | ✓ |
| F-6 | § 6.1 link dialog, both ends: options, none / all linked / loading / failed, notices, gone end, 403 | `TeamPage.test.tsx`, `TeamsPage.test.tsx` | ✓ |
| F-7 | § 6.2 unlink dialog, both ends: count, nothing preselected, destinations, owns none, skeleton, failed count, 409/404/400/403, fallback notice, cancel paths | `TeamsPage.test.tsx` (35), `TeamPage.test.tsx` | ✓, except the owns-none refusal **FD-1** |
| F-8 | § 7 project «Команды»: intro, unlink-only menu, marks, empty state, project views while no team, default dialog | `TeamsPage.test.tsx` | ✓ |
| F-9 | § 8 project «Общие» and «Новый проект» | `ProjectGeneralPage.test.tsx`, `ProjectListPage.create.test.tsx` | ✓ |
| F-10 | § 9 the project-nested team page is no route: «Страница не найдена», no redirect, no request | `paths.test.ts`; **QA** `routes.teams.test.tsx` (2) | ✓+ |
| F-11 | § 1 navigation entry by `team.read`; the resolver | `visibility.test.ts`, `switchTarget.test.ts`, `routes.organizations.test.tsx` | ✓ |
| F-12 | § 10 role matrix | `OrganizationsPages.roles.test.tsx`, `organizationRoles.ts` (four codes) | ✓ |
| F-13 | shell: switcher and pickers follow links; fallback notice | `useOrganizationsContextSource*.test.tsx`, `AppLayout.organizations.test.tsx` | ✓ |
| F-14 | § 2 `InlineMultiPickerField`, `Tag.removeLabel` | its test (22), `Tag.test.tsx`, showcase | ✓ |
| F-15 | API layer against the contract: paths, bodies (`destination` always sent), shapes, codes | `organizationsApi.test.ts`, `refusals.test.ts`; read beside the contract: no discrepancy | ✓ |
| F-16 | § 11 640 px, § 12 names and focus | names and focus asserted in page tests; **640 px not checked** (jsdom has no layout; no E2E) | ◐ |
| F-17 | design texts | scripted compare: 94 of 123 quoted strings verbatim in the sources; the other 29 are line-wrapped, templated or server texts, read by eye: no deviation | ✓ |

### Runs

| Date | What was run | Result |
|---|---|---|
| 2026-10-18 | `npx vitest run src/features/organizations src/shared/ui/fields src/shared/ui/marks src/app src/shared/api src/test src/features/workitems/lib` | 115 files, 2 523 passed, 6 skipped (conditional, pre-existing) |
| 2026-10-18 | verify.sh (above) | lint, `tsc -b` + build ok; affected 178 files, 2 737 passed, 5 skipped; source-reading guards 13 files, 590 passed |
| 2026-10-18 | `routes.teams.test.tsx`; a temporary probe of FD-1 (deleted) | 2 passed; the probe failed as FD-1 says |

### Defects

#### FD-1: the unlink dialog says nothing when a team that owns nothing meets a 404 (same as review CR-2)
- Severity: major · Layer: frontend · Status: open
- Steps: on a project's «Команды» (or a team's page) open «Отключить от проекта» for a team that owns no item there; make the server answer `POST …/unlink` 404 `organizations.team.not_found` (the team was deleted meanwhile); press «Отключить».
- Expected: the alert «Команда не найдена.»; on close the page reads itself again. Actual: nothing changes on screen, «Отключить» stays enabled, each press fails alike. `UnlinkDialog.tsx:90` passes `['destination']` always, the code lands on a field that is not drawn when `owned === 0`.
- Fix: `owned > 0 ? ['destination'] : []`, and a test for it (CR-2 has the wording).

#### FD-2: «Отключить» is natively disabled, where design § 6.2 says aria-disabled and focusable
- Severity: minor · Layer: frontend · Status: open (the code review put it to the human, accept now, kit task later)

### E2E

Skipped by the human's standing decision: no spec, no stand, no `stand.json`.

## Verdict

blockers: 0 · majors open: 2 (BD-2, FD-1; both already in the code review as CR-1, CR-2) · minors carried to gate-3: BD-1, BD-3, FD-2, and the reviews' CR-4..6, S-1..4 · result: **defects**. The re-check after the one fix round covers BD-2, FD-1 and what they touch.
