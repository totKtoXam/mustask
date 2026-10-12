# bug-004-board-shared-state-one-column — review

## Round 1

Scope: `git diff main...bug/004-board-shared-state-one-column` at ed4a33b
(merge base 8f6f84d), backend and frontend together (M, one round). Binding:
D-3.

Verdict: **changes-requested**. Open: 0 blocker, 2 major (R1-1, R1-2), 4 minor
(R1-3..R1-6). Both majors can be fixed in prose alone, so they get a delta read
and no QA.

The code does what D-3 asks:
- The seed has one «Разрешена», Resolved (`SeededProcess.cs:112-123`).
- The rule is in the blueprint and the aggregate (`SeededProcess.cs:69`,
  `ProcessDefinition.cs:91, 102-103`). Nothing else creates a state:
  `WorkItemType.AddState` and the `StateDefinition` ctor are internal, and
  Domain grants InternalsVisibleTo only to Domain.Tests.
- Every header is a state name (`BoardLayout.cs:88`). The column keeps its
  `category` (`GetBoardHandler.cs:73`).
- Everything else that reads a category takes it from the server: the dots,
  the form, the list and the backlog. No code filters or counts by category,
  apart from Removed.
- The migration touches only the seeded rows. Its Down mirrors Up, and it is
  idempotent. EF's migration lock serialises two concurrent `database update`
  runs.
- Tests: they fail on the old code (headers, seed rows, the process-screen
  sentence), and the E2E alignment is complete (see Direction 2). E2E 20 and
  23 were reviewed statically. The run is the orchestrator's (qa.md).

### Findings

**R1-1 — major — `src/backend/Mustask.Infrastructure/Migrations/20261011214251_MoveResolvedStatesToResolvedCategory.cs:21-24, 54-69`; `docs/features/doc-007-process-customization/spec.md:205-207`; `status.md` Log, the [dev] migration line.**
The migration's report never reaches whoever applies it. I tested this on a
throwaway `postgres:18-alpine` database, since removed:
1. Migrated it to `AddWorkItemFormFields`.
2. Inserted a process whose US «Разрешена» is Completed and whose BUG
   «Разрешена» is Resolved (test case C).
3. Ran `dotnet ef database update`, the documented command
   (`src/backend/AGENTS.md`). It printed only "Applying migration … Done.",
   with no NOTICE and no WARNING. `--verbose` prints the SQL text, not its
   messages.

The warning lands only in the PostgreSQL server log (`docker logs`). The
migration test sees it only because it hooks `NpgsqlConnection.Notice`.

Failure: the person applying it sees nothing and concludes that nothing needs a
decision. Organization C's «Истории» board then reads «Новая», «Активна»,
«Разрешена» (BUG), «Разрешена» (US Completed, and a bug dropped there gets
«Закрыта»), «Закрыта». That is D-1's symptom, under one name. D-3 (1) asks for
"reported, not guessed".

Also, doc-007's "names the rest in a warning" overstates: a renamed or
repositioned state that breaks no rule (test case B) is named nowhere.

Fix, prose is enough:
- Say where the report lands (the server log).
- Make the check a step after `database update`: the `SELECT` of the `FOR`
  loop, quoted verbatim, in the migration remarks and in
  `docs/runbooks/local-migrations.md` (or the gate-3 summary).
- Narrow doc-007's sentence to "names every process where a name still
  stands in two categories".

Better, but optional: the API logs `StateNameRule.Breaches` once per process
on a read. That also covers R1-3.

**R1-2 — major — `docs/features/feat-001-work-item-links/stories/US-6-level-drop-down-and-kanban.md:57-60`; `docs/features/mod-006-boards-backlogs/stories/US-6-create-from-board.md:9, 19, 24-25, 28, 34, 41`.**
Neither file is in the diff, and both still describe the old board:
- feat-001 US-6 quotes the empty board's product copy «…появятся в колонке
  «Новые».». The panel now renders «…в колонке «Новая».» (`board.ts`
  `emptyBoardHint`). Quoted story copy is the product copy (AGENTS.md,
  Language).
