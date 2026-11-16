# feat-070-search-dialog-kit

- type: feature
- size: M
- branch: feat/070-search-dialog-kit
- stage: 07-gate-3
- epic: platform/ui
- deps: -
- gates: gate-3

## Gates

gate-3: pending

## Plan

Spec and design are feat-065's (gate-1 and gate-2 approved there, 2026-11-13); this line writes neither and builds only the kit parts of [feat-065 design § 1](../feat-065-global-search/design.md) with the field look of D-14 and the panel behaviour of D-9.

1. `ThinSearchIcon` in `shared/ui/icons`: the thin magnifier of the prototype.
2. `HighlightedText` in `shared/ui/text`: `text` plus `ranges` or `query`; `<mark>` spans built from text, never from markup; bold plus the `search.match` tint.
3. `SearchField` in `shared/ui/fields`: 28 px borderless header field (D-14) with the icon, `InputBase` as `role="combobox"`, the «Ctrl K» / «⌘K» hint while empty, the clear ✕; width 300 px, 360 px focused.
4. `SearchOption`: link row with `role="option"`, icon, primary and secondary line, optional colour stripe, `↵` hint on the active row, plain `more` variant.
5. `SearchPanel` in `shared/ui/dialog`: non-modal `Popper` anchored to the field, `Surface` look, `ClickAwayListener`, slots for scope, «Полный поиск», body, key hints, `busy`, compact (full-width) mode.
6. Theme tokens `layout.searchField.width`, `layout.searchPanel.width` / `.maxHeight`, `layout.searchOption.height`, `palette.search.match`; exports from `shared/ui/index.ts`.
7. Every part has kit tests, a showcase section on `/ui-kit` and a guide block in `docs/conventions/ui-kit.md`. No feature uses the parts yet. Checked by the frontend tests and lint in CI.

## Log

- 2026-11-13 [orchestrator] route confirmed by the human in chat: M (queue line says S; about 15 files over five kit parts, the theme, the showcase and the guide, so S's 1–5 files would understate it). Route: frontend-dev → QA → code review → gate-3 → PR. Spec and design are feat-065's, so gate-1 and gate-2 have no subject here. Economy mode: one agent per stage, one Opus review; E2E stays paused.
- 2026-11-13 [orchestrator] the branch had already been cut by the session «feat-070: search dialog kit», which also took the queue line (PR #189). The human gave the task to this session («Это я, забери себе»): that worktree was detached with no changes, and the branch was checked out in .claude/worktrees/laughing-bartik-3ad2cc.
- 2026-11-13 [frontend-developer] plan items 1-7 built: ThinSearchIcon, HighlightedText, SearchField, SearchOption, SearchPanel, theme tokens (`layout.searchField|searchPanel|searchOption`, `palette.search.match`), exports, kit tests, `/ui-kit` section «Поиск», guide block. Lint, typecheck and the tests of the touched areas are green; the whole suite is left to CI (economy mode). Stage stays 05-frontend until QA.

- 2026-11-16 [qa-engineer] frontend QA done (qa.md): 4 edge test files finished, D-1 (SearchField dropped host onFocus/onBlur, major) found and fixed in kit code. Lint, tsc -b and touched-area vitest green; 0 blockers. Stage 06-code-review.
- 2026-11-16 [qa-engineer] Decisions: E2E: no (standing answer, paused).
- 2026-11-16 [code-reviewer] round 1 (review.md): request-changes; open: 1 major (M1, the SearchOption stripe renders 0 px high), 4 minors (debts).
- 2026-11-16 [frontend-developer] review round 1: M1 (option stripe rendered 0 px: now `layout.searchOption.stripeHeight`, pinned in tests), m1 (SearchField defaults `aria-expanded` to false) and m4 (highlight demo passes `aria-expanded={false}`) fixed; m2, m3 left as gate-3 debts.
- 2026-11-16 [orchestrator] delta read of 063f0401 against review round 1: M1, m1, m4 closed as described; no second review round (economy mode). Gate-3 debts: m2 (panel measures the field during render), m3 (narrow panel `100vw` includes the scrollbar), the `/ui-kit` «Поиск» section not checked by eye in a browser.

## Notes

Summary (from the queue line): The UI kit parts global search is built from (feat-065 design § 1, D-8): SearchField, SearchPanel, SearchOption, HighlightedText, ThinSearchIcon and their theme tokens, with kit tests, /ui-kit showcase and guide blocks; no feature uses them yet
