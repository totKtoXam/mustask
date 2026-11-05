# Review: feat-048-aggregate-level-views

## Round 1

Reviewed: `git diff origin/main...HEAD` at 82b4d233 (126 files), against spec.md (BR-AV-1..12, OQ-1..9), US-11,
design.md, api-contract.md, plan-backend.md, plan-frontend.md, qa.md, ADR-0036; `docs/conventions/backend.md` and
`frontend.md`. No suite run (the shared lane is busy; the 1M volume run and the backend assemblies on 34d0c5cf are taken as
given). This is the only Opus session of the task (economy mode), so the L security checklist is folded in here, not run as
a separate session.

Verdict: **changes-requested**. 0 blockers, 4 majors (two code, two prose-only), 8 minors (debt).

What holds: all eight routes carry `RequiresOrganizationPermission(workitems.workitem.read)`, so membership is checked before
the handler and before the limiter (`AggregateViewEndpoints.cs:49-63`). The route inventory and the access matrix list them
(`OrganizationAddressedRoutes.cs`, inventory 73 → 81). Tenant isolation: every statement is narrowed to
`caller.OrganizationId`. Projects are narrowed at `AggregateViewReadModel.cs:235`, a by-project group at `:88-93`, departments
through the org's areas at `:246-262`, and parents, checklists and assignees through org-scoped reads. A foreign
`group`/`projectId`/`teamId`/`departmentId` selects nothing. Cursor: unsigned but fingerprinted. A forged position only moves
the start inside the caller's own scope. `T` is range-checked (`GroupCursor.cs:40`, QA D-1). `P` and `L` reach SQL only as
parameters. All SQL is LINQ, with no raw or interpolated SQL. No secrets and no personal data in logs. The limiter is chained
per subject (6+6) and global (40+20), and its 3 s wait runs on `TimeProvider` (`AggregateReadLimiter.cs:32-67`). An abort
propagates, a timeout gives 429 + `Retry-After: 2`, and the lease is released when the handler returns. The grouped count and
the lists are outside the limiter by OQ-2 A (accepted risk).

### F1 — the panel offers «По департаменту» at team level; the server refuses it
Severity: major
Where: `src/frontend/src/features/boards/lib/grouping.ts:31`. The tests pin the defect:
`lib/grouping.test.ts:26` and `pages/AggregateViews.test.tsx:196` («offers no «По команде» at the team level» expects
`['Без группировки', 'По департаменту', 'По проекту']`).
Essence: BR-AV-2, US-11 scenario 1 ("with MARS chosen, only «Без группировки» and «По проекту»") and design § 2's table say a
team offers none and project. The backend agrees (`AggregateGrouping.cs` `Offers`: `(false, true, _) => Project`). The panel
adds `department` whenever no department or project is chosen. So on every team page without a department (team
«Рабочие элементы», and the team «Бэклог»/«Канбан» through `GroupedViewRoutes`) the switch shows «По департаменту». Choosing
it makes the groups read answer 400 `boards.invalid_grouping`, and the view shows the error banner. It is also remembered
per level, so the error comes back on every visit until the user switches away. MSW does not validate the offer, so no test
sees it.
Fix: `if (!selectors.departmentId && !selectors.projectId && !selectors.teamId) offered.push('department');`. Correct both
tests to `['none', 'project']` / `['Без группировки', 'По проекту']`. Add the backend case
`(false, true, false, Department, false)` to `AggregateGroupingTests`.

### F2 — grouped «Рабочие элементы» reaches «Удалена», the deleted-item matrix says it never does; total < count
Severity: major (the diff contradicts itself)
Where: `AggregateViewReadModel.cs:210-212` (`View == WorkItems && StatesSet` → `InAnyState`, used by the groups and rows
reads) against `DeletedItemSurfaceMatrixTests.cs:322,325` (`/workitems/groups`, `/workitems/rows` = `Reads.LiveOnly`). The
matrix assertion at `:376-379` names exactly four bin-reaching reads.
Essence: BR-AV-8 ("both forms show the same rows for the same filters") makes reaching the bin right, so the code is right
and the ADR-0030 matrix row is wrong. The next line that changes deleted-item behaviour will trust the matrix. Second effect:
the header total is read with `Filter = None` over `Live` (`:40`), so with `state=Удалена` the count can exceed the total
(«5 из 3»).
Fix: classify both routes `LiveOrChosenDeleted`, add their blocks (a deleted item is absent without `state`, present with its
state) and extend the four-route assertion to six. For a `workitems` view with `StatesSet`, read the unnarrowed total over
the same item set as the count (or clamp `total ≥ count`).

