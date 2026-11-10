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
