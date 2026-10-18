# mod-020-team-switcher-default-and-filter-address

- type: module
- size: S
- branch: mod/020-team-switcher-default-and-filter-address
- stage: 06-code-review
- epic: core/backlogs-boards
- deps: mod-017
- gates: gate-3

## Gates

gate-3: pending

## Plan

Frontend only; S route. The human's answers of 2026-10-17 to mod-017 OQ-8 B and OQ-9 B (queue line).

1. OQ-8: `defaultEntry` (`shared/lib/workContext.ts`) takes the project's default team first (the list's `defaultMark`, as `isDefault`), then today's rule (BR-OT-11: the user's own first, else the first by name). The context source marks the teams it reads, and `withFirstEntries` too; a remembered team that still exists stays. Tests: `workContext.test.ts`, `useOrganizationsContextSource.defaultTeam.test.tsx`.
2. OQ-9: `workitems` gets `useTeamFilter`, which keeps the chosen teams in the address as the repeated `teamId` (api-contract § 3's name, `none` for «Без команды»), by replace, other parameters kept (`?workitem=`). «Рабочие элементы», the project backlog and board read it; the level drop-down carries it; a team's views ignore it. Tests: the list, both project views.
3. Docs: doc-012 BR-MX-11 amended ("mod-020, 2026-10-18"), pointers in mod-017 BR-OT-4, -8, -11. feat-040 is not merged: whichever of it and this merges second keeps the fallback on feat-040's team level (doc-013 BR-NE-9, OQ-2 A); feat-043 retires it.
4. Check: Vitest on the touched files, then `BASE=origin/main bash scripts/verify.sh`.

## Decisions

- E2E = NO (standing answer since 2026-10-12).
- OQ-8 read as «the default team first, then BR-OT-11's rule»: the user's own first team, else the first by name. The queue line's «else the first team by name» is read as that rule, so mod-016 US-7 (a member lands on their own team) stands; dropping the membership step is one line in `defaultEntry` if the human meant it.
- OQ-9: the address carries the read's name, the repeated `teamId` (`none` for «Без команды»); a change replaces the history entry; a blank value is no filter, any other value is kept and selects nothing (BR-OT-8); a team's views ignore it.
- The level drop-down's links carry the filter; the tab, «Показать канбан», the team menu and «Весь проект» are other views and start unfiltered (design § 6.3 as before). A list page number is not in the address and returns to 1 when the filter changes.
- Spec pointers: doc-012 BR-MX-11 amended; mod-017 BR-OT-4, -8, -11 and design § 6.3 name the amendment. feat-040 is not merged: the fallback lives in the existing switcher (`defaultEntry`), and whichever of this and feat-040 merges second carries it onto feat-040's team level (doc-013 BR-NE-9).

## Log

- 2026-10-18 [qa-engineer] light QA (S route), findings in `review.md`: 0 blocker, 0 major, 2 minor (QA-1 the project views' tab drops the filter, QA-2 pre-amendment text in mod-017 spec/stories) and Q-1 for the human (the fallback below the default). Plan 1-4 covered; six mutations of the new code, one survived and was closed by 2 added tests (`ProjectViews.teamFilterAddress.qa.test.tsx`, 7 tests). `BASE=origin/main bash scripts/verify.sh`: exit 0, RESULT green (lint, build, Vitest affected 1969 passed, source-reading guards 590 passed). E2E not asked (standing NO, see Decisions). stage → 06-code-review; gate-3 pending.
- 2026-10-18 [frontend-developer] `BASE=origin/main bash scripts/verify.sh`: exit 0, RESULT green (lint, build, Vitest affected 1962 passed, source-reading guards 590 passed). Ready for QA; gate-3 pending. E2E not asked (standing NO).
- 2026-10-18 [frontend-developer] built: OQ-8 in `defaultEntry` and the switcher's team read; OQ-9 `useTeamFilter` on the list, project backlog and board, the level drop-down carries it. Frontend only: no backend, no api-contract change. Specs amended, see Decisions.
- 2026-10-18 [orchestrator] route S, taken under the human's standing approval of 2026-10-18 evening («Ты работай, я спать буду. Пр-ы сам мерджи», «Включай auto-merge для всех пр-ов»): no route confirmation; the PR is auto-merged by the orchestrator once CI is green. Gate-3 stays pending.
- 2026-10-18 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/mod-020-team-switcher-default-and-filter-address

## Notes

Summary (from the queue line): The human's answers to mod-017 OQ-8 B and OQ-9 B (2026-10-17). OQ-8: with no team chosen, the team views' switcher opens the project's default team, else the first team by name; amend doc-012 BR-MX-11 (the default no longer affects creation only). OQ-9: the team filter on «Рабочие элементы», the project backlog and the project board lives in the page address too, so a link carries it, and survives re-reads and level changes as before (BR-OT-8).