- mod-006 US-6 puts «Новый элемент» and new cards "in «Новые»", a column that
  no longer exists.

Failure: an agent that writes a test or copy from these stories asserts
«Новые» and fails, or "restores" the hint. The sibling stories US-4 and US-5
got D-3 notes; these two did not.

The diff also misquotes US-6's title: the test comment at
`CreateFromTeamEndpointTests.cs:217` reads «Новая», the story «Новые».

Fix: a superseded-in-part note in each story, like US-4's. Optionally, extend
mod-006 `spec.md`'s § Model note to rows `:304` ("the first column, «Новые»")
and `:333` ("the last card of «Новые»").

**R1-3 — minor — the migration as a one-shot (same file, `:44-51`); qa.md O-2.**
External DB mode shares one `mustask` database across checkouts
(`src/backend/AGENTS.md`). Failure: a checkout that has not been rebased (old
seed) creates an organization after Up has run. It gets the old shape, and
`database update` is a no-op from then on. The «Истории» board then shows
«Разрешена» twice (InProgress-2 and Resolved-1), and nothing reports it.

Fix: say in the migration remarks and O-2 that the `UPDATE` is idempotent and
must be re-run after an older build has written to a migrated database.
R1-1's optional read-time log catches it too.

**R1-4 — minor — `src/backend/tests/Mustask.Domain.Tests/Process/ProcessWritePathTests.cs:9-11`; `status.md` Log, [dev] write paths.**
The claims "a later path cannot appear unnoticed" and "fails the moment a
public way to add, rename or recategorize a state appears" are true only of the
three aggregate classes' public members. A repository rename through raw SQL
or `ExecuteUpdate` passes the test. Raw-SQL writes are already a pattern here
(`BacklogRepository.cs:37-38, 81-83`).

Fix: narrow both sentences. Optionally, add an architecture rule like
`UserDirectoryRawSqlQaRules`: nothing outside `ProcessSeedStore` writes
`state_definitions` or `StateDefinitions`.

**R1-5 — minor — `src/frontend/src/features/boards/api/boardsApi.ts:130`.**
Stale doc comment, not in the diff: "the category's label for k = 1, else the
state it maps". Fix: "the name of the state it maps for the first type, in
display order, that maps one (bug-004 D-3)".

**R1-6 — minor — analyses that still state two categories and are missing from the [dev] "Not annotated" list.**
The places:
- `doc-005-queries-dashboards/spec.md:127` (BR-M6-5) and its
  `stories/US-1-clause-builder-query-model.md:92`
- `doc-007-process-customization/stories/US-2-read-only-process-definition.md:47`
- `doc-002-work-items/spec.md:66` and its
  `stories/US-11-work-item-list-and-filters.md:34`
- `doc-001-domain-map/stories/US-10-filter-work-items.md:21` and its `US-3:34`

The ones the next implementer reads are doc-005 (the release-2 state filter)
and doc-007 US-2.

Fix: name them in the list, or put one-line notes in doc-005 BR-M6-5 and
doc-007 US-2.

### Behavioural claims in prose

