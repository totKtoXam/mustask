# feat-061-sight-in-structure-lists

- type: feature
- size: L
- branch: feat/061-sight-in-structure-lists
- stage: 8-review
- epic: core/org-access
- deps: feat-041
- gates: gate-3

## Gates

gate-3: pending

## Plan

Route L from stage 4. The requirements and the design are doc-016's,
approved at gate-1 and gate-2 on 2026-11-12:
`docs/features/doc-016-teams-under-departments/` (`spec.md` BR-TD-12..-17,
BR-TD-21; story US-6, surfaces part 1; `design.md`; `docs/adr/0040-*.md`).
Their gates are not repeated here.

Scope (US-6 surfaces, part 1): BR-TD-17 — exactly the objects of
BR-TD-12..-15, before paging, so no count or «ещё» reveals a hidden one — on
the selector offers' read, «Структура» (list and tree), «Проекты»,
«Команды», «Обзор» and the pickers; the «Структура» filter field «Найти
департамент, команду или проект» (BR-TD-21: the same sight-scoped match,
hides what does not match in the open view, a department staying while a
child matches, not remembered, never navigates). One rule: feat-041's set
form of the sight rule (the evaluator / `AccessSet`), no second
implementation. Closes feat-041's debt R-4 (header selectors offering hidden
objects; the aggregate views' generic error).

Out of scope: the multi-project views, search, counts and the sprint clauses
(feat-062); the context path button and its panel search (feat-063).

