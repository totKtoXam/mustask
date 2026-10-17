# mod-017-work-item-owning-team

- type: module
- size: L
- branch: mod/017-work-item-owning-team
- stage: 01-business-analysis
- epic: core/work-items
- deps: feat-039
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: pending
gate-2: pending
gate-3: pending

## Plan

Route L with UI: business analysis → requirements review → gate-1 → UX design →
gate-2 → backend plan (deep, `plan-backend.md`) → backend → QA pass 1 →
frontend → QA pass 2 → one code-review round + the security pass (≤ 2 delta
reads) → gate-3 → PR. Plans: `plan-backend.md`, `plan-frontend.md`.

## Log

- 2026-10-17 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/mod-017-work-item-owning-team
- 2026-10-17 [orchestrator] route L (queue size; doc-012 § Decomposition: breaking — retires area ownership, team default areas and the first team's role) taken under the human's night standing approval: no confirmation, gates stay pending for the human, work proceeds and is listed for the morning. E2E: no (paused 2026-10-12).
- 2026-10-17 [orchestrator] stacked on feat/039-project-board (0cf422c2, PR #8 → PR #6), which carries doc-012 and the MessagePack 2.5.303 pin (NU1902 fails main). Stages run as agents through one workflow.

## Notes

Summary (from the queue line or an argument): An explicit, optional owning team on the work item: the card, the creation dialogs, the list's «Команда» column and filter, the project views' team filter; team views hold owned items; the project default team and the creation defaults; retires area ownership, team default areas and the first team's role (doc-012 BR-MX-9..-11, -17; US-4)
