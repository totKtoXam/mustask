# Routing: which route a task takes

## Classification

The orchestrator classifies every incoming task along two axes.

### Task type

| Type | id prefix | Examples | Route |
|-----|-----------|---------|---------|
| `bugfix` | `bug` | a request fails, a wrong calculation | Dev → QA → Review → PR (stages 1–3 are skipped) |
| `hotfix` | `hot` | prod is down, a critical defect hitting users | S route from `main`, outside the queue; gate-3 and green CI are mandatory |
| `tech` | `tech` | refactoring, a dependency upgrade, CI | Dev → QA → Review → PR |
| `feature` | `feat` | new user-facing functionality | the full route |
| `module` | `mod` | creating a whole module | the full route, decomposition into features |
| `docs/analysis` | `doc` | only research or a document | stages 1–2, without development |

The id format is in [docs/features/README.md](../features/README.md), the
numbering rules in [docs/features/numbering.md](../features/numbering.md).
`hotfix` differs from `bugfix` only by urgency and by the branching point: it
is always S, always from a fresh `main` and does not wait in the backlog queue
— but it goes through the same test, review and gate-3. "Urgent" cancels none
of them.

### Size

| Size | Criterion | What changes |
|--------|----------|--------------|
| `F` | a trivial change with no change of behaviour: a typo, a comment, formatting, a broken link | Neither spec, nor design, nor plan. No separate QA and review sessions: verification is one deterministic command, the review is the human at the final gate. Closed admission list: [stages/f-route.md](stages/f-route.md) |
| `S` | ≤ ~2 h of work, 1–5 files, one layer | Without spec and design. The plan is 3–5 lines in `status.md`. One human gate — the review before the PR. Process: [stages/s-route.md](stages/s-route.md) |
| `M` | one feature in an existing slice | `spec.md` (index) + story files in a shortened form (BPMN optional), design only if there are new screens |
| `L` | **creating** a new slice/module or any of the "`L` thresholds" below | The full pipeline, all 3 gates |

Understating the size is forbidden: if you hesitate between M and L — take L.

### Route confirmation

The classification is **always confirmed by the human** — before creating the
branch (the claim on the work) and before the first change. The number already
exists by then: it was issued to the line when it was put into the `main`
queue, long before the classification — the route confirmation reserves not
the number but the right to start the work. Not "when in doubt": an agent that
got the size wrong is precisely the one that feels no doubt, otherwise it
would have taken the larger mode. It is cheaper to ask once than to discover
skipped gates at the review stage.

If the size confirmed by the human diverges from the `size:` in the queue
line, the one confirmed by the human is written into `status.md`, and the
divergence as a line in `## Log`
([../features/README.md](../features/README.md)).

The orchestrator shows the block and stops:

```
ROUTE · <the gist of the task>

Type:    <feature | bugfix | hotfix | tech | module | docs> (prefix <...>)
Size:    <F | S | M | L> — <one line: why exactly this one — by the checklist>
Route:   <stages through arrows — cross out the extra ones>
Gates:   <applicable ones; inapplicable ones with the reason why they have no subject>
Artifacts: <spec.md / design.md / plan / only status.md>

Confirm or name a different size.
```

The human's answer is recorded as a line in the task's `## Log`. Silence does
not count as a confirmation.

The confirmation itself is asked as options, not as prose — the same three
parts as any question to the human, in [feature.md](feature.md), "Gates and
defect loops". The block above is what the options are about; it does not
replace them.

A change of the classification in the course of the work (escalation `F` →
`S`, `M` → `L`) is a new route, and therefore a new confirmation. Lowering the
size after the confirmation is not allowed at all: see
[stages/f-route.md](stages/f-route.md).

For `F` the understatement rule is stricter than the rest: **any change of
observable behaviour raises the task to at least `S`**, and `bugfix` and
`hotfix` are never `F` — a bug has a test that failed before the fix, that is,
a change of behaviour by definition. If it turns out along the way that the
change is harder than expected, the task becomes `S` and does not come back
to `F`.

