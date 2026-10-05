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

- [ ] doc-011-release-2-scope — Release 2. Scope analysis: spec rows and one owning module for the release-2 items that have none (notifications, audit incl. BR-M1-15, bulk edit, templates, copy/move, other link types, change type, tag management, @mentions, WIP limits, custom columns/swimlanes, card styling, several boards, live push, level toggle, parent grouping rows, per-project and per-area overrides, team area sets, instance-rights screen); settles the notification and audit double claims; names the one line that owes the instance-rights ADR-0018 amendment; assigns every new line a milestone · epic:product/analysis · size:L · deps:doc-010 · note:doc-010 D-18, D-24; gates every release-2 implementation line (doc-010 PR-5); L: scopes authorization overrides across M1/M3/M4 and assigns an ADR (doc-010 PR-2)
- [ ] tech-006-serilog-static-logger-test-hosts — Program.cs calls UseSerilog(writeToProviders: true) without preserveStaticLogger, so disposing any test child host runs Log.CloseAndFlush() and every ILogger<T> created on the shared host afterwards binds to a SilentLogger: log-line assertions pass vacuously or fail by test order (bug-002 fixed one test on its own host). Set preserveStaticLogger: true, take the request logger from DI instead of Log.Logger, and make the integration tests' log capture per host · epic:platform/tests · size:S · deps:bug-002 · note:the exposed tests are listed in docs/features/bug-002-session-logout-test-order-dependent/review.md
- [ ] tech-008-user-directory-read-model-hardening — mod-014 gate-3 debts (QA re-check 4 M-1..M-3): a Domain test that pins the noncharacter rule of UserPushValidation over every scalar value (today only the Api tests catch its removal); LookupAsync/SearchAsync with AmongIds answer an id holding U+0000 or a lone surrogate as unknown instead of throwing (api-contract A.4); A.3 Matching says a lone surrogate reads as U+FFFD like a noncharacter · epic:core/org-access · size:S · deps:mod-014 · note:-
- [ ] tech-009-org-structure-review-debts — mod-002's review and QA debts. F1: the process read of a just-deactivated organization is served from the panel's cache; F2: GetTeamsHandler reads the teams and the area tree in two statements and answers 500 when a default area moves between them; F3: a project rename does not refresh the team reads; F4: the switcher compares address ids case-sensitively; F5: the team deletion text before the teams list is read; D-3: FormDialog's initial focus and rename selection are lost under StrictMode (shared pattern, mod-015) · epic:core/org-access · size:S · deps:mod-002 · note:-
- [ ] tech-010-work-item-core-review-debts — mod-003's review debts. m1: the form's draft copies the whole item on the first edit, so a re-read during an edit sends untouched fields back with old values (against BR-M3-25) — keep only touched fields; m2: the create dialog keeps its requestId after the user changes the type or title following a failure; m3: a NUL character in the title or description answers 500 instead of a field error (mod-002's names too); m4: the work items module locks the organizations module's areas row with raw SQL — expose the lock from the organizations module; m5: no test pins that the idempotency key is scoped to the project · epic:core/work-items · size:S · deps:mod-003 · note:-
- [ ] feat-022-work-item-form-fields — MVP. The work item form brought close to the reference tracker's form (human, 2026-10-05): the planning, classification and estimation fields per type and the type's text fields, laid out in sections; absorbs feat-019 (Приоритет), feat-007 (type fields) and feat-014 (estimation fields) · epic:core/work-items · size:M · deps:mod-003 · note:-
## Later (waiting on analysis/dependencies)
- [ ] feat-001-work-item-links — MVP. Parent/Child links across the five types with BR-M3-8's refusals, the atomic replace-parent the backlog mapping uses, unlinking from a deleted parent (BR-M3-9); no link write on a «Удалена» item (doc-001 BR-14) · epic:core/work-items · size:M · deps:mod-003 · note:doc-001 decomposition (b)
- [ ] mod-004-work-item-attachments — MVP. Founds attachment storage on each organization's own Google Drive: connect/check/disconnect on the organization settings (doc-002 US-13), upload, list, download, remove one's own (doc-010 D-11), admin moderation, a purge deletes the item's files; per-organization secrets; owes the attachment-storage ADR · epic:core/work-items · size:L · deps:mod-005 · note:doc-001 decomposition (b)
- [ ] feat-002-work-item-comments — MVP. Обсуждение: add; edit or delete one's own comment under the ownership predicate (doc-010 D-11) with an edited marker; admin moderation; history (BR-M3-12); no comment on a «Удалена» item (doc-001 BR-14) · epic:core/work-items · size:M · deps:mod-003,mod-016 · note:doc-001 decomposition (b)
- [ ] mod-005-work-item-delete-restore — MVP. Founds the soft-delete pattern: delete → «Удалена», restore to the previous or a chosen state, the organization administrator's purge (doc-010 D-13), doc-001 BR-14 for the surfaces and writes that exist when it ships, no cascade (BR-M3-9); owes the recycle-bin ADR, which references mod-002's soft-deactivation ADR · epic:core/work-items · size:L · deps:mod-003,mod-016 · note:doc-001 decomposition (b)
- [ ] mod-007-iterations — MVP. Founds M5: the iteration tree of releases and numbered sprints (doc-004 E-7, E-10..E-12), team selection and default, the work item's iteration pointer, the current iteration on the server's UTC date (doc-001 BR-9), confirmed soft removal that clears and counts every pointer incl. deleted items, a deleted team's settings dropped (doc-010 D-6) (doc-004 US-1..US-4, US-14) · epic:core/iterations-planning · size:L · deps:mod-003,doc-004 · note:doc-001 decomposition (b), refined by doc-004
- [ ] feat-003-work-item-search — MVP. List filters on the M3 work item list: state incl. «Удалена» (the way into the recycle bin), assignee incl. former members, tag, area with sub-areas, keyword over Заголовок and the identifier (doc-002 US-11, doc-010 D-14) · epic:core/work-items · size:M · deps:mod-005 · note:doc-001 decomposition (b)
- [ ] feat-004-backlog-hierarchy — MVP. Backlog parent/child hierarchy on M4: expand/collapse display of M3 Parent/Child links and the parent-mapping write from the backlog view, one level up, through feat-001's replace-parent (doc-003 US-3, BR-M4-9..BR-M4-11) · epic:core/backlogs-boards · size:M · deps:mod-006,feat-001 · note:doc-003 decomposition (DD-3)
- [ ] mod-008-queries-dashboards — Release 2. Founds M6: Query/QueryFolder, the clause-builder model incl. Under on Область and Итерация and a keyword clause (doc-010 D-16), personal «Мои запросы» under the personal-object code in every bundle (doc-010 D-11/D-12), shared «Общие запросы» with org-role permissions, soft delete/restore separate from the project's status, the organization administrator's purge (doc-010 D-13), queries bound to a deleted team stop (doc-010 D-6), results with column selection (doc-005 US-1..US-3) · epic:core/queries-dashboards · size:L · deps:feat-003,mod-007,doc-005,doc-011 · note:doc-005 decomposition
- [ ] mod-009-dashboards-widgets — Release 3. Founds Dashboard/DashboardWidget at team and project level (D-15): a protected default team dashboard seeded for new and existing teams, additional team and project dashboards, query-tile and markdown widgets, the 10-widget cap, soft delete/restore (doc-005 US-5 baseline, BR-M6-9); the organization administrator's purge of dashboards and widgets (doc-010 D-13); no dashboard for a deleted team (doc-010 D-6) · epic:core/queries-dashboards · size:L · deps:mod-008 · note:doc-005 decomposition
- [ ] feat-005-csv-import-export — Release 3. CSV import/export of work items from the Queries hub through M3's validation, one row per transaction, UTF-8 BOM, ';'-delimited, 5,000-row cap refused above (doc-005 US-4, BR-M6-8) · epic:core/queries-dashboards · size:M · deps:mod-008 · note:doc-005 decomposition
- [ ] feat-006-dashboard-charts — Release 3. Chart widgets backed by a shared query: grouping and rendering only, no new query language (doc-005 US-5 remainder) · epic:core/queries-dashboards · size:M · deps:mod-009 · note:doc-005 decomposition
- [ ] mod-010-query-hierarchy-operators — Release 3. Query operators beyond release 2: Was Ever, @Today, Contains Words, In Group, the Дата изменения field, tree and direct-links query types; reads M3 history, a new M3 read surface (doc-005 US-1 remainder, doc-010 D-16) · epic:core/queries-dashboards · size:L · deps:mod-008,feat-001 · note:doc-005 decomposition; L: a new M3 read surface changes M3's boundary
- [ ] feat-008-iteration-filter — MVP. Iteration filter on the M3 list and in the board/backlog filter bar; «Текущая итерация» on team surfaces only (doc-004 US-13, BR-M5-9, E-2/E-4) · epic:core/iterations-planning · size:M · deps:mod-007,feat-013 · note:doc-004 decomposition
- [ ] feat-009-iteration-planning-pane — MVP. Backlog planning pane: drag a work item onto an iteration of the team (doc-004 US-11, BR-M5-13, E-6) · epic:core/iterations-planning · size:M · deps:mod-007,mod-006 · note:doc-004 decomposition
- [ ] feat-010-move-unfinished-work — MVP. «Перенести незавершённые»: move a sprint's unfinished work items to another iteration from a planning-pane entry, all or nothing (doc-004 US-12, BR-M5-12, E-9) · epic:core/iterations-planning · size:M · deps:feat-009 · note:doc-004 decomposition
- [ ] mod-011-sprint-views — Release 2. Founds the sprint hub: sprint backlog and taskboard per team iteration (doc-004 US-5, US-6) · epic:core/iterations-planning · size:L · deps:feat-004,feat-009,feat-014 · note:doc-004 decomposition; L: it changes M4's BR-M4-1 — the Задачи level gains the taskboard, on M4's board
- [ ] feat-011-sprint-capacity — Release 2. Sprint capacity per member and activity with days off (doc-004 US-7, BR-M5-15) · epic:core/iterations-planning · size:M · deps:mod-011 · note:doc-004 decomposition
- [ ] mod-012-iteration-metrics — Release 2. Founds the history-derived metrics pattern: sprint burndown and velocity (doc-004 US-8, BR-M5-16); owes the history-derived-metrics ADR · epic:core/iterations-planning · size:L · deps:mod-011 · note:doc-004 decomposition; estimation fields reached through mod-011
- [ ] feat-012-backlog-forecast — Release 3. Forecast the backlog by velocity (doc-004 US-9) · epic:core/iterations-planning · size:M · deps:mod-012 · note:doc-004 decomposition
- [ ] mod-013-delivery-plans — Release 3. Founds the delivery plan: an organization-owned timeline of teams' iterations across the organization's projects, written by members and administrators (doc-004 US-10, BR-M5-17; doc-010 N-31) · epic:core/iterations-planning · size:L · deps:mod-007,mod-006 · note:doc-004 decomposition
- [ ] feat-013-board-backlog-filter — MVP. Filter bar on the M4 board and backlog (state, assignee, tag, area, keyword) reusing feat-003's filters; a filtered backlog hides rows but reorders against the full list (doc-003 US-2 filtered-order scenario, doc-010 D-14) · epic:core/backlogs-boards · size:M · deps:mod-006,feat-003 · note:doc-010 D-14
- [ ] mod-016-organization-access — MVP. Founds organization access: ORG_MEMBERSHIP with admin/member/viewer and who/when, team membership, the last-admin and «неактивен» rules, member search on the user projection, the ownership predicate (doc-010 D-11); the ADR-0018 amendment (doc-006 constraints 1–8: organization-scoped codes derived from membership, resolution chains, «not found» for non-members, codes served to the panel, the realm role inventory); the retrofit of the scoped checks onto every endpoint that exists when it merges (until then they run under the authenticated-only guard) and the membership-based assignee marking «не в организации» (doc-001 BR-13); the recorded creator becomes the organization's first administrator and the project creator a first-team member where BR-M1-10 allows (mod-002 BR-M1-21); a deleted team's memberships end (mod-002 BR-M1-18); under doc-010 PR-7 amends doc-001 § Roles, BR-5 and § Decomposition for the split; the shell's navigation reads the served codes, replacing mod-015's interim BR-8 rule (mod-015 BR-8, D-10, D-18); folds mod-001's organization-scoped registry into the scoped catalog · epic:core/org-access · size:L · deps:mod-002,mod-014 · note:split of mod-002 by the human 2026-09-25: access runs beside mod-003 instead of before it; L: owns the ADR-0018 amendment; consumers of roles (mod-005, feat-002) list it
- [ ] tech-003-keycloak-login-theme — Keycloak login theme in the panel's visual language: restyle deploy/keycloak/themes to mod-015's shell tokens and copy (mod-015 D-2) · epic:platform/ui · size:S · deps:mod-015 · note:-
- [ ] tech-004-stand-docs-drift — Stand docs drift: the old 'missing configuration value' wording in src/backend/AGENTS.md and the random-port/password wording in MustaskDbContextFactory.cs and ADR-0002 no longer match the fixed ports and secrets (deferred from tech-002 review CR-follow-up) · epic:platform/stand · size:S · deps:- · note:-
- [ ] tech-005-process-id-composite-guard — organizations.process_id references processes(id) alone, so the database would accept another organization's process on an organization; add the key (organization_id, id) on processes and a composite foreign key (noted by mod-002 QA D-1, deferred by D-x) · epic:core/org-access · size:S · deps:mod-002 · note:-
- [ ] bug-003-shell-address-case-navigation — An address in another letter case or percent-encoded (/Dictionaries/Languages, /dictionaries/%6Canguages) opens the page without its navigation marker, settings menu or trail (mod-015 QA D-4, minor); match routes the way the router does (matchPath) · epic:platform/ui · size:S · deps:mod-015 · note:-
- [ ] feat-015-work-item-history — History of every create and save of a work item; owes the history/audit ADR (doc-010 ADR debt); decides what items created before it show (mod-003 spec, Decomposition) · epic:core/work-items · size:M · deps:mod-003 · note:-
- [ ] feat-016-work-item-tags — Stored project tags on the work item form and in the list (doc-002 BR-M3-11, US-8) · epic:core/work-items · size:M · deps:mod-003 · note:-
- [ ] feat-017-work-item-assignee-marks — The «неактивен» and «не в организации» marks on an assignee, on the form and in the list (doc-001 BR-13) · epic:core/work-items · size:S · deps:mod-003,mod-016 · note:-
- [ ] feat-018-work-item-rich-text — Описание as rich text (doc-002 BR-M3-4), converting the stored plain text · epic:core/work-items · size:M · deps:mod-003 · note:-
- [ ] feat-020-work-item-preview-pane — The preview pane beside the work item list (mod-015 design 10.2) · epic:core/work-items · size:S · deps:mod-003 · note:-
- [ ] feat-021-work-item-concurrency-token — The client-visible concurrency token on work item writes with its ADR (doc-001 BR-11) and the stale-move notice on the board (doc-003); deferred by mod-003 D-2 and mod-006 D-2 · epic:core/work-items · size:M · deps:mod-003,mod-006 · note:-

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
