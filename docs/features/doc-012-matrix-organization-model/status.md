# doc-012-matrix-organization-model

- type: docs
- size: L
- branch: doc/012-matrix-organization-model
- stage: 02-requirements-review
- epic: product/analysis
- deps: mod-002,mod-016,doc-004
- gates: gate-1, gate-3

## Gates

gate-1: pending
gate-3: pending

## Plan

No backend/frontend plan: a docs task. Input: [input-brief.md](input-brief.md)
(the human's brief) and [prior-art.md](prior-art.md) (pitfalls only).
1. Stage 1 (deep): parallel as-is audits per subsystem into `audit/`, then
   `spec.md` + `stories/` + ADR drafts (Proposed) + the decomposition table.
2. Stage 2: requirements review in clean contexts, findings in `review.md`.
3. gate-1: the spec, the ADR drafts and the decomposition.
4. After gate-1: the orchestrator enters the delivery lines and withdraws or
   rewrites the superseded ones; final diff, `scripts/verify.sh`, gate-3.

## Log

- 2026-10-16 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/doc-012-matrix-organization-model
- 2026-10-16 [orchestrator] route confirmed by the human in chat: docs, L (ADR threshold; breaking change of the mod-002/mod-016/doc-004 models), BA → requirements review → gate-1 → final diff/verify → gate-3. gate-2 has no subject: screens are designed in the delivery tasks.
- 2026-10-16 [orchestrator] block 3: 3.1 no regulatory basis; 3.2 the human: existing data may be dropped (no live data), so the brief's § 13 data preservation is waived; 3.3 the scope is the whole brief.
- 2026-10-16 [orchestrator] human decision: a project has a default team, set in its settings or inferred when exactly one team is linked.
- 2026-10-16 [orchestrator] the human chose to start from scratch: an external agent's attempt was reviewed and rejected; only its pitfalls go in, as prior-art.md.
- 2026-10-16 [orchestrator] origin/main was synced by PR #2 (squash ee54131a); local main reset to it, trees identical, the old commits stay on sync/main-2026-10-16. Session bound natively (EnterWorktree) to this worktree, branch confirmed.
- 2026-10-16 [orchestrator] stage 1 done: 7 audits, spec.md, US-1..US-11, ADR-0028/0029 proposed (both go to gate-1), OQ-1..OQ-6. A completeness critic found 25 gaps; 24 fixed, the spec's packed long lines left. verify.sh green. Stage 2 goes to a new session.
- 2026-10-16 [orchestrator] owed: local main has queue commits not on origin (6d97479b Take doc-012; 8886d8c2 Queue tech-033 from another session). After gate-1, bring them to origin by a PR together with the delivery-line queue commits.
- 2026-10-16 [orchestrator] stage 2 started in a new session bound to this worktree. Round 1 fans out into five read-only lenses (coverage, structure, access, views/sprints, navigation/decomposition); fixes follow in runs owning disjoint files.
- 2026-10-17 [orchestrator] round 1: 5 lenses + 2 cross-checks, judged by one consolidator (the human cut a per-finding verification of ~230 agents to 3): 70 findings → R-1..R-34 in review.md (5 blocker, 19 major, 10 minor).
- 2026-10-17 [orchestrator] human answers in chat: R-6 A (the parent's «Управление» for «Наследовать доступ» and moves away; either end removes an area link), R-7 A (queue an area-analytics line), R-8 A (no area visibility), R-34 B (a deleted team's closed sprints stay readable).
- 2026-10-17 [orchestrator] human answers in chat: OQ-1 A (retire the item's «Область»), OQ-2 A (defer), OQ-3 A (admin has full access), OQ-4 A (additive migrations), OQ-5 A (doc-011 waits and restates), OQ-6 A (one Active sprint).

## Notes

Summary (from the queue line or an argument): Matrix organization model (the human's brief, 2026-10-16): audit of the current Organization/Project/Team/Area/Sprint model and access, the target model — Organization as the tenant boundary, Area as a universal workspace tree linked M:N to independent Projects and Teams, WorkItem with an optional owning team and a team-owned sprint across projects, Area/Project/Team boards over one item collection, scoped permissions with project visibility, Area inheritance and one effective-permissions algorithm — ERD, invariants, permission matrix, owed ADRs, and the decomposition into delivery lines that rewrite or withdraw feat-028 and mod-007 and touch doc-011/feat-038
