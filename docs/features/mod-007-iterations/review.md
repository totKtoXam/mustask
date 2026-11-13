# Review: mod-007-iterations

Rounds 1 and 2 are condensed (stage-6 budget: 32 KB a file); every finding
in them is resolved, and the full text is in this file's git history.

## Round 1 — requirements (condensed)

Sources: doc-012 (BR-MX-9..-24, US-9, US-10, US-12, § NFR, § Decomposition),
doc-004 (BR-M5-4, -11), doc-013 (BR-NE-4), feat-040 (merged), feat-043's
menu table (`addressModel.ts`), mod-019 (merged: § Roles, § Out of scope),
mod-005, and the queue lines of feat-008, -009, -015, -041, -042, mod-011,
doc-016, feat-054. Verdict then: `needs-human-decision`.

| id | Sev. | Finding | Resolution |
|----|------|---------|------------|
| R-1 | major | doc-012 team role names, renamed by mod-019 (team «Участник», «Менеджер», new team «Читатель») | § Roles, US-1, US-3, US-5 renamed; team «Читатель» refused |
| R-2 | major | the sprint code mod-019 assigns to this line was not stated | BR-13: registered in admin, team «Участник»/«Менеджер» bundles; checked through ADR-0029 |
| R-3 | major | six M/S lines, no L founder; dependents' deps broken | OQ-8 A: mod-007 stays the L, dependents keep `mod-007` |
| R-4 | major | feat-041 only on one row though visibility clauses sit in five stories; feat-040 on the wrong row | OQ-7 B: owed by feat-041; deps feat-040 only |
| R-5 | major | no criterion showed a Closed sprint's outcome | US-5 «A Closed sprint shows its outcome» |
| R-6 | major | join check would refuse every save of an item keeping a Closed sprint; "no later close moves it" untested | US-4 and US-5 scenarios |
| R-7 | major | doc-012 NFRs missing; invented 1 000-sprint figure; audit NFR crept into feat-015 | NFR rewritten |
| R-8 | minor | US-3 section order missed «Рабочие элементы»; Olga not in cast | fixed |
| R-9 | minor | criteria used the sprint filter (feat-008, feat-013) | rewritten on this line's surfaces |
| R-10 | minor | US-6 "no reload" contradicted its steps; board create command unstated | fixed |
| R-11 | minor | close vs close, start vs remove races missing | US-2 scenario |
| R-12 | minor | BR-9 counted a removal among acts touching hidden items | reworded |
| R-13 | minor | invented empty-state copy stated as fixed; error texts unowned | NFR "Interface copy" |
| R-14 | minor | card slot «Итерация» vs «Спринт» | US-4 states the change |
| R-15 | minor | a deleted item's sprint write unstated (mod-005) | BR-5, US-4 |
| R-16 | minor | a Planned sprint's contents visible nowhere | US-3 row counts; § Out of scope |
| R-17 | minor | moved item's «Изменён» unstated | BR-6 |
| R-18 | minor | OQ-1 had two false statements | corrected in OQ-1's result |
| R-19 | minor | OQ-2 "circular wait" overstated; US-8 child areas unbuildable | OQ-2 A, US-8 removed |

## Round 2 — requirements (condensed)

Delta after the human's answers (OQ-1..OQ-6 A, OQ-7 B, OQ-8 A). Verdict then:
`approved-by-ai`.

| id | Sev. | Finding | Resolution |
|----|------|---------|------------|
| R-20 | major | feat-042's figure "as the board counts it" pointed at mod-007, but the board is spun out | the read is the board line's; feat-042 depends on it |
| R-21 | major | owed-by-feat-041 clauses had no merge-order rule | whichever merges second runs them |
| R-22 | minor | queue edit 6 undecided; edit 7 edited a merged analysis under "queue files only" | decided; edit 7 moved out of the list |
| R-23 | minor | BR-10/-11 read as this line's; a US-4 title overclaimed | owners named; title fixed |

## Round 3 — requirements (scope delta after gate-2)

