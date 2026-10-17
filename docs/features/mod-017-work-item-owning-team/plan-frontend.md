# Plan: mod-017 frontend

Stage 6a artifact (`docs/ai-workflow/stages/05-frontend.md`). Inputs: `design.md` (final
strings), `api-contract.md` (fixed 2026-10-17; no mismatch found with the backend as built),
`plan-backend.md` § 12. Light route (the human, 2026-10-17): one pass, no gold-plating —
each design state is built once, in the cheapest form the design allows.

## Slices touched (no new slice)

| Slice | Change |
|---|---|
| `shared/ui` | New kit part `InlineMultiSelectField` (+ test, showcase entry in `FieldsDemo`, `ui-kit.md` row) |
| `app/theme` | Token `layout.backlogTable.teamColumn` = 120, pinned in `theme.test.tsx` (table min width 1 180) |
| `organizations` | `Team` = `{id, projectId, name, defaultMark, isMember?}`; `AreaNode`/`Area` lose owner fields; routes `setTeamDefaultArea`, `setAreaOwner` go, `setDefaultTeam` (PUT `…/default-team`) and `getOwnedItemsCount` come; `DefaultAreaDialog`, owner dialogs, `FIRST_TEAM_CHIP`, `firstTeamName` go; `DefaultTeamDialog` + `DeleteTeamDialog` with the count; `TeamsPage`/`TeamPage` marks and the last-team rule; `AreasPage` loses «Владелец»; project dialog helpers. Barrel exports `useTeams`, `Team` for the other slices |
| `workitems` | `WorkItem.team`, `team: {teamId}` wrapper in create/save, `teamIds` on the list read; `TeamField` (card, dialog), `TeamFilter` (exported), card band row, dialog field + caption, list column + `FilterBar`; `placeRefusal` maps the two team codes to the field `team` before the not-found level |
| `boards` | `BoardTeam` without `defaultArea`; `team` on rows and cards; backlog column «Команда» after «Статус», board card line; project views: create command, filter row, state above the level, `keepPreviousData`, empty states per role; dialog starts with the team of the view or the project default |

## State and data

- Server state: TanStack Query. The teams read is the existing `organizationsKeys.teams`
  (one request serves card, dialog, filter, «Команды», switcher); the filter is part of the
  list / backlog / board query keys (`teamIds` sorted) with `placeholderData: keepPreviousData`.
- Client state: the filter (`string[]`, `'none'` = «Без команды») lives in `WorkItemsList`,
  `ProjectBacklogPage`, `ProjectBoardPage` — above the level, dropped on leaving the page.
- The draft of the card stays touched-only: `team` joins `Edit`/`WorkItemEdits`; untouched
  sends nothing, a pick sends `team: {teamId}`, «Без команды» sends `{teamId: null}`.

## Components (MUI through the kit only; legacy files keep their kitGuard entries, none is added)

`TeamField` = `InlinePickerField` (card `start/hover`, dialog `top/always`); `TeamFilter` =
`InlineMultiSelectField` + teams read; `Tag` for the marks; `InlineNotice`, `FieldsSkeleton`,
`ConfirmDialog`/`ConfirmAction`, `FormDialog`, `StateFrame`, `PageHeader` as named in design.

## Simplifications taken (user's request: simplify), none drops a design state

1. Test doubles move to the new model once (`organizations/testApi.ts`, `workitems/testApi.ts`,
   `boards/testApi.ts` + fixtures: `teamId` on a record replaces area owners); old model tests
   retire by name (spec "Existing rules" fate table) instead of being bent.
2. One shared `teamTexts` module in `workitems` for the picker/filter lines (design § 3, § 5.2).
3. The default-team dialog and delete dialog reuse `NameDialog`-style `FormDialog` plumbing and
   `ConfirmAction`; no new shared dialog.
4. Component tests cover each design state once, at the slice that owns it; no per-surface copies.

## Out of this pass

E2E (standing NO). Kazakh texts are the backend's. `status.md`, `backlog.md` untouched.

## Verification

While working: `npx vitest run <touched files>` and `npx eslint <touched files>`; at the end one
`BASE=0cf422c2 bash scripts/verify.sh`.
