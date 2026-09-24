# doc-002-work-items

- type: docs
- size: L
- branch: doc/002-work-items
- stage: complete
- epic: product/analysis
- deps: doc-001
- gates: gate-1, gate-3

## Gates

gate-1: approved (confirmed by the human in chat, 2026-09-24)
gate-3: approved (confirmed by the human in chat, 2026-09-24, "Approved, squash")

## Plan

1. Input: the umbrella `docs/features/doc-001-domain-map/spec.md`
   (decomposition row (a) for doc-002; module M3 boundary; MVP table; ER
   model; BR-1..13), the binding human decisions D-1..D-12 in
   `docs/features/doc-001-domain-map/status.md`,
   `docs/features/doc-001-domain-map/research/ado-boards.md` and
   `screenshots/` (Azure DevOps Boards, the only reference product).
2. Stage 1 (business-analyst, deep tier — size L): `spec.md` = M3 boundary
   restated, the five built-in types with their MVP field lists and
   per-type state lists mapped to categories, the work item form, links,
   comments, attachments, history, tags, the identifier-scheme input for
   `mod-work-item-core`, module business rules under BR-1/2/4/6/8/10, BPMN
   (mandatory for L), operation completeness, out of scope; stories under
   `stories/`; a decomposition table that refines the already-queued
   mod-003 / feat-001 / mod-004 / feat-002 / mod-005 lines rather than
   inventing parallel ones.
3. Stage 2 (requirements-reviewer, deep tier, clean context) → gate-1.
4. gate-3 → local squash via `scripts/finish-task.sh` (no remote).

## Decisions

- D-1..D-12 of doc-001 are binding input and are not reopened here.
- "Reasons" from the queue line are covered only as a release-3 note and an
  out-of-scope line: D-6 rules the Reason field out of MVP.
- Size L confirmed by the human over the queue's M: deeper analysis of the
  core module (deep-tier business analysis, BPMN mandatory); the route and
  gates of a docs task are unchanged (stages 1–2, gate-1, gate-3).

- Human answers to the gate-1 questions (2026-09-24, in chat), binding for the rework:
  - W-1 (R-1) Parent/Child: the parent's backlog level is higher **or equal** to the child's (a Баг may be a child of a Пользовательская история); one parent, no cycles, not itself.
  - W-2 (R-2) «Задача» takes the Agile names «Новая» / «Активна» / «Закрыта» / «Удалена»; this spec carries an errata note against doc-001 US-3 (doc-001 is not reopened).
  - W-3 (R-3) An assignee whose organization membership is removed stays on the work item, shown as a former member, not offered for new assignments; history is not rewritten.
  - W-4 (R-4) The work item list surface (no filters) stays in `mod-003-work-item-core`, size L; the queue line's note records the growth.
  - W-5 (OQ-1) «Критерии приёмки» on Пользовательская история and «Шаги воспроизведения» on Баг are in MVP, as a separate M queue line `feat-NNN-work-item-type-fields` (deps work-item-core).
  - W-6 (OQ-2) Identifier: `{ProjectKey}-{WorkItemKey}-{Sequence}`, e.g. `MSK-BUG-42`; ProjectKey is a key of the project, WorkItemKey is the key of the type, the sequence is one per project shared by all types (the number alone is unique within a project).
  - W-7 (OQ-3) One set of state labels for all five types, regardless of grammatical gender.
  - W-8 (OQ-4) Attachment files are stored in Google Drive through its API; MUSTASK sets no size/count limits of its own, only Drive's limits apply (an external counterparty for `mod-004-work-item-attachments`).
  - W-9 (OQ-5) «Разрешена» stays on Эпик and Фича.