Delta: the human's request of 2026-11-10 — dynamics against previous sprints
over a chosen range, and results by participant — kept in mod-007. Read:
spec BR-6, BR-12, BR-14..-16, NFR, § Out of scope (mod-012), § Decomposition,
OQ-9..OQ-13; US-5 (snapshot), US-9, US-10; design.md D-1, D-2, D-4; the
queue lines of mod-012, feat-012, feat-017. Every figure in US-9's and
US-10's tables was recomputed by hand and matches its rule (38 %, 63 %, 6.3,
16.7, 67 %, 70 %, the participants' 2.0 / 63 % / 7.5), given that min,
average and max run over the previous sprints only, not this one.

### R-24
Severity: major
Affects: BR-14, BR-15, BR-16, US-5, US-9, US-10, mod-012
Problem: A sprint holds items of every level, and the figures add them all up: a feature and its stories that both carry story points count twice, and tasks inflate the item counts. The board shows one level at a time (BR-10), so the two pages would disagree, and mod-012 could not take these numbers as velocity.
Required resolution: The human decides which levels count.
Status: open
Resolution: Raised as OQ-14 in the spec (recommended: a level selector, default «Истории»).

### R-25
Severity: major
Affects: BR-14, BR-6, US-5, US-9, US-10
Problem: mod-005's purge removes an item and its content, while BR-14 says the snapshot never changes. Nothing says what a purged item does to past figures or to the outcome list (whose rows show title and link, design § outcome page).
Required resolution: The human decides.
Status: open
Resolution: Raised as OQ-15 (recommended: figures keep it; the list row shows its identifier and «Удалён навсегда»).

### R-26
Severity: major
Affects: § Out of scope (mod-012), § Decomposition
Problem: The boundary is right in data (snapshot here, history there) but the spec said "mod-012's line needs no edit now" while asking mod-012 to reuse the snapshot's closed SP as velocity. Left unrecorded, mod-012 builds velocity from history, and the two figures diverge as soon as an item is reopened. feat-012's forecast reads the same velocity.
Required resolution: A queue edit for mod-012 and feat-012.
Status: resolved
Resolution: § Out of scope reworded; queue edit 7 added.

### R-27
Severity: major
Affects: § NFR, US-10
Problem: The read budget used 20 previous sprints "of 500 items each", while the same spec closes sprints of 5 000 items; the page's worst case (this sprint at 5 000, 20 × 5 000 previous rows, filtered by visibility per project) was untested.
Required resolution: Size the budget to the spec's own maximum.
Status: resolved
Resolution: NFR and US-10 use 5 000 items a sprint, 50 projects, 300 assignees, ≤ 2 s, a fixed number of statements; marked as this spec's own figure. (A hint for the design, not a requirement: aggregates per sprint, project, assignee and done flag, written at the close, make the read independent of item count.)

### R-28
Severity: major
Affects: US-9
Problem: «A participant with no previous sprint in the range» said Борис's only earlier sprint is outside the range, but the setup puts Борис in «Спринт 11» and «Спринт 9», both inside the last 3. The scenario could not pass.
Required resolution: A participant whose history really lies outside the range.
Status: resolved
Resolution: Глеб Орлов, who held items only in «Спринт 7» (added to the setup with «Спринт 8»): "0 из 3" with the last 3, "1 из 10" with the last 10.

### R-29
Severity: minor
Affects: BR-15, US-10
Problem: "A sprint with no snapshot" cannot happen: the snapshot ships in the same L as the close, no sprint code has shipped (doc-012 § Decomposition), so every Closed sprint has one. The criterion could only be tested by forging data.
Required resolution: Drop it.
Status: resolved
Resolution: BR-15's clause, US-10's «Спринт 7» row and the "no snapshot" scenario removed; US-10 now has sprints 8 to 12.

