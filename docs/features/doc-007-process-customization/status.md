# doc-007-process-customization

- type: docs
- size: M
- branch: doc/007-process-customization
- stage: complete
- epic: product/analysis
- deps: doc-001
- gates: gate-1, gate-3

## Gates

gate-1: approved (2026-09-24, human in chat)
gate-3: approved (2026-09-24, human in chat, "Approve, squash")

## Plan

1. Input: the umbrella `docs/features/doc-001-domain-map/spec.md` (module M2
   boundary, MVP/release tables, BR-1/2/10, decomposition row (a) for this
   task), the binding decisions D-1..D-12 in doc-001's `status.md`, the
   story `US-3-built-in-process.md`, `research/ado-boards.md` and
   `screenshots/22..26-process-*.png` (Azure DevOps is the only reference).
2. Stage 1 (business-analyst, standard tier): `spec.md` = M2 glossary,
   PROCESS / WORK_ITEM_TYPE / STATE_DEFINITION / BACKLOG_LEVEL logical
   model, the MVP seeded process in full (five types, per-type state lists
   with categories, backlog levels, the seed-at-org-creation contract M1
   calls), the release-3 customization surface as analysis (custom types,
   fields, states, rules, form layout, backlog levels, Reason) — no
   editor designed; stories with Gherkin criteria; decomposition table
   refining mod-001-process-seed and naming the release-3 slices by slug.
3. Stage 2 (requirements-reviewer, deep tier, clean context) → gate-1.
4. Stages 3–7 skipped (docs task, no screens, no code); gate-2 inapplicable.
5. gate-3 → local squash via `scripts/finish-task.sh` (no remote configured).

## Decisions

D-1..D-12 of doc-001 (`docs/features/doc-001-domain-map/status.md`) are
binding input and are not reopened here.

