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