### F3 — the statement-count test the spec requires does not exist; plan-backend names six files that do not
Severity: major (a guarantee someone will rely on)
Where: spec.md:103 (NFR: "a test … asserts … the constant statement count per group"). plan-backend.md § 4 lists
`AggregateViewStatementCountTests` ("statement counts constant at 5 and at 50 projects"). The same table lists
`AggregateRequestTests`, `AggregateViewGroupsEndpointTests`, `AggregateViewRowsEndpointTests`,
`OrganizationWorkItemsEndpointTests` and `ReadLimiterTests`, and § 1 lists `AggregateRequest.cs` and `ReadLimiter*.cs`. None
of these exist (the real ones are `AggregateParameters*`, `AggregateViewEndpointTests`, `AggregateReadLimiter*`).
Essence: by reading, the reads are constant (counts 2+1, a page 1–2 + dress + parents), and the volume test measures time,
not statements. Nothing pins the count, but the plan says something does. A later change to the read model that adds a
per-project query would pass every test.
Fix: add `AggregateViewStatementCountTests`. It counts commands (a `DbCommandInterceptor`, as the other statement-count tests
do) for the groups read, a rows page of each grouping, a cell page and the flat list, at 5 and at 50 projects, and asserts
the counts are equal. Correct the file and test names in plan-backend §§ 1, 4.

