# Review: feat-032-board-card-checklists

## Round 1 — requirements

Reviewer: requirements-reviewer, 2026-10-12, clean context. Input: `spec.md`,
`stories/US-1..US-3`, `status.md` D-1/D-2, `research/reference-card.png`;
cross-checked against mod-006 `spec.md` (§ Model, BR-M4-3/-5/-14/-27/-28,
§ Page «Доски»), feat-027 `spec.md` (BR-T-4..-10, -15, -17, copy table),
mod-003 `spec.md` (BR-M3-3, -21, -26, -28), mod-001 `api-contract.md`
(appearance tokens) and the queue line of feat-031 in `docs/backlog.md`.

**Verdict: `needs-human-decision`.** No blocker. Six majors (R-1..R-6) the
analyst can fix without the human; OQ-1 and OQ-3 and a new question (R-20)
are the human's. The other open questions can take their recommended option
(see § Open questions below).

### Rule → story mapping

| Rule | Covered by | Gap |
|---|---|---|
| BR-C-1 children set (direct, live, three kinds, any Область) | US-1 «Only task, bug and test-case…», «Children of another team…», «A child bug is still its own card» | the test-case token is unnamed (R-1) |
| BR-C-2 counters row, order, «0/0» on a story | US-1 first, third, fifth scenarios | — |
| BR-C-3 done = Completed; Resolved not done; Removed excluded | US-1 «A «Разрешена» bug…», first scenario (MSK-TASK-7) | Removed data is unreachable before mod-005 (R-13) |
| BR-C-4 toggles, independence, chevron, memory | US-1 toggle, independence, chevron, memory scenarios | no keyboard scenario for a counter (R-17) |
| BR-C-5 rows, order, test case without checkbox | US-1 row, order, test-case-row scenarios | reasoning for no checkbox is wrong (R-5); bug order diverges from BR-T-6 (R-11) |
| BR-C-6 assignee on card and row | US-1 row and «assignee is on the card» | «Неизвестный пользователь» untested (minor, folded into R-17) |
| BR-C-7 tick/untick writes | US-2 tick, untick, bug, card-column, no-Completed, LWW | LWW scenario not executable (R-2); data contradiction (R-7) |
| BR-C-8 one write at a time, rollback | US-2 refusal, no answer, one-write; US-3 one-write | open add field during another write (R-14) |
| BR-C-9 «Добавить задачу» | US-3 entire | copy vs. type name (R-15); permission codes (R-12) |
| BR-C-10 one read, additive | US-1 «checklists come with the board's answer», «other behaviour unchanged» | NFR payload unbounded per card (R-16) |
| BR-C-11 test cases with feat-031 | US-1 «Before feat-031 there is no flask», flask scenarios | depends on R-1 |
| NFR accessibility | US-2 keyboard checkbox; US-1 chevron focus | counters, add button by keyboard (R-17); ambiguous names (R-3) |
| D-1 «как на скриншоте» | stories | «Story Points» cut silently (R-20) |