**A clarification for the greenfield stage of the project.** What is expensive
is precisely the *opening* of a new slice: the module boundaries, the data
model, the API contract, the first migrations. That is why `L` is the
**first** task in a slice, the one that creates it. Subsequent tasks inside an
already created slice are `M`, even if they add screens and migrations: the
skeleton is already set and has been checked at three gates.

Without this clarification, at the start of a project where existing slices do
not exist by definition, every task would get size `L`, and the number of
human gates would grow linearly with the backlog — the gates become devalued
and turn into a formality.

The rule does not cancel the ban on understating: if a task inside an existing
slice changes its boundaries, the data model or the API contract **in a
breaking way** — it is `L` again. Adding something new (a table, a column, a
DTO field, an endpoint) is not a breaking change; renaming, deletion, a change
of the type or the semantics of something existing is breaking.

> Introduced on 2026-08-22 following a decomposition review, the human's
> decision.

### Size determination checklist

Filled in by the agent **before** the `ROUTE` block and shown together with
it. Blocks 0–2 the agent answers itself from the code and the task description
— except the population of the tables (question 3.2), which must be asked of
the human before answering the migration threshold. The remaining questions of
block 3 are asked of the human — only those that are relevant to the task.

**Block 0 — triviality.**

| # | Question | Consequence |
|---|--------|-----------|
| 0.1 | Does the observable behaviour of the system change — even for a single user, test or integration? | "yes" → block 1. "no" → `F`, only if the change is in the closed admission list [stages/f-route.md](stages/f-route.md) **and** block 1 is clean (a task without a change of behaviour but with an ADR threshold is `L` anyway); otherwise `S` |

