# feat-062-sight-in-work-views: QA

## Backend

Pass 1, 2026-11-18, commit 3e6cf225 (HEAD d3652a78). Plan written from doc-016 BR-TD-12..-17 + Non-disclosure NFR, mod-007 stories
US-2/3/5/9/10 "owed by feat-041", feat-041 review R-5 and api-contract §1 item 4/§5, before the implementation was read.

### Plan and coverage

| # | Requirement | Test | Status |
|---|---|---|---|
| 1 | Aggregates (groups, rows, cells, flat list, assignee options, team backlog/board incl. `projects`) equal the twin without the hidden project; totals, pages, cursors | SightInWorkViewsTests twin test (paged 1/page, all groupings) | covered |
| 2 | Hidden project id as group = unknown id | AGroupNamingTheHiddenProject... | covered |
| 3 | Selector query ids naming a hidden project select nothing like an unknown id | ASelectorNamingTheHiddenProject... (QA, added) | covered |
| 4 | Administrator unchanged | TheAdministrator_StillSees... | covered |
| 5 | No over-narrowing: project grant, linked-team membership bring the project back | AViewer_WhoSeesTheProjectByAGrantOrByALinkedTeam... (QA, added) | covered |
| 6 | Sprint list, card, close preview, Closed outcome, participants, dynamics, items = twin | SightInSprintsTests twin test | covered |
| 7 | Range of an earlier Closed sprint ("k из N", dynamics range) | ASprintClosedAfterAnEarlierOne... (QA, added) | covered |
| 8 | Close moves hidden items, answers as twin; removal refusal identical | Closing..., Removing... | covered |
| 9 | Reorder item/anchor of hidden project = 409 reorder_stale like unknown; visible moves, admin moves | ATeamBacklogReorder... | covered |
| 10 | Body ids (team in create/save, sprint in save, close destination) = unknown; visible unlinked still 409 | two body-id tests | covered |
| 11 | Statement counts independent of project count | AggregateViewStatementCountTests (member = admin + set, 5 vs 50), SprintStatementCountTests (pins unchanged) | covered (read from diff, ran by developer) |
| 12 | One sight rule | `AccessSet.SeenProjects()` = `SeesProject` over all projects (unit test); no second implementation found | covered |

Surfaces audited for a missed read: all GET routes under Boards, Iterations, WorkItems, Organizations. Item counts elsewhere are
admin-only (`team.delete`, deletion-counts) or single visible project (`unlink-count`); `overview` and selector offers are feat-061's.
Frontend: no route, shape or status change in the contract, so no frontend change is needed (confirmed).

### Run

`dotnet test src/backend/tests/Mustask.Api.IntegrationTests --filter SightInWorkViewsTests|SightInSprintsTests`: 12 passed, 0 failed
(9 developer's, 3 added). Other classes not re-run (developer reported them green; economy mode).

### Defects

- D-1 · minor · fail-open defaults: `IOrganizationAccess.VisibleProjectsAsync` defaults to `All`, and
  `BacklogMembership.Members/TreeMembers(..., visible = null)` treats null as `All`. A new caller that forgets to pass the sight would
  silently read everything (the `CallerAccess.Projects` path fails closed, these two do not). Carried to gate-3 as a debt.

Hidden-id answers byte-identical to unknown, narrowing before paging, administrator, statement counts: no defect found.

### Verdict

Pass. No blocker or major open; one minor carried to gate-3. E2E: not run (standing answer NO).