### F4 — spec, story and design promise sorting on the flat list; plan-frontend says none exists
Severity: major (sibling contradiction in the same diff; prose-only fix)
Where: spec.md:87 BR-AV-8 ("the sorting of BR-WS-12"), spec.md:158 OQ-9 ("The flat list sorts by BR-WS-12's columns"),
US-11:59 ("returns the paged list with its pager, total and sorting"), design.md:91 ("Sortable columns as feat-003") and
design.md:198 (prototype "flat list with the pager and sorting"). Against these: plan-frontend.md § 7 and feat-003 spec.md:107
("Sorting by a column" is out of scope; BR-WS-12 fixes the order).
Essence: a tester or the next developer reading BR-AV-8 or US-11 looks for a sort control that never existed.
Fix: record the decision (flat list keeps BR-WS-12's fixed order, no sort control) in status.md § Decisions. Reword the five
places to "the fixed order of BR-WS-12". No code change.

### Minors (debt, not a round)

- **m1** `AggregateViewReadModel.cs:305`: the keyset tie `string.Compare(project.Key, key) > 0` compares in the database's
  default collation, while the order uses `Collate(…, "C")` (`:313`, `:160`). It is harmless for today's `[A-Z][A-Z0-9]`
  keys, but the cursor and the order must use one collation. Fix: compare `EF.Functions.Collate(project.Key, "C")`.
- **m2** `Program.cs:104-109`: the dev CORS policy does not expose `Retry-After`. The panel calls the API cross-origin
  (`VITE_API_BASE_URL`), so `retryAfterOf` reads null and falls back to 1/2/4 s. ADR-0036's "the BFF must pass `Retry-After`"
  has no anchor. Fix: `.WithExposedHeaders("Retry-After")`, and a deployment note for production.
- **m3** `AggregateReadLimiterTests`: no test refuses at the global queue (20). No host-level test shows a 429 reaching the
  client as problem+json `http.too_many_requests` (the filter test stops at `StatusCodeHttpResult`). The NFR "two tabs, 8
  reads, none refused" is shown for one waiter only. Fix: add the three cases.
- **m4** No cross-tenant test, although plan-backend § 4 promised "other organization absent". The code is safe by reading
  (above). Fix: one test that sends another organization's project/team/department as `group` and as selectors and gets
  404/empty.
- **m5** ADR-0036:45 "Changing the numbers is a configuration change": they are `const` (`AggregateReadLimiter.cs:15-23`).
  ADR-0036:5 cites design § 6 for the server side, which is § 7. Fix: reword ("a code change, not an ADR amendment"), fix the
  §.
- **m6** design.md § 7 specifies `ix_work_items_assignee_id_changed_at (assignee_id, changed_at DESC, number DESC)`; the code
  (`WorkItemConfiguration.cs:155`) omits `number`, and no statement of the slice is shown to use the index (the reads are
  project- or team-led). Fix: align the design, or drop the index if the volume run's plans never pick it.
- **m7** `AggregateViewReadModel.cs:379` `rows[candidate.Id]` throws (500) if an item is purged between the candidate
  statement and `ReadByIdsAsync`. Fix: skip ids not returned.
- **m8** The project list's «Команда» multi-select → «Без команды» changes mod-020's and feat-003's shipped list. Only
  feat-043's status.md got a P-10 note. Fix: a one-line note in mod-020's status.md.

### Behavioural claims checked (direction 1)

- api-contract § 1 non-member 404 → `AggregateViewEndpoints.cs:52` + `OrganizationAddressedRoutes` matrix.
- § 1 `grouping` 400 for not-offered/unknown → `AggregateParameters.cs:60-72`; held for the server, the panel is F1.
- § 1 `group` none / malformed 400; ignored for `none` → `AggregateParameters.cs:91-94,147-155`.
- § 1 `limit` 1..50 → `AggregateParameters.cs:96-105`.
- § 1 cursor foreign by view/level/grouping/group/column/filters → `AggregateParameters.cs:126-144,107` (organization too).
- § 2.1 order: none-group first, name, id → `GetViewGroupsHandler.cs:95-100`; empty absent `:61-64`; columns `:68-78`.
- § 2.1 unknown level 404 → `AggregateViews.cs` `LevelOf`.
- § 2.2 rank order by project, change time otherwise; inactive project 404 → `AggregateViewReadModel.cs:88-101`,
  `GetBacklogGroupRowsHandler.cs`.
- § 2.3 unknown `column` 400 → `GetBoardCellsHandler.cs:133-136`; cards carry checklists `:149-156`.
- § 2.4 work-item rows always change time → `AggregateViewReadModel.cs:96`.
- § 2.5 envelope, page < 1 400; deleted only by chosen state → `GetOrganizationWorkItemsHandler.cs:272-277`, `:210`.
- § 2 limiter 6/40, queue 6/20, 3 s, 429 Retry-After 2 → `AggregateReadLimiter.cs:15-23,52-67,79-90`; per process: a
  singleton (`Program.cs:146`).
- ADR-0036 "runs after authentication" → authorization policy precedes endpoint filters; "slot held until the read releases
  its connection" → lease disposed after `next` returns a materialised result.
- ADR-0036 "configuration change" → finding m5; "BFF must pass Retry-After" → m2.
- plan-backend D-7 deep page / keyword take the ordered pass → `AggregateViewReadModel.cs:139-155`.
- plan-backend § 4 test list → F3.
- plan-frontend § 2 4-per-tab, slot released while waiting, ≤3 retries, cursor-400 silent reload, 404 hides →
  `readQueue.ts`, `useGroupRead.ts:180-253`.
- plan-frontend § 2 grouping not in address, memory per user/org/view/level → `grouping.ts` `groupingStorageKey`.
- BR-AV-2 offers → backend holds; panel F1.
- BR-AV-6 move only by project, nothing narrowing; hints → `grouping.ts` `backlogCanMove`/`backlogHint`,
  `AggregateBacklogView.tsx`.
- BR-AV-7 drop only inside the lane → `AggregateBoardView.tsx` (`dragged` state per lane's `BoardColumns`).
- BR-AV-8 sorting → F4; "same rows both forms" → F2.
- OQ-6 A part-loaded «Ниже» off, «В конец» drops the row, no drop below last → `aggregateMove.ts`.
- D-3 A target cell re-read → `AggregateBoardView.tsx` `onMove` (always re-reads the target cell; a superset).
- spec NFR statement-count test → F3; two-tab test → m3.
- qa.md D-1 fixed → `GroupCursor.cs:40`; D-4 fixed → `useAnnouncement.ts` import (not re-run here).

### Direction 2

- Index `ix_work_items_owning_team_id_type_id` replaced: grep over `docs/`, `src/backend` → feat-040 api-contract:288 and
  plan-backend:66,201 describe their own migration (historical, still true of that migration); `BacklogMembership.cs` comment
  updated in the diff. No stale live claim.
- Project list «Команда» → «Без команды»: grep `TeamFilter` → feat-003 design/plan/spec and mod-020 status describe the old
  control (historical); no break note outside feat-043 → m8. `TeamFilter` stays used by `ViewFilterRow` (backlog/board), as
  the spec says.
- Org-level addresses now served (`routes.retired.qa.test.tsx` changed): grep "retired" in ADR-0028/0032 → the rule lists
  project-nested team addresses and settings pages, not the seven feat-048 addresses. None stale.
- New error codes and 429: `docs/error-codes.md` and `contracts/errors/*.json` updated in the diff; `src/backend/AGENTS.md`,
  `src/frontend/AGENTS.md`, `.claude/` not swept for the limiter (no identifier of it found there by grep of
  `Retry-After`/`too_many`).
- Not swept: feat-043's BR-NV-20/US-10 wording against the amended behaviour (recorded by the P-10 note in feat-043
  status.md, by design not edited).

## Round 2 (delta)

Reviewed: `git diff 80566c6c..779fee12` (bcd88413, 779fee12; 15 files) against the Round 1 findings, nothing else. No
suite run. The coordinator reports the full frontend Vitest run on 82b4d233 green (350 files, 5339 passed); the touched
files and classes pass per the developers.

Verdict: **changes-requested**. F1-F4 are closed. One new major (R2-1) comes from the m8 fix itself and is a one-line prose
fix: it needs a delta read only, no QA. Open: 0 blockers, 1 major, 7 minors (debt).

### Round 1 findings

- **F1 closed.** `grouping.ts:31-33` offers `department` only with no department, team or project. The corrected expectations
  are `grouping.test.ts:26` and `AggregateViews.test.tsx:196`. A remembered «По департаменту» at team level now falls back
  silently (`grouping.test.ts:68-75`). The suggested backend case `(false, true, false, Department, false)` was not added. The
  code already returns false there, so this is not a finding.
- **F2 closed.** `DeletedItemSurfaceMatrixTests.cs:322,325` now classify both routes `LiveOrChosenDeleted`, and the
  six-route assertion pins them. A real block (`:177-187`) shows the rows and the count are live without `state` and the
  bin's three items with «Удалена». The clamp `Math.Max(total, count)` (`GetViewGroupsHandler.cs:81`) was one of the two
  fixes Round 1 offered, and it removes «5 из 3». With «Удалена» the header reads «3 из N», where N is the group's live
  total. That fits BR-AV-5 ("total does not follow the filters") as well as a same-set total would. The developer's reason
  is not accurate: a same-set read would not shrink the total for a live state, because with no deleted state chosen the
  set is just the live items. The choice holds anyway.
- **F3 closed.** `AggregateViewStatementCountTests` (Infrastructure.Tests, real handlers, a `DbCommandInterceptor`, 5 and
  50 projects) asserts equal counts for:
  - the groups read under none, team and project, for all three views;
  - a group page in change-time order (a team, and the group of no team) and in rank order;
  - a cell;
  - the flat list, shallow, deep and with a keyword.

  "Constant", which is what the NFR asks, is exactly equality across sizes. A pinned number is not owed. Department
  grouping is not among the cases (minor m9). plan-backend §§ 1, 4 now name real files.
- **F4 closed.** BR-AV-8, OQ-9, US-11:59 and design.md:91 and :198 now say "the fixed order of BR-WS-12", and the Decisions
  bullet records it for gate-3. The bullet's "design § 4" is correct: § 4 is «Рабочие элементы»: the flat list and the
  grouped form, and it holds the table at :91. One leftover is design.md:94, "the page number and sort are dropped" (minor
  m10).

### Minors claimed fixed

- **m2 closed:** `Program.cs:111` `.WithExposedHeaders("Retry-After")`. The deployment note for production is not
  written; it is carried into m2' below.
- **m5 closed:** ADR-0036:5 now cites § 7, and :45 says the numbers are code constants.
- **m7 closed:** `AggregateViewReadModel.cs:379-383` skips ids that are no longer there. The fix has no test; this is
  accepted for a race.
- **m8: see R2-1.**

### R2-1 — the mod-020 change note misdescribes the shipped control
Severity: major (the same diff contradicts it; prose only)
Where: `docs/features/mod-020-team-switcher-default-and-filter-address/status.md`, the 2026-11-04 Log line: "replaced by a
multi-select with «Без команды»".
Essence: the shipped control is one checkbox, `NoTeamFilter.tsx:1,31` (`Checkbox`), used at `WorkItemsPage.tsx:222`.
feat-048 spec § "The team filter of the project's list" says "one control, «Без команды»". Someone who reads the note looks
for a multi-select that no longer exists.
Fix: "…the «Команда» multi-select is replaced by a single «Без команды» checkbox, exclusive with the team selector (feat-048
spec; feat-043 BR-NV-12, Q-6 A)…".

