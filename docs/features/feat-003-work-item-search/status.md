# feat-003-work-item-search

- type: feature
- size: M
- branch: feat/003-work-item-search
- stage: gate-1+gate-2
- epic: core/work-items
- deps: mod-005
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: pending
gate-2: pending
gate-3: pending

## Plan

M with UI: analysis → requirements review → UX design → gate-1 + gate-2 to the human as one package → backend → frontend → one QA pass with the code review and the security pass → fix round → gate-3 → PR to main.

## Decisions

- E2E = NO: the standing answer since 2026-10-12; not asked, not run.
- Started under the human's standing approval of 2026-10-18 evening («Ты работай, я спать буду. Пр-ы сам мерджи»): the route confirmation was not asked; gate-1 + gate-2 go to the human as one package, one question per open question and per design decision.

## Log

- 2026-10-18 [ux-designer] design.md (30 KB), no prototype; D-1..D-6 open, each with a recommended A. Package for the human: OQ-1..OQ-3, D-1..D-6; stage gate-1+gate-2. No gate set; no code under src/.
- 2026-10-18 [business-analyst] R-1 resolved (review.md): a purge returns to the filtered list the card came from (BR-WS-5, amends mod-005 design § 6); the trail opens the list unfiltered (BR-WS-11); US-1 +3 scenarios. No new OQ. No code under src/.
- 2026-10-18 [requirements-reviewer] round 1: changes-requested. R-1 major is open for the analyst (where the card page lands after a purge, and its trail, under «Удалена»). R-2..R-15 minor are fixed in place in spec.md and US-1..US-3 (review.md). OQ-1..OQ-3 unchanged, for the human at gate-1. No gate set; no code under src/.
- 2026-10-18 [business-analyst] spec.md + US-1..US-3 (40b16959): BR-WS-1..17; tag filter to feat-016, assignee marks in the list's column; OQ-1..OQ-3 open, A recommended, for the human at gate-1. No code under src/.
- 2026-10-18 [orchestrator] route M with UI (queue size), gate-1 + gate-2 as one package after the design; started under the human's standing approval of 2026-10-18 evening, the route confirmation not asked. E2E: no.
- 2026-10-18 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/feat-003-work-item-search

## Notes

Summary (from the queue line or an argument): MVP. List filters on the M3 work item list: state incl. «Удалена» (the way into the recycle bin), assignee incl. former members, tag, keyword over Заголовок and the identifier (doc-002 US-11, doc-010 D-14); no area filter, since the item's «Область» retires (doc-012 OQ-1 A)
