# mod-018-area-workspace

- type: module
- size: L
- branch: mod/018-area-workspace
- stage: 05-backend-qa
- epic: core/org-access
- deps: feat-028
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: approved (2026-10-18, human in chat, one question per open question: OQ-1 A, the upgrade drops every per-project area; OQ-2 B, «Наследовать доступ» stored now, shown and governed from mod-019)
gate-2: approved (2026-10-18, human in chat, one question per design decision: D-1..D-7 A)
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
- Design (2026-10-18): D-1..D-7 of `design.md` § 14 went to the package, each with a
  recommended option A; the prototype follows the stage file's rule for an L.
- Gate-1 + gate-2 (the human in chat, 2026-10-18, one question each): OQ-1 A,
  OQ-2 B, D-1..D-7 A; spec and design stand as written (both already went on
  with these). OQ-2 B: `mod-019-scoped-access`'s queue line gains «Наследовать
  доступ»'s control, its rules and doc-012 US-1's switch clauses (spec
  § Decomposition), amended by the orchestrator's queue PR.
- Also in the ADR-0028 amendment, by the human's answer at mod-005 gate-3
  (2026-10-18, «⭐ A: В поправку mod-018»): rule 8 states the one lock order
  with the project row and the hierarchy tier in it (mod-005 review-code
  DR2-2 (c)), written today only in mod-005 `plan-backend.md` § 14.3 and
  feat-028 `plan-backend.md` § 7.1: unlink and team deletion take the project
  hierarchy lock before the owned rows (mod-005 QD-1).
- Development waits for feat-040 to merge: both re-lay the card's band
  (feat-040 D-2 puts «Проект» in block A row 2; this design's D-6 moves
  «Команда» up in block B), and both touch the item's read models.

## Log

- 2026-10-18 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/mod-018-area-workspace
- 2026-10-18 [orchestrator] route L (queue size), light route (the human's choice of 2026-10-17 for the lines after mod-017); started under the human's standing approval of 2026-10-18 evening («Ты работай, я спать буду. Пр-ы сам мерджи»), the route confirmation not asked; gate-1 + gate-2 go to the human as one package, one question per open question. E2E: no.
- 2026-10-18 [business-analyst] spec.md + US-1..US-6; stage → 02-requirements-review. For gate-1: OQ-1 (per-project areas in the upgrade; goes on with A, drop all), OQ-2 («Наследовать доступ» before grants; goes on with B, shown from mod-019).
- 2026-10-18 [requirements-reviewer] one round (light route): needs-human-decision, OQ-1 and OQ-2 to the package. R-1 major, R-2..R-10 minor; R-1..R-4, R-6, R-7, R-10 fixed in place; R-5's offer, R-8 and R-9's band go to stage 3 (review.md).
- 2026-10-18 [business-analyst] fix round: R-1 residual closed (tree routes retired by meaning, US-4, US-6, NFR); BR-AW-3 states R-5's move; the pickers and the tree hold under any design choice; US-4 names the band; R-8 stays for stage 3; OQ-1, OQ-2 unchanged.
- 2026-10-18 [ux-designer] design.md (32.5 KB) + prototype/index.html; D-1..D-7 take R-5, R-8's four, R-9 and the tree component. Package for the human: OQ-1, OQ-2, D-1..D-7; stage gate-1+gate-2. No gate set.
- 2026-10-18 [orchestrator] the human answered in chat, one question per open question and per design decision: OQ-1 A, OQ-2 B, D-1..D-7 A. gate-1 and gate-2 approved; stage → 04-backend. The mod-005 gate-3 answer adds the one lock order to the ADR-0028 amendment (Decisions); mod-019's queue line is amended by the orchestrator's queue PR; development starts after feat-040 merges.
- 2026-10-27 [orchestrator] build session started in economy mode (one agent per stage, one Opus review incl. security); main (e4d8f124: feat-040 #31, feat-003 #32) merged in; usage at start: weekly 0%, 5-hour 2%.
- 2026-10-27 [backend-developer] backend done, stage → 05-backend-qa. plan-backend.md + api-contract.md (contract unchanged since it was fixed); org-level area tree, 3-end links, creation with links, item area retired; migration OrganizationAreaWorkspace (Down works); ADR-0028 rule 8 amended in place; error catalog (depth_exceeded, not_empty text, root_is_fixed deprecated). Tests: Domain 827, Application 762, Contracts 248, Architecture 86 green; Api.IntegrationTests and Infrastructure.Tests COMPILE but were NOT RUN: Docker Desktop was not running and could not be started in this session, so the whole RequiresDocker set (new AreaWorkspaceEndpointTests, AreaLinkEndpointTests, OrganizationAreaWorkspaceMigrationTests, every edited fixture) is unverified and is the first thing QA runs. Deviations: handler-side second codes on create (area.create / area.link / project.link / team.link read in the handler); BoardsWorld.RootAreaId is a Guid.Empty shim, MoveToAreaAsync now sets the owning team; item-area tests dropped (US-4). Not yet written: concurrency pairs (name, cycle, depth, delete vs create, link vs deletion), 1 000-area volume and statement-count tests for the new reads. feat-028's migration test now targets its own migration, not head.

## Notes

Summary (from the queue line or an argument): The organization's area tree replaces the per-project tree: optional parents, a description, «Наследовать доступ», area–project and area–team links; the item's «Область» retires (OQ-1 A); area pages in the organization settings until the navigation line (doc-012 BR-MX-3..-8; US-1, US-3)