Direction 1 (prose the diff adds):
- `SeededProcess.cs:109-112` «Разрешена» is Resolved on every type → `:112-123`; `SeededProcessTests` theory.
- `StateNameRule.cs` remarks, every place asks the rule → `SeededProcess.cs:69`, `ProcessDefinition.cs:91`; only creator `WorkItemType.cs:53` (internal).
- `ProcessDefinition.cs:96-99` Seed is the only creator → grep: `AddState` only at `ProcessDefinition.cs:85`.
- `BoardLayout.cs:34-39` header rule and tie rule → `:88`; `BoardLayoutTests` (`Derive_TypesNameTheirFirstStateDifferently…`).
- Migration `:14-18` only rows still as seeded → `:44-51`; migration test `:54-56` (ran green).
- Migration `:21-24` reported in a RAISE WARNING → emitted, not delivered → R1-1.
- Migration `:27-30` idempotent and reversible → migration test `:84-100`.
- status Plan 1, 5 types and 24 states → `SeededProcess.cs:119-123`.
- status [dev] done, the process-screen sentence gone and the hint «Новая» → `ProcessDefinitionPage.tsx:55-57`, `board.ts` `emptyBoardHint`, `BoardPage.tsx:113`.
- status [dev] done, the column's `category` stays → `GetBoardHandler.cs:73`.
- status [dev] done, the "Not annotated" list → incomplete → R1-2, R1-6.
- status [dev] migration, 7 rows / one warning / Down / Up again → migration test `:46-100`.
- status [dev] write paths, "nothing adds or renames a state" → grep confirms; the `ProcessWritePathTests` sentence → R1-4.
- status [dev] contract note → mod-006 contract note; `GetBoardEndpointTests` (keys, headers, targets); `GetProcessDefinitionEndpointTests` `ExpectedTypes`.
- status [qa], specs 22, 24-26 need nothing → my grep of `src/frontend/e2e`: no old header outside 20's Task rows and 26's comments.
- doc-007 note, "names the rest in a warning" → overstated → R1-1.
- doc-007 note, a write path must refuse a breach (only the seed creates states) → `ProcessDefinition.cs:91`, `SeededProcess.cs:69`.
- mod-001 contract § 1.2 and § 1.4 notes → `GetProcessDefinitionEndpointTests` `ExpectedTypes`.
- mod-001 design note, T-03/PC-2 sentence gone → `ProcessDefinitionPage.tsx:55-57`; page test `:206-211`.
- mod-001 US-1/US-2 notes, the seed refuses a breach and one colour → `SeededProcess.cs:69`; `ProcessDefinitionPage.test.tsx` one-colour test.
- mod-006 spec note, four columns, a null target only off-seed, `InProgress-2` gone → `BoardLayoutTests` (`Derive_ACategoryOnlyOneTypeHas…`); `GetBoardEndpointTests:46-48`.
- mod-006 US-4/US-5 notes → `GetBoardEndpointTests` (`Get_Card_SitsInItsColumn`, `Move_*`, `Move_ToAStateOfAnotherType_IsRefused`).
- mod-006 contract note → as above.
- mod-015 design note, seeded headers and hint «Новая» → `BoardLayoutTests`, `emptyBoardHint`. "§ 2.3's legend" is that section's category table (loose wording, no finding).
- E2E 23 header comment → 23.4. qa.md O-2 → true, but silent on the old-build window → R1-3.

Direction 2 (behaviour the diff changes, prose outside the diff), over `docs/`, `.claude/`, the AGENTS.md files, `src/frontend` and `src/backend`:
- Headers by state name. Grepped «Новые»/«В работе»/«Решённые»/«Завершённые» as headers, "label for k = 1", "колонке «Новые»", `InProgress-2` and "five columns". Unannotated hits → R1-2 (feat-001 US-6, mod-006 US-6, mod-006 spec `:304`, `:333`) and R1-5 (`boardsApi.ts:130`).
- «Разрешена» Resolved everywhere. Grepped "two categories", "different categor", "one label" and "разным категориям". Unannotated hits → R1-6. doc-001 BR-2, doc-002 US-3, doc-003 and doc-010 were declared left as written.
- Process-screen sentence. Grepped "разным категориям". Hits only in the annotated mod-001 design and the tests.
- Not swept: `prototype/` HTML (declared by the dev), and the historical plan, qa and review files of mod-001 and mod-006 (records of past runs).

### Commands run

- `dotnet test` Domain.Tests → 503/503.
- `dotnet test` Application.Tests → 550/550.
- `dotnet test` Infrastructure.Tests, filtered to the migration and Process → 38/38.
- `dotnet test` Api.IntegrationTests, filtered to Boards, Process and WorkItems → 391/391.
- `npx vitest run src/features/boards src/features/process` → 35 files, 518/518.
- R1-1's experiment: a throwaway `postgres:18-alpine` (removed afterwards), with `dotnet ef database update` at default verbosity and with `--verbose`.

### Resolution

