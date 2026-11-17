# feat-071-search-structure-users-read

- type: feature
- size: L
- branch: feat/071-search-structure-users-read
- stage: 04-backend
- epic: platform/ui
- deps: feat-041
- gates: gate-1, gate-3

## Gates

gate-1: approved (by the human in chat, 2026-11-17)
gate-3: pending

## Plan

Backend plan: [plan-backend.md](plan-backend.md); contract: [api-contract.md](api-contract.md).
One new `Search` module, one `POST /api/organizations/{id}/search`, sight sets first, one statement per group; pg_trgm migration.
Decision record: ADR-0044, accepted with the plan at gate-1 (2026-11-17); Q-1..Q-5 decided as recommended.

## Log

- 2026-11-17 [new-task] task started; session bound to worktree .claude/worktrees/objective-torvalds-200108, branch cut from feat/041-project-visibility (PR #203 not merged yet; origin/main is merged in once it lands). Queue line taken by PR #210.
- 2026-11-17 [new-task] size mismatch: queue has size:M, human confirmed L
- 2026-11-17 [orchestrator] route confirmed by the human in chat: L (thresholds: an ADR for the match technique and the limiter; a new Search slice; pg_trgm and indexes on other modules' tables). Requirements and design are feat-065's (gate-1 and gate-2 approved there 2026-11-13). Route: plan-backend + api-contract + ADR (proposed) → gate-1 here (the human accepts the ADR and the plan) → backend dev → QA → code review → gate-3 → PR. gate-2 has no subject (no UI). Economy mode: one agent per stage, one Opus review; E2E: no (standing answer).
- 2026-11-17 [backend-developer] plan-backend.md, api-contract.md, ADR-0044 (proposed) written; questions Q-1..Q-5 (limiter separate, sight arrays, raw-SQL indexes, pg_trgm on prod host, CI volume margin)
- 2026-11-17 [orchestrator] gate-1 approved by the human in chat: ADR-0044 accepted; Q-1 a separate limiter, Q-2 sight as id arrays from AccessSet, Q-3 raw-SQL expression indexes in the Search migration, Q-4 the human confirmed pg_trgm is available on the production DB host, Q-5 the volume test asserts 2x the spec's p95/p99 and records measured values in qa.md. Stage 4 development started.

## Notes

Summary (from the queue line): Global search read for departments, teams, projects, sub-areas and users (feat-065 US-2, US-4): one read per organization under sight, groups, ranking tiers with the typo tier, limit+1 more, group expansion, trigram indexes, POST transport, per-user and per-process limiter, the ADR for match technique and limiter, hidden-object, statement-count and volume tests.