### R-1
Severity: major
Affects: BR-C-1, BR-C-2, BR-C-5, BR-C-11, US-1 flask scenarios
Problem: test cases are "recognised by `type.appearance`", but no token
spelling exists anywhere: mod-001's contract closes the set to `epic`,
`feature`, `story`, `task`, `bug` (the panel types it as that union, with a
fallback mark for unknown tokens), and feat-031 is only a queue line ("a flask
appearance") — not specced, not started. feat-032 is told not to wait for it
(D-2), so it must ship code keyed to a token nobody has fixed; if feat-031
picks another spelling, the flask counter silently never appears and no test
of either task catches it.
Required resolution: BR-C-1 names the token (e.g. `testcase`, matching the
existing single-word lowercase style) and the flask icon's source (feat-032
adds it to the panel's appearance union and theme mark, or feat-031 does);
the orchestrator records the same spelling on feat-031's queue line so its
analysis inherits it. Alternative, if the orchestrator prefers: drop the
flask counter from this task and put "the card's flask counter and list,
per feat-032 BR-C-2/-5" on feat-031's line (revisits D-2, the orchestrator's
call, not the human's).
Status: open

### R-2
Severity: major
Affects: US-2 «The later of two writes decides»
Problem: not executable. Both users see MSK-TASK-6 in «Активна», i.e.
**unchecked**. Борис "unticks it from his stale view" — but his stale view
shows an unchecked box; he can only tick it, which writes «Закрыта», not
«Новая». The scenario's Then cannot be reached.
Required resolution: e.g. "Given MSK-TASK-3 in «Закрыта» on Борис's board;
When Анна sets it to «Активна» on its form, and Борис, his board not read
again, unticks it; Then MSK-TASK-3 is in «Новая», Анна's «Активна» is
overwritten, and neither is shown any notice."
Status: open

### R-3
Severity: major
Affects: § Screens and copy (counter, chevron, add button), US-1, US-3
Problem: the counter's accessible name «Задача: выполнено 1 из 3», the chevron
«Свернуть список: Задача» and the button «Добавить задачу» carry no card
identifier, so a board of hundreds of cards has hundreds of identically named
controls: a screen-reader user's control list cannot tell them apart (WCAG
2.4.6 / 4.1.2), and tests have no unique locator. feat-027 puts the ID in every
such name («Добавить: Фича (<ID>)», «Заголовок: Задача (<ID>)»); this spec does
so only for the checkbox.
Required resolution: name them with the card's ID, e.g. «<ID>, <тип>: выполнено
<n> из <m>», «<ID>, <тип>: <m>», «Свернуть список: <тип> (<ID>)», «Добавить
задачу (<ID>)» (visible text stays «Добавить задачу»); update US-1 and US-3.
Status: open

### R-4
Severity: major
Affects: BR-C-4, BR-C-8, US-1, US-3; mod-006 card drag (US-5)
Problem: the card is a drag source (mod-006 § Page «Доски»: "drag a card"),
and it now contains a checkbox, links, toggles and a text field. Nothing says a
press on them does not start a card drag, nor that text can be selected and
typed in the add field inside a draggable card (with the native drag the panel
uses — tech-011 t3 — inputs inside a `draggable` element are a known trap). The
spec's "never moves the card" covers only opening and closing.
Required resolution: add to BR-C-4/-9 and a US-1 scenario: "A press or a drag
started on a counter, a checkbox, a row, a link, «Добавить задачу» or the add
field never starts a card drag; text in the field can be selected with the
pointer; the card is dragged from the rest of its surface as before."
Status: open

### R-5
Severity: major
Affects: BR-C-5, OQ-2
Problem: the rule justifies the checkbox-less test-case row with "its states
are not open/done (feat-031)". feat-031's queue line gives it «Проектирование»
/ «Готов» / «Закрыта» / «Удалена» by category — «Закрыта» is a Completed state,
so a test case can be done exactly like a task. The premise that feeds OQ-2's
recommendation is false; the human would be asked on a wrong basis. Also
unstated: whether a test case in «Закрыта» still counts in «<m>».
Required resolution: ground (a) on D-1 alone (the reference shows a count
only), state that every live test case counts whatever its state, «Закрыта»
included, and reword OQ-2 accordingly.
Status: open

### R-6
Severity: major
Affects: OQ-4, BR-C-1; feat-027 BR-T-4
Problem: BR-C-1 counts a child of another team; feat-027 BR-T-4 deliberately
shows such a child beneath no row on the backlog. The same story will then
read "3 tasks" on its card and show 2 beneath it on the backlog. The spec cites
BR-T-4 as a source but never says it departs from it, so the next reader will
take the mismatch for a bug.
Required resolution: BR-C-1 says in words "unlike the backlog's tree (feat-027
BR-T-4), which shows only children on the team's surfaces"; the decision log
of OQ-4 carries the same sentence. (OQ-4 itself can take (a).)
Status: open

### R-7
Severity: minor
Affects: US-2 «A closed child that is also a card moves column»; US-1 data
Problem: US-2 takes US-1's data, where MSK-BUG-5 is «Закрыта»; the scenario's
Given puts it in «Активна» and ticks it. Run after US-2's bug scenario the
counters no longer match the data table.
Required resolution: use MSK-BUG-8 («Разрешена» → ticked → its card leaves
«Разрешена» for «Закрыта»), or add MSK-BUG-5 «Активна» to a scenario-local
Given with the counter it yields.
Status: open

