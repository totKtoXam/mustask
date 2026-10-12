# feat-018-work-item-rich-text

- type: feature
- size: L
- branch: feat/018-work-item-rich-text
- stage: 02-requirements-review
- epic: core/work-items
- deps: mod-003
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: pending
gate-2: pending
gate-3: pending

## Plan

<!-- S/M: implementation plan, 3-10 lines. L: see plan-backend.md / plan-frontend.md -->

## Log

- 2026-10-12 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/feat-018-work-item-rich-text

## Notes

Summary (from the queue line or an argument): Описание as rich text (doc-002 BR-M3-4), converting the stored plain text

## Route confirmation

- 2026-10-12 [orchestrator] human confirmed: type feature, size M (as queued), scope = «Описание» only (not «Критерии приёмки», «Шаги воспроизведения», comments). Human instruction: no E2E tests for this task. Open points handed to analysis: editor library + storage format (ADR threshold — escalate to L if an ADR turns out to be needed), plain→HTML conversion reversibility / table population (migration threshold).
- 2026-10-12 [orchestrator] stage 01 done: spec.md + US-1..3; OQ-1..4 open for gate-1 (OQ-1 ADR → L unless human keeps M)
- 2026-10-12 [orchestrator] stage 02 round 1: verdict needs-human-decision; 1 blocker (R-1 size must be L: ADR is unavoidable, contract change), 7 majors, 5 minors — see review.md. Waiting for human: re-confirm size L + OQ-1..3.
- 2026-10-12 [human] size re-confirmed as L (escalation from M: an ADR on the editor is unavoidable — routing.md ADR threshold — and the `description` contract meaning changes). Diverges from the queue line's size:M.
- 2026-10-12 [human] OQ-1/OQ-4: sanitized HTML + TipTap (ProseMirror) + server-side sanitizer, new dependency accepted (ADR written and accepted before code). OQ-2: lazy conversion at read with a stored-format marker, no data migration. OQ-3: the limit counts visible text, with a higher physical storage ceiling.
- 2026-10-12 [orchestrator] next: analyst revises spec/stories for review findings R-1..R-13 under these decisions; one delta read; then gate-1 (human approval is still pending).
