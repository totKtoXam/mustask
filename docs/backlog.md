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

- [ ] mod-001-process-seed — MVP. Founds M2: the five built-in types with keys EPIC/FEAT/US/TASK/BUG (doc-002 W-11), ordered states per doc-010 D-4 (Задача per doc-002 W-2), the five categories with Russian labels (doc-010 D-5), backlog levels with rank and kind, seeded per organization, plus the read-only process screen; registers process.definition.read; no editing UI · epic:core/process-customization · size:L · deps:doc-007,doc-010 · note:doc-001 decomposition (b)
- [ ] mod-014-keycloak-user-projection — MVP. Founds the Keycloak user projection: a custom Keycloak event-listener provider pushes user created/updated/disabled into MUSTASK, a backfill of existing users; member search and the «неактивен» marking read the projection; no admin REST call and no production service account, ADR-0023 guards unchanged; owes the user-projection ADR (doc-006 constraint 5, doc-010 D-9) · epic:core/org-access · size:L · deps:doc-006,doc-010 · note:doc-010 D-9; L: new deployable, external counterparty, secrets, ADR
- [ ] doc-011-release-2-scope — Release 2. Scope analysis: spec rows and one owning module for the release-2 items that have none (notifications, audit incl. BR-M1-15, bulk edit, templates, copy/move, other link types, change type, tag management, @mentions, WIP limits, custom columns/swimlanes, card styling, several boards, live push, level toggle, parent grouping rows, per-project and per-area overrides, team area sets, instance-rights screen); settles the notification and audit double claims; names the one line that owes the instance-rights ADR-0018 amendment; assigns every new line a milestone · epic:product/analysis · size:L · deps:doc-010 · note:doc-010 D-18, D-24; gates every release-2 implementation line (doc-010 PR-5); L: scopes authorization overrides across M1/M3/M4 and assigns an ADR (doc-010 PR-2)
- [ ] mod-015-app-shell-design-system — MVP. Founds the panel's look and the screen patterns every module's design.md builds on: the application shell (top bar with the organization/project/team context switcher, left navigation, page header, content area), the MUI theme in app/theme (palette, typography, density, icons), and the reusable patterns (registry with a filter bar, work item form, board with columns, ordered backlog, dialogs, empty/loading/error states) as design.md plus a clickable prototype, then implemented in app/theme, AppLayout and shared/ui with the existing screens restyled; the visual model is the tracker captured in doc-001's screenshots, reproduced in layout, navigation, density and colour language and never named; the context switcher is designed now and wired when mod-002 lands · epic:platform/ui · size:L · deps:doc-001,doc-010 · note:the human wants the panel to look like the reference tracker; no product names in artifacts, code, UI copy or commits; L: founds the shell and theme every screen reuses
## Later (waiting on analysis/dependencies)
- [ ] mod-002-organizations-projects-teams — MVP. Founds M1: organizations (the create right is granted by a Keycloak realm administrator, doc-010 D-10), ORG_MEMBERSHIP with admin/member/viewer and who/when, the last-admin and «неактивен» rules, member search on the user projection; projects with an immutable key (doc-002 W-10); teams: create, rename, membership, per-team default area (doc-010 D-7), soft delete with cascade (doc-010 D-6); the area tree, root owned by the first team; soft deactivate/restore; the organization settings screen mod-004 extends; owes the ADR-0018 amendment (doc-006 constraints 1–8, ownership predicate doc-010 D-11) and the soft-deactivation ADR · epic:core/org-access · size:L · deps:mod-001,mod-014 · note:doc-001 decomposition (b), amended by doc-004 E-5
- [ ] mod-003-work-item-core — MVP. Founds M3: create dialog, form and common field set, state changes, the identifier КЛЮЧПРОЕКТА-ТИП-№ resolved inside the organization (doc-010 D-8), stored project tags, the list surface, history, the M3 data model, assignee markings, the area-deletion check counting every item; owes the history/audit ADR (scoped for BR-M1-15, Was Ever, metrics) and the client-visible concurrency-token ADR · epic:core/work-items · size:L · deps:mod-002,doc-002 · note:doc-001 decomposition (b)
- [ ] feat-001-work-item-links — MVP. Parent/Child links across the five types with BR-M3-8's refusals, the atomic replace-parent the backlog mapping uses, unlinking from a deleted parent (BR-M3-9); no link write on a «Удалена» item (doc-001 BR-14) · epic:core/work-items · size:M · deps:mod-003 · note:doc-001 decomposition (b)
- [ ] mod-004-work-item-attachments — MVP. Founds attachment storage on each organization's own Google Drive: connect/check/disconnect on the organization settings (doc-002 US-13), upload, list, download, remove one's own (doc-010 D-11), admin moderation, a purge deletes the item's files; per-organization secrets; owes the attachment-storage ADR · epic:core/work-items · size:L · deps:mod-005 · note:doc-001 decomposition (b)
- [ ] feat-002-work-item-comments — MVP. Обсуждение: add; edit or delete one's own comment under the ownership predicate (doc-010 D-11) with an edited marker; admin moderation; history (BR-M3-12); no comment on a «Удалена» item (doc-001 BR-14) · epic:core/work-items · size:M · deps:mod-003 · note:doc-001 decomposition (b)
- [ ] mod-005-work-item-delete-restore — MVP. Founds the soft-delete pattern: delete → «Удалена», restore to the previous or a chosen state, the organization administrator's purge (doc-010 D-13), doc-001 BR-14 for the surfaces and writes that exist when it ships, no cascade (BR-M3-9); owes the recycle-bin ADR, which references mod-002's soft-deactivation ADR · epic:core/work-items · size:L · deps:mod-003 · note:doc-001 decomposition (b)
- [ ] mod-006-boards-backlogs — MVP. Founds M4: one board per team per level (Эпики/Фичи/Истории) for every team incl. teams and items that predate it (entries backfilled by identifier), columns mapped per (type, state) (doc-010 D-4/D-5), the ordered backlog with stack rank, create a work item from a team's board or backlog with that team's default area (doc-010 D-7), a deleted team's boards and backlogs hidden (doc-010 D-6), deleted items hidden and restored to position, the stale-move notice; reuses the concurrency ADR; no hierarchy, no filter bar · epic:core/backlogs-boards · size:L · deps:mod-005,doc-003 · note:doc-001 decomposition (b), refined by doc-003 (DD-3, R-23) and doc-004 E-5; planning pane is feat-009
- [ ] mod-007-iterations — MVP. Founds M5: the iteration tree of releases and numbered sprints (doc-004 E-7, E-10..E-12), team selection and default, the work item's iteration pointer, the current iteration on the server's UTC date (doc-001 BR-9), confirmed soft removal that clears and counts every pointer incl. deleted items, a deleted team's settings dropped (doc-010 D-6) (doc-004 US-1..US-4, US-14) · epic:core/iterations-planning · size:L · deps:mod-003,doc-004 · note:doc-001 decomposition (b), refined by doc-004
- [ ] feat-003-work-item-search — MVP. List filters on the M3 work item list: state incl. «Удалена» (the way into the recycle bin), assignee incl. former members, tag, area with sub-areas, keyword over Заголовок and the identifier (doc-002 US-11, doc-010 D-14) · epic:core/work-items · size:M · deps:mod-005 · note:doc-001 decomposition (b)
- [ ] feat-004-backlog-hierarchy — MVP. Backlog parent/child hierarchy on M4: expand/collapse display of M3 Parent/Child links and the parent-mapping write from the backlog view, one level up, through feat-001's replace-parent (doc-003 US-3, BR-M4-9..BR-M4-11) · epic:core/backlogs-boards · size:M · deps:mod-006,feat-001 · note:doc-003 decomposition (DD-3)
- [ ] mod-008-queries-dashboards — Release 2. Founds M6: Query/QueryFolder, the clause-builder model incl. Under on Область and Итерация and a keyword clause (doc-010 D-16), personal «Мои запросы» under the personal-object code in every bundle (doc-010 D-11/D-12), shared «Общие запросы» with org-role permissions, soft delete/restore separate from the project's status, the organization administrator's purge (doc-010 D-13), queries bound to a deleted team stop (doc-010 D-6), results with column selection (doc-005 US-1..US-3) · epic:core/queries-dashboards · size:L · deps:feat-003,mod-007,doc-005,doc-011 · note:doc-005 decomposition
- [ ] mod-009-dashboards-widgets — Release 3. Founds Dashboard/DashboardWidget at team and project level (D-15): a protected default team dashboard seeded for new and existing teams, additional team and project dashboards, query-tile and markdown widgets, the 10-widget cap, soft delete/restore (doc-005 US-5 baseline, BR-M6-9); the organization administrator's purge of dashboards and widgets (doc-010 D-13); no dashboard for a deleted team (doc-010 D-6) · epic:core/queries-dashboards · size:L · deps:mod-008 · note:doc-005 decomposition
- [ ] feat-005-csv-import-export — Release 3. CSV import/export of work items from the Queries hub through M3's validation, one row per transaction, UTF-8 BOM, ';'-delimited, 5,000-row cap refused above (doc-005 US-4, BR-M6-8) · epic:core/queries-dashboards · size:M · deps:mod-008 · note:doc-005 decomposition
- [ ] feat-006-dashboard-charts — Release 3. Chart widgets backed by a shared query: grouping and rendering only, no new query language (doc-005 US-5 remainder) · epic:core/queries-dashboards · size:M · deps:mod-009 · note:doc-005 decomposition
- [ ] mod-010-query-hierarchy-operators — Release 3. Query operators beyond release 2: Was Ever, @Today, Contains Words, In Group, the Дата изменения field, tree and direct-links query types; reads M3 history, a new M3 read surface (doc-005 US-1 remainder, doc-010 D-16) · epic:core/queries-dashboards · size:L · deps:mod-008,feat-001 · note:doc-005 decomposition; L: a new M3 read surface changes M3's boundary
- [ ] feat-007-work-item-type-fields — MVP. «Критерии приёмки» on Пользовательская история and «Шаги воспроизведения» on Баг, rich text (doc-002 W-5, US-12); read-only on a «Удалена» item (doc-001 BR-14) · epic:core/work-items · size:M · deps:mod-003 · note:doc-002 decomposition
- [ ] feat-008-iteration-filter — MVP. Iteration filter on the M3 list and in the board/backlog filter bar; «Текущая итерация» on team surfaces only (doc-004 US-13, BR-M5-9, E-2/E-4) · epic:core/iterations-planning · size:M · deps:mod-007,feat-013 · note:doc-004 decomposition
- [ ] feat-009-iteration-planning-pane — MVP. Backlog planning pane: drag a work item onto an iteration of the team (doc-004 US-11, BR-M5-13, E-6) · epic:core/iterations-planning · size:M · deps:mod-007,mod-006 · note:doc-004 decomposition
- [ ] feat-010-move-unfinished-work — MVP. «Перенести незавершённые»: move a sprint's unfinished work items to another iteration from a planning-pane entry, all or nothing (doc-004 US-12, BR-M5-12, E-9) · epic:core/iterations-planning · size:M · deps:feat-009 · note:doc-004 decomposition
- [ ] mod-011-sprint-views — Release 2. Founds the sprint hub: sprint backlog and taskboard per team iteration (doc-004 US-5, US-6) · epic:core/iterations-planning · size:L · deps:feat-004,feat-009,feat-014 · note:doc-004 decomposition; L: it changes M4's BR-M4-1 — the Задачи level gains the taskboard, on M4's board
- [ ] feat-011-sprint-capacity — Release 2. Sprint capacity per member and activity with days off (doc-004 US-7, BR-M5-15) · epic:core/iterations-planning · size:M · deps:mod-011 · note:doc-004 decomposition
- [ ] mod-012-iteration-metrics — Release 2. Founds the history-derived metrics pattern: sprint burndown and velocity (doc-004 US-8, BR-M5-16); owes the history-derived-metrics ADR · epic:core/iterations-planning · size:L · deps:mod-011 · note:doc-004 decomposition; estimation fields reached through mod-011
- [ ] feat-012-backlog-forecast — Release 3. Forecast the backlog by velocity (doc-004 US-9) · epic:core/iterations-planning · size:M · deps:mod-012 · note:doc-004 decomposition
- [ ] mod-013-delivery-plans — Release 3. Founds the delivery plan: an organization-owned timeline of teams' iterations across the organization's projects, written by members and administrators (doc-004 US-10, BR-M5-17; doc-010 N-31) · epic:core/iterations-planning · size:L · deps:mod-007,mod-006 · note:doc-004 decomposition
- [ ] feat-013-board-backlog-filter — MVP. Filter bar on the M4 board and backlog (state, assignee, tag, area, keyword) reusing feat-003's filters; a filtered backlog hides rows but reorders against the full list (doc-003 US-2 filtered-order scenario, doc-010 D-14) · epic:core/backlogs-boards · size:M · deps:mod-006,feat-003 · note:doc-010 D-14
- [ ] feat-014-work-item-estimation-fields — Release 2. Estimation fields, the Agile process set: Story Points on Пользовательская история and Баг; Original Estimate, Remaining Work, Completed Work and Activity («Вид работ», a fixed M3 list) on Задача and Баг; form, list and history (doc-002 § Release 2, doc-010 D-15) · epic:core/work-items · size:M · deps:mod-003,doc-011 · note:doc-010 D-15; read by mod-011, feat-011, mod-012

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
`core/<module>` — the product epics are `core/org-access` (M1),
`core/process-customization` (M2), `core/work-items` (M3),
`core/backlogs-boards` (M4), `core/iterations-planning` (M5),
`core/queries-dashboards` (M6) and `product/analysis` for analysis
tasks — for platform and process work `platform/<area>` or `workflow`. The field exists so you can tell "what is this even about"
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