Human decisions of 2026-09-24 (answers to review round 1 and the spec's open questions):

- P-1 (R-1) Эпик, Фича, Пользовательская история follow Azure DevOps Agile: «Новая» Proposed, «Активна» InProgress, «Решена» InProgress, «Закрыта» Completed, «Удалена» Removed. — superseded by doc-010 D-4 (2026-09-24)
- P-2 (R-1) Баг gets its own flow: «Новый» Proposed, «В работе» InProgress, «Исправлен» Resolved, «Закрыт» Completed, «Удалена» Removed. Resolved is used by Баг alone. — superseded by doc-010 D-4 (2026-09-24)
- P-3 (open question 1) Release-3 customization changes the organization's one process in place; no inherited/forked process.
- P-4 (open question 3) One seed template forever: no Basic/Scrum/CMMI picker, ever.
- P-5 (R-20) Each type's icon and colour are fixed in the seed and not editable in MVP.

Orchestrator resolutions from the doc-001 artifacts, shown to the human at gate-1:

- R-5 M2 references nothing in M1: the seed takes the owning organization's identifier from its caller; the ORGANIZATION–PROCESS link is recorded as M1 calling M2, and the module order M2 → M1 stands.
- R-6 MVP has a read-only view of the types and their states (doc-001 US-3 opens "the project's work item type list"); no editing screen.
- R-7 M2 defines the transition and delete/restore rules; enforcing them on work items is delivered by mod-003-work-item-core and mod-005-work-item-delete-restore.

Defaults proposed by the orchestrator, confirmed by the human at gate-1 (2026-09-24):

- R-4 A failed seed fails the whole organization creation; an organization never exists without its process.
- R-19 No separate seed time budget; the dangling reference is removed.
- Open question 2 The release-3 field-kind list stays as drafted, as a starting assumption for release-3 analysis.

## Log

- 2026-09-22 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/doc-007-process-customization
- 2026-09-22 [orchestrator] size checklist: 0.1 documents only (not F: analysis is not on the f-route list); block 1 clean (no migration, project, ADR, slice, breaking change, external exchange or auth change); 2.1 not S (spec + stories > 5 files, > 2 h); 3.1 no regulatory basis; 3.2 n/a; 3.3 human: nothing beyond the queue line → M
- 2026-09-22 [orchestrator] route confirmed by the human: docs, M, stages 1–2 → gate-1 → gate-3 → local squash; gate-2 inapplicable (no screens); size equals the queue line
- 2026-09-22 [orchestrator] dependency doc-001 merged into main (b5fde80, 2026-09-22); queue line taken in main (47347d2)
- 2026-09-22 [orchestrator] stage 1 delegated to business-analyst (standard tier); the subagent's Write tool is bound to the orchestrator's own worktree, so artifacts were staged in the session scratchpad and copied into the task worktree by the orchestrator
- 2026-09-22 [business-analyst] spec.md (242 lines: roles, M2 logical model incl. release-3 entities, the MVP seeded process with per-type state→category tables, release-3 customization surface as analysis, state diagram, BR-M2-1..7, operation completeness, out of scope, decomposition: mod-process-seed refined + five release-3 feat-NNN-process-* slices) + stories US-1..US-5 (all `shippable: no`); 3 open questions for the human (in-place mutation vs fork of the process, release-3 field-kind list, single seed template forever); `bash scripts/verify.sh` → RESULT: green (orchestrator run)
- 2026-09-22 [orchestrator] stage 2 delegated to requirements-reviewer (deep tier, clean context)
- 2026-09-22 [requirements-reviewer] round 1 in review.md: needs-human-decision — 1 blocker (R-1 «Решена» category contradicts the ADO source), 7 majors (R-2 default/initial state and ordering, R-3 idempotency key, R-4 seed failure path, R-5 ORGANIZATION↔PROCESS direction, R-6 MVP read surface, R-7 US-3 attributed to the wrong slice, R-8 three release-3 rows > M), 12 minors (R-9..R-18 fixed in place by the reviewer, R-19/R-20 open); spec.md 248 lines; verify.sh green (orchestrator run)
- 2026-09-22 [orchestrator] decisions requested from the human before round 2 (R-1, R-20, the spec's three open questions); R-5/R-6/R-7 answered from doc-001 artifacts, R-4/R-19 and open question 2 proposed as defaults

- 2026-09-24 [orchestrator] session moved: this worktree (ecstatic-neumann-4f5193) now holds the branch; the separate doc-007 worktree was removed after a Claude Desktop reinstall
- 2026-09-24 [orchestrator] human decisions P-1..P-5 recorded (R-1 split: ADO categories for Эпик/Фича/История, own Баг flow; in-place customization; one template forever; fixed icon/colour); R-5/R-6/R-7 resolved from doc-001, R-4/R-19/OQ-2 defaults to confirm at gate-1; rework delegated to business-analyst
- 2026-09-24 [business-analyst] rework: P-1..P-5 and the R-5/R-6/R-7 resolutions applied, R-1..R-8/R-19/R-20 marked resolved in review.md; spec.md 283 lines
- 2026-09-24 [requirements-reviewer] round 2 in review.md: approved-by-ai — round-1 findings hold; 3 majors (R-21 «Удалена» missing from the seeded lists, R-22 idempotency key, R-27 custom-fields slice raised to L) and 7 minors (R-23..R-26, R-28..R-30) fixed in place; R-31 accepted-risk: spec.md 290 lines, over-guide content is the release-3 analysis and decomposition, not leaked story content; verify.sh green (orchestrator run)
- 2026-09-24 [orchestrator] gate-1 requested from the human
- 2026-09-24 [orchestrator] gate-1: approved by the human in chat; the three defaults (R-4, R-19, field-kind list) confirmed; release-3 decomposition rows stay in spec.md only, not queued (human's choice); spec.md § Open questions updated to say so
- 2026-09-24 [orchestrator] stages 3–7 skipped (docs task: no screens, no code); gate-3 requested
- 2026-09-24 [orchestrator] gate-3: approved by the human in chat; stage: complete; local squash via scripts/finish-task.sh (no remote)
- 2026-09-25 [doc-010] amended: § Roles (`process.definition.read`, non-member «Организация не найдена»), logical model (WORK_ITEM_TYPE key, STATE_DEFINITION order, BACKLOG_LEVEL rank/kind, field note), § MVP seeded process (state table per D-4, «Ключ», rank/kind, category labels, one-label-two-categories consequences), release-3 invariants, BPMN, BR-M2-2/4/6/7/9 rewritten, BR-M2-3 retired to doc-001 BR-10, Operation completeness, Decomposition (mod-001 row), US-1..US-5 (cross-review N-1, N-3..N-5, N-9, N-14, N-20, N-27, N-29, N-32; doc-010 D-4, D-5)
- 2026-10-02 [mod-001] amended: each WORK_ITEM_TYPE stores one appearance token from {epic, feature, story, task, bug}, no two types sharing one, in place of its icon and colour; STATE_DEFINITION stores its category as the category code, the Russian label derived (doc-010 D-5); edited in § Logical model (ER and the note under it), § MVP seeded process (screen sentence, the token per type), § Release 3 custom types, § Out of scope, and US-2's first scenario. P-5 above still reads "icon and colour"; the amendment replaces them with the token (mod-001 status.md D-6; doc-010 PR-7)

## Notes

Summary (from the queue line or an argument): Process customization module analysis: custom types, fields, states, rules, form layout, backlog levels
