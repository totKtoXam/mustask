# mod-018-area-workspace

- type: module
- size: L
- branch: mod/018-area-workspace
- stage: gate-1+gate-2
- epic: core/org-access
- deps: feat-028
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: pending
gate-2: pending
gate-3: pending

## Plan

Route L with UI on the light route (the human's choice of 2026-10-17 for the
lines after mod-017): business analysis → requirements review (one round) →
UX design (there are new screens) → gate-1 + gate-2 put to the human as one
package, one question per open question and per design decision → backend
(plan, then code) → frontend → one QA pass over both layers alongside the code
review and the security pass → one fix round → at most one delta read →
gate-3 → PR to main. Plans: `plan-backend.md`, `plan-frontend.md`.

## Decisions

- E2E = NO: the standing answer since 2026-10-12; not asked, not run.
- Light route: the human's choice of 2026-10-17, see Plan.
- In scope by the human's answer at feat-028 gate-3 (2026-10-18): the
  ADR-0028 amendment carrying rule 8's clarification (links locked
  `FOR NO KEY UPDATE`, feat-028 plan-backend D-3) and A-9's corollary (a
  locked row's presence is judged by the lock statement's own answer,
  feat-028 review-code DR3-4). Its text is written at the backend stage, not
  in stage 1 (spec § ADR-0028 amendment).
- Interim surfaces for one line (doc-013): the area pages in the organization
  settings and the project's area links in the project's settings;
  `feat-043-area-project-team-navigation` moves them.
- Design (2026-10-18): D-1..D-7 of `design.md` § 14 are open for the package, each with a
  recommended option A; the prototype follows the stage file's rule for an L. No code before the
  human's answer.

## Log

- 2026-10-18 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/mod-018-area-workspace
- 2026-10-18 [orchestrator] route L (queue size), light route (the human's choice of 2026-10-17 for the lines after mod-017); started under the human's standing approval of 2026-10-18 evening («Ты работай, я спать буду. Пр-ы сам мерджи»), the route confirmation not asked; gate-1 + gate-2 go to the human as one package, one question per open question. E2E: no.
- 2026-10-18 [business-analyst] spec.md + US-1..US-6; stage → 02-requirements-review. For gate-1: OQ-1 (per-project areas in the upgrade; goes on with A, drop all), OQ-2 («Наследовать доступ» before grants; goes on with B, shown from mod-019).
- 2026-10-18 [requirements-reviewer] one round (light route): needs-human-decision, OQ-1 and OQ-2 to the package. R-1 major, R-2..R-10 minor; R-1..R-4, R-6, R-7, R-10 fixed in place; R-5's offer, R-8 and R-9's band go to stage 3 (review.md).
- 2026-10-18 [business-analyst] fix round: R-1 residual closed (tree routes retired by meaning, US-4, US-6, NFR); BR-AW-3 states R-5's move; the pickers and the tree hold under any design choice; US-4 names the band; R-8 stays for stage 3; OQ-1, OQ-2 unchanged.
- 2026-10-18 [ux-designer] design.md (32.5 KB) + prototype/index.html; D-1..D-7 take R-5, R-8's four, R-9 and the tree component. Package for the human: OQ-1, OQ-2, D-1..D-7; stage gate-1+gate-2. No gate set.

## Notes

Summary (from the queue line or an argument): The organization's area tree replaces the per-project tree: optional parents, a description, «Наследовать доступ», area–project and area–team links; the item's «Область» retires (OQ-1 A); area pages in the organization settings until the navigation line (doc-012 BR-MX-3..-8; US-1, US-3)