### R-8
Severity: minor
Affects: US-1 «Only task, bug and test-case children are listed»
Problem: MSK-BUG-4's state is not in the data, but the Then expects «0/1».
Required resolution: give MSK-BUG-4 a state (e.g. «Активна») in the data.
Status: open

### R-9
Severity: minor
Affects: BPMN, US-2 «Ticking closes the child», § Screens (announcements)
Problem: the BPMN announces «<ID> закрыт.» only after the server accepts; US-2
reads as if it is announced at once with the optimistic tick; mod-006
announces a card move at once. One of them must give.
Required resolution: pick one (recommended: at once, as mod-006's move, with
the refusal notice correcting it) and align the BPMN and US-2.
Status: open

### R-10
Severity: minor
Affects: BR-C-7, § Screens (announcements)
Problem: «<ID> закрыт.» / «снова открыт.» is fixed copy, but the tick writes
"the first Completed state" — in a process whose state is not «Закрыта» the
announcement lies.
Required resolution: either «<ID>: статус «<state>».» or note that the copy
assumes the seeded names and is mod-016/process-editing's to revisit.
Status: open

### R-11
Severity: minor
Affects: BR-C-5; feat-027 BR-T-6
Problem: rows order by number for every kind; the backlog orders a story's
bug children by the bug level's backlog order (BR-T-6). The same children
appear in different orders on the two surfaces.
Required resolution: state the divergence ("by number for every kind, unlike
BR-T-6") or follow BR-T-6 for bugs.
Status: open

### R-12
Severity: minor
Affects: BR-C-7, BR-C-9; mod-006 BR-M4-27, feat-027 BR-T-17
Problem: the permission codes are not declared: a checkbox is
`workitems.workitem.write`; a task created with a parent is
`workitems.workitem.write` plus `workitems.link.write`. mod-016 will read them
from the specs.
Required resolution: one sentence in BR-C-7 and BR-C-9 (or the Roles table).
Status: open

### R-13
Severity: minor
Affects: US-1 data (MSK-TASK-7), US-2; mod-003 BR-M3-21, mod-006 § Model 4
Problem: no item can be in «Удалена» before mod-005 (mod-003 BR-M3-21, mod-006
§ Model 4), so MSK-TASK-7 is reachable only by seeding the database directly;
and nothing says what a stale tick does on a child deleted meanwhile (after
mod-005: refused, row returns — or not?).
Required resolution: US-1 data notes "seeded directly, as before mod-005";
US-2 gains "a child in «Удалена» since the read: the change is refused with
the code's text, the row returns" (or explicitly defers it to mod-005).
Status: open

### R-14
Severity: minor
Affects: BR-C-8, BR-C-9
Problem: BR-C-8 turns off «Добавить задачу» during another write, but an add
field already open (title typed) is not mentioned: its Enter during a tick's
save would start a second concurrent write.
Required resolution: "an open add field keeps its text but its Enter is off
until the board is read again".
Status: open

### R-15
Severity: minor
Affects: BR-C-9, § Screens
Problem: «Добавить задачу» hard-codes the type name, while the counters and
feat-027 (BR-T-9) use "the process's own name of that type"; and BR-C-2 says
"a story card" by name where BR-C-9 says "a type that offers a task".
Required resolution: either «Добавить: <тип>» as feat-027, or state that the
copy is the human's D-1 wording and fixed; BR-C-2 says "a card whose type
offers a task as a child (BR-C-9)" instead of "a story card".
Status: open

### R-16
Severity: minor
Affects: NFR Volumes
Problem: "5 000 cards, each with five children" sets an average, not a bound;
a story with 200 tasks and the 100 ms open is unstated, and the answer grows
from 5 000 to 30 000 items under the same 2 s (mod-006's render cost of 5 000
cards is itself unproven, tech-011 t3).
Required resolution: add "a single card with up to 200 children opens its list
within 100 ms", and say the 2 s budget is the read, with every list collapsed.
Status: open

### R-17
Severity: minor
Affects: US-1, US-3, NFR Accessibility
Problem: the NFR says counters, the add button and the chevron work by Enter
and Space; only the checkbox (US-2) and the chevron's focus return (US-1) are
scenarios. «Неизвестный пользователь» on a row is untested.
Required resolution: a US-1 scenario "Tab to the task counter of MSK-US-2,
Enter opens, Space closes"; a row whose assignee the directory does not know
shows «Неизвестный пользователь».
Status: open

### R-18
Severity: minor
Affects: BR-C-4
Problem: "kept for the visit to the page" — a row title that navigates to the
item's form (until feat-029's dialog lands) ends the visit, so a user who opens
a task from the list and comes back finds every list collapsed. feat-027
BR-T-7 accepted the same, but on a board this is the main path.
Required resolution: state it as accepted ("coming back from an item's form is
a new visit"), or keep the memory for the browser session.
Status: open

### R-19
Severity: minor
Affects: US-2 «A closed child that is also a card moves column»
Problem: whether the child's own card moves at once (optimistically) or only
after the read is implicit.
Required resolution: "the child's card moves at the read, not at once".
Status: open

### R-20
Severity: minor (question for the human)
Affects: § Out of scope («Story Points»); D-1
Problem: D-1 records the human's choice "the full scope «как на скриншоте»";
the screenshot's «Story Points» line is cut by the analyst with "the card
shows no planning fields". The human's AskUserQuestion items did not list it,
so it may be fine — but it is a scope cut against "как на скриншоте" made
without them, and «Трудоёмкость» already exists (feat-022), so adding it is
cheap.
Required resolution: put it to the human at gate-1 as a question: «Трудоёмкость»
on the card — (a) not in this cut (recommended: keeps M; its own line), (b) in.
Status: open

### Open questions — reviewer's view

| OQ | Needs the human? | Why |
|---|---|---|
| OQ-1 first state | **yes** | precedent: on the backlog the human overrode a "collapsed" recommendation and chose the reference's expanded first state (feat-027 D-6 OQ-2 (d)); the screenshot again shows an open list |
| OQ-2 test cases | no, take (a) | matches the screenshot's flask «1»; but reword it per R-5 first |
| OQ-3 tick/untick | **yes** | a product semantic with data effect: an «Активна» task ticked by mistake and unticked lands in «Новая», a «Разрешена» bug ticked and unticked loses «Разрешена»; the human's word was «переоткрывать», which may mean «Активна» |
| OQ-4 other team, duplicate bug | no, take (a) | keeps mod-006's board unchanged; record the divergence from BR-T-4 (R-6) |
| OQ-5 row «…» | no, take (a) | nothing to put in it; one line to add later |
| OQ-6 epic/feature cards | no, take (a) | D-1 names tasks and bugs; a bug child of a feature still shows (US-1) |

### Size

One M holds: one additive field in the board read (children per card, with
assignee and state category) and a card component in the existing slice;
three stories on the same file. The real cost driver is R-4 (controls inside a
draggable card) and the feat-029 rebase; neither pushes it past M.

## Round 2 — requirements (delta)

Reviewer: requirements-reviewer, 2026-10-12. Delta only: the round-1 findings
against the reworked `spec.md` and `stories/US-1..US-3` (analyst's commit
0b1b509), `status.md` D-3 (human) and D-4 (orchestrator). One extra lookup:
mod-003 `api-contract.md` § 2.4 (`stateId`), for R-21.

**Verdict: `needs-changes`.** One new major (R-21), which the rework itself
introduced: a single US-2 scenario, fixed by the replacement text below, with
no further review round needed. Every round-1 finding is resolved. With R-21
applied as written, the spec is `approved-by-ai`, with the minors R-22..R-23
left open.

### Round-1 findings

| Finding | Status | Resolution |
|---|---|---|
| R-1 | resolved | BR-C-1 and BR-C-11 fix the token as `testcase`, and this task adds the token and the flask mark; US-1 tests run on a fixture process (D-3). The spelling on feat-031's line is owed (R-22) |
| R-2 | resolved | US-2's LWW scenario reworked: Борис's stale board shows MSK-TASK-3 checked, Анна sets «Активна», Борис unticks it, and the item ends in «Новая» with no notice |
| R-3 | resolved | § Screens: the counter, the chevron and the add button carry the card's ID; US-1 and US-3 use those names |
| R-4 | resolved | BR-C-12 added; US-1 «Controls inside the card do not start a card drag», US-3 «Typing in the field does not drag the card» |
| R-5 | resolved | BR-C-5 rests on D-1/D-4 and counts every live test case, «Закрыта» included; US-1 adds MSK-TC-12 |
| R-6 | resolved | BR-C-1 states the divergence from feat-027 BR-T-4; US-1 asserts the backlog shows no row for the child |
| R-7 | resolved | US-2 uses MSK-BUG-8 for the card-column scenario |
| R-8 | resolved | MSK-BUG-4 is «Активна» in US-1 data |
| R-9 | resolved | Announcements come at once; the BPMN and US-2 agree |
| R-10 | resolved | The announcement is «<ID>: статус «<состояние>».» |
| R-11 | resolved | BR-C-5 states the divergence from BR-T-6; US-1 has an ordering scenario |
| R-12 | resolved | BR-C-7 and BR-C-9 declare the codes |
| R-13 | resolved in part | The US-1 data note is in. The stale-deleted case became R-21 |
| R-14 | resolved | BR-C-8; US-2 and US-3 scenarios |
| R-15 | resolved | BR-C-9 says the copy is D-1's and fixed; BR-C-2 says "a type that offers a task" |
| R-16 | resolved | The NFR bounds the read to collapsed lists and a card of 200 children opens within 100 ms; US-1 asserts both |
| R-17 | resolved | US-1 adds the keyboard counter and «Неизвестный пользователь»; US-3 adds the button by keyboard |
| R-18 | resolved | BR-C-4 accepts that coming back from a form is a new visit; US-1 asserts it |
| R-19 | resolved | The BPMN and US-2 say the child's card moves at the read |
| R-20 | resolved | Out of scope; feat-034-board-card-effort is queued (D-3) |

### R-21
Severity: major
Affects: US-2 «A child removed since the read»; BR-C-7; § Screens ("No new
error codes"); mod-003 api-contract § 2.4
Problem: the new scenario expects a tick on a child in «Удалена» to be
refused, "its code's text shown". No such refusal exists. mod-003's PATCH
checks only the **target** `stateId`: a state of the item's own type other
than «Удалена», any of them from any other. Nothing checks the stored state,
so today the tick is accepted and moves the item out of «Удалена», which un-deletes it. To
pass, the scenario needs a new backend rule on mod-003's state change and a
code. That contradicts BR-C-7 ("last write wins, no check") and "No new error
codes", and it decides restore-from-deleted semantics that belong to mod-005.
As written it cannot pass, or it pulls unplanned backend scope into the M.
Required resolution: replace the scenario with a deferral, for example:
"Scenario: A child removed since the read — Given no item can be in «Удалена»
before mod-005; Then what a tick on a child removed since the read does is
mod-005's (its delete and restore rules); this task adds no check." Add one
sentence to BR-C-7: "a stale tick on a child removed since the read is
mod-005's to refuse; this task adds no check". The orchestrator should also
check that mod-005's queue line or analysis carries this. The same gap
already exists for mod-006's card move, so it is not new to feat-032.
Status: open

### R-22
Severity: minor
Affects: spec § Decomposition ("owed at gate-3: the `testcase` spelling on
feat-031's queue line")
Problem: feat-031 is queued in `main` and may start before feat-032's gate-3.
If the spelling waits for gate-3, feat-031's analysis can pick another one.
Required resolution: the orchestrator amends feat-031's queue line now, in
`main`, as it did for feat-034.
Status: open

### R-23
Severity: minor
Affects: BR-C-12 "Source" column
Problem: it cites "review R-4". The spec should stand without the review
file, so the source should be a decision or a rule.
Required resolution: cite mod-006 § Page «Доски» / US-5 and D-1 only.
Status: open

## Round 1 — code (backend + frontend)

Stage 8, the one round of an M, over `git diff main...HEAD` (f715ec4..85632a0,
54 files). Read: spec, US-1..US-3, api-contract, design, qa.md, status D-1..D-5.
No stand started, no test run (QA's verify.sh run stands); nothing modified
but this file.

Verdict: **needs-fixes** · blockers 0 · majors 2 · minors 8.

### F-1 (major, frontend) — every card on the board redraws on every board render; QA D-5, escalated

- `src/frontend/src/features/boards/hooks/useChecklists.ts:354`, `:394`:
  `submitAdd` depends on `createItem` and `tick` on `setState`, the whole
  `useMutation` results. TanStack v5 returns a fresh object each render
  (`node_modules/@tanstack/react-query/src/useMutation.ts`, last line
  `return { ...result, mutate, mutateAsync: result.mutate }`), so `actions`
  (`:413`) and `env` (`:438`) are new each render. `env` is a prop of the
  memoised `BoardCard` (`BoardColumns.tsx:344`), so **every** card misses its
  memo, not only open rows as QA thought: on `setDrag` at dragstart and each
  column enter (`BoardColumns.tsx:220`, `:243`), on a menu open, on each
  announcement. That regresses mod-006 US-5 (US-1 "The board's other
  behaviour is unchanged") at its 5 000-card volume. It also disproves prose
  in the same diff: `useChecklists.ts:65`, `:81` ("Stable"),
  `CardChecklists.tsx:280` and the untouched `BoardColumns.tsx:394` ("a drag
  over one column redraws no card"), design § 7.3 (100 ms met "by drawing rows
  once (memo per row)").
- Fix: depend on `setState.mutateAsync` / `createItem.mutateAsync`, which
  query-core binds once (`mutationObserver.ts:61`), or read both through the
  `latest` ref. Add a test that pins it: with a list open, a dragstart and a
  column enter render no `BoardCard`/`ChecklistRow` again (a render-counting
  mock or `React.Profiler`).

### F-2 (major, docs: orchestrator/analyst; prose-only) — the `testcase` hand-off to feat-031 is claimed done and owed at once

- `spec.md:66` (BR-C-11) says the spelling is "recorded on its queue line";
  `spec.md:138-140` (§ Decomposition) calls the same thing "owed at gate-3".
  Neither holds: feat-031 is taken, so its line has left `docs/backlog.md` in
  `main`, and nothing on `feat/031-test-case-type` names `testcase` (`git grep`:
  no hit). The status Log says only that it was "sent" to that session. QA D-4
  (`WorkItemField.Of(Testcase)` throws,
  `src/backend/Mustask.Domain/WorkItems/WorkItemField.cs:117-121`) is not
  recorded on feat-031 either. A feat-031 that picks another spelling, or never
  extends `WorkItemField`, loses the flask counter or answers 500 on creating a
  test case. The diff contradicts itself, so this is major by the item-6 rule.
- Fix: write both facts into `docs/features/feat-031-test-case-type/status.md`
  (or its spec): the spelling, and "extend `WorkItemField.Of` for `Testcase`".
  Reword BR-C-11 to name that file instead of the queue line, and align
  § Decomposition. Prose only: a delta read, no QA.

### Minors (debts, listed at gate-3; not sent back)

- **m-1 (frontend; QA D-1 confirmed minor).** `CardChecklists.tsx:313` with
  `useChecklists.ts:178-186`: until the process read answers, every checkbox is
  `disabled` and out of the Tab order; if that read fails, they stay so
  silently. A tick cannot be computed without the process, and design § 3.2
  already hides «0/0» and «Добавить задачу» in that state, but the checkbox
  case is written nowhere. Fix: record it in design § 7.2 as accepted, or show
  the process read's failure (a notice with retry).
- **m-2 (frontend; QA D-2 confirmed).** Design § 9 promises a Firefox fallback
  (`draggable={false}` while the add field is open) that is not shipped
  (`BoardColumns.tsx:430`). Fix: `draggable={!saving && adding === null}` on
  `BoardCard`, one line, or mark the fallback deferred in § 9.
- **m-3 (frontend; QA D-3 confirmed).** Spec NFR "200 children open within
  100 ms" has no anchor, and its only stated mechanism is F-1's broken memo.
  Fix: after F-1, measure once in a browser, or mark the NFR unverified in
  qa.md.
- **m-4 (backend; QA D-4 confirmed, feat-031's).** `WorkItemField.cs:117-121`
  throws for `Testcase`; unreachable here, since no such type is seeded. Fix:
  F-2 records it on feat-031.
- **m-5 (docs, owed at gate-3).** The owed list (status Plan; api-contract
  § 7) covers mod-006 (`api-contract.md:140` § 2.3 "a backlog row, a board
  card", `:268`; `spec.md:195` BR-M4-14; `spec.md:309` card row) and mod-001
  (`api-contract.md:50`, `spec.md:89`, `design.md:132`, `:257`). Right but
  **incomplete**: the five-token set also stands in ADR-0024 § 3
  (`docs/adr/0024-app-shell-and-visual-language.md:149`, "The set is …"). It
  is an accepted ADR, so per `docs/adr/README.md` rule 3 the human picks a note
  or a new ADR; § 3 itself allows growth "only by a change to `app/theme`",
  which this is. The set also stands in mod-015 `spec.md:117` BR-4 ("closed
  set") with `stories/US-1-visual-language.md:56`, and in mod-003
  `api-contract.md:132`. `spec.md:138-140` also omits mod-001. Fix: add these
  to the gate-3 notes.
- **m-6 (frontend).** Stale five-token comments the token change strands:
  `src/frontend/src/features/workitems/lib/fields.ts:33` ("The five kinds …
  mod-001's appearance tokens") and
  `src/frontend/src/features/workitems/api/workItemsApi.ts:20`. Fix: say six,
  or "the tokens this slice draws a form for".
- **m-7 (frontend).** `ChecklistAddField.tsx:92`, `:134`: `height: 28` is a
  raw dimension (frontend.md § Theme), copied from `AddChildRow.tsx:217`. Fix:
  `theme.spacing(3.5)` in both files.
- **m-8 (backend).** `src/backend/Mustask.Domain/Boards/CardChecklist.cs:18`
  `Holds` has no production caller, only `CardChecklistTests.cs:25`. Fix:
  drop it with its test, or use it.

### What holds (checked)

- The backend read is scoped and fixed-count. `BacklogReadModel.cs:65-70`
  filters by project, parent among the board's cards, not Removed, kind by
  token, ordered by number, with no Область filter. Assignees are read in
  batches (`:203`), and `ix_work_items_project_id_parent_id` serves the read.
  No IDOR: the parents are the board's own items, after GetBoard's outside-in
  404s (`GetBoardHandler.cs:30-55`). The field is additive and `[]` when
  absent (`BoardViews.cs:87`). No migration: the converter and
  `AppearanceMaxLength` take the new token (`WorkItemTypeConfiguration.cs:27-29`).
- The write paths are mod-003's PATCH and feat-027's POST, under the board's
  one `inFlight` (`useChecklists.ts:309`, `:362`; `BoardColumns.tsx:129`,
  `:168`). The `requestId` is kept for every retry of an opening (`:312`).
- Tests check what they claim. The 25 000-child volume test asserts every
  checklist's length. The drag-guard tests assert that `dragstart` is refused
  and no drop target appears. Gap: nothing pins the memo (F-1).

### Behavioural claims (item 6)

Direction 1, the prose the diff adds:
- api-contract § 1 (1)-(4), "nothing else narrows it" → `BacklogReadModel.cs:65-70`.
- api-contract § 2.1 `[]`, never absent → `BoardViews.cs:87`.
- api-contract § 1, fixed-count read in batches → `BacklogReadModel.cs:59`, `:203`.
- api-contract § 4, no migration → `WorkItemTypeConfiguration.cs:27-29`.
- BR-C-3 done = Completed, Resolved is not → `lib/checklist.ts:25-27`.
- BR-C-2 «0/0» on a story only → `lib/checklist.ts:56`, `lib/addChild.ts:47`.
- BR-C-4 kept across reads, reset per visit → `useChecklists.ts:141`, `BoardPage.tsx:44`, `:118`.
- BR-C-7 tick/untick targets, no-Completed disabled → `lib/checklist.ts:133-144`, `CardChecklists.tsx:313`.
- BR-C-8 one write at a time → `BoardColumns.tsx:129`, `useChecklists.ts:309`, `:362`.
- BR-C-9 create beneath the card, focus back → `useChecklists.ts:320-352`.
- BR-C-10 nothing fetched on open → `useChecklists.ts:197-211` (`toggleList` changes state only).
- BR-C-11 "recorded on its queue line" → **F-2**.
- BR-C-12 drag guard → `BoardColumns.tsx:433-446`, `CardChecklists.tsx:101`.
- D-5 counter name → `lib/checklist.ts:81-89`.
- NFR 200 children within 100 ms; design § 7.3 memo → **F-1**, **m-3**.
- `useChecklists.ts:65`, `:81` "Stable"; `CardChecklists.tsx:280` → **F-1**.
- Design § 9 Firefox fallback → **m-2**.
- Design § 4.2, the avatar is not a tab stop → `Marks.tsx` `AssigneeAvatar` (no `tabIndex`).

Direction 2, behaviour the diff changes, grepped under `docs/`, `.claude/`,
the `AGENTS.md` files and `src/`:
- The board card gains `checklist`. Grepped `board card`, `BacklogItem`,
  `BR-M4-14`. Hits: mod-006 api-contract `:140`, `:268`, spec `:195`, `:309`
  (owed, m-5). feat-027 api-contract § 2.3 "no `children`" still holds,
  because the field is named `checklist`.
- The token set goes from five to six. Grepped the five-token list and "five
  tokens". Hits: mod-001 (owed), ADR-0024, mod-015, mod-003 (m-5), two
  frontend comments (m-6), `WorkItemField.cs:117` (m-4). doc-007 and the
  mod-015 status and review files are history, not swept.
- The memo of `BoardCard`. Grepped `Memoized`. Hit: `BoardColumns.tsx:394` (F-1).
- Not swept one by one: mod-015 `design.md:300`, `:1058` and `US-10:31` ("five
  tokens" on the theme page); they belong to the same m-5 note.

## Round 1 — delta

Scope: `git diff 6475cd9..HEAD`: 76352c4 (F-1, m-6, m-7) and abbfa7f (F-2, m-1,
m-5); 28c760f is status only. Read, not run: a verify.sh run was going on in
the worktree.

Verdict: **approved-with-minors** · blockers 0 · majors 0 · F-1 and F-2
closed · minors: m-2, m-3, m-4, m-8 open as debts, m-1/m-5/m-6/m-7 closed, two
new (d-1, d-2).

- **F-1: closed.** `useChecklists.ts:138-139` keeps only `mutateAsync`, which
  query-core binds once. Every other dependency of `actions` and `env` is
  stable: the `useState` setters, `readAgain` (a `useCallback`), the
  notice callbacks, the refs, and `types` (a cached query object or
  `NO_TYPES`). So `env` changes only when the process does. The test
  (`BoardPage.checklist.memo.test.tsx`) pins it. It wraps `CardChecklists` in
  a memoised counting spy, opens a list, then fires dragstart and a column
  enter. The `data-drop-target` assertion proves `BoardColumns` re-rendered.
  It then expects no new draw. With the old dependency lists, `setDrag` at
  dragstart would give `BoardCard` a new `checklistEnv`. Its memo, and then
  the spy's, would miss, the count would rise, and the test would fail.
- **F-2: closed.** BR-C-11 no longer claims a queue-line record. It names the
  hand-off (status Log), the owner (feat-031) and `WorkItemField.Of(Testcase)`.
  § Decomposition agrees with it. Keeping ADR-0024 untouched is reasoned
  (§ 3 allows the growth) and left to the human at gate-3.
- **m-1:** accepted in design § 7.2. **m-5:** the notes are in mod-001, mod-003,
  mod-006 and mod-015 at the places listed. **m-6, m-7:** fixed.

New (minor, docs):
- **d-1.** `api-contract.md:161` and `spec.md:66` say "feat-031 owns adding
  `testcase` to mod-001's token set". This branch already adds it
  (`AppearanceToken.cs`, `tokens.ts`), and the new mod-001 notes say "the set
  is six". Fix: "feat-031 owns the type of that appearance and the
  `WorkItemField` support".
- **d-2.** `spec.md:66` "the human sent it to the feat-031 session". The status
  Log says the orchestrator sent it, at the human's word. Fix: "the
  orchestrator, at the human's word". The spelling and D-4 still live only in
  another session's context, not in a feat-031 artifact, so the orchestrator
  should check that they reach feat-031's `status.md`.
