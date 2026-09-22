# Backlog

**Queue only: tasks that have not been filed yet.** The file lives in `main`
and changes only there — commits by the orchestrator or the human that touch
only this file (the single exception to the ban on direct commits to `main`,
`docs/conventions/git.md`). As soon as a task starts —
`scripts/new-task.sh` has created a branch (that is the claim on the work) —
the orchestrator removes its line from here in a separate commit to `main`.
Finishing a task does not touch the backlog at all. Task branches do not edit
this file — a merge conflict over the queue is impossible.

## Next (ready to work, by priority)
- [ ] doc-001-domain-map — Azure DevOps Boards domain map: glossary, module boundaries, cross-module logical data model, MVP boundary and releases, module order · epic:product/analysis · size:M · deps:- · note:umbrella for doc-002..007; source: ADO research of the Agile process
- [ ] doc-002-work-items — Work items module analysis: types, fields, states and reasons, rules, form, links, comments, attachments, history, tags · epic:product/analysis · size:M · deps:doc-001 · note:-
- [ ] doc-003-backlogs-boards — Backlogs and boards module analysis: backlog levels, ordering, hierarchy, Kanban columns, WIP, swimlanes, cards · epic:product/analysis · size:M · deps:doc-001 · note:-
- [ ] doc-004-iterations-planning — Iterations and planning module analysis: sprints, capacity, taskboard, burndown and velocity, forecast, delivery plans · epic:product/analysis · size:M · deps:doc-001 · note:-
- [ ] doc-005-queries-dashboards — Queries, search, dashboards and analytics module analysis: query language, saved queries, charts, widgets · epic:product/analysis · size:M · deps:doc-001 · note:-

## Later (waiting on analysis/dependencies)

## Withdrawn (number taken, task not done)

Moved to [backlog/withdrawn.md](backlog/withdrawn.md): the section only ever
grows — a number is never reused — and is never read to pick up work. It is
still part of the queue for every rule that mentions the queue: it lives in
`main`, it is edited by the same orchestrator commit, and `scripts/backlog.sh`
counts the numbers in it as taken.

## How the queue works

Previously the file combined four roles, and three of them already live
elsewhere:

| What you need to know | Where it actually lives |
|------------------|------------------------------|
| What is in progress | task branches + `stage:` in their `status.md` |
| What is done | `scripts/backlog.sh --done` — merge date, id, epic, size; the source is `stage: complete` in `docs/features/<id>/` plus `git log` |
| Which numbers are taken | `docs/features/` directories + branch names + numbered queue lines — the max across all three and all branches ([features/numbering.md](features/numbering.md)) |
| **What to take next** | **this file** |

Duplication was expensive: every task edited the shared file twice — at start
and at finish — and parallel sessions reliably got a merge conflict in the
same place. A summary across all four questions: `scripts/backlog.sh`; the queue itself
is edited with `scripts/queue.sh add|take|withdraw`, which issues the
number, writes the line by the format above and commits only queue files.

Line format:

`- [ ] <id> — <what it is> · epic:<epic> · size:<F|S|M|L> · deps:<short id|-> · note:<why it matters | ->`

**`epic:`** — what the task belongs to. For product tasks it is
`core/<module>`, for platform and process work — `platform/<area>`
or `workflow`. The field exists so you can tell "what is this even about"
without opening the spec, and so a future split into
`docs/backlog/<cluster>.md` has a ready-made key.

The full hierarchy: **epic** (module or area) → **task** (a line here and a
`docs/features/<id>/` folder) → **user stories and criteria** (in the task's
`spec.md`, stage 1). The intermediate "module" level is expressed by the
second part of `epic:` and by the `deps` invariant: for every `M` the
module's founder (`L`) is reachable through dependencies.

The id format — [features/README.md](features/README.md), the numbering
rules — [features/numbering.md](features/numbering.md).
A task gets its number at the moment its line is put here, by a commit to
`main` — and only that way; `scripts/new-task.sh` does not issue numbers
(except for `hot`) and files a task strictly from the queue line, character
by character.

**When to split.** A queue entry is one long line with a `note:` — about
370 bytes — so counting lines measures the wrong thing: 19 entries already
cost 7 KB, and "80 lines" would not fire until 30 KB. The threshold is
entries, and the order is fixed, cheapest first:

| Trigger | What moves |
|---|---|
| a section only ever grows and is never read to pick work | out of this file entirely — that is how `Withdrawn` left |
| `Later` past ~40 entries | `docs/backlog/later.md` |
| `Next` past ~40 entries | `docs/backlog/<cluster>.md` by epic cluster, this file left as the table of contents |

Everything under `docs/backlog/` is the queue for every rule that says
"queue": it lives in `main`, one orchestrator commit may change any of it
(`.githooks/pre-commit`), and `scripts/backlog.sh` reads all of it when it
counts taken numbers.
