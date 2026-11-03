# feat-033-orphaned-organization-rescue

- type: feature
- size: L
- branch: feat/033-orphaned-organization-rescue
- stage: 01-business-analysis
- epic: core/org-access
- deps: mod-016,tech-024
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: pending
gate-2: pending
gate-3: pending

## Plan

<!-- L: see plan-backend.md / plan-frontend.md -->

## Log

- 2026-11-03 [orchestrator] task started in the worktree .claude/worktrees/antigravity-gemini-analysis-8e712c (app-made; branch renamed, based on origin/main 41d784ee; new-task.sh not used because the session is already bound here). Orchestrated from the doc-012 session as a parallel lane next to feat-048; local test lane shared through it (full runs and pushes need its go).
- 2026-11-03 [orchestrator] size mismatch: queue has size:M, human confirmed L. Route confirmed by the human: feature, L (thresholds: the authorization rules change — an instance role acts inside an organization without a membership — and an ADR amending ADR-0027/ADR-0033), full route, gates 1/2/3 separate; block 3.3: only the queue line. Economy mode; E2E no (standing answer).
- 2026-11-03 [orchestrator] queue: PR #54 takes the line (`queue.sh take`, auto-merge on; the doc-012 session's go for the push). Stage 1 started (business-analyst, deep tier for L).

## Notes

Summary (from the queue line): system-admin rescues an organization left with no active administrator: sees the list of all organizations and appoints an admin there, audited, with no access to the organization's content. Closes doc-006 accepted risk H-1
