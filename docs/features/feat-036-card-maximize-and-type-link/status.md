# feat-036-card-maximize-and-type-link

- type: change
- size: S
- branch: feat/036-card-maximize-and-type-link
- stage: complete
- epic: core/work-items
- deps: feat-029
- gates: gate-3

## Gates

gate-3: approved · orchestrator under the human's standing approval of 2026-10-05 (D-1); verify.sh green

## Plan

<!-- S/M: implementation plan, 3-10 lines. L: see plan-backend.md / plan-frontend.md -->

1. `DialogShell` (kit): `onExpand` keeps its place and meaning «a plain callback, never guarded», but now means «Развернуть на весь экран»; a new `expanded` flag makes the card fill the window (the narrow-window full screen, reused) and renames the button «Свернуть» with a new `ThinCollapseIcon`.
2. `CardHeaderBar` (kit): `typeHref`; when given, the type line («<Тип> <ID>») is a link opened in a new tab (`target=_blank`, `rel=noopener noreferrer`, title «Открыть на отдельной странице»). Only the dialog passes it (`useCardParts`, level 2); on the page the line stays plain text.
3. `WorkItemCardDialog` keeps `expanded` above the per-identifier dialog, so a switch to a related item does not shrink the window.
4. The carried-edits path (BR-C-3) goes: `useWorkItemDialog().expand`, `useCarriedEdits`, `CARRIED_EDITS_KEY`, `readCarriedEdits`, `initialEdits` in the card and `useWorkItemEdits`, `CardFrameProps.edits` (now `pagePath`). Their tests go with them; the behaviour tests are rewritten for the new meaning, not deleted.
5. E2E 27 (27.6 steps) and 28 follow; not run (E2E is paused). `.gitignore` gets `.agents/teamwork/`.

## Decisions

- D-1 (human 2026-10-13, in chat, with two screenshots): «Нажатие кнопки "Развернуть страницу" не должна открывать карточку в странице, а растянуть модалку на весь экран»; «Для открытия на новой по нажатию на текст с типом каталога и номером». The route is the short one (2026-10-05): the orchestrator decides the gates.
- D-2 (orchestrator): the button's name becomes «Развернуть на весь экран» / «Свернуть»; the type line's link opens the page in a new tab, so unsaved edits do not travel with it (a new tab is a new session of the card) — which makes the carried-edits path dead, and it goes.
- D-3 (orchestrator): the page's own type line stays plain text (it is the `h1`; a link to itself helps nobody).
- D-4 (orchestrator, E2E paused 2026-10-12): the E2E steps are written and not run; they join tech-027's first run.
- D-5 (human 2026-10-13): `.agents/teamwork/` (another tool's local state) goes into `.gitignore`; main was kept clean meanwhile through `.git/info/exclude`.

## Log

- 2026-10-13 [dev] implemented per the Plan; tsc, eslint green; unit tests of workitems, dialog, card, uikit and src/test run (see the merge message for verify.sh).
- 2026-10-13 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/feat-036-card-maximize-and-type-link

## Notes

Summary (from the queue line or an argument): The card dialog's expand icon stretches the dialog to the full screen instead of opening the page, and the type line (type name and identifier) opens the item's page in a new tab (the human, 2026-10-13); the old carried-edits path goes; .agents/teamwork/ joins .gitignore
