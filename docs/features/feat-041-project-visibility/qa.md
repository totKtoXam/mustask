# QA — feat-041-project-visibility

## Backend

Plan written from doc-016 spec BR-TD-12..-16 / OQ-5 A, US-6 (core), ADR-0040 § 4, ADR-0029 § 8 and api-contract.md before the implementation was read. Out of scope: lists, selectors, counts (feat-061/062).

### Coverage matrix

| Criterion | Check | Test | Status |
|---|---|---|---|
| US-6 sight through a linked team (BR-TD-12b, -13, -14) | project, team, department visible; others 404 | `ProjectSightEndpointTests.SightThroughALinkedTeamsMembership_…` | covered |
| Sight through a department grant, «Наследовать доступ» on/off | reach, then cut; team stays | `…SightThroughADepartmentGrant_…` | covered |
| Seeing a team does not disclose its projects | D hidden, JUPITER seen | same | covered |
| Project grant | B, linked team, department; write 403 | `…SightThroughAProjectGrant_…` | covered |
| Flag on/off, next request | project, team, department follow | `…TheFlag_…` | covered |
| Administrator sees everything | 200 on all | `…TheAdministrator_…` | covered |
| Role applies on what one sees (BR-TD-16) | codes on A; viewer 403 not 404 | `…SightThroughALinkedTeamsMembership_…`, QA `SightAddsNoCode_…` | covered |
| Hidden object does not exist; 404 before 403; team reorder → «Команда не найдена.» | path and query | `AHiddenProject_…`, `AHiddenTeamAndDepartment_…` | covered |
| Hidden = missing, no oracle | path (byte for byte), query | QA `APathIdThatIsHidden_…` (green), `AHiddenQueryId_…` (D-1, skipped) | D-1 |
| Sight changes on the next request | membership add | `ATeamMembershipChange_…` | covered |
| Linked-team member keeps sight with inheritance off; grantee loses it | QA | `ALinkedTeamMember_KeepsSight_…` | added |
| Team grant: team + department, not projects | QA | `ATeamGrant_…` | added |
| Team seen via visible project answers team routes | QA | `ATeamSeenThroughAVisibleProject_…` | added |
| `PUT /visibility` authorization: project/department «Менеджер», admin; reader 403; hidden 404; cut manager 404; bad body 400 | | `TheFlagIsWrittenUnderTheVisibilityCode_…`, QA `SightAddsNoCode_…` | covered |
| Assignee candidates and a new assignee limited to those who see the project; existing stays | | `TheAssigneeCandidates_…`, QA `Save_ANewAssignee…` | covered |
| Upgrade (OQ-5 A): additive, default false, per-project log on populated data, Down | | `ProjectVisibleToOrganizationMigrationTests` (4) | covered |
| Set / single / inverse parity | | `AccessSetSightTests`, parity test | covered |

### Additional scenarios

Not covered and left as notes: body ids (an owning team, link end) naming a hidden team or project are outside the gate (contract § 1 lists path and query ids only; ADR-0029 § 7 gives them to the operation); the sight set costs 4 extra statements on every request that names an object (accepted, deviation e).

### Runs

| Date | What was run | Result |
|---|---|---|
| 2026-11-16 | Api.IntegrationTests `ProjectSightEndpointTests` + `ProjectSightQaTests` | 17 pass, 1 fails (D-1, now skipped) |
| 2026-11-16 | Infrastructure.Tests migration/AccessSet/ScopedAccess filter | 4 pass |
| 2026-11-16 | Application.Tests `Sight` filter | 6 pass |
| 2026-11-16 | `ProjectSightQaTests` after the Skip | 7 pass, 1 skipped |
| 2026-11-16 | re-check of 339f7a04: Api.IntegrationTests Features.Organizations + Boards + AssigneeMembership (Skip removed, new probe `QueryIds_HiddenUnknownAndMalformedAreAlike_…`: hidden, unknown, malformed byte-identical 404; `none` and empty 200; administrator 200) | 2036 pass, 0 fail |

The developer's full-run results are in status.md and were not repeated. E2E: standing answer NO.

### Defects

#### D-1: a hidden query id is distinguishable from an id that names nothing

- Severity: major
- Steps: as a member who does not see JUPITER, «Финансы» or project C, call `GET /api/organizations/{org}/projects/{A}/workitems?teamId={JUPITER}` (also `departmentId={Финансы}`, and `GET /api/organizations/{org}/workitems?projectId={C}`), then the same with a random GUID.
- Expected: the same answer (spec: a hidden object answers «as for an address that names nothing»; US-6 "A hidden object does not exist"; ADR-0040 § 4: "alike").
- Actual: hidden → 404 `organizations.team.not_found` / `area.not_found` / `project.not_found`; random GUID → 200 with an empty page. A caller can test whether an id exists in the organization. This is the developer's deviation (a), and api-contract § 1 row 3 writes it in. Fix: a query id that is not a visible object of the organization (unknown, foreign or hidden; `none` excepted) answers the 404 of its kind. The skipped test `ProjectSightQaTests.AHiddenQueryId_IsIndistinguishableFromOneThatNamesNothing` checks it; remove its Skip with the fix.
- Status: fixed in 339f7a04, re-checked

