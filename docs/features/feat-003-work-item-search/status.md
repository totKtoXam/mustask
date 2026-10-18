# feat-003-work-item-search

- type: feature
- size: M
- branch: feat/003-work-item-search
- stage: 01-business-analysis
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

- 2026-10-18 [orchestrator] route M with UI (queue size), gate-1 + gate-2 as one package after the design; started under the human's standing approval of 2026-10-18 evening, the route confirmation not asked. E2E: no.
- 2026-10-18 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/feat-003-work-item-search

## Notes

Summary (from the queue line or an argument): MVP. List filters on the M3 work item list: state incl. «Удалена» (the way into the recycle bin), assignee incl. former members, tag, keyword over Заголовок and the identifier (doc-002 US-11, doc-010 D-14); no area filter, since the item's «Область» retires (doc-012 OQ-1 A)
