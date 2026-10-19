# Plan (frontend): work item list filters (feat-003)

Stage 5. Contract: [api-contract.md](api-contract.md) (unchanged); design: [design.md](design.md), D-1..D-6 answered A; backend as built: [plan-backend.md](plan-backend.md) § As built. All in `src/frontend/src/features/workitems`; no new slice, kit part, route, dependency or permission. M with UI: plan and code in one pass, then QA.

## 1. Slice map

| File | What |
|---|---|
| `api/workItemsApi.ts` | `WorkItemListFilter` (`teamId`, `state`, `assigneeId`, `search`); `getWorkItems(..., filter)` writes `?page=n` then `teamId*`, `state*`, `assigneeId*`, `search` (trimmed, omitted when blank); `getAssigneeFilterOptions(org, project, states)`; `WorkItemListAssignee` (the row's assignee gains `isActive`, `isMember`); `NO_ASSIGNEE_FILTER_VALUE = 'none'` |
| `api/queryKeys.ts`, `hooks/queries.ts` | list key = page + the four dimensions as sets; `assigneeOptions` key under the `lists` prefix, so every write that refreshes the list refreshes the options; `useWorkItems(..., filter)`; `useAssigneeFilterOptions` (`keepPreviousData`: the old options stay while the new come) |
| `hooks/useWorkItemFilter.ts` | reads the four from the address; one writer (`setFilter(change)`): one `navigate` (replace; router state, hash and every other parameter kept), built on the address as the last write left it, so a clear or two fast changes never lose a dimension; `useTeamFilter`'s `TEAM_FILTER_PARAM` and `teamFilterOf` imported, the hook not edited |
| `lib/workItemFilter.ts` | `filterOf`, `isFiltered`, `filterKey` (content, sorted), `stateOptions(process)`, `filterMismatch(item, filter)` (state, assignee, team, keyword by title / identifier / number, case and «ё» ignored), `createdInListNotice`, `filterTexts` («Фильтр задан», «Загрузка…», the new failure texts) |
| `lib/assigneeFilterOptions.ts` | the read's options to the picker's choices: `userNameWithMarks` text, «Не назначен» (`none`) last, `tellApart` (D-2: e-mail after a dash for equal texts that differ in e-mail, else « № n») |
| `components/KeywordFilter.tsx`, `StateFilter.tsx`, `AssigneeFilter.tsx` | take values and `onChange`, not the page (feat-013 reuses them); `InlineTextField` (255, hint and rest text «Заголовок или ID»; applies after 250 ms, on Enter, on leave), `InlineMultiSelectField` over the process's state names, `InlineMultiPickerField` (D-1) with `getOptionKey` = id; «Фильтр задан» in place of «Все …» when the address holds a value no option names (D-4) |
| `components/TeamFilter.tsx` | one line: `emptyText` follows the address (D-4); the only shared-file edit besides the page |
| `components/Marks.tsx` | `MarkedAssigneeName` for the list's column (mod-005's `userNameWithMarks`); `AssigneeName` untouched (boards) |
| `pages/WorkItemsPage.tsx` | the band `FilterBar` with «Поиск», «Статус», «Исполнитель», «Команда»; the key of the page from the four; states 1-9 of design § 5; the result announced through `LiveRegion`; the notice judged by all four; both clears send focus to «Поиск»; each title link carries the list's address in router state |
| `lib/listOrigin.ts`, `pages/WorkItemFormPage.tsx` | the mark and its check; after a purge `replace` to the marked address of this project's list, else the unfiltered list |

## 2. State

Server state: TanStack Query only. Client state: the keyword's typed text (`KeywordFilter`), the page number (keyed by the filters' content, so any change shows page 1), the announcement text. Nothing is stored beyond the address.

## 3. Tests (Vitest, Testing Library, MSW)

`testApi.ts` serves the new list parameters (deleted items under `state`), `assignee-options` and the row's flags (`listFilterDataset()` is spec § Common data). Unit: `workItemFilter`, `assigneeFilterOptions`, `listOrigin`, `useWorkItemFilter`, the three new reads in `workItemsApi.test.ts`. Component: `KeywordFilter.test.tsx` (pause, Enter, leave, trim, 255, address sync, a late answer of the router). Page: `WorkItemsPage.filters.test.tsx` (band, «Статус», «Удалена» rows and the card, unnameable values, the address and both clears, every state of design § 5, announcements, the creation notice, widths), `.assignee.test.tsx` (options, marks in options, tags and column, alike options, keys, a measured 2 000-person picker), `.keyword.test.tsx` (title, identifier, number, «ё», `%`, trim, link). `WorkItemFormPage.landing.test.tsx`: the purge landing both ways, page 1, Back, a restore, the trail.

## 4. Files shared with feat-040 (L, in flight)

`WorkItemsPage.tsx`, `lib/teams.ts` (not edited: the notice is composed in `lib/workItemFilter.ts`), `api/workItemsApi.ts`, `api/queryKeys.ts`, `hooks/queries.ts`, `TeamFilter.tsx`, `index.ts` (additions only). Whichever merges second reconciles and notes it in `status.md`.

## Findings (stage 5)

- **2 000 people in the picker (D-1, design § 11).** Measured in jsdom under load (`WorkItemsPage.assignee.test.tsx`): opening the list of 2 001 options about 25 s, narrowing by typing «1999» 2.7 s. jsdom is no browser, so these are a floor for regressions, not the screen's speed; QA times the open and the first keystroke on the stand. If the open is slow there, D-1 B (the plain multi-select) is the fallback. The kit is not virtualised.
- **Kit.** `InlineTextField` (`InputField`) drops `inputProps.onBlur` and `onFocus`: MUI's `InputBase` calls only the legacy `inputProps` for them, and the kit passes `slotProps.input`. «Поиск» therefore takes the blur from its wrapper. A kit fix is a separate task (a change to `shared/ui` runs the whole suite).
- **Merge with feat-040.** The second to merge reconciles the shared files of § 4 and notes it in `status.md`; `lib/teams.ts` is untouched, so feat-040's team reason in `createdNotice` stays a one-line reconcile: this line's notice is `createdInListNotice` in `lib/workItemFilter.ts`.
- **Router transitions.** react-router 7 applies a change of the address as a transition, so the address answers a write a moment late: `useWorkItemFilter` builds on its own pending writes, and `KeywordFilter` remembers the keywords it wrote, or the text typed meanwhile would be overwritten.

## 5. Not done, by decision

No tag filter (OQ-1), no row action or column for a deleted row (OQ-3), no line about restoring (D-6), `FilterBar` not changed (D-5), no E2E (standing NO; specs that may meet the band: `22-work-items.spec.ts`, `27-work-item-card.spec.ts`, both use exact or card-scoped locators).
