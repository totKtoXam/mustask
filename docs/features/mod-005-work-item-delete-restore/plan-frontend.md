# Plan — frontend (mod-005-work-item-delete-restore)

Stage 6a, light route. The card, three dialogs and the hosts; no page, no route, no kit part, no token, no icon glyph
(design § 1, § 12). Inputs: [design.md](design.md) (every Russian string quoted from it), [api-contract.md](api-contract.md)
(§ 1.1 `deletion`, § 2 three POST routes, § 4 two codes), [plan-backend.md](plan-backend.md) § 12 (as built: the contract is
unchanged), `docs/conventions/frontend.md`, `ui-kit.md`. E2E: standing answer NO — not asked, not run, specs listed in the report.

## Slice

All code is in `features/workitems` (one slice, no new one); `boards` gets tests only (its views already show the server's
text and read again). `shared/api/errorCodes.ts` gains the two codes (the drift test checks them against the catalog).

| Layer | Change |
|---|---|
| `api/workItemsApi.ts` | `WorkItem.deletion` (`fromState`, `by` {id, displayName, isActive, isMember}, `at`) or `null`; `deleteWorkItem`, `restoreWorkItem(stateId?)`, `purgeWorkItem` (POST sub-routes, contract § 2); the list item omits `deletion` |
| `permissions.ts`, `hooks/useWorkItemAccess.ts` | `workitems.workitem.delete` / `.purge`; the hook returns `canDelete`, `canPurge` |
| `lib/deletion.ts` (new) | `isDeleted(item)` by the state's **category** (BR-2), the deletion line with its marks (one helper for the deleter now and the assignee marks later) |
| `lib/cardTexts.ts` | every string of design § 2-§ 6, § 8 (menu entries, three dialogs, notices, the notice's two sentences) |
| `lib/deletionRefusals.ts` (new) | one place for § 8's matrix by `errorCode`: `gone` (a frame), `stale` (409 deleted / not_deleted: notice, close, read again), `stays` (the dialog's error) |
| `hooks/mutations.ts` | `useDeleteWorkItem`, `useRestoreWorkItem`, `usePurgeWorkItem`: set the item in the cache (not Purge), invalidate the lists and the other items |
| `hooks/useWorkItemEdits.ts` | a read that shows the Removed category drops the draft (both ways); a save refused `…deleted` = error notice + read again (D-5); `refresh({ notice: false })` for a silent read |
| `components/card/` | `DeleteWorkItemDialog`, `RestoreWorkItemDialog`, `PurgeWorkItemDialog`; `CardActions` (menu per card, «Восстановить» in the Save group's place); `WorkItemCard` (derives `deleted`, read-only look, notice + deletion line, owns the dialogs and their focus) |
| hosts | `WorkItemCardDialog` and `WorkItemFormPage` pass `canDelete`, `canPurge`, `onChanged`, `onPurged` (dialog: `close()`, page: `navigate(list, { replace })`) |

## State

Server state in TanStack Query (the item's key is set by the three writes). Client state in the card: which dialog is open
(`'delete' | 'restore' | 'purge' | null`), a focus request after a write. The dialogs hold their own busy/error and keep no copy of
server data except the restore field's chosen state. Deleted mode is **derived** from the read, never stored (design § 4).

## Kit parts and MUI

`ConfirmDialog` (`destructive`, `cancelAutoFocus` for the purge), `FormDialog` (restore), `InlineSelectField frame="always"`,
`CategoryMarker`, `FieldsSkeleton`, `InlineNotice`, `ActionButton tone="primary"`, `MoreActionsMenu` (entries: `DeleteOutlineOutlined`,
`DeleteForeverOutlined`, `destructive`), `RequiresOrganizationCode`, `useNotice().show(text, 'error')`. No MUI import beyond `Typography`/`Box`.

## Order of work (a commit each, tests with the code)

1. wire layer: api, codes, permissions, test API (deletion marks, three routes, list on live items, save refused first), role bundles (+ their test)
2. library: `deletion.ts`, texts, refusals; unit tests
3. mutations and the edits hook (mode flip, D-5, silent read); hook tests
4. the card: menu, deleted look, notice, «Восстановить»; the three dialogs; hosts; component and page tests
5. views: boards tests (move, tick, reorder, host re-read after the three writes), list test, `status.md`

## Tests (Vitest + MSW, `testApi.ts` extended per the contract)

Menu per role and card; every string; the three texts of Delete; the deleted card (reader, member, no marks, unknown deleter, marks);
restore preset / list without Removed / no mark / process loading and failed; each cell of § 8 incl. a double activation sending one request;
mode flip both ways with the draft dropped; save lost to a deletion (D-5); Purge's navigation and focus; boards: move/tick refused with the
server's text and a re-read; the host re-reads after each write. Retired: none (no test asserts a rule this line removes).

## Risks

- `useAccessCheck()` outside the gate fails closed, so dialog tests mount the gate (`renderWorkItems`); card unit tests that need no dialog do not.
- Focus after a menu-chosen dialog closes: MUI restores to a node that may be gone, so the card moves focus itself (effect after the write).
- The unknown deleter: the contract flags an unknown user `isActive: false`; the line shows «(неактивен)» only for a known name (for the human: design leaves it open).

## Merge with feat-028 (2026-10-18, frontend check of 83c68c69)

The backend pass changed no frontend file; this pass read the merged tree for what a textual merge cannot show. Nothing was broken. Six tests pin what the two lines share.

- Types and wire: `WorkItem` carries `deletion` beside `team`, the list row omits `deletion`; `testMembership.ts` holds the three writes beside feat-028's organization-level team routes (the project-nested owned-items and team routes are not in it); `errorCodes.ts` holds both lines' codes.
- Role mirrors: `src/test/organizationRoles.ts` holds 23 codes (17 + feat-028's four + mod-005's two); its test reads the three contracts.
- The unlink and team-deletion dialogs show the server's `unlink-count` and `deletion-counts` (any state, BR-DR-9) and read no work item: `TeamsPage.test.tsx`, `OrganizationTeamsPage.test.tsx`.
- A deleted card shows its owning team as stored (an unlink's destination; «Без команды» after the team's deletion) and Restore names no team (BR-DR-10): `WorkItemCard.deleted.test.tsx`, `WorkItemCard.restore.test.tsx`.
- Ran: `tsc -b`, ESLint over `src`, all 305 Vitest files (4669 passed, 6 skipped) plus the 6 new tests. E2E: standing NO, not run or edited. Specs broken (found by reading `e2e/`): mod-005's 27-work-item-card 27.5 and 27-organization-members 27.9 (served bundles; now also feat-028's four codes); feat-028's project-nested team helpers (`addTeam`, `deleteTeam`, `isFirstTeam`, `teamPath`, `createContext`) leave 19, 21.2, 23, 24, 25, 26, 27-organization-members, 28-team-members and 29 stale, which feat-028's status does not list.