### Minors (debt)

Still open from Round 1: m1 (keyset collation), m3 (limiter test gaps), m4 (no cross-tenant test), m6 (assignee index vs
design § 7). New:

- **m2'** Production has no CORS rule, so the API needs either same-origin hosting or `Retry-After` exposed there. ADR-0036's
  "the BFF must pass `Retry-After`" still has no deployment anchor. Fix: one line in the deployment notes.
- **m4'** plan-backend § 4 (the `AggregateViewEndpointTests` row) still claims "other organization absent", and the
  `AggregateReadLimiterTests` row still claims "2 wait" and "global 40+20" with the queue refused. Neither is tested (m3,
  m4). Fix: weaken the rows, or add the tests.
- **m9** `AggregateViewStatementCountTests` has no department-grouping case for the groups or rows reads. Fix: add
  `department` to the theory and one department group page.
- **m10** design.md:94 "the page number and sort are dropped". Fix: "the page number is dropped".
- **m11** 779fee12 committed mod-020's status.md with CRLF endings (repo blob, 48 CRs in 49 lines; `.gitattributes`
  `* text=auto`), so the whole file shows as rewritten (97 lines) for a one-line note. Fix: renormalise to LF with the R2-1
  edit.

### Behavioural claims checked (direction 1, the delta)

