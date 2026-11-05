# Review: doc-012-matrix-organization-model

> Condensed to fit the 32 KB budget; the uncut text, with every full Problem and Required resolution, is at commit 1af29413.

## Round 1 — requirements

Seven lenses (coverage, structure, access, views and sprints, navigation and decomposition, ADR consistency, cross-story consistency) read the spec, the eleven stories and the ADR-0028/0029 drafts, each in a clean context; verifiers tried to refute lens 1. One consolidator, who wrote none of the artifacts, judged every finding against the cited lines and merged them: 70 findings, 2 dropped, 68 merged into 34 entries (several narrowed). Result: 5 blockers, 19 majors, 10 minors. By fix kind: 27 edit, 3 rework, 4 human.

### Completeness: brief → where covered

Of 24 rows, the 10 partial or not-covered ones are kept. Twelve are covered (notes: "never changes" goes past rule 1 → R-24; filter options' source → R-16; area figure → R-4, sprint board → R-12; wording against the matrix → R-10; SC-10 is OQ-2). Brief § 13 / § 20 data / § 21.12 is waived (status.md); "status.md 3.3, the scope is the whole brief" points at R-7, R-8, R-33.

| Brief | Gap | Verdict |
|---|---|---|
| § 2 | "automations, notifications": audits say nothing; out-of-scope "notifications" has no reason | partial → R-33 |
| § 2 | split an entity's duties: OQ-1, ADR-0028 rule 3 drop the classification duty unsaid | partial → R-24 |
| §§ 3.2, 9, 14 | area membership, project participation: mapping to grants not stated | partial → R-30 |
| § 3.2 | area "visibility settings": out of scope, no human decision | partial → R-8 |
| §§ 3.2, 11.2 | area analytics, «Analytics» section: deferred to mod-008/mod-009, which do not cover areas (doc-005 D-15, BR-M6-1) | NOT COVERED → R-7 |
| § 7.1 | filter by date: «Целевая дата» only, no reason | partial → R-16 |
| §§ 7.1, 11.2 | blockers filter and figure: owner unnamed | partial → R-33 |
| § 8.5 | history of completed sprints: a deleted team's sprints unreachable (ADR-0028 only) | partial → R-34 |
| § 14 | one filtering logic: NFR Query shape binds the views only | partial → R-18 |
| § 16 UI/E2E, § 18.6 | nine UI/E2E checks, final run: not traced to the pipeline | NOT COVERED (trace) → R-33 |

### Dropped lens findings

- **L3-8** (minor, three inheritance edge-case scenarios): BR-MX-22 and ADR-0029 settle all three, ADR-0029 § 12 already owes tests, the stories are at 149-150 lines.
- **L5-10** (minor, trim deps, split line 11): optimizes delivery order, not a defect; lens 5 finds the serial chain sound; the orchestrator can reorder after gate-1.

### Parts narrowed away

- L1-1: "visibility rule missing" is wrong (BR-MX-21 covers every aggregate), and the edit would add a third dashboard level against doc-005 D-15 and BR-M6-1; became a human question (R-7).
- L1-3: the § 13 leg dropped (waived). L1-6: settled by doc-002 § Out of scope and doc-010 D-18; became an edit.
- L1-4: the pipeline already covers § 16 and § 18.6; option B would pre-decide each task's E2E question, against routing.md; a trace edit remains (R-33).
- L1-5: OQ-1 already defines the list's area filter; making feat-013 the founder would reverse doc-010 D-14 (R-18 keeps it).
- L1-7, L1-8: proposed wordings inaccurate; corrected in R-30, R-16.
- L3-5: US-9's removal outline covers the hidden-item refusal; only the unnamed exception remains (R-9).
- L4-1, L4-10: each became an edit. L6-3: the fate table already lists mod-016 BR-M1-34.

### R-1
Severity: blocker
Affects: BR-MX-12; US-2, US-3, US-4; ADR-0028 § 6 *Rule 8*
Problem: An unlink racing a save that sets the team had three prescribed outcomes, so the race tests could not pass; two unlinks naming each other deadlocked into a 500; destination answers were unspecified.
Fix: edit
Source: L2-1, L2-2, L2-9, L2-10 (US-2 part)
Status: resolved
Resolution: BR-MX-12 fixes both orders of the race («Команда не участвует в проекте.» after the unlink), the destination answers, «Изменён», and that a team deletion reaches deactivated projects. US-2, US-3 (8 items; two unlinks never a 500) and US-4 follow; ADR-0028 Rule 8 gets lock order (3) "then team id", the destination's link FOR SHARE, and a two-unlinks race.

### R-2
Severity: blocker
Affects: NFR Concurrency; US-5 "A grant removal and a request that uses it…"; ADR-0029 § 7 (Concurrency)
Problem: The NFR makes a grant removal vs a request using it a race with one refusal, but ADR-0029 § 7 lets both pass (no lock covers grants), so US-5's test could not fail.
Fix: edit
Source: L3-1
Status: resolved
Resolution: NFR Concurrency says an access change is decided by the access committed when the check runs; US-5 retitled with a testable Then; ADR-0029 § 7 says both orders pass and leaves a mutual-removal write skew to the organization admin.

### R-3
Severity: blocker
Affects: BR-MX-24; ADR-0029 §§ 1, 6, 7, 8; ADR-0028 § 7 *Addresses*, Consequences "Amends"; mod-007's sprint routes
Problem: BR-MX-24 answers only the project, team and area not-found before the 403, ADR-0029 § 8 also the sprint's and item's, so a «Читатель» saving a missing item gets 403 or 404; the restore exception and body ids were unanswered.
Fix: edit
Source: L3-2, L6-2
Status: resolved
Resolution: BR-MX-24 puts the sprint's and item's not-found before the 403, keeps the restore exception, names a grantee or new member as a body id («Участник не найден.»); ADR-0029 §§ 1, 6, 7, 8 and ADR-0028 § 7 (a sprint under its team) and *Amends* follow; US-5 pins 404, not 403.

### R-4
Severity: blocker
Affects: BR-MX-17; US-8 "A team linked to two areas adds no item to either", "The overview shows…"; US-9 last scenario
Problem: US-9 shows a linked team's Active sprint in the area overview whatever the project, US-8 says no figure holds an item of a project linked elsewhere; the two stories' tests fail each other.
Fix: edit
Source: L4-1
Status: resolved
Resolution: BR-MX-17 adds each linked team's Active sprint to the overview (brief § 8.8); US-8 excepts that figure from "no figure holds PD-US-1" and "each figure equals the total". It cites US-12 (R-12's story), not US-9.