### R-30
Severity: minor
Affects: BR-15, US-10
Problem: The rule did not say whether an average percent was computed from exact or from rounded per-sprint percents (17 % and 50 % give 34 % one way, 33 % the other), nor how an average of counts is rounded.
Required resolution: Compute on exact values, round only for display; one example that tells the two apart.
Status: resolved
Resolution: BR-15 reworded; US-10 «Averages use exact values, rounded only for display».

### R-31
Severity: minor
Affects: BR-12, BR-14
Problem: The new snapshot reads assignee, points and state at the close, but the race list did not cover a save of those fields concurrent with the close.
Required resolution: Add it to BR-12.
Status: resolved
Resolution: BR-12: the snapshot holds the values committed before the close's lock; a later save works on the closed sprint as on any other.

### R-32
Severity: minor
Affects: US-9, BR-9
Problem: Non-disclosure did not cover the participant's "k из N": a previous sprint in which a person's items were all hidden would still count, so the count would differ from the twin organization without the hidden project. The `«Без исполнителя»` 0-SP wording ("counts as 0 in the SP's") was ambiguous.
Required resolution: State both.
Status: resolved
Resolution: US-9's hidden-items scenario gains the "k из N" line; the division-by-zero line reworded.

### R-33
Severity: minor
Affects: BR-16, feat-017
Problem: «не в организации» is the mark feat-017 owes on assignees, built through mod-005's one helper; a second spelling here would drift.
Required resolution: Name the shared source.
Status: resolved
Resolution: BR-16 cites mod-005's helper, shared with feat-017.

