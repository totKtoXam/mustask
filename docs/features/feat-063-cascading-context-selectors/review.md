# Review: feat-063-cascading-context-selectors

## Round 1 — requirements

Reviewed at faa9cea0: `spec.md`, `stories/US-1..US-3` against doc-016 (spec BR-TD-7..-11, -18, -21, -22, § Decomposition; US-3, US-4; design § 3), ADR-0040 § 3, ADR-0032 §§ 5-6, and the header in main (`app/shell/ContextSelectors.tsx`, `ContextHost.tsx`, `app/navigation/selection.ts`).

### Rule → story map

| Rule | Covered by | Gap |
|---|---|---|
| BR-TD-7 invariant | US-2 (all scenarios) | — |
| BR-TD-8 offers | US-1 «An organization chosen…» .. «Only visible…» | org «все» → feat-064 (OQ-1) |
| BR-TD-9 parents | US-2 first three, «Opening an object…» | choosing another organization: R-2 |
| BR-TD-10 «все» | US-2 ««все» at the department…», «… team or project…» | org «все»: OQ-1, R-5 |
| BR-TD-11 empty | US-1 «Empty offers», «The lists follow…» | organization column, empty chosen department: R-6 |
| BR-TD-18 repair | US-2 last four | read trigger: R-7 |
| BR-TD-21 search (panel) | US-3 | «Структура» field: R-4 |
| BR-TD-22 button, panel | US-1, US-2 «Choosing a team…» | page move and focus on a choice: R-1 |
| ADR-0032 § 6 remembered | US-2 «A remembered…» | notice text in the second scenario: R-8 |
| doc-016 NFR volumes, non-disclosure | spec NFR, US-3 last | statement count: R-9 |

