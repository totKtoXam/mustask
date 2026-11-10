# Review: mod-007-iterations

## Round 1 — requirements

Reviewed: `spec.md` and `stories/US-1..US-8` at the stage-1 state, against
doc-012 (BR-MX-9..-24, US-9, US-10, US-12, § NFR, § Decomposition), doc-004
(BR-M5-4, -11 and the rules doc-012 retired), doc-013 (BR-NE-4, the section
table), feat-040 (merged, `stage: complete`), feat-043 and its second line
(the menu table in `src/frontend/src/app/navigation/addressModel.ts`),
mod-019 (merged: § Roles, § Out of scope), mod-005 (a deleted item is
read-only) and the queue lines feat-008, feat-009, mod-011, feat-015,
feat-041, feat-042, doc-016, feat-054.

### Rule → story mapping

| Source rule | Spec rule | Stories | Note |
|---|---|---|---|
| BR-MX-13 (sprint and team) | BR-1, BR-2 | US-4 | covered |
| BR-MX-12 (unlink, delete clear pointers) | BR-2 | US-4, US-5 races, US-7 | covered |
| BR-MX-14 lifecycle, dates | BR-3, BR-4 | US-1, US-2 | covered |
| BR-MX-14 join only open sprints | BR-5 | US-4 | covered; re-save with a kept Closed pointer added (R-6) |
| BR-MX-14 close, outcome | BR-6 | US-5 | covered; a Closed sprint's page with its outcome added (R-5) |
| BR-MX-14 "no close moves it" (reopened item) | BR-6 | US-5 | added (R-6) |
| BR-MX-14 removal | BR-8 | US-2 | covered; a Completed-only row added |
| BR-MX-14 deactivated projects, BR-MX-21 | BR-9 | US-2, US-5, US-6, US-7 | covered; visibility clauses need feat-041 (R-4) |
| BR-MX-14 sprint board; BR-MX-18, -19 | BR-10 | US-6 | covered; needs feat-040's team order (R-4) |
| BR-MX-14 deleted team's archive | BR-11 | US-7 | covered |
| BR-MX-14 races | BR-12 | US-1, US-2, US-5 | covered; close vs close, start vs remove added (R-11) |
| BR-MX-15 numbering, names | BR-7 | US-1 | covered |
| BR-MX-17 overview's Active sprints | - | US-8 | covered; OQ-2 (R-19) |
| BR-MX-23; mod-019 § Roles, § Out of scope (sprint codes) | BR-13 | US-1, US-2, US-5 | role names and the code fixed (R-1, R-2) |
| BR-MX-24 answer order | BR-13 | US-1, US-3, US-4, US-7 | covered; the full order is feat-041's |
| doc-004 BR-M5-4, -11 (kept for Planned) | BR-4 | US-1 | covered |
| doc-004 BR-M5-13, -15, -16, -17 | - | Out of scope (feat-009, feat-011, mod-012, mod-013) | covered |
| doc-012 US-10 sprint clauses (team and project sections) | - | US-3, US-7 | covered; team order fixed (R-8) |
| mod-005 (a deleted item takes no write) | - | - | not stated (R-15) |
| doc-012 NFR Query shape, Non-disclosure, Tenant isolation, Volumes | NFR | - | were missing, added (R-7) |

### R-1
Severity: major
Affects: § Roles, US-1, US-3, US-5, OQ-1
Problem: The spec uses doc-012's team role names «Планирование» and «Управление». mod-019 (merged) renamed them by the human's decision of 2026-11-06 (OQ-10): the team «Участник» is doc-012's «Планирование», «Менеджер» is «Управление», and the team gained a «Читатель» role that "does not touch sprints" (mod-019 § Roles, US-1 «The team «Читатель» only lists the person»). Built as written, the UI would show retired names, and the team «Читатель» refusal had no test.
Required resolution: Use mod-019's names everywhere, prefixed with "team"; add the team «Читатель» to the refusals.
Status: resolved
Resolution: § Roles rewritten with the mapping and the team «Читатель»; US-1's cast and permission scenario (team «Читатель» on MARS added to the 403 list, «Недостаточно прав» named), US-3, US-5's cast and race row reworded. OQ-1 (2) still uses the old names: R-18.

### R-2
Severity: major
Affects: BR-13
Problem: mod-019 § Out of scope gives "Sprint codes: `mod-007-iterations`", but the spec says only "managing sprints needs the team code" — which code, in which bundles, and that it is this line's to register, is not stated. OQ-1's recommendation A also depends on "every sprint check reads the team's codes through the policy", which was not a rule.
Required resolution: State that this line registers the sprint-management code in the organization admin's and the team «Участник»/«Менеджер» bundles only, checked through the effective-permission policy (ADR-0029).
Status: resolved
Resolution: BR-13 reworded; the code string is the design's (ADR-0029 fixes code strings).