- Human answers to the round-2 gate-1 questions (2026-09-24, in chat):
  - W-10 (OQ-6) The project key is set when the project is created and never changes (recommendation accepted: organization administrator sets it; 2..10 Latin capitals and digits, starting with a letter; unique within the organization, deleted projects included).
  - W-11 (OQ-7) Type keys: EPIC / FEAT / US / TASK / BUG, fixed in the seed.
  - W-12 (OQ-8) Google Drive **per organization**: each organization connects its own Drive for its attachments (not one instance-wide Drive, not the uploader's Drive).
  - W-13 (R-23) No file-extension filter and no virus scanning in MVP.

## Log

- 2026-09-24 [orchestrator] gate-3: approved by the human in chat; stage: complete; local squash via scripts/finish-task.sh
- 2026-09-24 [orchestrator] queue line issued in main: feat-007-work-item-type-fields (M, Later, deps mod-003; commit 77171f8); the scope growth of mod-003 (list surface, W-4) and mod-004 (per-organization Google Drive, W-8/W-12) lives in this spec's decomposition — queue.sh cannot edit an existing line's note; stages 3–6 skipped (docs task); gate-3 requested
- 2026-09-24 [orchestrator] gate-1: approved by the human in chat
- 2026-09-24 [requirements-reviewer] round 3 (delta W-10..W-13) in review.md: approved-by-ai — R-25 major (Drive owner can open files; promise limited to what MUSTASK controls) and R-26..R-28 minor fixed in place; attachment removal never waits on the Drive (R-28, reversible by the human); spec.md 263 lines accepted; verify.sh green (orchestrator run)
- 2026-09-24 [orchestrator] gate-1 requested from the human for approval
- 2026-09-24 [business-analyst] W-10..W-13 applied, OQ-6..OQ-8 closed, R-23 resolved; new BR-17 and US-13 (per-organization Google Drive connection, delivered by mod-004); assumptions: the organization administrator connects the Drive, the setting lives in mod-004, purge is refused while the Drive is unreachable; spec.md 263 lines; no new open questions; verify.sh green (orchestrator run)
- 2026-09-24 [orchestrator] narrow reviewer check of the W-10..W-13 delta delegated
- 2026-09-24 [orchestrator] round-2 questions answered by the human (W-10..W-13); W-12 changes the spec (per-organization Drive), rework delegated to business-analyst, then a narrow reviewer check
- 2026-09-24 [requirements-reviewer] round 2 (delta) in review.md: needs-human-decision — W-1..W-9 applied, R-1..R-4 resolved; R-18 major (purge removes Drive files) and R-19..R-22, R-24 minor fixed in place; R-23 open (no extension filter/virus scan needs the human's confirmation); spec.md 265 lines, accepted; questions for gate-1: OQ-6 project key, OQ-7 type keys, OQ-8 whose Google Drive, R-23; verify.sh green (orchestrator run)
- 2026-09-24 [orchestrator] gate-1 requested from the human, round 2
- 2026-09-24 [business-analyst] rework: W-1..W-9 applied, R-1..R-4 resolved, OQ-1..OQ-5 closed; new US-12 (type-specific fields); spec.md 262 lines (12 over the index budget, no split proposed); 3 new open questions OQ-6 project key, OQ-7 type keys, OQ-8 whose Google Drive; verify.sh run by the orchestrator
- 2026-09-24 [orchestrator] round 2 (delta check) delegated to requirements-reviewer, deep tier, clean context
- 2026-09-24 [orchestrator] the task worktree .claude/worktrees/doc-002-work-items was removed outside this session (branch intact); work continues in worktree laughing-neumann-1324e1 checked out on doc/002-work-items
- 2026-09-24 [orchestrator] gate-1 questions answered by the human item by item (W-1..W-9 in § Decisions); rework delegated to business-analyst, then a narrow reviewer check of the delta before gate-1 is asked for approval
- 2026-09-22 [requirements-reviewer] round 1 in review.md: needs-human-decision — 1 blocker (R-1 Баг under Пользовательская история), 3 majors open for the human (R-2 Задача state names, R-3 assignee who leaves the org, R-4 mod-003 scope grew by the list surface), 13 findings fixed in place (R-5..R-16); R-17 (queue line still in backlog.md) is not a finding: the line is removed in main, the branch copy is stale; spec.md 285 lines; verify.sh green (orchestrator run)
- 2026-09-22 [orchestrator] gate-1 requested from the human with 9 questions: OQ-1..5 and R-1..R-4
- 2026-09-22 [business-analyst] deep tier: spec.md (251 lines: types/levels/states, MVP fields, BPMN, BR-1..14, operation completeness, NFRs, out of scope, decomposition refining the six queued lines, 5 open questions OQ-1..5) + stories US-1..US-11 (all `shippable: no`); `bash scripts/verify.sh` → RESULT: green (orchestrator run, the BA session has no shell); sandbox note: subagents cannot write into this worktree from the orchestrator's session — files are written in the orchestrator's worktree and moved here
- 2026-09-22 [orchestrator] stage 2 delegated to requirements-reviewer (deep tier, clean context); reviewer note: doc-001 US-3 fixes Задача states to the Basic-process names, the BA kept them and flagged the divergence — check it as a finding, not a fix
- 2026-09-22 [orchestrator] route confirmed by the human: docs, **L** (queue said M; raised by the human "L — усилить анализ"), stages 1–2 → gate-1 → gate-3 → local squash; gate-2 inapplicable (no screens); doc-001 dependency verified merged (b5fde80)
- 2026-09-22 [new-task] size mismatch: queue has size:M, human confirmed L
- 2026-09-22 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/doc-002-work-items

## Notes

Summary (from the queue line): Work items module analysis: types, fields, states and reasons, rules, form, links, comments, attachments, history, tags