Stages: backend (plan-backend.md, code, tests, api-contract.md) → backend QA
→ frontend → frontend QA → code review (one Opus pass with the security pass)
→ gate-3 → PR on GitHub, merge commit, auto-merge asked for, branch kept.
The PR waits for feat-041 (PR #203) to reach main; then origin/main is merged
in (merge, not rebase).

Economy mode: one agent per stage, one Opus review. E2E: no (paused).
Verification: affected tests locally, the full verify.sh in CI.

## Log

- 2026-11-17 [orchestrator] task started on branch feat/061-sight-in-structure-lists in worktree .claude/worktrees/feat-061-sight-lists-c61481, cut from feat/041-project-visibility 9e0c9262 (PR #203 not merged yet); git rev-parse --show-toplevel and git branch --show-current confirmed. The worktree's stale core.hooksPath override (doc-012 worktree) unset.
- 2026-11-17 [orchestrator] route confirmed by the human in chat: L from stage 4 (the queue line said M; the L threshold "authorization rules change" fires: what each list and count discloses changes, a mistake is a leak); spec and design inherited from doc-016 (gate-1, gate-2 approved 2026-11-12); gate-3 only.
- 2026-11-17 [orchestrator] queue line taken by scripts/queue.sh take feat-061, PR #212 with squash auto-merge.
- 2026-11-17 [backend] plan-backend.md and api-contract.md written, code and tests committed (see the commit). Rule: feat-041's `AccessSet.Sees*` only; new `AccessSet.Sighted()` (id sets for the two database-side reads; memoized, linear), `CallerAccess.Sight` (the navigation read model fails closed for a non-administrator without it). Surfaces: selector-offers and overview (sight inside the statements, before count and cut; still 4 and 7 statements), projects, projects/deactivated, teams (rows and each team's nested projects), teams/{t}/projects, projects/{p}/teams and team-links, areas tree (list and tree), areas/{a} (children), areas/{a}/teams and /deletion, areas/writable-ids: handler-side `Sees*` filters. «Структура» filter (BR-TD-21) is client-side over the narrowed lists, no endpoint. No migration, no new route/field/error code. Tests: Application 889 passed (+6: `Sighted()` parity, administrator null, team grant; handler caller carries sight); Infrastructure `Organizations` 215 passed (+2 in `NavigationReadModelTests`: sight narrows counts/rows/offers in 7 and 4 statements, fail-closed; `ScopedAccessStatementCountTests` untouched and green); Api integration `Features.Organizations` 1658: 1657 passed, 1 failed = `UnlinkTeamVolumeTests` 5 s timing budget (19 s under the full run load, untouched code), which passes when run alone; new `StructureListsSightTests` 10 passed (US-6 cast + 6 hidden filler per kind, Olga/Vera/Max/Ann on every surface for items and counts, «Обзор» cut after the narrowing, nested team projects, flag/switch/membership change, link pickers). Deviation: `NavigationReadsQaTests.Cast_OfUs2` restated (Olga now sees MARS and VENUS, not JUPITER, per BR-TD-13); `NavigationReadModelTests` helper uses an administrator caller. `CallerAccess` gained a trailing optional `Sight`: feat-062 (parallel) should pass `set.Sighted()` the same way.
- 2026-11-17 [backend QA] passed: no blocker/major, one minor (m-1, team seen through a deactivated project, debt for gate-3). Plan from the spec before the code; all Organizations read endpoints checked; +2 tests in `StructureListsSightTests` (hidden-address 404s on every list route; deactivated project), class 12 passed. qa.md § Backend written. E2E not asked (standing NO).
- 2026-11-17 [frontend] plan-frontend.md written; code and tests committed. (1) «Структура» filter: the page's existing filter reshaped to BR-TD-21 — label «Найти департамент, команду или проект», 360 px, Esc clears; `lib/structureFilter.ts` matches name, project key (from `GET projects`) and team name (from `GET teams`, read only while the field holds text) over the server-narrowed tree, a match keeps its ancestors, a matching department keeps its descendants, not remembered, never navigates. (2) R-4: the selector offers, the remembered-selection repair and «Остальные» already read only the server's offers, so nothing client-side adds a hidden object back (no change); an aggregate read that answers the object's not-found code (stale offers) now shows the not-found frame with the navigation kept, no generic error and no «Повторить» (`boards/lib/goneSelection.ts`; backlog, board, «Рабочие элементы» grouped and flat). (3) Totals: «Обзор» counts are the server's; the other lists have no paging, their length is over the narrowed list; nothing to change. Tests: `structureFilter.test.ts` 6, `AreasPage.test.tsx` 35 (+2), `AggregateViews*` 6 files 48 (+3 in `AggregateViews.gone.test.tsx`), all passed; `tsc -b`, ESLint and Prettier clean on the touched files; no verify.sh (economy). Deviations: the shipped page is feat-043's «Дерево»/«Схема» (diagram) tabs, not doc-016's «Список»/«Дерево» cards (a separate later piece), so the filter acts on the tree view; the diagram keeps its own «Найти узел» search (feat-049) and is not narrowed by this field; a team match keeps only its department because a team is no row of this tree; the 404 frame is the object's own (`Область/Команда/Проект не найден(а)`), not the ADR-0032 § 6 repair notice, which stays the path for the remembered selection.
- 2026-11-17 [frontend QA] passed: no blocker or major, two minors (m-2, m-3) carried to gate-3 as debt. Plan from doc-016 spec/design (BR-TD-17, -21, US-6, design § 7.2) and feat-041 R-4 before the code; the four deviations judged: all acceptable. +3 `AreasPage.test.tsx` (team name finds its department, «Очистить фильтры» and no field on the diagram tab, not remembered), +1 `AggregateViews.gone.test.tsx` (board). Run: structureFilter 6, AreasPage 32, gone 4, all passed; ESLint and Prettier clean. E2E not asked (standing NO). qa.md § Frontend written. stage: 8-review.
- 2026-11-18 [code-reviewer] Round 1 (one Opus pass with the L security pass) over 9e0c9262..6c0957ec: approved; 0 blocker, 0 major, 5 minors open (R-1..R-5) plus QA m-1..m-3 confirmed, all debts for gate-3; findings in review.md.
- 2026-11-18 [backend] Code review R-1..R-5 fixed (see review.md § Fixes). R-1 link-pickers test with Manager grants, exact non-empty sets; R-2 `CallerAccess.Sight` required, eight feat-062 sites use `CallerAccess.NotNarrowed(...)`; R-3 deviation recorded in plan-backend; R-4 `filterTree` + test deleted; R-5 comment. Tests (foreground, affected only): `StructureListsSightTests` 12 passed (the picker test now asserts exact non-empty sets); Application 908 passed; Infrastructure `Organizations` 215 passed; Api, Architecture, Application, Infrastructure test projects build; frontend `areaTree.test.ts` 9 passed (-1 removed), `tsc -b` and ESLint clean. No verify.sh (economy).