**Block 1 — signs of `L`.** Go through the "[Thresholds at which the size must
be `L`](#thresholds-at-which-the-size-must-be-l)" list below — it is block 1
itself, one question per threshold. Any "yes" = `L`, the remaining questions
need not be asked. The only source of the wordings is the threshold list
itself: it is not duplicated here so that the two do not drift apart (in the
first editions the duplicate diverged from the original twice in one day). For
the migration threshold the population of the tables is clarified by question
3.2 — ask it of the human before answering.

**Block 2 — the `S`/`M` boundary** (if block 1 is clean).

| # | Question | Consequence |
|---|--------|-----------|
| 2.1 | Does the work fit into ~2 h **and** 1–5 files **and** one layer? | all three "yes" → `S`; any "no" → not `S` |
| 2.2 | Is the migration reversible with respect to data (the migration threshold has already given "no")? Additive (a table, a column, an index, a type widening) or a reversible schema change (dropping an index, a constraint) | a reference question: it records the nature of the migration in the `ROUTE` block; it no longer affects the size |
| 2.3 | Is more than one layer touched (domain + API + screen), but all inside its own module? | "yes" → `M` |

**Block 3 — questions for the human** (about what is not in the code).

| # | Question | Why |
|---|--------|-------|
| 3.1 | Does the task have a regulatory basis (Rules, a regulation, a protocol with an external party) that has to be checked against? | The legal cost of a mistake may raise it to `L` |
| 3.2 | Do the tables the migration touches contain data (prod or stands with important data)? | Input for the migration threshold: an irreversible transformation over a populated table = `L` |
| 3.3 | Is more expected from the task than is written in its line? | The main source of "`M` grew into `L`" escalations |

**The inference rule:**

```
F — block 0 gave "no" + f-route admission + not a single L threshold fired
L — at least one threshold from the "L thresholds" list fired
S — the thresholds are clean and all three conditions of 2.1 are met
M — everything else
```

Block 3 does not change the size automatically: 3.2 is the input for the
migration threshold, and the answers to 3.1 and 3.3 are attached to the
`ROUTE` block — whether to raise the size because of them is decided by the
human at the confirmation.

A doubt between two sizes always means the larger one. The checklist answers
go into the `ROUTE` block as the line "Size: … — why" and are recorded in the
task's `## Log` together with the human's confirmation.

> Introduced on 2026-08-23: the human requested an explicit
> checklist instead of the agent's implicit judgement; the gradation of
> migration risks comes from the human.

### Thresholds at which the size must be `L`

**The type of a task does not limit its size.** The type table says which
stages are gone through, not how big the task is: `tech` happens to be both
`S` and `L`. Until this was written down, the `tech` prefix worked as an
unspoken pass to the short route.

A task is `L`, even if by its description it looks like a technical trifle,
when at least one of the following holds:

- a DB migration is **irreversible with respect to data** — a rollback by
  itself will not bring the data back (a move between tables, a transformation
  with truncation, deletion, splitting a table) — **and the affected tables
  already contain data** (the population is confirmed by the human, question
  3.2 of the checklist). Adding tables, columns and indexes of **one's own**
  module does not count as a threshold (agreed with greenfield above); adding
  into someone else's module is a change of its boundaries, the threshold
  below;
- a **new project** appears in the solution;
- an **ADR** is accepted — that is, a decision that will outlive the task;
- a new slice or module is created (see the greenfield clarification above);
- the boundaries of an existing slice, the data model or the API contract
  change **in a breaking way**. Breaking = renaming, deletion, a change of the
  type or the semantics of something existing; adding something new (a table,
  a column, a DTO field, an endpoint) is not breaking;
- the **exchange with an external counterparty** changes, whose behaviour we
  do not control: a mistake there cannot be fixed with a single deploy;
- the **authentication/authorization rules or the handling of secrets**
  change: the price of an unnoticed mistake is an open endpoint or a leak.

**`hotfix` and the thresholds.** A hotfix always goes the S route (see the
type table) — even if the incident falls into thresholds 1.5/1.6: putting out
a prod fire with the full pipeline is not allowed. The compensation: gate-3
and green CI are mandatory, and before the hotfix is merged a full-route tech
task for reworking the same place is put into the queue.

**Escalation by volume.** If in the course of the work the diff has become
twice as large as was expected at the route confirmation — stop, reclassify
and ask for the confirmation again. Lowering the size is not allowed under any
circumstances.

> Introduced from the experience of the original project: a task with
> `size: M` grew to 32 files and 2418 lines, created a new test project, the
> first migrations and an ADR — and at the same time went past `gate-1` and
> `gate-2`, because the short route relied on the `tech` prefix and not on the
> scale. The migration item was clarified later: the original formula
> "DB migrations are added or changed" declared any migration `L` and
> contradicted the greenfield clarification; the threshold became
> irreversibility with respect to data.

### Rules for skipping stages

- Stage 3 (design) is skipped if there are no UI changes.
- Stages 6–7 (frontend) are skipped for purely backend tasks, and vice versa.
- **The E2E question is asked once per task**, at whichever QA stage runs
  last before code review — frontend QA when the task has one, backend QA
  otherwise ([04-backend.md](stages/04-backend.md), § 5;
  [05-frontend.md](stages/05-frontend.md), § 7). Skipping a QA stage moves
  where the question is asked, it never removes it — except on the `F` and
  `S` routes, which say this themselves
  ([f-route.md](stages/f-route.md), [s-route.md](stages/s-route.md)).
- Human gates are never skipped. But a gate can be **inapplicable**: stage 3
  was skipped — `gate-2` has no subject, and it does not appear in `status.md`
  at all. The applicable gates are listed in the technical field `gates:`;
  their value is still set only by the human
  ([../features/README.md](../features/README.md)).

### Route shortcuts for `M`

The calendar time of a task is eaten not by the stages but by waiting for the
human: agent steps run in minutes, a gate in hours or days. That is why `M`
has two shortcuts:

- **`gate-1` and `gate-2` are shown as a single package** after stage 3. The
  spec and the design of an `M` are small, and keeping a separate stop between
  them means paying with a second wait for a look at a couple of screens. For
  `L` the gates stay separate: there the design leans on the approved spec so
  much that doing it before the approve is a risk of rework.
- **One QA pass instead of two.** For `M` stages 5 and 7 merge into a single
  QA after the frontend: the acceptance criteria are shared, and two test
  plans for one small feature is duplication. For `L` the two passes remain.

Both shortcuts are about logistics, not about strictness: code review, tests
together with the code and the final gate are not touched at any size.

## Model tiers and parallelism

Which model executes a stage, and what may run at the same time, are in
[dispatch.md](dispatch.md).