### R-1
Severity: major
Affects: US-1, US-2, BR-6; spec NFR "Keyboard"
Problem: Nothing says what the page does when a column choice is made while the panel stays open. Today (`ContextHost.change`) every choice navigates per ADR-0032 § 5 (keep the entry, else the new menu's first) and then moves focus to the page `h1`; doc-016 design § 3.0 wants focus back on the chosen row and the panel open. Left open, the designer and the developer will each pick one, and the existing focus-to-`h1` test will either be kept (panel loses focus on every choice) or silently dropped.
Required resolution: Add to US-2 (or US-1) a criterion: a column choice moves the address per ADR-0032 § 5 at once, the panel stays open, focus returns to the chosen row; Esc / click outside returns focus to the button; a search result choice closes the panel and focus goes where feat-043 sends it today (the page `h1`). State that feat-043's focus-to-heading after a choice applies only when the panel closes.
Status: open
Resolution:

### R-2
Severity: major
Affects: US-2 cast (line 10), BR-3
Problem: Choosing another organization in the «Организация» column has no scenario; the cast defers it to "ADR-0032 § 6 unchanged". But the column is new UI built in this task, and doc-016 design § 3.3 adds a step ADR-0032 § 6 lacks: the other organization's remembered selection is repaired by BR-TD-18 (with the remembered-selection notice). Also unstated: does the panel stay open while the page moves to that organization's «Обзор», and do the lower columns re-read immediately? doc-016 US-4 had this scenario ("Choosing another organization clears the rest"); feat-063 dropped it.
Required resolution: Add a scenario: Olga on «Ромашка» / «Логистика» / VENUS / A chooses «Другая»; the page opens «Другая»'s «Обзор», the lower three read her remembered selection there (repaired per BR-TD-18 with «Ранее выбранный контекст недоступен. Выбрано: …» if it changed), else «все»; nothing is written for «Ромашка»; the panel stays open with the lower columns showing «Другая»'s offers.
Status: open
Resolution:

### R-3
Severity: minor
Affects: BR-3; ADR-0040 § 3
Problem: ADR-0040 § 3 says "«все» at a level resets every lower level"; doc-016 BR-TD-10 (human, gate-2 2026-11-12) says team «все» and project «все» reset nothing. US-2 follows BR-TD-10, correctly, but an implementer reading the ADR first gets the opposite rule for team «все» with a project chosen.
Required resolution: BR-3 states that BR-TD-10 governs where ADR-0040 § 3's sentence is broader; the orchestrator owes ADR-0040 a correction note (outside this write zone).
Status: open
Resolution:

### R-4
Severity: minor
Affects: spec § Out of scope (line 61), OQ-2 (line 73)
Problem: The conclusion (field not in scope) is right, the reason is wrong: the «Структура» filter field is already in main (`src/frontend/src/features/organizations/lib/areaTexts.ts` `search` = «Найти департамент, команду или проект», AreasPage tests). OQ-2 asks the human to find a queue line for work that is done.
Required resolution: Reword the out-of-scope line to "already delivered in main; not touched" and close OQ-2. (Not edited by the reviewer: outside this session's write permission.)
Status: open
Resolution:

### R-5
Severity: minor
Affects: OQ-1, US-1, BR-6, BR-8
Problem: OQ-1's recommendation matches main: with «Все организации» `ContextHost` clears the selection and navigates to the organizations list, and the lower selectors have no offers (offers are read per organization), so they are inert. It is coherent and decidable without the human. But the spec gives the state no criterion and no copy: under BR-TD-11 an empty column would read «Нет доступных департаментов», which is false for a member of several organizations, and a disabled search field needs a reason.
Required resolution: Decide OQ-1 as recommended and add a US-1 scenario: selection «Все организации» → button «Все организации › все › все › все»; the lower columns show only «все» and the muted line «Выберите организацию» (doc-016 BR-TD-6's existing text), not the BR-TD-11 lines; the search field is disabled with the same helper; choosing an organization in the first column follows R-2.
Status: open
Resolution:

### R-6
Severity: minor
Affects: US-1 «Empty offers» (lines 69-73)
Problem: Two empty states are unpinned. (a) The organization column for a user with no organization: BR-TD-11 «Нет доступных организаций» vs today's header link «Нет организаций» (`ContextHost`, line 156). (b) A chosen department that holds no visible team or project: BR-TD-11's «Нет доступных команд» vs doc-016 design § 3.4's «Команд нет» / «Проектов нет»; BR-8's "use the doc-016 text" fits both.
Required resolution: Add the two cases with the one text each (recommended: (a) keep today's link, since no panel can open without an organization; (b) «Нет доступных команд» / «Нет доступных проектов», one text per column).
Status: open
Resolution:

### R-7
Severity: minor
Affects: US-2 «A change seen while the panel is open» (lines 70-74), «A remembered…» (lines 60-68)
Problem: "Olga's panel next reads the offers" and "When the panel loads" name no trigger, so the tests cannot set one up. Today offers are read by the shell's context source, not by opening a selector; the remembered repair runs at the source's read, before any panel opens.
Required resolution: Name the occasions: the remembered repair runs when the shell first reads the organization's offers (no panel needed); the open-panel repair runs whenever the offers are re-read, and opening the panel re-reads them.
Status: open
Resolution:

### R-8
Severity: minor
Affects: US-2 «A remembered value that is gone or hidden» (lines 65-68), US-1 (lines 59-61, 63-67)
Problem: Unverifiable or confusing criteria. (a) "the notice names the repaired selection" has no full remembered selection and no text; main already formats it (`fallbackNotice`: «… Выбрано: Ромашка / Логистика / VENUS.»). (b) «Мои команды» gives no selection, so it is unclear whether JUPITER appears under «Другие команды». (c) «Only visible and active objects» makes B deactivated and then uses B as Olga's hidden project, so the sight half tests deactivation, not sight.
Required resolution: (a) Give the full remembered selection and quote the notice. (b) Fix the selection («Ромашка» / «Логистика»). (c) Use a separate deactivated project (e.g. E), keep B active and hidden from Olga.
Status: open
Resolution:

### R-9
Severity: minor
Affects: spec NFR (line 51)
Problem: doc-016's volume NFR also pins a statement-count test for the offers read ("does not grow with … objects"). The spec keeps only the 2 s figure. If this task adds server parameters (spec NFR line 54), nothing guards their query count.
Required resolution: Add: any offers or search read this task adds or changes keeps a statement count that does not grow with objects, pinned by a test; none is owed if the read is unchanged.
Status: open
Resolution:

### R-10
Severity: minor
Affects: US-2 cast (line 10)
Problem: The cast moves the context notices to an `InlineNotice` under the header, closable. Today they go through `useNotice` with a timed dismissal and a queue rule shared with other shell notices (mod-016 DC-11, `ContextHost` lines 54-60). That is a behaviour change to existing notices, not just the new panel, and is not listed in the context or the operation table.
Required resolution: Say so explicitly in the spec (which notices move: «Выбор сброшен», the remembered-selection fallback, the link notice) and whether they still auto-dismiss, so stage 3 and QA treat it as in scope.
Status: open
Resolution:

Accepted as designed, no finding: US-3 «Only the chosen organization is searched» narrows BR-TD-21 on purpose; feat-064 must replace that scenario. Size M holds: one panel, `selectorFit` rewrite, client search over one organization's offers, no migration.

Verdict: needs rework — one business-analyst pass for R-1, R-2 (spec holes) and the minor fixes; no question needs the human (OQ-1 is decidable per R-5, OQ-2 is moot per R-4). Expected `approved-by-ai` after the pass.

## Round 2 — requirements (delta)

Delta read of the analyst's rework: `spec.md` and `stories/US-1`, `US-2` against R-1..R-10.

| Finding | Status | Resolution |
|---|---|---|
| R-1 | resolved | spec BR-9; US-2 «A column choice moves the page and keeps the panel open» |
| R-2 | resolved | US-2 «Choosing another organization» (page, panel, repair, nothing written for «Ромашка») |
| R-3 | resolved | BR-3 states BR-TD-10 governs; the ADR-0040 note is held by the orchestrator (spec § Open questions) |
| R-4 | resolved | § Out of scope "already delivered in main"; OQ-2 closed |
| R-5 | resolved | BR-10 and US-1 «Organization «все» is inert here» with «Выберите организацию»; OQ-1 closed |
| R-6 | resolved | US-1 «A chosen department holding nothing visible», «A user with no organization» |
| R-7 | resolved | BR-11; the triggers are named in US-2's repair scenarios |
| R-8 | resolved, except (a) | (b) the selection is given; (c) the deactivated project is E, B is active and hidden. For (a), see R-11 |
| R-9 | resolved | spec NFR, statement-count line |
| R-10 | resolved | BR-12 and the operation table's «Context notices» row; doc-016 design § 3.3 requires it |

### R-11
Severity: minor
Affects: US-2 «A remembered value that is gone or hidden resets to «все»» (lines 79-82)
Problem: The expected result contradicts the sight rules. Olga sees MARS only because she sees A, which is linked to it (BR-TD-13; US-1's cast: she sees VENUS, MARS, A, «Логистика», and is a member of VENUS only). Once A is hidden from her, MARS is hidden too, so BR-TD-18 also resets the team. The repaired selection is «Ромашка» / «Логистика» / «все» / «все» with «… Выбрано: Ромашка / Логистика.», not the MARS path the scenario expects. A test written from the scenario fails against a correct implementation.
Required resolution: Either add "Olga holds a grant on MARS" to the Given and keep the expectation, or change the expectation to «Ромашка» / «Логистика» / «все» / «все» and «Ранее выбранный контекст недоступен. Выбрано: Ромашка / Логистика.»
Status: open
Resolution:

Nothing else in the rework broke: the new BR-9..BR-12 agree with doc-016 design §§ 3.0, 3.3 and with ADR-0032 §§ 5-6, and the new scenarios are testable. (The ids run BR-1..3, BR-9..12, BR-4..8 in the table. That is cosmetic, and no finding is raised.)

Verdict: `approved-by-ai` once R-11 is fixed. It needs a one-line fix in the story and no third round. The orchestrator can confirm the fix from the diff. Nothing is left for the human at gate-1.