- `grouping.ts:26-27` doc "a team and a project fix the department as well" → `:31`.
- DeletedItem matrix comment "the organization-wide list, its groups and rows … reach the bin by a chosen state" →
  `AggregateViewReadModel.cs:210`, block `:177-187`.
- `GetViewGroupsHandler.cs:80` comment "never reads below the count" → `:81`.
- `AggregateViewReadModel.cs` comment "a purged item is dropped, not an error" → `:380-383`.
- ADR-0036:45 "numbers are constants" → `AggregateReadLimiter.cs:15-23`.
- plan-backend § 4 statement-count row → `AggregateViewStatementCountTests` (department gap, m9); the endpoint and limiter
  rows → m4'.
- spec BR-AV-8, OQ-9, US-11:59, design :91 and :198, the Decisions bullet → no sort control exists in `WorkItemsPage.tsx`;
  feat-003 spec:107.
- mod-020 note → R2-1.

### Direction 2

- Behaviours the delta changes are: the team-level offer, the grouped list's deleted set, the header clamp and the CORS
  exposed header. Grep `groupingsOffered`, `LiveOrChosenDeleted`, `WithExposedHeaders` and `Retry-After` over `docs/`,
  `.claude/`, `src/*/AGENTS.md`. The hits are only this feature's files and ADR-0036, all read above. None stale.

## Round 3 (delta)

Reviewed: `git diff 038f7101..2c6ae5a8` (prose only: design.md, both status.md files). No suite run; none owed.

Verdict: **approve**. Open: 0 blockers, 0 majors, 8 minors (debt for gate-3).

- **R2-1 closed.** The mod-020 Log line now says "a single «Без команды» checkbox, exclusive with the team selector". That
  matches `NoTeamFilter.tsx` (`Checkbox`) and the feat-048 spec.
- **m10 closed.** design.md:94 drops the page number only, and the flat list returns "in the fixed order of BR-WS-12".
- **m11 mostly closed.** mod-020's status.md is LF again, and `git diff origin/main...HEAD` on it is one added line. That
  line still starts with a bare CR byte (`\r- 2026-11-04 …`). It renders harmlessly, but it is a stray character. Fix (debt):
  delete the byte when the file is next touched.
- Claims in the delta: design § 4 "starts at page 1 in the fixed order of BR-WS-12" → feat-003 BR-WS-12 and
  `WorkItemsPage.tsx`, which has no sort control. The orchestrator's Log line "Open: 0 blockers, 0 majors" → this round.
  Direction 2: nothing in the delta changes behaviour, so there is nothing to sweep.

Debt for gate-3 (proposed fixes in Rounds 1-2):
- m1: keyset collation.
- m2': `Retry-After` exposure in production.
- m3: limiter test gaps.
- m4: no cross-tenant test.
- m4': plan-backend § 4 overclaims the endpoint and limiter tests.
- m6: the assignee index differs from design § 7.
- m9: no department case in the statement-count test.
- m11': the stray CR above.
