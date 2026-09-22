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
- [ ] doc-002-work-items — Work items module analysis: types, fields, states and reasons, rules, form, links, comments, attachments, history, tags · epic:product/analysis · size:M · deps:doc-001 · note:-
- [ ] doc-003-backlogs-boards — Backlogs and boards module analysis: backlog levels, ordering, hierarchy, Kanban columns, WIP, swimlanes, cards · epic:product/analysis · size:M · deps:doc-001 · note:-
- [ ] doc-004-iterations-planning — Iterations and planning module analysis: sprints, capacity, taskboard, burndown and velocity, forecast, delivery plans · epic:product/analysis · size:M · deps:doc-001 · note:-
- [ ] doc-005-queries-dashboards — Queries, search, dashboards and analytics module analysis: query language, saved queries, charts, widgets · epic:product/analysis · size:M · deps:doc-001 · note:-

## Later (waiting on analysis/dependencies)
- [ ] mod-001-process-seed — Founds M2: the five built-in types (Эпик, Фича, Пользовательская история, Задача, Баг) with per-type states/categories and backlog levels, seeded per organization, no editing UI (doc-001 D-3/D-6) · epic:core/process · size:L · deps:doc-007 · note:doc-001 decomposition (b)
- [ ] mod-002-organizations-projects-teams — Founds M1: Organization/Project/Team CRUD, instance-level 'create organizations' permission, Area tree with team ownership, ORG_MEMBERSHIP holding the admin/member/viewer role that scopes permission codes; owes an ADR-0018 amendment (doc-001 D-9/D-10) · epic:core/organizations · size:L · deps:doc-006,mod-001 · note:doc-001 decomposition (b)
- [ ] mod-003-work-item-core — Founds M3: WorkItem CRUD across the five types (Заголовок/Описание/Статус/Исполнитель/Область/Приоритет/Теги), history (BR-4), work item identifier scheme; owes the history/audit ADR · epic:core/work-items · size:L · deps:mod-002,mod-001,doc-002 · note:doc-001 decomposition (b)
- [ ] feat-001-work-item-links — Parent/Child link across the five types · epic:core/work-items · size:M · deps:mod-003 · note:doc-001 decomposition (b)
- [ ] mod-004-work-item-attachments — Founds the attachment storage pattern on a work item; owes the attachment-storage ADR · epic:core/work-items · size:L · deps:mod-003 · note:doc-001 decomposition (b)
- [ ] feat-002-work-item-comments — Comment add/edit/delete with moderation (BR-4) · epic:core/work-items · size:M · deps:mod-003 · note:doc-001 decomposition (b)
- [ ] mod-005-work-item-delete-restore — Founds the soft-delete/recycle-bin pattern: delete → «Удалена», restore (BR-6/BR-10); owes the soft-delete ADR · epic:core/work-items · size:L · deps:mod-003 · note:doc-001 decomposition (b)
- [ ] mod-006-boards-backlogs — Founds M4: one board per team per level (Эпики/Фичи/Истории), the ordered backlog per team per level incl. stack rank (BR-12) · epic:core/boards · size:L · deps:mod-003,doc-003 · note:doc-001 decomposition (b)
- [ ] mod-007-iterations — Founds M5: project-owned iteration list, team iteration selection and default, the work item's iteration pointer, current-iteration derivation (BR-9, D-11) · epic:core/iterations · size:L · deps:mod-003,doc-004 · note:doc-001 decomposition (b)
- [ ] feat-003-work-item-search — Inline filter of the work item list and board by state/assignee/tag/area (M3/M4 surfaces, D-4) · epic:core/work-items · size:M · deps:mod-003,mod-006 · note:doc-001 decomposition (b)

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