#### D-2: a malformed query `projectId` on the assignee candidates offers every member

- Severity: minor
- Steps: `GET …/workitems/assignee-candidates?projectId=abc`.
- Expected / Actual: no leak (all active members are an organization-wide fact), but the filter silently drops; harmless, noted for the contract. Carried to gate-3 as a debt.
- Status: fixed in 339f7a04 (malformed projectId now 404), re-checked

### Verdict

blockers: 0 · majors: 0 · result: pass (re-check 1: D-1, D-2 fixed)

## Frontend

Plan written from doc-016 design § 6 / 6.1, spec BR-TD-12..-16 and US-6 («Видим всей организации», «A hidden object does not exist») before the implementation was read. Out of scope: list/selector/search narrowing (feat-061/062). E2E: not asked, skipped by the human's standing answer (NO).

### Coverage matrix

| Criterion / screen | Check | Test | Status |
|---|---|---|---|
| Control placement (design § 6.1: project «Параметры», `Section` «Видимость») | switch is in the settings section (`…/settings`), not elsewhere | `ProjectVisibility.test.tsx` 1 | covered; the task text says «Настройки», the panel and design say «Параметры» — consistent with design |
| Off by default, helper copy | switch off, «Проект и его команды увидят все участники организации.» | same | covered |
| Turn on asks | `ConfirmDialog` title/text/button «Сделать видимым» exactly as design; nothing sent before confirm; PUT `{visibleToOrganization:true}`; notice «Проект «D» виден всей организации.»; switch becomes on | same | covered |
| Turn off no confirmation | no dialog, PUT false, notice «Проект «D» виден только участвующим.» | `ProjectVisibility.test.tsx` 2 | covered |
| Refusal when turning on | server text in the dialog, switch stays off | `ProjectVisibility.test.tsx` 3 | covered |
| Refusal when turning off | error notice, no dialog, switch stays on, no success notice | `ProjectVisibility.qa.test.tsx` 3 | added |
| Permission gating: only a holder of `organizations.visibility.write` | section and switch absent when the code is missing (role otherwise full) | `ProjectVisibility.test.tsx` 4 | covered. Code read from the project's `codes` (`useObjectAccess`); settings route itself still gated by `project.write` (project/department «Менеджер» and administrator hold both) |
| Hidden project = missing project | `organizations.project.not_found` shows frame «Проект не найден», no «Недостаточно прав» | `ProjectVisibility.test.tsx` 5 | covered |
| Hidden team | `team.not_found` on the team page shows «Команда не найдена» frame | `ProjectVisibility.qa.test.tsx` 1 | added |
| Hidden department/area | `area.not_found` shows «Область не найдена» frame | `ProjectVisibility.qa.test.tsx` 2 | added |
| Assignee 409 (`assignee_unavailable`) | text under «Исполнитель», pick kept, nothing patched | `WorkItemFormPage.assignee.test.tsx` | covered |
| Assignee picker sends `projectId` | existing (mod-019 OQ-11) | existing assignee tests | covered |
| Error-code catalog | new code pinned | `paths.test.ts` | covered |

### Copy against design

Every string of § 6.1 matches character for character (section, label, helper, confirm title and text, button, both notices). Refusal texts are the server's or the existing «Недостаточно прав».

Not-found headings: design § 6 writes «Проект не найден.» with a period. The shared `NotFound` frame (`shared/ui/StateFrame.tsx`) renders its heading for all three kinds without a period, and every existing test and screen follows that; the period in design/story is the sentence form of the API's `title` («Проект не найден.»). Judged as convention, not a defect; no change.

### Runs

| Date | What was run | Result |
|---|---|---|
| 2026-11-16 | `npm ci`; `tsc -b` | clean (the developer's `@tiptap/*` errors were a missing install, confirmed) |
| 2026-11-16 | Vitest `src/features/organizations` + `WorkItemFormPage.assignee.test.tsx` | 68 files, 946 tests pass |
| 2026-11-16 | Vitest `ProjectVisibility*.test.tsx` incl. 3 added tests | 8 pass |
| 2026-11-16 | ESLint on touched files; Prettier `--check` | clean; clean |

The full Vitest suite was not run (CI runs it; the change touches only organizations and one workitems test).

### Defects

None open. Observations, not defects: (1) the visibility control lives on the settings route, which is gated by `organizations.project.write`; a hypothetical holder of `visibility.write` without `project.write` would see «Недостаточно прав» for the page (no such built-in role exists, the bundles hold both); (2) the 403 text in the control is a literal «Недостаточно прав», the same as the other organizations texts, not a shared constant.

### Verdict

blockers: 0 · majors: 0 · result: pass
