# feat-003-work-item-search

- type: feature
- size: M
- branch: feat/003-work-item-search
- stage: 06-code-review
- epic: core/work-items
- deps: mod-005
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: approved (2026-10-18, human in chat, one question per open question: OQ-1..OQ-3 A)
gate-2: approved (2026-10-18, human in chat, one question per design decision: D-1..D-6 A)
gate-3: pending

## Plan

M with UI: analysis → requirements review → UX design → gate-1 + gate-2 to the human as one package → backend → frontend → one QA pass with the code review and the security pass → fix round → gate-3 → PR to main.

## Decisions

- E2E = NO: the standing answer since 2026-10-12; not asked, not run.
- Started under the human's standing approval of 2026-10-18 evening («Ты работай, я спать буду. Пр-ы сам мерджи»): the route confirmation was not asked; gate-1 + gate-2 go to the human as one package, one question per open question and per design decision.
- Gate answers, the human in chat, 2026-10-18: every OQ and D-n as recommended. The queue amendments OQ-1 A and OQ-2 A owe (spec § Decomposition) are the orchestrator's next queue sync:
  - feat-016 gains the list's tag filter;
  - feat-017 drops the list column's assignee marks;
  - feat-043's BR-NE-8 clause carries no tag filter.

  Development merges main first (mod-020 is in main, PR #25). Whichever of feat-003 and feat-040 merges second reconciles the shared files and notes it here.

## Log

- 2026-10-18 [frontend-developer] stage 5 done (plan-frontend.md): «Поиск», «Статус», «Исполнитель», «Команда» in the list's own `FilterBar` (D-1..D-6 A), `useWorkItemFilter` writes the four in one replace (`useTeamFilter` imported, not edited), «Удалена» rows read like any row, the marks through `userNameWithMarks` in options, tags and column, the purge lands on the filtered list from a row and the trail opens it unfiltered (mod-005 design § 6 and status.md, mod-003 status.md noted). Findings: (1) 2 000 people in the picker, measured in jsdom: open 2 001 options about 25 s, narrow by typing 2.7 s: jsdom is no browser, so QA times it on the stand (design § 11), D-1 B is the fallback; (2) the kit's `InlineTextField` drops `inputProps.onBlur`/`onFocus` (MUI's `InputBase` calls only the legacy `inputProps`), so «Поиск» takes a blur from its wrapper: a kit fix is a separate task. Merge with feat-040: shared files `WorkItemsPage.tsx`, `api/workItemsApi.ts`, `api/queryKeys.ts`, `hooks/queries.ts`, `TeamFilter.tsx`, `index.ts` (additions; `lib/teams.ts` untouched, the notice is `lib/workItemFilter.ts`'s); the second to merge reconciles and notes it here. No E2E spec breaks (22, 27 use exact or card-scoped locators; the standing NO stands)
- 2026-10-18 [backend-developer] stage 4 done: `state`, `assigneeId`, `search`, `…/workitems/assignee-options` and the row's `assignee.isActive` per api-contract.md (unchanged since 6ae3bed3); deviations in plan-backend.md § As built; mod-003 and mod-005 noted. No E2E spec breaks
- 2026-10-18 [backend-developer] resumed after a stop: restored the 7 salvaged, untested test files (route inventory, OpenAPI, SearchTestData, state, assignee and keyword endpoint tests) over d4dbf178; read against the plan, all sound, kept and passing
- 2026-10-18 [orchestrator] gate-1 (OQ-1..OQ-3 A) and gate-2 (D-1..D-6 A) approved by the human in chat, one question each; boxes ticked in spec.md and design.md; stage 04-backend.
- 2026-10-18 [orchestrator] the gate-1 + gate-2 package goes to the human, one question per open question and per design decision; no code before the answers.
- 2026-10-18 [ux-designer] design.md (30 KB), no prototype; D-1..D-6 open, each with a recommended A. Package for the human: OQ-1..OQ-3, D-1..D-6; stage gate-1+gate-2. No gate set; no code under src/.
- 2026-10-18 [business-analyst] R-1 resolved (review.md): a purge returns to the filtered list the card came from (BR-WS-5, amends mod-005 design § 6); the trail opens the list unfiltered (BR-WS-11); US-1 +3 scenarios. No new OQ. No code under src/.
- 2026-10-18 [requirements-reviewer] round 1: changes-requested. R-1 major (the card page's landing after a purge, and its trail) went to the analyst; R-2..R-15 minor fixed in place (review.md). OQ-1..OQ-3 unchanged. No gate set; no code under src/.
- 2026-10-18 [business-analyst] spec.md + US-1..US-3 (40b16959): BR-WS-1..17; tag filter to feat-016, assignee marks in the list's column; OQ-1..OQ-3 open, A recommended, for the human at gate-1. No code under src/.
- 2026-10-18 [orchestrator] route M with UI (queue size), gate-1 + gate-2 as one package after the design; started under the human's standing approval of 2026-10-18 evening, the route confirmation not asked. E2E: no.
- 2026-10-18 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/feat-003-work-item-search

## Notes

Summary (from the queue line or an argument): MVP. List filters on the M3 work item list: state incl. «Удалена» (the way into the recycle bin), assignee incl. former members, tag, keyword over Заголовок and the identifier (doc-002 US-11, doc-010 D-14); no area filter, since the item's «Область» retires (doc-012 OQ-1 A)