The developer's fix pass, one pass for round 1. The fixes were prose in the
reviewer's reading. Where the prose makes a claim a test can hold (R1-1, R1-3,
R1-4) it got one; for R1-2, R1-5 and R1-6 the row says why it did not. No
runtime behaviour changed. Commits are on `bug/004-board-shared-state-one-column`.

| Finding | Status | Where |
|---|---|---|
| R1-1 major | fixed in b6a8df5; the optional read-time log declined | The runbook has a section "Migrations that report" (`docs/runbooks/local-migrations.md`): it says where the report lands (the server log) and that `database update` prints none of it, and hands over the check to run after the update, with what a row and an empty answer mean. The migration's remarks say the same and quote the `SELECT` verbatim. doc-007's note is narrowed to "names every process where a name still stands in two categories", and says that a rename breaking no rule is named nowhere. Tests: `MoveResolvedStatesToResolvedCategoryRunbookTests` pins the runbook's two statements and the remarks' quote to the SQL the migration runs (I broke the runbook's `HAVING` and both pins failed); `MoveResolvedStatesToResolvedCategoryMigrationTests` runs the runbook's own check on a database and gets C and only C. |
| R1-2 major | fixed in ff5cb63 | Superseded-in-part notes in mod-006 US-6 (every «Новые» reads «Новая») and feat-001 US-6 (the hint reads «…в колонке «Новая».»); mod-006 `spec.md` § Model's note extended to the «Новый элемент» row, the dialog's "last card of «Новые»" and OQ-2's «Завершённые». The test comment that misquoted US-6's title quotes it as written, with the D-3 reading beside it. No test: the copy is already held by `board.test.ts` (`emptyBoardHint`) and `GetBoardEndpointTests`. |
| R1-3 minor | fixed in b6a8df5 | The migration's remarks, the runbook and qa.md O-2 say that the move runs once per database and that its `UPDATE`, idempotent, is run by hand again after an older build wrote the old shape; the runbook quotes the statement. Test: the migration test creates organization D after the migration with the old shape, sees `database update` leave it, sees the runbook's check name D beside C, runs the runbook's `UPDATE`, and the check is back to C alone. |
| R1-4 minor | fixed in d3daf35 | `ProcessWritePathTests`, `StateNameRule` and `ProcessDefinition` now say what they pin (the aggregate's public surface; the aggregate's own creation). The status Log's old sentence cannot be edited, so a new Log line corrects it. The optional architecture rule is added: `ProcessStateWritesRules` keeps `state_definitions` in SQL, and tracked or bulk writes on the `StateDefinitions` set, out of the code outside the process sub-slice, with a non-vacuity test over sample sources (reads such as a join are not flagged). |
| R1-5 minor | fixed in 93afbcb | `BoardColumn.header`'s comment in `boardsApi.ts` states the D-3 rule. Comment only; the board tests hold the rule. |
| R1-6 minor | fixed in b544e78 | One-line notes in doc-005 BR-M6-5, doc-005 US-1 and doc-007 US-2, the places the state filter's implementer and the process screen's reader take their scenarios from. The rest (doc-002 `spec.md:66` and US-11:34, doc-001 US-10:21 and US-3:34) go on the status's left-as-written list, with a Log line. No test: notes in analyses that no code reads; the seed and the process-screen tests already hold the rule they point to. |

**Declined: R1-1's optional read-time log** (the API logs `StateNameRule.Breaches`
on a process read). It would report only when somebody opens a screen, and
again on every read: noise on the board read, silence the rest of the time, and
a handler gaining a logger for data that no code path of this product can break
today (only the seed writes states). The runbook step reaches the person who
applies the migration, which is who R1-1 is about, and R1-3 is covered by the
idempotent `UPDATE` plus the same check. When doc-007's customization adds a
write path, that path refuses the breach at the write (`StateNameRule`), which
is the better place; revisit the log then if a stand ever needs it.

Commands run for this pass: `dotnet test` Infrastructure.Tests on the migration
and runbook tests (3/3), Architecture.Tests (83/83), Domain.Tests on Process
(81/81); `npx prettier --check` and `eslint` on `boardsApi.ts`; the last line of
`bash scripts/verify.sh` is in the Log of `status.md`.
