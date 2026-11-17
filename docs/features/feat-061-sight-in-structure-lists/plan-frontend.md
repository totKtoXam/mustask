# Plan (frontend): feat-061-sight-in-structure-lists

Requirements: doc-016 `spec.md` BR-TD-17, BR-TD-21, US-6; `design.md` § 7.2. Contract: [api-contract.md](api-contract.md) (no new route or
field; the lists arrive already narrowed). No new slice, no new route.

## 1. «Структура» filter (BR-TD-21)

The shipped page is feat-043's tree with the diagram tab; doc-016's «Список»/«Дерево» card view is a separate, later piece of work, so the
field is the page's existing filter, reshaped to BR-TD-21:

- `areaTexts.areasPage.search` becomes «Найти департамент, команду или проект» (spec text); the field is 360 px (`ListFilter` gains
  `fieldWidth`) and Esc clears it (`escapeClears`); the band's «Очистить фильтры» stays the other way out.
- `lib/structureFilter.ts` `filterStructure(areas, text, {projects, teams})`: from the first character, case-insensitive; matches a node's
  name, a project node's key and the name of a team (a team is no row of the tree, its department stands for it). A match keeps its
  ancestors, a matching department keeps all its descendants. The tree it matches over is the server-narrowed `GET areas`; `projects`
  (`GET projects`) and `teams` (`GET teams`) are read with the existing hooks only while the field holds text, and are narrowed by the
  same server rule, so no hidden key or team name can match. No remembered state (a `useState`), never navigates.
- Nothing found: the existing «Ничего не найдено» frame.

## 2. R-4: a hidden selection on the aggregate views

- Selector offers: the client builds nothing of its own. `useNavigationSource` resolves the address and the remembered selection against the
  server's offers, so a remembered or addressed object the user cannot see is `lost` and takes the ADR-0032 § 6 repair («Ранее выбранный
  контекст недоступен. Выбрано: …»); «Остальные» is the offers' own non-fitting rest. No change needed there.
- The remaining path is an offers read that is stale (sight changed after it): the aggregate read answers the object's not-found code.
  `boards/lib/goneSelection.ts` (`notFoundLevel` of the organizations slice) turns it into the not-found frame (`NotFound keepNavigation`:
  the selectors stay usable) in `AggregateBacklogView`, `AggregateBoardView` and `AggregateWorkItemsPage` (grouped headers and the flat
  list); any other failure keeps the generic error with «Повторить». No new text: the frames' own.

## 3. Totals

«Обзор» counts come from the server (`projectCount`, `teamCount`, …). «Проекты», «Команды» and the tree have no paging; their `length` is a
count over the already narrowed list. Nothing counts over a broader list.

## Tests

`structureFilter.test.ts` (6), `AreasPage.test.tsx` (+2: field, key, Esc, empty), `AggregateViews.gone.test.tsx` (3).
