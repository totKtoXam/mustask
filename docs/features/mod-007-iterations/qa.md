# QA — mod-007-iterations

## Backend

Scope: US-1..US-5, US-9, US-10 (US-6/US-7 are other lines; visibility clauses are feat-041's). E2E: standing answer NO, not run.
The plan below was written from spec, stories, api-contract and design § 10 before the implementation was read.

### Coverage matrix

| Criterion | Check | Type | Status |
|---|---|---|---|
| BR-7 numbering, names | next number per team, no reuse, concurrent creates, default name, case/space-insensitive uniqueness, 1..100 chars | integration | covered (pre-existing) |
| BR-4 dates | paired, reversed, per state on update, start needs both | integration | covered |
| BR-3 lifecycle | one Active per team, two starts at once, never back | integration | covered |
| BR-8 removal | only empty Planned; number not reused; items in «Удалена» lose pointer | integration | covered, «Удалена» clearing added, green |
| BR-2/BR-5 pointer | team rule, closed target, unchanged pointer unchecked, deleted item first, team change | integration | covered |
| BR-2 unlink/delete of team clears pointers (Closed sprint included) | integration | added, green |
| BR-6/BR-14 snapshot | completeness: project, level, SP (none = 0), assignee (none), done flag, «Удалена» excluded, immutable after later edits, purge keeps it | integration | added, green |
| BR-15 range | inclusive bounds, current sprint excluded, other teams/non-Closed excluded, 20 cap latest, reversed/half/garbage = default | integration | added, green |
| BR-15/16 figures | min/avg/max, mean of per-sprint percents, half up, held-0 sprint left out of percents | domain + integration | added, green |
| BR-14 level | epics/features/stories filters | integration | added, green |
| BR-16 participants | k из N, total = sum, none-assignee last, left org, paging | integration | covered; multi-sprint added, green |
| BR-12 races | close vs save into sprint (covered); vs destination removal, vs team unlink, vs team change, vs state save, removal vs save | integration (Race kit) | added, green |
| BR-13 roles | write routes x {org Admin, team Manager, team Member, team Reader, org Member, org Viewer}; reads open to all; cross-team grant | integration | partly; matrix added, green |
| NFR statements | list, outcome reads, close, close-preview; no growth with items, range, projects, participants | infra | added: preview, range, participants (green) |
| NFR volume | 5 000-item close + 20x5 000 range read in time | integration | covered (VolumeTests) |
| Tenant isolation | foreign team/sprint/destination id answers not-found | integration | partly |
| Migration | additive, rollback, reapply, no pending model changes | infra | covered |

### Runs

| Date | What was run | Result |
|---|---|---|
| 2026-11-12 | `dotnet test Mustask.Api.IntegrationTests --filter Iterations` | 70 passed, 0 failed (12 new: snapshot, purge, range, k of N, level, unlink/delete pointers, 5 races, role matrix x7) |
| 2026-11-12 | `dotnet test Mustask.Infrastructure.Tests --filter Iterations` | 7 passed (2 new statement-count tests: range/participants, close-preview) |

First run of the new tests: 2 failures, both in the test code (features and epics carry no story points; a decimal printed as 3.00), fixed in the tests. Note: a hung `testhost` of an earlier session locked this worktree's `bin`; the runs used `-p:UseArtifactsOutput=true -p:ArtifactsPath=<scratch>`. Not killed (not mine).

### Defects

#### D-1: participants' names are looked up for every assignee, not for the page

- Severity: minor
- Steps: read `GetSprintParticipantsHandler`: `NamesAsync` runs over all assignee ids of the sprint in batches of 500, then 200 rows are cut.
- Expected / Actual: statements "do not grow with participants" (NFR). Up to 500 assignees it is one lookup (the NFR is sized to 300 and a 300-assignee test is green); above 500 each further 500 adds one statement.
- Status: open, carried to gate-3 as a debt

#### D-2: a person the directory does not know has no name

- Severity: minor
- Steps: an assignee id that the user directory has no record of, in a Closed sprint's snapshot.
- Expected / Actual: BR-16 «last known name»; the row has `displayName: null` and sorts after the known people. Not reproducible through the API today (an assignee must be an active member), so a speculative case.
- Status: open, debt

### Verdict

blockers: 0 · majors: 0 · result: pass (no blocker or major found; the focus list held: snapshot completeness and purge, inclusive range bounds with the current sprint out and the 20 cap, reversed/half/garbage range, mean of percents with half-up, level filter, close races, role matrix; visibility is feat-041's)

## Frontend

Scope: US-1..US-5, US-9, US-10 screens (design § 3-§ 8). E2E: skipped by the human's standing decision (NO), not run. The plan (design § 1-§ 8 as a matrix) was written before the implementation was read.

### Coverage matrix

| Criterion / screen | Check | Status |
|---|---|---|
| § 1 «Спринты» in the left team menu between «Рабочие элементы» and «Проекты»; routes `sprints`, `sprints/:s` | addressModel test, routeTree | covered (pre-existing); menu code is the section entry like its neighbours (design said `read.teamRead`), not a defect |
| § 3 list: order, states, dates «—», «6 из 10», only a Closed name is a link | page tests | covered; Closed link and «закрыто из всего» added |
| § 3.3 row menus per state; § 8 manage-only controls (create, menu column, «Переименовать»), Reader read-only | page tests, role table | covered |
| § 4 states: pending, not found, first read failed, empty (planner / other hint), «Показать ещё», its failure | page tests | covered; paging, failed append, reader's empty hint added |
| § 5.1 forms, Active dates cannot be cleared (hint, client refusal, nothing sent) | page tests | added |
| § 5.3 start without dates, `active_exists` stays open | page tests | covered |
| § 5.4 remove | page tests | covered |
| § 5.5 close preview, plural, dated destination label, preselection, none, «Без спринта», refusals (destination invalid under field, destination gone clears choice and re-reads, not active closes) | page tests | covered/added |
| § 6.2 `?level=`, `?from=&to=`, reversed / half / garbage / impossible date fall back to default with no error, half pair typed is not applied, valid range sent | page + lib tests | added (8 cases) |
| § 6.2 count and the cap notice | page test | covered (uses the server's numbers, no hard-coded 20) |
| § 6.3 dynamics: this sprint first, comma «6,3», «63 %», empty range text and «—» summary | page tests | covered/added |
| § 6.4 participants: expand, «k из N», «Без исполнителя», «не в организации», «Итого», empty level without «Итого», half-up percent, «—» | page tests | covered/added; «Итого» expansion fails (D-2) |
| § 6.5 purged row: identifier, «Удалён навсегда», no link, recorded points | page tests | covered; recorded project fails (D-3) |
| § 6.6 per-block error and «Повторить» (dynamics, participants) | page tests | covered/added |
| § 6.1 head: sprint name is the h1, trail «Команды › MARS › Спринты» | page test | fails (D-1) |
| § 7 card field: options Active+Planned, none, kept Closed, no team, cleared line, failed list, four refusals, read-only | card tests | covered; read-only, `open=true`, picked state added |
| Russian copy verbatim against design | read `sprintTexts.ts` against design § 3-§ 7 | matches; one deviation (card failed-list line, D-5) |

### Deviations judged

- InlinePickerField instead of InlineSelectField on the card: accepted. The neighbours (team, assignee) use the picker, the label placement and value texts match § 7; the select would not show a kept Closed value that is not among the options.
- Expanded rows as sibling rows: accepted. The columns line up under the parent's headers; `aria-expanded` and the control name are right. The nested rows have no `aria-label`'d table of their own (design § 6.4 says nested `DataTable`), cosmetic, no defect.
- No preselection on close without a destination: accepted, it is design § 5.5 (OQ-4); the primary stays inert until «Без спринта» is picked.

### Runs

| Date | What was run | Result |
|---|---|---|
| 2026-11-12 | `vitest run` on the three new files | 35 passed (3 are `it.fails`, pinned to D-1, D-2, D-3: they turn red when the defect is fixed, then drop the `.fails`) |

### Defects

#### D-1: the outcome page's h1 is the team name, with no trail

- Severity: major
- Steps: open a Closed sprint's page. `SprintOutcomePage` passes `title={teamName}` to `PageHeader variant="team"` and puts the sprint name in a `Section`.
- Expected / Actual: design § 6.1: the only `h1` is the sprint's name, trail «Команды › MARS › Спринты» through `ancestors`, the state `Tag` «Завершён» beside it. Actual: h1 is the team name, no ancestors, no state tag; the sprint name is an h2.
- Pinned by: `SprintOutcomePage.qa.test.tsx` «makes the sprint name the h1» (`it.fails`).
- Status: open, to the frontend-developer

#### D-2: «Итого» does not expand

- Severity: major
- Steps: open the outcome page, «Итоги по участникам»: «Итого» has an empty first cell, no chevron.
- Expected / Actual: US-9 (line 36) and design § 6.4: every row, «Итого» and «Без исполнителя» included, expands to its min, average and max. The API gives `total.dynamics`. «Без исполнителя» does expand.
- Pinned by: «expands «Итого»…» (`it.fails`).
- Status: open, to the frontend-developer

#### D-3: a purged row loses its project

- Severity: major
- Steps: a done list with a purged item. `ResultBlock` renders `''` in «Проект» for `purged`.
- Expected / Actual: D-9 A (taken by the human) and design § 6.5: project and points stay as recorded. The API sends the project; points stay, the project is blanked (that is D-9 B).
- Pinned by: «keeps the recorded project of a purged row» (`it.fails`).
- Status: open, to the frontend-developer

#### D-4: a read-only card field still reads the sprints on click

- Severity: minor
- Steps: Reader opens the field; `onOpen` enables the sprint read though no list can open.
- Expected / Actual: no request is needed; one wasted GET. Harmless.
- Status: open, debt

#### D-5: the card's failed list has no «Повторить»

- Severity: minor
- Steps: fail the sprints read, open the field.
- Expected / Actual: design § 7: menu item «Не удалось загрузить спринты» with «Повторить». Actual: «Не удалось загрузить спринты. Попробуйте ещё раз.» as the empty text; reopening the list retries (tested). Same shape as the team field's.
- Status: open, debt

### Verdict

blockers: 0 · majors: 3 (D-1, D-2, D-3) · result: fail, back to the frontend-developer; the re-check covers the three fixes (flip the three `it.fails` to `it`) and the outcome page's other blocks. The visibility clauses are feat-041's.