### R-5
Severity: blocker
Affects: spec § Existing rules (mod-002 BR-M1-20); BR-MX-14; US-9; ADR-0028 § 6 *Rules 2, 3*
Problem: The spec keeps mod-002 BR-M1-20 (a deactivated project is frozen), but ADR-0028 makes a sprint close, a team deletion and an area deletion reach it, and the spec and stories say nothing.
Fix: edit
Source: L4-2, L2-8
Status: resolved
Resolution: The BR-M1-20 fate row names the three exceptions; BR-MX-14 says a deactivated project's items are moved and recorded but count on no board, outcome or overview figure until restore; US-12 gains the scenario, US-9's removal outline counts them as held, ADR-0028 § 10 agrees.

### R-6
Severity: major
Affects: BR-MX-3, BR-MX-6, BR-MX-8; § Roles (area «Управление»); § Operation completeness (Area, Area–project link, Area–team link); US-1, US-3; ADR-0029 § 4 (Link management), § 6 (Consequence writes)
Problem: BR-MX-6 asks «Управление» on both ends of an area link, but a move, an inheritance switch and a leaf-area deletion change or end the same reach with fewer checks; brief §§ 10.1, 10.4 are silent.
Fix: human
Decision: the human's, asked before the fixes
Source: L2-4, L3-4, L2-5, L3-6, L2-10 (US-1 part)
Status: resolved
Resolution: The human's answer A. BR-MX-3 (a switch or move away also needs the current parent's «Управление»; a create checks the parent, switch applied last), BR-MX-6 (create needs both ends, a project–team unlink both, an area link's removal either), BR-MX-8, the Operation completeness rows, BPMN A1, US-1, US-3, ADR-0029 §§ 4, 6 follow. Turning the switch off ends the actor's own inherited «Управление»: the self lock-out is bounded, not removed. US-10's "either end" was owed and is done (round 2).

### R-7
Severity: major
Affects: § Out of scope (area analytics); § Decomposition; US-10 "An area's page"; brief §§ 3.2, 11.2; status.md 3.3
Problem: The brief asks for area "shared analytics and reporting"; the spec defers it to mod-008/mod-009, which cannot own it (doc-005 D-15, BR-M6-1), and no open question puts the cut to the human.
Fix: human
Decision: the human's, asked before the fixes
Source: L1-1
Status: resolved
Resolution: Human R-7 A. § Decomposition gains `feat-NNN-area-analytics` (M; amends doc-005 D-15 and BR-M6-1; deps mod-009-dashboards-widgets, feat-NNN-area-board-and-overview, the navigation line); § Out of scope names it; US-10: "until `feat-NNN-area-analytics` there is no analytics section".

### R-8
Severity: major
Affects: § Out of scope (area visibility); BR-MX-21; brief § 3.2; status.md 3.3
Problem: The brief lists "visibility settings" among an area's properties; the spec's argument against a per-area setting does not cover the area itself, and no open question puts it to the human.
Fix: human
Decision: the human's, asked before the fixes
Source: L1-2
Status: resolved
Resolution: Human R-8 A. § Out of scope: the bullet reads "declined by the human (gate-1, 2026-10-17)", keeps its argument and states the accepted cost (a confidential program's area name stays readable by every member).

### R-9
Severity: major
Affects: Glossary "Link"; BR-MX-7; BR-MX-21; NFR Non-disclosure; § Operation completeness (link rows)
Problem: The Glossary and BR-MX-7 say a link grants nothing, yet an area–project link carries area grants; "a link has no fields" contradicts BR-MX-20; two stories accept refusals that depend on hidden data.
Fix: edit
Source: L2-3, L3-5
Status: resolved
Resolution: Glossary "Link", BR-MX-7 (an area–project link is the one path that carries access), BR-MX-21 (two named exceptions: a rename to a hidden project's name, a Planned sprint holding hidden items), NFR Non-disclosure and the link rows' Update cell ("nothing to edit") reworded.

### R-10
Severity: major
Affects: § Roles; BR-MX-22; BR-MX-23; ADR-0029 § 4; US-5
Problem: BR-MX-23 would give a «Участник» the role's change codes on a «Участники областей» project; § Roles says less than the matrix in four places, and no code covers a top-level move or reading an object's grants.
Fix: edit
Source: L3-3, L3-7, L7-2
Status: resolved
Resolution: § Roles rows (Member, Project, Area «Управление» with R-6 A, Team «Планирование») follow the matrix; BR-MX-22 names the sprint-close exception; BR-MX-23's role bundle applies always on the organization, areas, teams and sprints, on a project only when «Организация»; ADR-0029 § 4 adds the top-level move on `area.create` and a read-code bullet; US-5 cites US-12 and adds the refused sprint set.

### R-11
Severity: major
Affects: ADR-0028 § 6, § 8; ADR-0026 rule 7; BR-MX-12
Problem: ADR-0026 rule 7 makes the status the only cross-module contract, but ADR-0028 *Rule 2* and BR-MX-12 write other modules' rows in one transaction with no named write path.
Fix: edit
Source: L6-1
Status: resolved
Resolution: ADR-0028 § 6 gains *Rule 7* (one in-process write contract per owning module, called by the Organizations handler in its transaction, locking in *Rule 8*'s order), a narrow exception to ADR-0026 rule 7; § 8 points to it. The amends lists are R-23's.

### R-12
Severity: major
Affects: BR-MX-14; § Operation completeness (Sprint); § Decomposition (`mod-007-iterations`); US-9; § Stories
Problem: `mod-007-iterations` is to build "the team sprint board", but no artifact defines it, so QA cannot test it; US-9 is at 150 lines.
Fix: rework
Source: L4-5
Status: resolved
Resolution: BR-MX-14 defines the sprint board (Active sprint, «У команды нет активного спринта.», one level, process columns, team order, moves as on the team board, no reorder, totals after visibility) as a view in the team's «Спринты»; US-9 keeps the lifecycle (85 lines), the new `US-12-items-in-a-team-sprint.md` (130 lines) the rest; the story table has the US-12 row.

### R-13
Severity: major
Affects: BR-MX-14, BR-MX-15; spec § Existing rules (doc-004 BR-M5-4, -11); ADR-0028 § 6 *Rule 9*, § 10; US-9
Problem: A close moves "every item outside the Completed category", so an «Удалена» item rides from sprint to sprint unseen and blocks its Planned sprint's removal; the outcome, reopening, re-dating and name rules were unstated.
Fix: edit
Source: L4-3, L4-4, L4-6
Status: resolved
Resolution: BR-MX-14 (a close moves the Proposed, In Progress and Resolved categories; the outcome is the Completed category at close; Completed and «Удалена» items keep the Closed sprint, a reopened one too; a Planned sprint with only «Удалена» items is removable; Active dates not clearable, Closed dates fixed), BR-MX-15 (name rules), the doc-004 fate row, ADR-0028 Rule 9 and § 10, US-9, US-12 follow. Two refusal texts written: «Даты завершённого спринта изменить нельзя.», «Название спринта — от 1 до 100 символов.»

### R-14
Severity: major
Affects: BR-MX-14; ADR-0028 § 10 (and § 6 *Rule 8*'s sprint binder); US-9
Problem: Only one sprint race is named; four more can break BR-MX-13/-14 or end in a 500, and ADR-0028 contradicts itself on whether sprint creation locks the team row.
Fix: edit
Source: L4-7 (races), L2-7
Status: resolved
Resolution: BR-MX-14 names the races with "one order and no server error", not "one loser" (a close vs an unlink or a team change refuses neither). ADR-0028: creation locks the team `FOR SHARE`, then the counter row (lock-order item (4)); § 10 gains *Sprint races*. US-12 has a six-row race outline; US-9 gains the numbering and creation-vs-deletion races.

### R-15
Severity: major
Affects: BR-MX-17; US-8; US-11 "The level, the filters and «Включая дочерние области» are remembered…"
Problem: US-8 has one «Включая дочерние области» switch per area while its data paragraph and US-11 remember it per view, so the overview can match neither; the default and area create commands were unsaid.
Fix: edit
Source: L4-8, L4-14
Status: resolved
Resolution: BR-MX-17: one choice per user and area, shared by backlog, board and overview, on until turned off, no create command in area views; US-8 scenarios and US-11's remember scenario (switch per area, level and filters per view) follow.

### R-16
Severity: major
Affects: US-11; brief § 7.1
Problem: US-11 does not say where filter options come from, so the twin test cannot be written; «Спринт» has no «Без спринта», and the brief's "date" became «Целевая дата» without a reason.
Fix: edit
Source: L4-9, L1-8
Status: resolved
Resolution: US-11 gains the option-source list (project, team, assignee, sprint with «Текущий спринт» and «Без спринта», type, status; no counts; «Целевая дата» is the brief's date) and the «Без спринта» Examples row; the sprint scenario pins Closed sprints and newest-first order.

### R-17
Severity: major
Affects: NFR Client load, NFR Volumes; US-7 "Large team and project views"
Problem: The 5 000-item client budget cannot bound whole project levels at the spec's volumes, US-7's 2 s check never measures the grant path (P-4), and doc-004's 5 s bulk-write budget has no successor.
Fix: edit
Source: L4-10, L4-11 (US-7 part), L4-7 (budget part)
Status: resolved
Resolution: NFR Client load states the 5 000-item assumption and the accepted risk above it; NFR Volumes sizes the area view at 10 or more projects and carries over the 5 s all-or-nothing bulk write for close, unlink and team deletion; US-7's large-view Given runs at the NFR volumes (50 projects of all three visibilities, 50 grants).

### R-18
Severity: major
Affects: NFR Query shape; § Decomposition (rows `mod-NNN-work-item-owning-team`, `feat-NNN-team-surfaces-across-projects`, `feat-013-board-backlog-filter`); spec § Existing rules (doc-003 US-2 row); doc-010 D-14; doc-003 BR-M4-22
Problem: Brief § 14 forbids duplicated filter logic, but the NFR binds only the views; feat-013 "drops its feat-003 dependency", cutting the human's D-14 tie (BR-M4-22) with no fate row.
Fix: edit
Source: L1-5, L5-9, L4-11 (Query shape part)
Status: resolved
Resolution: NFR Query shape counts statements against rows, projects, teams, areas, tree depth and grants and binds the views and the «Рабочие элементы» list to one filter specification, founded by the first line that adds a filter and extended by feat-003 and feat-013 (D-14); rows 2, 4, 10 and the doc-003 US-2 row (adds BR-M4-22, the retired area filter) follow.

### R-19
Severity: major
Affects: § Decomposition (lines 1, 3, 4, 6, 8, 9, 11; Notes for the queue); § Operation completeness (Surface header); ADR-0028 § 7 *Old addresses*, Consequences (delivery table); US-10
Problem: Six new surfaces have no interim home before the navigation line, line 11 gives no fate to the old settings addresses, and "feat-038 is unaffected" is wrong.
Fix: rework
Source: L5-1, L5-5
Status: resolved
Resolution: Option A. The Surface header and the Task cells of lines 1, 3, 4, 6, 7, 8, 9 name their interim entry and address; line 4's Breaks adds the switcher's context model; ADR-0028 § 7 and its Consequences rows, US-10's retired-kind clause and the feat-038 note follow. Spec cells only, no new line.

### R-20
Severity: major
Affects: spec § Stories (paragraph and depends-on column); § Decomposition (rows 2 and 4); the depends-on headers of US-2, US-3, US-4, US-9; US-6; US-7
Problem: The depends-on column contradicts the delivery order, the owed-by rule omits an owning team and a multi-project team so US-7 cannot run on line 1, and rows 2 and 4 both claim "US-7, team part".
Fix: edit
Source: L5-2, L7-4
Status: resolved
Resolution: § Stories names the owning team and the multi-project team in the owed-by list; story-table rows US-2 → US-4, US-3 → US-1, US-2, US-4, US-4 → "-" (US-9 and US-12 are B3's); US-10 gains US-12; rows 2 and 4 say what each takes from US-7. Story headers and US-6/US-7 markers belong to the story batches.

### R-21
Severity: major
Affects: spec § Existing rules (mod-002 BR-M1-23, -24 row); US-10; feat-001 BR-H-16 (the human, D-7); mod-015 BR-6, -11, -18, -19; ADR-0024 §§ 1, 4
Problem: US-10 overturns shell rules the fate table never names, says nothing of how a user changes organization, and renames the board «Доска», reversing the human's feat-001 BR-H-16 («Канбан»).
Fix: edit
Source: L5-3, L5-4
Status: resolved
Resolution: The BR-M1-23/-24 fate row is split into replaced (mod-015 BR-6, BR-11's project and team levels, BR-18, ADR-0024 §§ 1, 4) and kept (organization level with «Все организации», BR-19, «Главная», feat-001 BR-H-15/-16, «Канбан»); US-10: «Доска» → «Канбан», the switcher scenario adds the organization part.

### R-22
Severity: major
Affects: § Decomposition (Task, Rewrites and Breaks cells); the queue lines mod-011, feat-003, mod-008, feat-023, feat-024, feat-008, bug-003, tech-019
Problem: Several Decomposition rewords cannot be entered as written (no scope, direction or deps), the nine new lines carry no epic, bug-003 and tech-019 overlap line 11, and line 7's Breaks cell cites the wrong route.
Fix: rework
Source: L5-7, L5-12, L6-3 (Breaks-cell part)
Status: resolved
Resolution: The cells state each reword's scope, deps and size: mod-011 (sprint backlog and «Задачи» taskboard; deps mod-007-iterations, feat-009), feat-003/mod-008 (area filter and "Under" dropped, OQ-1 A), feat-023/-024 (deps; feat-023 becomes M), feat-008, an epic on every row, row 11 (bug-003, tech-019), row 7 Breaks (mod-016 api-contract § 3.4, inventory row 34), feat-013's deps.

### R-23
Severity: major
Affects: ADR-0028 header Context line, § 6, Consequences ("Amends", "Retires or replaces"); ADR-0029 Consequences; spec § Existing rules (doc-006 BR-M1-7 row, ADR-0024/ADR-0026 row)
Problem: The ADRs' lists of retired rules and per-line duties drifted from the spec's fate table; ADR-0028 also changes ADR-0026 rules 5 and 7 unlisted.
Fix: edit
Source: L6-3, L6-4
Status: resolved
Resolution: ADR-0028 Retires points at the spec's fate table; Context and Amends add ADR-0026 rules 5, 7 and the index-line instruction; § 6 gains *Rule 5*. ADR-0029 Consequences name the fate table as authority, give mod-007 and the navigation line their set-form duties, tie the matrix's visibility cross to `feat-NNN-project-visibility`; the spec fate rows follow.

### R-24
Severity: major
Affects: OQ-1, OQ-4, OQ-5; BR-MX-9; ADR-0028 § Problem, § 1, § 3
Problem: OQ-1 calls team ownership the item's «Область» "one real duty" though the audits count the classification path as a second (brief § 2); OQ-5 omits doc-011's move/copy rows against BR-MX-9; OQ-4's option B breaks backend.md's never-edit rule unsaid.
Fix: edit
Source: L1-3, L1-10, L5-13, L6-5
Status: resolved
Resolution: OQ-1 names both duties, the departure from brief § 2 and the rejected option C; OQ-4 (B) and OQ-5 carry the added text; OQ-1..OQ-6 marked answered A (gate-1, 2026-10-17). BR-MX-9 and ADR-0028 § 1 say no structure operation changes an item's project; ADR-0028 § 3 and Problem reworded, Departures line added.

### R-25
Severity: minor
Affects: US-6 "A Restricted project linked to two areas and a team stays protected"; US-3; US-10
Problem: US-3 and US-10 make the link multiselect add-only, but US-6 treats it as a set editor, which needs another API and would skip SC-09's destination choice.
Fix: edit
Source: L7-1
Status: resolved
Resolution: US-6 says X is neither a linked row nor an option of an add multiselect, and no link or unlink U sends removes a link of X; R-20's two owed-by lines in US-6 applied.

### R-26
Severity: minor
Affects: BR-MX-10; US-4 "A child starts with its parent's team, else the project default"
Problem: BR-MX-10 lets the creator change the team before saving while US-4 gives a row's or checklist's add no «Команда» field; a checklist add on another team's card has two answers by precedence.
Fix: edit
Source: L2-6
Status: resolved
Resolution: BR-MX-10 rewritten with the child first ("as stored when the child is saved") and no «Команда» field on a row's or checklist's add; US-4's child scenario gains the unlink-race line and "even on a card opened over another team's page"; BPMN W2 reordered.

### R-27
Severity: minor
Affects: BR-MX-18; spec § Existing rules (the two mod-006 order rows); US-7 "An item that changes team…"
Problem: The fate table keeps mod-006 BR-M4-13 per view while BR-MX-18 replaces its order; "then by identifier" reads as a text compare; the end of mod-006's "return to a former place" is unrecorded.
Fix: edit
Source: L4-12, L5-8
Status: resolved
Resolution: BR-M4-13 is retired with BR-M4-24, whose Why also ends the return to a former place; BR-MX-18 orders ties by project key and number; US-7's tie-break scenario adds A 9 and A 10, and its team-change scenario the set-back-to-MARS case.

### R-28
Severity: minor
Affects: BR-MX-19; ADR-0028 § 11; OQ-2
Problem: Under OQ-2 (A) no line can make processes differ, so BR-MX-19's category-column rule can be neither built nor tested, and it disagrees with ADR-0028 § 11.
Fix: edit
Source: L4-13, L2-12
Status: resolved
Resolution: BR-MX-19 gives every view the organization process's columns for the level (OQ-2 A) and leaves state-category columns to the line that lets processes differ; ADR-0028 § 11 matches (the projects a view can show and the user may see).

### R-29
Severity: minor
Affects: spec § Business process (BPMN)
Problem: The BPMN's cycle check follows "Create areas", where it cannot fire; the move, depth and sibling-name refusals are undrawn; only the link step shows a permission gate.
Fix: edit
Source: L2-11, L3-9
Status: resolved
Resolution: Node texts A1, A2, A2x, G1, W1, SP1, U1 changed, no line added; A1 names the current parent's «Управление» for a move (R-6); W2 follows BR-MX-10.

### R-30
Severity: minor
Affects: Glossary "Grant / participant"; BR-MX-26; § Operation completeness (Grant, Team membership); US-10 "A team's page spans its projects"
Problem: Brief § 9's area membership and project participation are folded into grants and the organization role unsaid, and one «Участники» label covers a team's members (who get nothing) and the grants on it.
Fix: edit
Source: L1-7, L7-3
Status: resolved
Resolution: Glossary "Grant / participant" states the mapping; BR-MX-26 lists teams and grants only; the Grant and Team membership Surface cells follow; US-10: team sections end in «Участники и доступ», with a VENUS check (Olga a member with no role, Pavel's grant).

### R-31
Severity: minor
Affects: BR-MX-27, BR-MX-28; ADR-0028 § 7 (*The switcher*, *Breadcrumbs*); US-10
Problem: Area names are unique only among siblings yet shown bare, ADR-0028's "may show ancestors" and US-10 disagree, and several US-10 criteria have no checkable value.
Fix: edit
Source: L5-11, L7-5, L5-6
Status: resolved
Resolution: BR-MX-27 names an area by its path outside the tree; BR-MX-28 fixes the group order and the browser as the place of the choice; ADR-0028 § 7 follows. US-10 (149 lines) adds Irina, «Склад» linked to A and C, counts as numbers, paths, the list order and D's area-link command.

### R-32
Severity: minor
Affects: spec § Stories; BR-MX-21, BR-MX-24; the not-found and 403 copy in US-1..US-10; docs/error-codes.md
Problem: The registry spells the not-found texts with a full stop but the BRs and most stories omit it, so one deleted team answers two strings; refusals are written two ways.
Fix: edit
Source: L7-6
Status: resolved
Resolution: § Stories closes with the registry rule. Corrected from the proposal: «Недостаточно прав» has no full stop in docs/error-codes.md, so the rule says "spelled as the registry spells it" and lists «Организация не найдена.» too.

### R-33
Severity: minor
Affects: § Out of scope (blockers; the last bullet); NFR Regression; brief §§ 2, 7.1, 11.2, 16, 18.6
Problem: Three brief items lack a reason or owner: the blockers cut, "notifications" and automations (none in `src/backend`), and NFR Regression's "every test stays green", which takes in E2E specs `verify.sh` never runs.
Fix: edit
Source: L1-6, L1-11, L1-4
Status: resolved
Resolution: The blockers bullet names doc-011 and doc-010 D-18 as the owner; the last bullet gives the reason for notifications and automations; NFR Regression adds the E2E trace, citing SC-01..SC-09 because SC-10 is deferred by OQ-2 A.

### R-34
Severity: minor
Affects: BR-MX-14; ADR-0028 § 6 *Rule 2*; US-9; brief § 8.5
Problem: Brief § 8.5 says completed sprints' history must survive, but a team deletion makes its sprints "unreachable with the team" (ADR-0028 *Rule 2*), and the spec states this nowhere.
Fix: human
Decision: the human's, asked before the fixes
Source: L1-9
Status: resolved
Resolution: The human's answer B (gate-1, 2026-10-17): BR-MX-14 says a deleted team's Closed sprints and outcomes stay listed, read-only, on the «Спринты» of the projects whose items they record; BR-MX-13, the Sprint row, ADR-0028 Rule 2 and § 10 *The archive read* and US-12 follow. US-10 had no project «Спринты» section; it was added in round 2.

## Round 2 — delta

A clean-context reviewer who wrote none of the artifacts checked every round-1 Resolution against the current spec.md, the twelve stories (US-12 new), the ADR drafts and the human's answers of 2026-10-17. No shell, so `git diff f10005f6` was not run; each changed section was read in full.

- **Round-1 entries.** All 34 hold; none reopened. The two cross-batch items left owed are done: US-10's link row says "either end" for an area link (R-6), and the project page lists «Спринты» as the read-only Closed-sprint list (R-34).
- **The human's answers are applied wherever they bite.** R-6 A in BR-MX-3, -6, -8, § Roles, the Area and link rows, BPMN A1/L2/U1, US-1, US-3, US-10, ADR-0029 §§ 4, 6 (a project–team unlink still needs both ends). R-7 A: `feat-NNN-area-analytics` sits after `mod-009-dashboards-widgets` (release 3, epic `core/queries-dashboards`), cited by § Out of scope and US-10. R-8 A in § Out of scope; R-34 B in BR-MX-13/-14, ADR-0028 Rule 2 and § 10, US-12, US-10. OQ-1..OQ-6 are marked answered A; R-39 removes the leftover option-B branches.
- **Parallel batches** left BR ids, the story table and the depends-on headers consistent; only the US-9/US-12 split left references behind (R-36).
- **Budgets** (`wc -l`): spec.md 248 lines (~250); every story at or under 150; every round-2 edit line-neutral.

### R-35
Severity: minor
Affects: US-12 (the race outline's close-vs-deletion row; "A deleted team's Closed sprints stay readable…"); BR-MX-14; ADR-0028 § 10 *The archive read*
Problem: The US-12 close-vs-deletion row cannot pass (its Given gives an empty outcome, listed on no «Спринты»), the archive scenario's closing order is unstated, and BR-MX-14 was silent on an empty-outcome sprint, narrowing the human's R-34 B wording.
Fix: edit
Status: resolved
Resolution: The row reads "the close commits first, moving login and integration to «Спринт 13», and the deletion then leaves both «Без команды» with no sprint"; the archive Given says «Спринт 11» closed after VENUS's «Спринт 4»; BR-MX-14 appends "one whose outcome recorded no item records no project and is listed on none". The human sees this at gate-1; only the name and dates of a deleted team's sprint that completed nothing are lost.

### R-36
Severity: minor
Affects: spec § Stories (US-11 row); US-11 header; US-4 (sprint block); US-8 (overview scenario); ADR-0029 Consequences (`mod-007-iterations` bullet)
Problem: After the US-9/US-12 split four references (US-11's dependency, US-4's marker, ADR-0029's mod-007 duty, US-8's sprint clause) still pointed at US-9 alone.
Fix: edit
Status: resolved
Resolution: US-11's row and header depend on US-7, US-8, US-9, US-12; US-4 reads "(US-9, US-12)"; US-8 "(owed by `mod-007-iterations`, US-12)"; ADR-0029's mod-007 bullet adds "the step-3 gate on a project's «Спринты» (US-6, US-9, US-12)".

### R-37
Severity: minor
Affects: ADR-0028 § 7 *Old addresses*; ADR-0028 Consequences (navigation row); spec § Decomposition (`mod-007-iterations` row); US-10
Problem: ADR-0028 § 7 lists "sprints" among settings pages the navigation line retires, but `…/projects/:p/sprints` is a live section address.
Fix: edit
Status: resolved
Resolution: § 7 drops "sprints" from the retired pages and says `…/teams/:t/sprints` and `…/projects/:p/sprints` stay, only their interim entries move; the Consequences navigation row says the same.

### R-38
Severity: minor
Affects: ADR-0029 § 4 (Link management bullet; the `organizations.area.create` row); BR-MX-3 (R-6 A)
Problem: ADR-0029 § 4 names no code for moving a top-level area under a parent or switching its inheritance (BR-MX-3), so the matrix test has no row.
Fix: edit
Status: resolved
Resolution: The § 4 bullet reads "…on the area's current parent, or `organizations.area.create` when the area is top-level (BR-MX-3)"; that code's Covers cell adds "a move to or from the top level, and «Наследовать доступ» on a top-level area".

### R-39
Severity: minor
Affects: ADR-0028 § 3 and § 6 *Rule 8*; ADR-0029 §§ 2, 6, 7; US-6 preamble; BR-MX-3
Problem: Text left by the fix batches: *Rule 8*'s "one loser", option-B branches for rejected options, an incomplete body-id list, an ambiguous "checked on the parent".
Fix: edit
Status: resolved
Resolution: *Rule 8* opens with "ends in one order and no server error; the later request is refused, or completes on what the earlier one left"; the four option-B branches are removed (ADR-0028 § 3 records that the human chose A); ADR-0029 § 7's body-id list adds a grantee and a new team member; BR-MX-3: «Управление» is checked on the parent for everything the form sets, the switch included, and the switch is applied last.