### R-34
Severity: minor
Affects: design.md D-4, US-3, BR-15
Problem: D-4 shows a Closed row's «Элементы», «Story points» as the outcome's figures, that is, the done part (US-5: "2 items and 8 SP"). The snapshot now gives a held count too, and US-10's page shows "10 items, 6 closed". The list and the page would read different numbers under the same column name.
Required resolution: The design restates D-4 at the reopened gate-2: e.g. a Closed row reads closed of held («6 из 10», «25 из 40 SP»), or the column is renamed.
Status: open
Resolution: For the designer; no spec change needed. D-1 (a page) and D-2 (200 rows then «Показать ещё», matching the participants' pages of 200) still hold.

### Verdict

`needs-human-decision`. Eight findings of this round fixed in the spec and
stories (R-26..R-33). R-24 and R-25 wait for the human (OQ-14, OQ-15); R-34
is the designer's at the reopened gate-2. The mod-012 boundary is right once
queue edit 7 is applied: this line reads snapshots, mod-012 reads history,
and a Closed sprint's velocity has one source. Questions for gate-1: OQ-9,
OQ-10, OQ-11 (US-10's exact-value example assumes A), OQ-12, OQ-13, OQ-14,
OQ-15.

## Round 1 — code

Stage 8 over `git diff origin/main...HEAD` at 0ed44f31 (161 files), against spec BR-1..BR-16, US-1..US-5, US-9, US-10,
design D-1..D-10, api-contract, qa.md, ADR-0028 rules 7-8, ADR-0029, backend.md and frontend.md. Tests not re-run (economy
mode: CI). Visibility clauses are feat-041's and not judged.

### M-1 — the sprint writers lock the sprint before the hierarchy, against ADR-0028 rule 8's one order
Severity: major
Where: `CloseSprintHandler.cs:45,63`, `RemoveSprintHandler.cs:33,41`, `SaveWorkItemHandler.cs:125,129`;
`SprintRepository.cs:77` (the comment cites "rule 8 step 5"); `WorkItemTeamOwnership.cs:74`.
Problem: rule 8 fixes one order: (5) hierarchy lock rows, (6) sprints, (7) items. Close and removal take the sprint
`FOR UPDATE` (6), then `LockItemsAsync` takes the hierarchy locks (5); a save with a parent takes the sprint `FOR SHARE` before
`LockHierarchyAsync`. No deadlock today only because no writer takes 5 then 6: the unlink and the team deletion skip the
"open-sprint step" rule 7 says mod-007 adds, and clear `sprint_id` without locking a sprint. The first line that follows the
ADR (an unlink with its sprint step, feat-009's drag, the board) deadlocks against a close. The close also takes its
destination `FOR SHARE` (`CloseSprintHandler.cs:54`), where the ADR's sprint races say both sprints `FOR UPDATE` together.
The outcomes of BR-12 hold (race tests green); the mechanism is an unrecorded departure from an accepted ADR.
Fix: take the hierarchy locks before the sprint lock (close and removal: read the sprint's projects, lock their hierarchies,
then the sprint, then re-read the items; the save: sprint after `LockHierarchyAsync`), and fix the comment. If the developer
holds that sprint-before-hierarchy is right, that is an ADR amendment for the human: stop as `blocked-on-decision` instead.

### M-2 — the backend deviations are pointed at but recorded nowhere
Severity: major (prose)
Where: commit a42f3f17 body "Deviations: see status.md"; `status.md:90` "Deviations and the NU1903 note: commit bodies and
the hand-back".
Problem: each points at the other; neither lists a deviation, and the hand-back was chat, not a file. The human at gate-3
reads status.md for what departs from plan and contract, finds nothing, and approves as if nothing did (M-1 may be one).
Fix: list the backend deviations from plan-backend.md and api-contract.md in status.md (one line each), or write "none".
Prose only: a delta read, no QA.

### m-1 — «k из N» counts N over the whole range while only 20 sprints are read
`GetSprintParticipantsHandler.cs:89`. With 30 sprints in range a person present in all 20 read reads «20 из 30». Fix: N =
the sprints read (`shownSprints`), or say in US-9 that N is the range count.

### m-2 — the close reads every state definition of the database
`SprintRepository.cs:103`, `SprintItemPointers.cs:27`. Statement count is fixed, but the payload grows with organizations.
Fix: filter by the organization's process (or the sprint's item types).

### m-3 — BR-16's "mark and name come from mod-005's one assignee helper" is not anchored
`GetSprintParticipantsHandler.cs:39,42` builds name and «не в организации» from `IUserDirectory` and its own membership read;
the work item read model's helper is not used. Reads alike today; may drift. Fix: reuse the helper or weaken BR-16.

### m-4 — QA log lines sit under `## Notes`
`status.md:111-113`. Fix: move them into `## Log`.

### m-5 — the card field's texts live outside `sprintTexts.ts`
`CardSprintField.tsx:8-19`, against design § 11 ("every text … in one `sprintTexts.ts`"). Fix: move them, or amend § 11.

### Known items judged
- QA backend D-1 (names in batches of 500): acceptable debt, the NFR is sized to 300 assignees and a 300 test is green.
- QA backend D-2 (null name sorts last): acceptable debt, unreachable through the API today.
- InlinePickerField on the card, sibling expansion rows, no preselection without a Planned sprint: acceptable; preselection of
  the lowest Planned sprint is in place (`CloseSprintDialog.tsx:56`), as OQ-4 says.

### Claims checked
- BR-2 team change clears: `WorkItem.cs` Save (`ownerChanged` → `edit.Sprint?.SprintId`); DB backstop composite FK + check.
- BR-2 unlink/deletion clear, Closed included: `WorkItemTeamOwnership.cs:74`.
- BR-3 one Active: `ix_sprints_team_id_active` + `SprintRepository.SaveAsync` maps to `active_exists`.
- BR-5 check only on change, deleted item first: `SaveWorkItemHandler` `DecideSprint` early return; `EnsureLive` before 404.
- BR-6/BR-14 one transaction, snapshot then move, «Изменён»: `CloseSprintHandler.cs:63-66`, `SprintItemPointers.cs:43`.
- BR-7 numbers never reused, refusal rolls back: `TakeNextNumberAsync` in the create transaction.
- BR-8 removal counts deactivated projects: `LockItemsAsync` counts `Live` with no project filter.
- BR-12 snapshot = values committed before the lock: items locked `FOR UPDATE` before the INSERT…SELECT. Mechanism: M-1.
- BR-13 code in admin, team member, manager only: `ScopedRoleBundles.cs`, `role-bundles.json`; routes `IterationsEndpoints.cs:44-86`.
- BR-14 purge keeps figures: no FK from `sprint_snapshot_items` to `work_items` (migration).
- BR-15 rounding half up on exact values, held-0 out of percents: `SprintFigures.cs`; default range `SprintRange.cs`.
- BR-16 rows by assignee at close, «Итого» = sums: `GetSprintParticipantsHandler.cs`; helper claim: m-3.
- NFR statement counts: pinned in `SprintStatementCountTests.cs` (list, dynamics, participants, items, close; preview equal).
- Tenant isolation: every lock filters by organization (`SprintRepository.cs`); sprints FK `(organization_id, team_id)`.
- Commit a42f3f17 "Deviations: see status.md": M-2. NU1903 note: not re-run (CI only).
- Reference tracker not named: grep of the added lines, none.
- Direction 2: pointer clearing on unlink grepped (`sprint pointer`): feat-028 api-contract.md:390 still says "owed by
  mod-007" (merged artifact, keeps wording; now met). Card label «Итерация» grepped: feat-029 design.md:297,315 keep
  «Итерация» (merged; mod-007 design § 7 supersedes). Not swept: the prototype HTML.

### Verdict
`changes-requested`: 0 blocker, 2 major (M-1 code, M-2 prose), 5 minor (debts). One delta read after the fix.

## Round 2 — code (delta)

Delta read of `git diff 3657d022..6ee18e5b` (14 files) against the round-1 open findings. Tests not re-run (economy mode: CI);
the developer reports 2711 API integration and 22 infrastructure tests green.

- **M-1 resolved.** Close: team `FOR SHARE` → `LockHierarchiesAsync` (all the team's linked projects, project-id order) →
  `LockPairForUpdateAsync` (sprint and destination `FOR UPDATE` in one statement, `ORDER BY id`) → items
  (`CloseSprintHandler.cs:47-65`). Removal: hierarchies → sprint → items (`RemoveSprintHandler.cs:34-44`). Save: hierarchy (when
  a parent is sent) → sprint `FOR SHARE` → item (`SaveWorkItemHandler.cs:125-133`). Unlink and deletion: hierarchies →
  `LockOpenAsync` → owned items (`UnlinkTeamHandler.cs:180-183`, `DeleteTeamHandler.cs:101-104`). Every writer now follows rule 8
  steps 5 → 6 → 7, and rule 7's open-sprint step exists. Destination answers hold: another organization → 404; another team,
  Closed, Active or the sprint itself → 409 (facts are read after the lock). The close's statement count is unchanged (17 still
  holds: the old share lock and project read are replaced by the link read and the facts read); unlink and deletion pins +1,
  with the reason in the comment.
- **M-2 resolved.** status.md § Notes "Backend deviations" lists eight items; the Log line and commit a42f3f17 now have a target.
- **m-4 resolved.** The QA lines are in § Log (out of date order, harmless).

### m-6 — the hierarchy set is "fixed before the sprint is locked" overstates
Severity: minor
Where: `ISprintRepository.cs` `LockHierarchiesAsync` summary; status.md § Notes, the lock-order deviation.
Problem: the link read is not locked. A link creation takes the team `FOR SHARE`, which the close's share does not block, so a
new project's item can join the Active sprint between the link read and the sprint lock, and the close then locks it with no
hierarchy lock for its project. No deadlock follows (no writer holds that hierarchy and then waits on items the close holds),
so the sentence is wrong but nothing an agent does differs.
Fix: weaken it to "every project linked when the close starts".

### Carried to gate-3 as debts
m-1 («k из N» over the whole range), m-2 (the close reads every state definition), m-3 (BR-16's helper claim), m-5 (card
texts outside `sprintTexts.ts`), m-6 (above), QA backend D-1 (names in batches of 500) and D-2 (an unknown user's null name).

### Verdict
`approved`: 0 blocker, 0 major open; 6 minor debts.
