# docs/features/ — feature working directories

Each task = a folder `docs/features/<id>/`. This is the task's pipeline
state; any agent (Claude Code, Codex, a human) continues the work by reading
this folder.

**Language.** Artifacts are written in English (`AGENTS.md`, "Hard
rules"). Product interface copy quoted anywhere in these folders stays
Russian regardless of the artifact's language.

**Filled examples.** [`docs/templates/examples/`](../templates/examples/README.md)
holds two finished task folders kept as illustrations of the format — an
`S` tooling task and a full-cycle task with spec, stories, plans, QA and
review. They are not tasks: they live outside `docs/features/` precisely so
that numbering, the queue, `scripts/backlog.sh`, `scripts/new-task.sh` and
`scripts/check-gates.sh` never see them, and their ids and dates are
historical.

## Task id

Format: **`<prefix>-<NNN>-<slug>`** — for example
`feat-001-order-submission-form`, `bug-014-amount-rounding`,
`hot-002-order-pdf-500`.

| Part | Rule |
|------|------|
| `<prefix>` | task type, see the table in [routing.md](../ai-workflow/routing.md): `feat`, `bug`, `hot`, `tech`, `mod`, `doc`. `mod` also marks an `L` line that founds a sub-slice or a shared pattern inside an existing module (an attachment store, a recycle bin), not only a whole module |
| `<NNN>` | sequence number, 3 digits with leading zeros, counter **separate per prefix** (`feat-001` and `bug-001` exist at the same time). The number is issued the moment the line is put into the `docs/backlog.md` queue by a commit to `main` — see [numbering.md](numbering.md) |
| `<slug>` | kebab-case, Latin script, 2-4 words on the task's substance |

The numbering rules — who issues a number, how occupancy is computed
across every branch, what happens to a number when a task is renamed or
withdrawn — are in [numbering.md](numbering.md). They are read when a task
is set up, and by nobody else: writing a spec, a design or a review needs
the id, not the procedure that produced it.

## Branch

`<prefix>/<NNN>-<slug>` — the same id with the first hyphen replaced by a
slash: `feat/001-order-submission-form`, `bug/014-amount-rounding`. This way
branches group by type (`git branch --list 'bug/*'`), and the id stays
recoverable.

## Folder contents

| File | Appears at stage | Purpose |
|------|-------------------|---------|
| `status.md` | start | current state only: stage, gates, plan, standing decisions, recent log |
| `review.md` | 2, 8 | review findings and verdicts — requirements review and code review, by round |
| `history.md` | on demand | audit trail: rolled-off log entries, superseded decisions, run history |
| `spec.md` | 1 | the index: context, roles, BPMN, business rules, NFRs, out of scope, decomposition, and the story table — id, one-line title, `depends-on`, file (template: templates/feature-spec.md) |
| `stories/US-<N>-<slug>.md` | 1 | one user story: header (`story`, `depends-on`, `shippable`, `status`) + Gherkin criteria (template: templates/user-story.md) |
| `design.md` | 3 | screens, MUI components, states (template: templates/design-spec.md) |
| `plan-backend.md`, `plan-frontend.md` | 4, 6 | implementation plans — for `L` only; for S/M the plan is written as the `## Plan` section in `status.md` |
| `api-contract.md` | 4 | API contract — the backend ↔ frontend interface (mandatory whenever there is an API) |
| `qa.md` | 5, 7 | a single test plan and defects, Backend and Frontend sections (template: templates/test-plan.md) |
| `prototype/` | 3 | HTML prototypes (optional) |

For `F` nothing is created besides `status.md`: no `spec.md`, no
`design.md`, no `plan-*.md`, no `qa.md`. If any of them turns out to be
needed — then the task is not `F` (see
[f-route.md](../ai-workflow/stages/f-route.md)).

## Story files

`spec.md` is the index, not the whole spec: it carries what does not belong
to any one story — context and goal, roles, the BPMN, business rules,
operation completeness, NFRs, out of scope, the decomposition table — plus a
table of stories (id, one-line title, `depends-on`, file). Each user story is
its own file, `docs/features/<id>/stories/US-<N>-<slug>.md`, with its own
Gherkin scenarios and a header:

```markdown
# US-<N>: <title>

- story: US-<N>
- depends-on: US-1, US-3 | -
- shippable: yes | no — <one line: why not>
- status: pending | in-progress | done
```

`depends-on` is what lets two independent stories be worked in two sessions —
a stage reads the index plus the story it implements plus that story's
`depends-on` chain, not the whole folder. `shippable` is what the per-story
delivery rule keys on
([stages/01-business-analysis.md](../ai-workflow/stages/01-business-analysis.md),
"Per-story delivery"): a story that is independently verifiable and leaves no
capability nothing uses becomes its own queue line at decomposition instead
of a file here. The one role that always reads every story regardless of
`depends-on` is the requirements reviewer — its job is the whole picture, not
one story's slice of it. The per-file line budget is stated once, in
[stages/01-business-analysis.md](../ai-workflow/stages/01-business-analysis.md)
— not repeated here.

## status.md format

```markdown
# <id>

- type: feature | bugfix | hotfix | tech | module | docs
- size: F | S | M | L
- branch: <prefix>/<NNN>-<slug>
- stage: 01-business-analysis | 02-requirements-review |
         gate-1 | 03-ux-design | gate-2 | 04-backend | 05-frontend |
         06-code-review | gate-3 | complete
- epic: <wave/module | platform/<area> | workflow>
- deps: <short ids comma-separated | ->   <!-- optional, see below -->
- gates: gate-1, gate-2, gate-3   <!-- applicable to the route; agent writes this -->

## Gates

<!-- Only the gates from the `gates:` line. There are no non-applicable
     ones here — there is nothing to "skip". The value is set exclusively
     by the human. -->

gate-1: pending | approved | rejected: <reason>
gate-2: pending | approved | rejected: <reason>
gate-3: pending | approved | rejected: <reason>

## Plan

<!-- for F: one line — what is being fixed, the checklist item, the
     verification command. For S/M: a 3-10 line implementation plan (S —
     the start stage, M — stages 4/6). For L, instead of this section —
     the files plan-backend.md / plan-frontend.md. -->

## Decisions

<!-- Standing decisions that explain why the current state is what it is:
     an api-contract change, an escalation that was resolved, a deviation
     from a convention. One line each, in force right now. How the decision
     was arrived at — the discussion, the rejected alternatives, the round
     that produced it — belongs in history.md; a decision significant enough
     for an ADR goes to docs/adr/ (docs/adr/README.md). -->

## Log

<!-- Newest first. One entry is one line of at most 300 characters: who,
     what happened, the commit or the verdict. The details are in the commit
     message, review.md or qa.md, and the entry points there. Recent entries
     only: when status.md outgrows its budget, the oldest entries move to
     history.md verbatim. -->

- 2026-08-21 [business-analyst] spec.md ready, 2 open questions
- ...
```

### Size budget

`status.md` answers "where is this task now", not "how did it get here".
That is a budget, and it is checked:

| Lines | Bytes | Meaning |
|-------|-------|---------|
| ≤ 100 | ≤ 8 KB | target |
| > 150 | > 12 KB | warning — roll the oldest `## Log` entries into `history.md` |
| > 250 | > 24 KB | `scripts/verify.sh` fails on a `status.md` you changed |

Either column trips the row. The bytes column exists because the lines one
alone was beaten by long lines: feat-027's `status.md` had 73 lines and 35 KB,
single `## Log` entries of 1–3 thousand characters, and every agent entering
the task read all of it. `verify.sh` also counts, per file, the `## Log` entries over 300
characters — a note, not a failure: the cure is to move the detail into the
commit message or `review.md` and leave the pointer.

**Which tasks this applies to.** The format is mandatory for new tasks and
for tasks currently in flight — the latter adopt it at their next stage, not
by a rewrite. Folders of tasks already closed are archives: they keep the
old shape, nobody splits them retroactively, and their size is not a finding.
The check therefore looks only at a `status.md` that the current change
actually touches; an untouched archive never fails it.

### Artifact budgets

Every agent after a stage reads what that stage wrote, so an artifact's size
is paid again by each of them. `spec.md` and the story files keep their line
budgets where they are stated,
[01-business-analysis.md](../ai-workflow/stages/01-business-analysis.md),
"Rules". The rest:

| Artifact | Budget | Over it |
|----------|--------|---------|
| `design.md` | 32 KB | the designer returns a split proposal to the orchestrator instead of writing on ([03-ux-design.md](../ai-workflow/stages/03-ux-design.md)) |
| `review.md` | 16 KB a round, 32 KB the file | the round is written shorter: findings, not essays ([06-code-review.md](../ai-workflow/stages/06-code-review.md)) |
| `qa.md` | 16 KB a pass or an E2E run, 32 KB the file | the section is written shorter: failures and totals, not a row per passing test ([04-backend.md](../ai-workflow/stages/04-backend.md), § 5) |

`scripts/verify.sh` names a changed `design.md`, `review.md` or `qa.md` of a
task in flight that has passed 32 KB — a note, not a failure. Designs already
in flight stand at 75–212 KB, and rewriting an approved design to fit costs
more than the reading it saves; a new task meets the budget as it writes.
The per-round and per-run numbers are for the author, not the script.

> Introduced on 2026-10-12 by tech-023. Across the folders in `main`,
> `design.md` averaged 72 KB, `review.md` 53 KB and `qa.md` 44 KB;
> feat-027 alone carried a 77 KB design, a 154 KB review and a 28 KB QA
> report into every session that came after them.

### What goes where

| Content | File |
|---------|------|
| current stage, gates, plan, standing decisions, recent log | `status.md` |
| review findings, severities, resolutions, verdicts, rounds | `review.md` |
| rolled-off log, superseded decisions, run history, resolved defects | `history.md` |
| test plan, coverage, defects | `qa.md` |
| significant architectural decision | `docs/adr/` |

Review findings never live in `status.md`, in `spec.md`, or in a story file.
A downstream stage reads `status.md` and the artifacts it needs; it does not read
`review.md` or `history.md` unless it has a reason to.

Rules:

- The `stage:` line is changed by whoever finished the stage; the log is
  append-only within `status.md`, and rolling entries into `history.md`
  preserves them verbatim — the audit trail is moved, never trimmed.
- **Which gates are applicable** is a consequence of the route, it is the
  technical field `gates:`, and the agent fills it in. **The gate's value**
  is the human's decision, and the agent never writes anything there except
  `pending`. These two different facts used to live on one line
  (`gate-1: skipped (S-route)`), which made the rule "only the human writes
  gate lines" contradict the S-route instructions.
- The `gate-N` lines express the human's decision and only that. The human
  either edits the line themselves, or gives an explicit approve/reject in
  chat — then the orchestrator records
  `approved (confirmed by the human in chat, <date>)`. Inferring an approve
  from silence, from an AI reviewer's verdict, or "from context" is
  forbidden.
- A substantive change to `spec.md` or a story file under `stories/` after
  `gate-1: approved` (or to `design.md` after `gate-2: approved`) resets the
  corresponding gate to `pending` with a line in Log — the feature goes back
  for another approve. Typos and formatting do not reset the gate.
- **`- deps:` is an optional field.** While the task is a line in the
  queue, the source of truth for its dependencies is that line's `deps:` in
  `docs/backlog.md` (the format is there too, at the start of the file).
  After the start, the line is removed from the queue, and there is
  nowhere left to learn the dependencies of an already-started task — for
  this, `- deps:` is duplicated in the front matter of `status.md`:
  `scripts/new-task.sh` carries it over from the queue line when the task
  is set up, the same way it does for `size`/`epic`. The field may be
  absent (tasks set up before this rule, or set up manually) — then
  dependencies simply do not show up for this task, which is not a
  discrepancy and not a reason to
  fix it retroactively. The value format is the same as `deps:` in the
  queue: short ids comma-separated (`tech-091,tech-092`) or `-`, no slugs
  and no `NNN`.
