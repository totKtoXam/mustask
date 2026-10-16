# feat-039-project-board

- type: feature
- size: M
- branch: feat/039-project-board
- stage: 01-business-analysis
- epic: core/backlogs-boards
- deps: mod-006
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: pending
gate-2: pending
gate-3: pending

## Plan

Route M with UI: business analysis (spec.md + stories, short form, from doc-012
US-7 project part, BR-MX-17/-18) → requirements review → UX design → gate-1 +
gate-2 as one package → backend → frontend → one QA pass over both layers →
one code-review round → gate-3 → PR. The developers' plan goes here after the
design.

## Log

- 2026-10-17 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/feat-039-project-board
- 2026-10-17 [orchestrator] route M (queue size; doc-012 § Decomposition: an additive view in the boards slice) taken under the human's night standing approval (no confirmation needed; gates stay pending for the human, work proceeds and is listed for the morning). E2E: no (paused 2026-10-12).
- 2026-10-17 [orchestrator] the branch stacks on doc/012-matrix-organization-model (a653917f, PR #6 unmerged) so doc-012's spec, stories and ADR-0028/0029 are in the tree; the PR targets that branch until #6 merges. Stages run as agents through workflows («используй агентов и воркфлоу»).

## Notes

Summary (from the queue line or an argument): A project backlog and board per level over every item of the project, whatever its team or with none, in the project's own order; reached from the page head's team drop-down until the navigation line (doc-012 BR-MX-17, BR-MX-18; US-7, project part)