### R-3
Severity: major
Affects: § Decomposition
Problem: The decomposition replaces `mod-007-iterations` by six lines of size M/S. (a) routing.md § Size: L is "the first task in a slice, the one that creates it"; row 1 opens the iterations slice (module boundary, sprint data model, first migrations, API), so it is L, and calling it M understates it. (b) feat-008, feat-009, mod-011, mod-013, feat-013 and mod-008 all name `mod-007` in deps; replacing the line leaves them pointing at nothing, and the spec does not say which new line each needs (feat-008, feat-009, feat-013, mod-008 need the pointer; mod-011 the board; mod-013 the lifecycle). (c) The backlog's invariant "for every M the module's founder (L) is reachable through dependencies" breaks for M5's Ms if no L founds the module. Precedent: feat-040 kept itself as the L and spun out only the extra line.
Required resolution: Keep `mod-007-iterations` as the L founding line (its id and its dependents' deps unchanged) carrying the slice-opening stories, and spin out only the rest as M/S lines that depend on it; or, if the six-line split stays, name the founder L and rewrite each dependent's deps explicitly. Which stories ride in the L is question Q-8 below. Back to the business analyst.
Status: resolved
Resolution: Round 2: the human answered OQ-8 A; mod-007 stays the L line with US-1..US-5, the dependents keep `mod-007`; US-6 and US-7 are M lines depending on it (§ Decomposition, queue edits 1-3, 6).

### R-4
Severity: major
Affects: § Decomposition rows 1-6, status.md deps, US-2, US-5, US-6, US-7, US-8
Problem: (a) feat-041 (project visibility, not started, L) is listed only on row 5, yet «Ограниченный» / "hidden from him" clauses sit in US-2 (removal row), US-5 (close of hidden items, the new outcome scenario), US-6 (board counts), US-7 and US-8. Until feat-041 merges every project is «Организация» (feat-040 § Roles), so those clauses cannot be tested in rows 1-4. (b) feat-040 is not unknown: it is merged (`stage: complete`) and is what the sprint board needs — the team's one order across projects (feat-040 BR-TS-6), the card read and written in its own project (BR-TS-4, -5), «Проект» on every card (BR-TS-3). It is attached to row 1, which needs none of it, and missing from the board row. Being merged, it blocks nothing either way.
Required resolution: Put feat-040 on the board row (or drop it as satisfied). For feat-041, either add it to every row that carries a visibility clause, or mark those clauses "owed by feat-041" per doc-013 BR-NE-4 and the feat-041 line's own "non-disclosure on every surface that exists when this line merges". Which of the two is question Q-7. Back to the business analyst after the human's answer.
Status: resolved
Resolution: Round 2: OQ-7 B; mod-007's deps are feat-040 only, the visibility clauses are marked owed by feat-041 in US-2, US-3, US-5, US-6, US-7; the merge-order half was added in round 2 (R-21).

### R-5
Severity: major
Affects: US-5, US-3, BR-6
Problem: BR-MX-14 says "A Closed sprint's page and figures show its outcome", and US-5 claims "shippable: yes — … the record is kept, readable on the sprint's own page in the list", but no criterion opened a Closed sprint or showed its outcome anywhere except the project archive (US-7). The outcome was write-only in the team's surfaces.
Required resolution: A scenario that opens a Closed sprint from the team's list and shows its outcome with totals, after visibility.
Status: resolved
Resolution: US-5 gains «A Closed sprint shows its outcome» (items, SP, totals, rename only; a hidden project's item drops out). Its visibility half is subject to R-4.

### R-6
Severity: major
Affects: US-4, US-5, BR-5, BR-6
Problem: BR-MX-14 keeps a Completed or reopened item in its Closed sprint "until a save changes it", while BR-5 says items join only Planned and Active sprints. Nothing told the implementer that the join check applies only when the pointer changes: a naive validation refuses every later save of every completed item («Спринт завершён: в него нельзя добавить элемент.»). doc-012's "no close moves it" was also untested.
Required resolution: A scenario where an item keeping a Closed sprint is saved untouched, then moved out; a scenario where a later close does not move it.
Status: resolved
Resolution: US-4 «An item that keeps a Closed sprint is saved like any other»; US-5 «A later close never moves an item that kept a Closed sprint».

### R-7
Severity: major
Affects: § Non-functional requirements
Problem: (a) doc-012's binding NFRs for every new surface were absent: Query shape (a statement-count test per surface), Non-disclosure (a with/without-X comparison per surface), Tenant isolation, and the 2 s team-view budget for the sprint board. (b) "A team with 1 000 sprints lists in ≤ 2 s" is invented (doc-012 sets no sprint count) and contradicted OQ-5 B, which says no paging breaks it. (c) The audit NFR ("every sprint change and the close's item moves record who and when") is scope creep into feat-015, which owes the history/audit ADR and records "the sprint pointer's changes and sprint acts (mod-007)"; building it here pre-empts that ADR.
Required resolution: Add the doc-012 NFRs; mark the 1 000-sprint figure as this spec's own, approved with OQ-5; reduce the audit line to the fields the stories need (created and closed by/at) and leave history to feat-015.
Status: resolved
Resolution: NFR section rewritten accordingly; the 5 000-item close budget kept (doc-012 NFR Volumes).

### R-8
Severity: minor
Affects: US-3
Problem: The section-order scenario read «Обзор», «Канбан», «Бэклог», «Спринты», «Проекты», «Участники и доступ», but the team menu today is «Обзор», «Канбан», «Бэклог», «Рабочие элементы», «Проекты», «Участники и доступ» (addressModel.ts, feat-043's second line). The criterion would fail on the existing page. It also used "Olga", who is not in the cast.
Required resolution: Insert «Рабочие элементы»; keep doc-012 US-10's "after «Бэклог», before «Проекты»"; use a cast member.
Status: resolved
Resolution: US-3 reads «Обзор», «Канбан», «Бэклог», «Рабочие элементы», «Спринты», «Проекты», «Участники и доступ»; Vera instead of Olga. The design may argue «Спринты» before «Рабочие элементы» at gate-2; both satisfy doc-012 US-10.

### R-9
Severity: minor
Affects: US-2, US-7
Problem: "gone from every list and filter" (US-2) and "a filter by «Спринт 11» finds nothing" (US-7) cannot be tested here: the sprint filter is feat-008's (list) and feat-013's (views).
Required resolution: Assert on surfaces this line builds.
Status: resolved
Resolution: US-2 "every list and sprint picker"; US-7 "their «Спринт» reads empty".

### R-10
Severity: minor
Affects: US-6
Problem: «The board follows a start and a close without a page reload» had Vera "reopen the board" — the title promised live update, the step tested a re-read; a tester cannot tell which. The board's create command and what a card opens were not stated (the team board offers «Новый элемент», feat-040 BR-TS-9).
Required resolution: Make it a next-read criterion; state that the sprint board creates nothing and a card opens in its own project.
Status: resolved
Resolution: Scenario renamed «The board's next read follows a start made elsewhere»; «The sprint board creates nothing» added.

### R-11
Severity: minor
Affects: US-2, BR-12
Problem: Races listed only doc-012's pairs. Two closes of the same sprint, and a start against a removal of the same sprint, are just as reachable from two tabs and had no criterion.
Required resolution: One scenario for the lifecycle-against-lifecycle races.
Status: resolved
Resolution: US-2 «Two lifecycle acts on one sprint end in one order». Removal and start by Vera (403) also covered.

### R-12
Severity: minor
Affects: BR-9
Problem: BR-9 said "a close, a removal or a move touches items the actor cannot see … and discloses nothing"; a removal does not touch hidden items — it is refused because of them, BR-MX-21's named exception.
Required resolution: Reword.
Status: resolved
Resolution: BR-9 names the close, team change and deletion, and the removal refusal as the one exception.

### R-13
Severity: minor
Affects: US-3, US-7, US-5, § NFR
Problem: «У команды пока нет спринтов», «Завершённых спринтов с работой этого проекта пока нет» and «Без спринта» are invented by the analyst (doc-012 has none of them), stated as fixed copy. About a dozen new error texts from doc-012 (US-1, US-2, US-4, US-5) are not in `docs/error-codes.md` and the spec did not say who registers them.
Required resolution: Mark the invented copy as proposals the design fixes (feat-040 precedent); say who registers the error texts.
Status: resolved
Resolution: NFR "Interface copy" bullet. The doc-012 texts stay fixed.

### R-14
Severity: minor
Affects: US-4
Problem: The card's slot is labelled «Итерация» today (feat-029 design § 4.8, `cardSlots.ts`, `WorkItemCard.slots.test.tsx`); the stories call the field «Спринт» without saying the label changes, so the design and the feat-029 slot test would disagree.
Required resolution: State the label change; the design names the feat-029 test it rewrites (P-10 note in feat-029's status.md at design time).
Status: resolved
Resolution: US-4's setting says «Итерация» becomes «Спринт». The P-10 note is the designer's.

### R-15
Severity: minor
Affects: US-4, BR-8
Problem: mod-005 makes an item in «Удалена» read-only ("Every other write on it is refused"). The spec does not say whether setting a deleted item's sprint is refused (it should be, with mod-005's answer), nor that a removal's and a team deletion's clearing of such an item's pointer is a system write mod-005 allows.
Required resolution: One sentence in BR-5 or BR-8 and a row in US-4's refusal outline using mod-005's existing text.
Status: resolved
Resolution: BR-5 states both; US-4 gains the refusal row (mod-005's text) and a scenario for the system clearing.

### R-16
Severity: minor
Affects: US-3, US-6
Problem: Nothing in this line shows what a Planned sprint holds: the board shows only the Active sprint, the sprint backlog is mod-011 (release 2), the sprint filter feat-008. A planner fills a Planned sprint from cards and sees its contents only in the close dialog's counts of another sprint.
Required resolution: Either the team's list shows each sprint's item count and story points after visibility, or the gap is accepted until feat-008 with a sentence in § Out of scope.
Status: resolved
Resolution: Round 2: both — US-3 «Each row shows how much the sprint holds, after visibility», and § Out of scope leaves browsing the items to feat-008, feat-013 and mod-011.

### R-17
Severity: minor
Affects: US-5, BR-6
Problem: BR-MX-12 makes each reassigned item's «Изменён» the unlink's time; the spec is silent on whether an item the close moves gets a new «Изменён». The card shows that field, so the tests must know.
Required resolution: State it (parity with BR-MX-12 is the natural reading).
Status: resolved
Resolution: Round 2: BR-6 — a moved item's «Изменён» becomes the close's time; an item that stays keeps its own.

### R-18
Severity: minor
Affects: OQ-1
Problem: The analysis is sound in substance — a sprint is the team's whatever its place, and the only sprint behaviour doc-016 can move is who manages and which overview shows the figure. Two statements are wrong: "feat-042, which is queued after doc-016's decisions anyway" — feat-042's deps are mod-018, feat-041, feat-043, feat-048, no doc-016; and (2) speaks of «Управление» and a future department «Менеджер», while mod-019 already has a department «Менеджер» whose grant reaches projects and their items, not teams (BR-MX-22). The real doc-016 question for sprints is whether a department grant will reach the teams under it.
Required resolution: Correct the two statements before the human reads OQ-1; option A's reasoning is unaffected.
Status: resolved
Resolution: Round 2: OQ-1 answered A; its recorded result names the department «Менеджер» reaching projects, not teams (BR-MX-22), and drops the false feat-042/doc-016 claim.

### R-19
Severity: minor
Affects: OQ-2, US-8
Problem: The analysis says "each waits for the other": feat-042's deps do not include mod-007 — its text "with mod-007" only says the figure comes with mod-007 — so the current order feat-042 → mod-007 is not a cycle; mod-007 just waits for feat-042. Option A's cost ("feat-042 is delayed until mod-007 merges") is real and avoidable: the decomposition's row 6 is already its own S line depending on both, so a third option exists — mod-007 drops feat-042, feat-042 keeps its deps and leaves the figure to row 6, and row 6 runs after both; nobody waits. Independently, US-8's «Child areas follow the switch» is not buildable as written: in the typed tree a department holds project nodes and a plain «Область» stands only below a project node (mod-019 § Roles, ADR-0031), so «Склад» as a child of «Логистика» linked to a team is doubtful (mod-019's own review hit the same, its R-3), and the switch is feat-042's and does not exist in the code yet. Both strengthen moving US-8 out.
Required resolution: Add the third option to OQ-2 and correct the "circular" wording; if US-8 stays anywhere, rewrite its child-area scenario against the typed tree.
Status: resolved
Resolution: Round 2: OQ-2 answered A; US-8 deleted, § Out of scope sends it to feat-042 to be rewritten against the typed tree. The dependency it left on mod-007 was wrong and is fixed as R-20.

### Verdict

`needs-human-decision`. Twelve findings fixed in place (R-1, R-2, R-5..R-14).
R-3 and R-4 go back to the business analyst after the human answers Q-7 and
Q-8; R-15..R-19 are small analyst edits. Questions for gate-1: OQ-1..OQ-6 as
written (with R-18 and R-19 corrected first), plus:

- **Q-7 (R-4) Does mod-007 wait for feat-041, or owe its visibility clauses to it?**
  (A) wait: every row carries feat-041 and its visibility clauses run in this line; ➕ one line proves non-disclosure on its own surfaces; ➖ M5 waits for an L that has not started. (B) owe: rows 1-4 drop feat-041, their «Ограниченный» clauses are marked owed by feat-041, whose line already covers "every surface that exists when this line merges"; ➕ M5 starts now (feat-040, feat-043, mod-019 are merged); ➖ feat-041 grows by four sprint surfaces.
- **Q-8 (R-3) Which stories make up the L line `mod-007-iterations`?** Recommended by the reviewer: US-1..US-5 (the sprint, the pointer and the close share one data model and one set of races), with the board (US-6), the project archive (US-7) and, per OQ-2, the overview figure (US-8) as M/S lines depending on mod-007; the dependents' deps stay `mod-007`.

## Round 2 — requirements

Delta over round 1: R-3, R-4, R-15..R-19; the rewritten § Stories,
§ Out of scope, § Decomposition (queue edits) and § Open questions; the
human's answers of 2026-11-10 in chat (OQ-1..OQ-6 A, OQ-7 B, OQ-8 A). US-8's
file is gone and no story or rule of the spec still names it except the
§ Out of scope pointer to feat-042. The owed-by-feat-041 marks sit on every
«Ограниченный» clause of US-2, US-3, US-5, US-6, US-7, and none on a
deactivated-project clause, which stays testable now. Queue edits checked
against docs/backlog.md: the six dependents (feat-008, feat-009, feat-013,
mod-008, mod-011, mod-013) do name `mod-007`; feat-042's current deps are
mod-018, feat-041, feat-043, feat-048, as quoted.

### R-20
Severity: major
Affects: § Out of scope, § Decomposition (feat-042 row), queue edit 4
Problem: feat-042's figure is defined as "the Active sprint of a team, as the team sprint board counts it for the viewer" (BR-MX-17), and mod-007 was to expose that read — but the board, its level and its totals (BR-10, US-6) are now `feat-NNN-team-sprint-board`'s. mod-007 would have to build a count whose rule another line owns, and feat-042, depending on mod-007 alone, could merge before the board it must match.
Required resolution: The read belongs to the board line; feat-042 depends on `feat-NNN-team-sprint-board`.
Status: resolved
Resolution: mod-007's row drops the read; the board row carries it; the feat-042 row, § Out of scope and queue edit 4 name `feat-NNN-team-sprint-board` (which itself depends on mod-007).

### R-21
Severity: major
Affects: § Stories (owed clauses), NFR Non-disclosure, queue edit 5
Problem: OQ-7 B relies on feat-041 covering "every surface that exists when it merges". feat-041 and the three sprint lines are all unstarted, and nothing fixes their order: if feat-041 merges first, no line runs the sprint surfaces' visibility clauses.
Required resolution: A merge-order clause, as feat-040 and mod-020 used: whichever of feat-041 and a sprint line merges second runs the owed clauses.
Status: resolved
Resolution: § Stories paragraph, NFR Non-disclosure and queue edit 5 say so.

### R-22
Severity: minor
Affects: § Decomposition, queue edits 6 and 7
Problem: Edit 6 left mod-011's deps "if the human wants" — an open choice inside a list meant to be executed. Edit 7 rewrote doc-013's table, a merged analysis outside the queue files, under a heading that says "commits to `main` touching only queue files", which the hard rule forbids.
Required resolution: Decide edit 6; take edit 7 out of the queue list.
Status: resolved
Resolution: mod-011 keeps `mod-007,feat-009`; doc-013 keeps its wording and this spec's § Decomposition supersedes its mod-007 row (stated outside the queue list).

### R-23
Severity: minor
Affects: BR-10, BR-11, NFR, US-4
Problem: BR-10 and BR-11 and the board/archive NFRs read as this line's though their stories moved out; US-4's «Removing a sprint or deleting its team clears a deleted item's pointer» tested only the removal.
Required resolution: Mark who delivers each; match the title to the steps.
Status: resolved
Resolution: BR-10 and BR-11 name their lines; NFR says the board's and archive's figures and tests are their lines'; the US-4 title now names only the removal (the team deletion's clearing is in US-4's unlink-and-delete scenario).

### Verdict

`approved-by-ai`. Every finding of both rounds is resolved; the human answered
every open question in chat on 2026-11-10. `gate-1` stays the human's to set
in status.md. Not for this spec but for the orchestrator: status.md's
§ Plan (waiting for feat-041 and feat-042) and its § Notes summary (the
board, the overview's sprints, the project's «Спринты») still carry the old
scope.
