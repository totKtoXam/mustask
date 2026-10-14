# Plan — frontend (mod-016-organization-access)

Stage 6a (L task, deep pass) per `docs/ai-workflow/stages/05-frontend.md`,
written on `mod/016-organization-access` at `86e3dc5`. No product code is written
in this step. A bare "§ n" below is a section of this plan; the design is cited as
"design § n" and the contract as "contract § n". Inputs read: `status.md` (D-1..D-19;
gate-2 is D-17), `spec.md`, US-1..US-8, `design.md` (its DC-1..DC-12 are binding),
`api-contract.md` (the backend is built and accepted, so the contract is the truth
and its § 6 is the hand-over), ADR-0027 and ADR-0024, `docs/conventions/frontend.md`,
`src/frontend/AGENTS.md`, mod-015's `plan-frontend.md` as the format precedent —
and the panel as it stands at that commit: `features/auth/AuthContext.tsx`,
`shared/lib/{access,workContext,format}.ts`, `shared/api/{httpClient,errorCodes}.ts`,
`shared/ui/{StateFrame,PageHeader,TreeRegistry,ConfirmDialog,FormDialog}.tsx`,
`app/{AppLayout,routes}.tsx`, `app/navigation/*`, `features/organizations/*`
(pages, hooks, context source, `permissions.ts`, `testApi.ts`),
`features/workitems/*` (assignee picker, form, list, `Notice.tsx`),
`features/boards/*` (`BacklogTable`, `BacklogRow`, `BoardColumns`, `Notice.tsx`),
`src/test/*` and `src/frontend/e2e/*`. Other lines in flight and what they touch
are in § 5.

## 0. What is built where

| Story | Frontend part | Pass |
|---|---|---|
| US-1 «Участники» | the page, three dialogs, the user search | B |
| US-2 scoped access | the panel side only: «Организация не найдена» at every depth, «Недостаточно прав» placement, the organization list; the server side is accepted | A |
| US-3 team members | `TeamMembersSection` and its dialog on the team page; the creator's first-team membership has no screen | B |
| US-4 served codes, navigation, controls | the access read, the gate, navigation by served codes, notices, re-read triggers (A); the controls and empty states by role on every existing page (B) | A + B |
| US-5 assignee rule | the picker keeps its behaviour (§ 3.6), the reader's form shows the assignee as text | B |
| US-6 existing data | no frontend code; E2E 27.8 and 20.4 see its result (a non-member meets «Организация не найдена») | E2E |
| US-7 default team | `isMember` on the team list; the default is the first member team | A |
| US-8 realm | no frontend code; E2E 16 and 19.9 see it (a user in no group reads «Языки») | E2E |

**Pass A** builds the machinery and leaves the application working: every
organization page sits behind the gate, the shell reads the served codes, the
navigation, the notices and the default team follow the new rules. Pages still draw
their write controls (the server refuses what it must). **Pass B** adds the new
screens and takes the controls away by role. The two passes can be one agent after
the other; after A the lanes B1 (new screens) and B2 (role views of existing pages)
touch disjoint files and may run in parallel (§ 3.9).

## 1. Decisions of this plan (P-1..P-15)

Everything the design leaves "to the frontend plan" (design § 2.4, § 12) and the
calls this plan adds. Each is reversible in the one place it names.

- **P-1 Home of the access layer.** `features/organizations/access/` (design § 12:
  everything new lives in `features/organizations`). `app/` reaches it through the
  slice barrel, as it already reaches `useOrganizationsContextSource`. No new slice,
  no new global store: the data lives in TanStack Query.
- **P-2 What the access query holds.** The query `['organizations','access',<id>]`
  *answers* instead of throwing on a 404: its data is `AccessAnswer = { kind:
  'member', role, permissions } | { kind: 'not-member' }`; only a failed read
  (not 404) is an error. This is design § 2.1's "codes / a 404 / nothing" with
  TanStack's own semantics: `data` is the last answer, `error` is a read that failed
  since, and a failed re-read leaves `data` as it was (design § 2.2). A 404 from any
  other scoped call writes the same `not-member` sentinel with `setQueryData` (the
  watcher, § 2.2).
  The query's options: default `staleTime` (0, so a mount of the gate or of a new
  selection re-reads in the background) and `refetchOnWindowFocus: true` (the client's
  default is `false`; the switcher's lists opt in the same way).
- **P-3 The gate is two pieces.** `OrganizationGate` is a layout route (pathless)
  that owns the query, binds the check and rescues focus; `OrganizationPage` is a
  thin per-route wrapper (`code`, `title`, `skeleton`) that draws rows 1–5 of design
  § 2.1 and mounts the page only in row 5. The layout route survives moves between pages of
  one organization, so a page move re-reads nothing; the wrapper is what knows each
  page's code and its skeleton. In react-router 7.18 `useParams()` inside a pathless
  layout route sees the child route's `:organizationId` (checked against
  `matchRoutes` in this worktree: the layout's match carries `{organizationId}`);
  `OrganizationGate.test.tsx` pins it.
- **P-4 Scoped failures are watched under the client, not in `App.tsx`.** Tests build
  their own `QueryClient`, so options on the application's client would not run
  under test. `useAccessWatcher()`, mounted once in `AppLayout`, subscribes to the
  `QueryCache` and the `MutationCache` of whatever client is provided. It needs the
  failing request's path, so `ApiError` gets an optional `path` (a third constructor
  argument, set by `send()` for a non-OK answer); a missing `path` means "not
  scoped".
- **P-5 One check, read from the cache.** `AppLayout` builds the unbound check:
  instance codes from the token (`useAuth().can`), organization codes from the access
  query of the organization asked about (`undefined` organization or no `member`
  answer: "no"). The check reads the cache, not React state, and `AppLayout`
  re-renders its consumers through a version counter (`useSyncExternalStore` over the
  cache's access events), so the context value changes identity exactly when a served
  answer changes. The gate binds it to the address's organization (design § 2.6).
  `AuthContext.can` stays instance-only (token); only its comment changes.
- **P-6 The notice mechanism: one signal, not one shared corner.** Design § 2.4 left
  the choice. A shared corner would rewrite three hosts that two other lines (tech-015,
  feat-029) also touch. Instead: `shared/lib/noticeCorner.ts` carries the role-change
  notice's id and a `claim()`; each host calls `useNoticeHost(close)`, which closes
  its own notice when the shell raises a new fallback (read from the work context,
  as `OrganizationsSection` does today) or a new role change, and returns `claim`, to
  call when the host shows a notice (the newer replaces the older, so the shell's
  role-change notice closes). The fallback notice wins: while it is shown a role
  change is not raised. The shell renders the role-change `Snackbar`.
- **P-7 User search: slice-local, the assignee picker untouched.** A second consumer
  of the picker pattern would justify `shared/ui`, but moving `AssigneePicker` means
  editing `features/workitems`, which feat-029 is rewriting. `UserSearchField` and a
  copy of `useDebouncedValue` live in `features/organizations` ("deliberately
  duplicated", frontend.md). Folding both into one shared field is the kit's job
  (tech-015) and is listed as a debt (OP-3).
- **P-8 Default team (D-5).** `isMember` rides the team list: `Team` list items carry
  it; `LevelList` items and `WorkContextTeam` accept an optional `isMember`; one
  helper `defaultEntry(items)` = the first item with `isMember === true`, else the
  first, is used by `effectiveId`, `withFirstEntries` and `resolveWorkContext`. A
  remembered or address team that exists still wins (BR-M1-24, mod-015 BR-18).
- **P-9 A dialog whose code has gone.** Design § 2.3: the dialog stays, the primary
  becomes disabled. Eight dialog components would each need a prop. Instead
  `shared/ui/RequiresOrganizationCode` (a provider, `code`) wraps a page's dialogs and
  `FormDialog` and `ConfirmDialog` read it: locked means the primary is disabled and
  Enter does not submit; «Отмена» and «Закрыть» still work. Two one-line edits in the
  shared dialogs, one new file.
- **P-10 Focus rescue.** `useFocusRescue` in the gate remembers the last focused
  element inside `#main-content`; after a commit in which the served codes or the row
  changed, if focus fell to `body` and that element is gone, it focuses the main
  region (`shared/lib/mainRegion.ts` owns the id and `focusMainRegion()`). This one
  rule covers design § 2.2's "a control that disappears" and row 3's "focus goes to
  the main region".
- **P-11 The role-change notice names the organization from the work context.** The
  switcher's organization list holds every organization the user belongs to. When the
  name is not there yet (a list read still running after a refocus) the notice waits
  for it, and is dropped if the organization's access turns `not-member` meanwhile.
- **P-12 Test infrastructure first.** `src/test/mocks/handlers.ts` serves a default
  `GET /api/organizations/:id/access` answering the administrator bundle, so every
  existing test that mounts the shell with a real selection still passes
  (`onUnhandledRequest: 'error'`); `renderShell` primes the cache with the same answer
  for its source's selection so the first frame has codes; `testRender` helpers take a
  `role` (default `admin`) instead of "everything allowed" (§ 2.8).
- **P-13 Two skeleton kinds in row 1.** Design § 2.1 names `RegistrySkeleton` and
  `FormSkeleton`. The wrapper takes `skeleton: 'registry' | 'form'` (and the columns).
  Process, board, backlog and work-item pages have slice-internal skeletons; row 1
  shows the shared one of the closest kind and the page's own takes over when it
  mounts. No new string.
- **P-14 `routes.tsx` composition.** The nine organization routes stay inside
  `OrganizationsSection` (the notice host survives moves between the list and an
  organization); the gate sits inside it for the seven organization-addressed ones
  plus «Участники». The process, work-item, board and backlog routes get their own
  gate instance. The two list routes (`/organizations`, `/organizations/deactivated`)
  carry no organization and stay outside any gate.
- **P-15 The 403-or-404 re-read is a background read of the access key, and a
  `not-member` answer re-reads the organization list once.** The list read is
  `invalidateQueries` with `cancelRefetch: false` on the active list key, started when
  an organization's access *turns* `not-member` (from `member`, from nothing, or from a
  scoped call's 404). A second `not-member` answer for the same organization is not a
  turn, so the access read that follows a call's 404 starts no second list read
  (design § 15 item 12).

## 2. Pass A — infrastructure

### 2.1 Shared pieces (D-17), kept minimal

| File | Change |
|---|---|
| `shared/lib/access.ts` | `AccessCheck = (code, scope, organizationId?) => boolean`; export `bindAccessCheck(check, organizationId)` (the third argument defaults to the bound id); the comment loses the interim clause and says an organization code asks about one organization (design § 2.6). `denyEverything` stays the default context value. `PermissionScope` stays (`project` and `team` only say how much of the context an entry needs; all three answer from the organization's served codes). |
| `shared/api/httpClient.ts` | `ApiError` gets `readonly path?: string`; `send()` passes the request path to the `ApiError` it throws for a non-OK answer. Nothing else changes. |
| `shared/lib/format.ts` | `formatDate(iso)`: «ДД.ММ.ГГГГ» for a `YYYY-MM-DD` or ISO string, `'—'` for empty or invalid, as `formatDateTime`. Used by the read-only form (§ 3.4). |
| `shared/lib/workContext.ts` | `WorkContextTeam.isMember?: boolean`; `defaultEntry(items)`; `resolveWorkContext`/`selectInWorkContext` pick the default through it. The comment "the first team is the default until mod-016 (D-19)" is rewritten. |
| `shared/lib/mainRegion.ts` (new) | `MAIN_REGION_ID = 'main-content'`, `focusMainRegion()`; `AppLayout` takes its `id` from it. |
| `shared/lib/noticeCorner.ts` (new) | P-6: `NoticeCornerContext` (`roleChangeId`, `claim`), `useNoticeHost(close)` — closes on a new fallback id (work context) or role-change id; returns `claim`. Inert outside the shell. |
| `shared/ui/StateFrame.tsx` | optional `headingLevel?: 2 \| 3` for a frame that is not full page: the element is `h3`, the look is unchanged (the `h2` variant). Full-page frames stay `h1`. |
| `shared/ui/PageHeader.tsx` | a plain header (no `variant`, no `viewsEnd`) draws the tab strip only for two or more views; a single view draws none. The team head of «Бэклоги» and «Канбан» (`variant="team"` with `viewsEnd`, feat-027) keeps its one tab and the level drop-down beside it. |
| `shared/ui/TreeRegistry.tsx` | the actions column (header cell, row cell, button) is drawn only when some node has an action; optional `showActions` overrides it for the loading skeleton so an administrator's skeleton keeps the blank column. The context-menu keys already do nothing for an empty menu. |
| `shared/ui/RequiresOrganizationCode.tsx` (new) + `FormDialog.tsx`, `ConfirmDialog.tsx` | P-9. `RequiresOrganizationCode code={…}` provides "locked" = the bound check says no; both dialogs read it into the primary's disabled state and into the submit guard. |

Tests: `access.test.ts` (third argument, binding, default deny), `httpClient` (the
`path` of a thrown `ApiError`; none on 401), `format.test.ts`, `workContext.test.ts`
(`defaultEntry`), `noticeCorner.test.tsx`, `StateFrame.test.tsx` (the `h3` level, the
`h2` look), `PageHeader.test.tsx` (one view: no tabs; two: tabs; the team head keeps its single tab), `TreeRegistry.test.tsx`
(no actions column; with `showActions` the skeleton keeps it), `FormDialog` and
`ConfirmDialog` tests (locked: primary disabled, Enter inert, «Отмена» works).

### 2.2 The organizations slice: codes, roles, the access read

| File | Change |
|---|---|
| `permissions.ts` | The interim model goes: the header comment (reads "in no token yet … the shell's interim clause satisfies them") is replaced. `organizationsPermissions` names the organization codes of the contract matrix: `organizationRead`, `organizationWrite`, `organizationDeactivate`, `membershipRead`, `membershipWrite`, `projectRead`, `projectWrite`, `projectDeactivate`, `teamRead`, `teamWrite`, `areaRead`, `areaWrite`, and keeps `organizationCreate` (the one instance code). |
| `lib/roles.ts` (new) | `ORGANIZATION_ROLES = ['admin','member','viewer'] as const`, `OrganizationRole`, `ROLE_LABELS` («Администратор», «Участник», «Читатель»), `ROLE_DESCRIPTIONS` (the three helper texts of design § 3.6, verbatim), `roleChangeNotice(organizationName, role)` (design § 2.4's string, verbatim). |
| `api/accessApi.ts` (new) | `getAccess(organizationId): Promise<AccessAnswer>` per contract § 1.1: 200 → `member` (the `role` and `permissions` as served, `permissions` kept as given), `404 organizations.organization.not_found` → `not-member`, every other failure throws. `OrganizationAccess` is the wire shape. |
| `api/queryKeys.ts` | `access(o)`, `members(o)`, `memberCandidates(o, text)`, `teamMembers(o, p, t)`, `teamMemberCandidates(o, p, t, text)`; `teamMembersOf(o)` (prefix) for the removal's invalidation. |
| `access/accessAnswer.ts` (new) | the `AccessAnswer` type; `gateRow({answer, fetching, failed}, holds)` → `'loading' \| 'failed' \| 'notFound' \| 'denied' \| 'content'` — pure, the five exclusive rows of design § 2.1: a `not-member` answer is row 3 whatever else is running or failed; a `member` answer is row 4 or 5 by `holds`; no answer is row 1 while a read is running or has not started, row 2 once the last read failed and nothing is running. |
| `access/useOrganizationAccess.ts` (new) | `useOrganizationAccess(organizationId, { enabled? })`: `useQuery` on the key of P-2 with P-2's options, enabled for a non-empty id unless `enabled === false`; returns the query result (data = the held answer). The gate reads the address's organization, `AppLayout` the selection's; the two share the cache entry when they are the same organization. |
| `access/servedAccess.ts` (new) | `readServed(client, o)` (cache read), `useServedAccessCheck(can)` → `{ access, isCodesRead(o) }` (P-5), the version counter over access cache events. |
| `access/scopedFailure.ts` (new) | `scopedFailureOf(error)`: for an `ApiError` of status 403 or 404 with a `path` (query string ignored) under `/api/organizations/<id>/…` or exactly `/api/organizations/<id>`, `{ organizationId, status, organizationNotFound }`; `null` for `/api/organizations`, `/api/organizations/deactivated`, every other prefix, and an `ApiError` without `path`. The access read itself (`…/<id>/access`) is a scoped path too but never fails as 404 (P-2); any other failure of it is ignored here. |
| `access/roleMemory.ts` (new) | per `QueryClient` `WeakMap` of the last role read per organization: `rememberRole`, `lastRole`, forgotten when the organization's access query is removed. An own role change remembers the new role before the re-read, so it raises no notice (design § 2.4). |
| `access/useAccessWatcher.ts` (new) | three duties in one subscription (P-4): (1) a scoped failure, from the query cache (`error` actions) and the mutation cache: 403 or any 404 → `invalidateQueries(access(o))`; a 404 whose code is `organizations.organization.not_found` first writes `not-member` into `access(o)`; (2) an access answer that *turns* `not-member` → one organization-list read (P-15); (3) a `member` answer whose role differs from `lastRole` (never the first) → the role-change notice, unless a fallback is shown (P-6, P-11). Options `{ fallbackShown, organizationName(id) }` from the work context; returns `{ roleChange, dismissRoleChange }`. |
| `access/useFocusRescue.ts` (new) | P-10. |
| `access/OrganizationGate.tsx` (new) | P-3. |
| `access/OrganizationPage.tsx` (new) | P-3 and § 2.3 below. |
| `hooks/mutations.ts` | the own-role-change hook (pass B) and the removal hooks call `rememberRole` and invalidate through helpers in `access/`; pass A only exports the helpers. |
| `index.ts` | exports `OrganizationGate`, `OrganizationPage`, `useServedAccessCheck`, `useOrganizationAccess`, `useAccessWatcher`, `organizationsPermissions` (extended), and later `MembersPage`, `MEMBERS_ROUTE`, `membersPath` (B). |

### 2.3 The gate and the five rows (US-4, DC-1, DC-12)

`OrganizationGate`: reads `:organizationId`; runs the access query; binds the check
(`bindAccessCheck(base, organizationId)`) and provides it, with a small `GateContext`
(`answer`, `fetching`, `failed`, `retry`), to its `<Outlet />`. `OrganizationPage`
keys its content by the organization id (page state never survives a change of
organization) and draws, from `gateRow`:

| Row | What the wrapper renders |
|---|---|
| 1 `loading` | `PageHeader` with the static `title` (no commands, no views) over the `skeleton` (`RegistrySkeleton columns` or `FormSkeleton`, `role="status"` «Загрузка данных»); the page is not mounted, so its data reads wait |
| 2 `failed` | the same header over `ErrorBanner` titled «Не удалось загрузить права доступа» with «Повторить» (`retry` → `refetch`, which is row 1 while it runs) |
| 3 `notFound` | `NotFound level="organization"` (full page, registers not-found with the shell); held while any re-read runs, fails or answers 404 again |
| 4 `denied` | `AccessDenied` (the page's `code` is not held) |
| 5 `content` | the page |

The table of routes the wrappers go on (`title` for rows 1–2, `skeleton`):

| Route | Code | Title | Skeleton |
|---|---|---|---|
| `ORGANIZATION_GENERAL_ROUTE` | `organizationRead` | Общие | form |
| `MEMBERS_ROUTE` (B) | `membershipRead` | Участники | registry: Имя, Почта, Роль, Добавлен, Роль изменена |
| `PROJECTS_ROUTE` | `projectRead` | Проекты | registry: Название, Ключ |
| `DEACTIVATED_PROJECTS_ROUTE` | `projectDeactivate` | Проекты | registry: Название, Ключ, blank |
| `PROJECT_GENERAL_ROUTE` | `projectRead` | Общие | form |
| `TEAMS_ROUTE` | `teamRead` | Команды | registry: Команда, Область по умолчанию, blank |
| `TEAM_ROUTE` | `teamRead` | Команда | form |
| `AREAS_ROUTE` | `areaRead` | Области | registry: Область, Владелец, blank |
| `PROCESS_DEFINITION_ROUTE` | `processPermissions.definitionRead` | Процесс | form |
| `WORK_ITEMS_ROUTE` | `workItemsPermissions.workItemRead` | Рабочие элементы | registry |
| `WORK_ITEM_ROUTE` | `workItemsPermissions.workItemRead` | Рабочий элемент | form |
| `BOARD_ROUTE` | `boardsPermissions.workItemRead` | Канбан | registry |
| `BACKLOG_ROUTE` | `boardsPermissions.workItemRead` | Бэклоги | registry |

The re-read triggers (D-9, BR-M1-31) and how each runs; all of them are background
reads of the one key (§ 2.2), never row 1 after an answer is held:

| Trigger | Mechanism |
|---|---|
| organization switch (the switcher, or the address moving to another organization) | the observer of the new key mounts (`AppLayout` for the selection, the gate for the address) and the entry is stale, so it reads; a switch back reads in the background over the cached answer |
| window refocus | `refetchOnWindowFocus: true` on the key (the observers are the shell's and the gate's; TanStack dedupes to one request) |
| the user's own role changed (pass B) | the mutation's success calls `rememberRole`, then `invalidateQueries(access(o))` |
| any 403 or 404 from a scoped call other than the access read | `useAccessWatcher` duty 1: `invalidateQueries(access(o))`; a 404 «Организация не найдена» first writes `not-member` and (duty 2) reads the organization list once |

Behaviour the tests pin (design § 12): the retry case (a re-read in rows 3–5 never
shows row 1; «Повторить» and a refocus in row 2 do); the row walk (one row per state);
the 404 of a read, of a dialog's submit and of the work-item form's save each put the
page in row 3 at once, no `Alert`, the dialog or form gone, focus in the main region,
the organization list read once and the access re-read once, whose own 404 starts
neither a second list read nor a second access read; while that access re-read runs the
frame holds (no skeleton, no banner, no content). The tests hold an
answer with `src/test/gate.ts` to see rows 1 and 3 while a read runs, and walk the
states of the tab (nothing held with a read running and with the last read failed; a
404 held; codes held with the page's code missing and held) to see exactly one row each.

### 2.4 The shell: `AppLayout`, navigation, switch target

| File | Change |
|---|---|
| `app/AppLayout.tsx` | the interim clause (`status === 'authenticated'`) goes; `useServedAccessCheck(can)` supplies the check and `isCodesRead`; the selection's organization is read through `useOrganizationAccess(selection.organizationId, enabled: isReal)` so the navigation has its codes; `resolveSwitchTarget` passes `isCodesRead`; `isSettingsPage(currentPage, visible, selection)`; `useAccessWatcher()`; the role-change `Snackbar` (`Alert severity="info"`, 6 s, bottom left, text from `roleChangeNotice`, not shown while a fallback is) and the `NoticeCornerContext` provider; `id={MAIN_REGION_ID}`. |
| `app/navigation/visibility.ts` | `isEntryVisible` asks `access(entry.permission, entry.scope, context.selection.organizationId)`; `NavigationContext` gains `skipServedCodes?: boolean` (the switch resolver's unread `next`, DC-8): the permission is not asked for a non-instance entry, the page-exists and context-real conditions stay. `isSettingsPage(currentPage, visible, selection)` counts the entries the user sees; `deriveTrail` already works on `visible`. |
| `app/navigation/switchTarget.ts` | `switchTarget(model, access, request, isCodesRead)`: asks `isEntryVisible` about `next` with `skipServedCodes: !isCodesRead(next.organizationId)`. A switch from «Участники» of A to B opens «Участники» of B whatever B's codes (design § 12). |
| `app/navigation/model.ts` | the comment on the interim clause goes. «Участники» is registered in B with the page (BR-7: an entry exists once its page does). |
| `app/routes.tsx` | P-14. The comments "no gate: every signed-in user passes until mod-016" are replaced. |
| `features/auth/AuthContext.tsx` | `can`'s comment: instance codes only; organization codes are not in the token (ADR-0027) and are asked through `useAccessCheck()`. No behaviour change. |

Navigation by served codes (design § 6.1): every entry's code is a read code held by
all three roles, so the three roles see the same entries; what decides an entry is
the real context and the read codes. Unread codes hide the organization's entries,
and a page no visible entry matches is beneath no landing: no «Меню настроек», no
marker, no trail, «Языки» stays visible (it is an instance code from the token, now
in every token — D-7).

Tests: `visibility.test.ts` (the organization argument, `skipServedCodes`,
`isSettingsPage` on visible entries only), `switchTarget.test.ts` (an unread `next`
keeps the entry and takes its landing in the new context; a read `next` without the
code falls to «Главная»; a model entry with an organization code injected through
`NavigationModelContext`), `AppLayout.access.test.tsx` (the three roles see the same
entries; unread codes show no «Меню настроек», no marker, no trail and «Языки» alone
in the menu, and the menu gains «Организация» and «Проект» when the read answers; a
user with no organization still gets «Настройки» → «Языки»; whose codes, § 3.8),
`AppLayout.roleChange.test.tsx` (no notice on a first read or an own change; the text
with the organization's name; it waits for the name; the fallback wins; a host's newer
notice closes it), `routes.access.test.tsx` (the real routes under the gate: a
non-member's address at every depth, a page by address without its code, the
organization list outside any gate), and an `AuthContext` test that a token carrying
only instance roles answers `can('workitems.workitem.write')` with `false`.

### 2.5 Notice hosts (design § 2.4, DC-11)

| File | Change |
|---|---|
| `features/organizations/components/OrganizationsSection.tsx` | its own fallback bookkeeping is replaced by `useNoticeHost(() => setNotice(null))`; `notify` calls `claim()` first. |
| `features/workitems/components/Notice.tsx` | `useNotice()` calls `useNoticeHost` and `claim()` before it shows. |
| `features/boards/components/Notice.tsx` | the same for `showSuccess`, `showSuccessAlert`, `showError`. |

Tests: each host closes its notice for a new fallback id and for a new role-change id,
and shows neither together with the shell's; the 403-refusal-then-role-change case
(the refusal notice closes and the role-change notice stands; with the role unchanged
the refusal keeps its notice; the fallback outranks both), on the organization pages,
the backlog, the board, the work-item list and the form.

### 2.6 Default team (US-7, D-5)

| File | Change |
|---|---|
| `api/organizationsApi.ts` | `getTeams` returns `TeamInList[]` (`Team` plus `isMember: boolean`, contract § 3.3); the single-team read and the writes keep `Team`. |
| `context/resolveSelection.ts` | `LevelList` items accept `isMember?`; `effectiveId` falls back to `defaultEntry(items)`. |
| `context/useOrganizationsContextSource.ts` | the team list maps `isMember` through `listOf`; the tree's teams carry it; `withFirstEntries` takes `defaultEntry(teams)`. |
| `testApi.ts`, `testFixtures.ts` | `teamAnswer`/`teamList` carry `isMember` from the dataset's `teamMembers` for the acting user (empty by default → the first team, as before). |

Tests: `resolveSelection.test.ts` (own team, first of several in the project's order,
none → first, remembered kept, remembered gone → own team), the source hook on all
US-7 scenarios (choose a project, choose an organization, first load, gone team
falls back with the notice, address wins, leaving a team counts at the next read),
`withFirstEntries` for a team-level page moved to another project.

### 2.7 The organization list, and the interim model's comments (US-2, design § 5)

`pages/OrganizationListPage.tsx`: the active empty state is «Вы пока не состоите ни в
одной организации» with the create-right hint and action, or «Попросите администратора
организации добавить вас.» and no action; the deactivated empty state keeps the
heading «Деактивированных организаций нет» with the new hint «Здесь показываются
деактивированные организации, в которых вы администратор. Их можно восстановить.»
(design § 5 and spec § Screens and copy, verbatim).
The interim model's remaining mentions are rewritten (comments only):
`workitems/permissions.ts`, `boards/permissions.ts`, `process/permissions.ts`,
`process/pages/ProcessDefinitionPage.tsx`, `shared/api/errorCodes.ts` (the line
"where each shows is stage 6's" now points at design § 9), `shared/lib/workContext.ts`.
`workItemsPermissions` gains `linkWrite: 'workitems.link.write'` (pass B uses it).
`docs/conventions/frontend.md` "Permissions" gains two sentences: organization pages
sit under `OrganizationGate`; `useAccessCheck()` below it answers for the address's
organization.

### 2.8 Test infrastructure (P-12)

| File | Change |
|---|---|
| `src/test/organizationRoles.ts` (new) | `ADMIN_CODES`, `MEMBER_CODES`, `VIEWER_CODES`: each lists every code the bundle holds, hand-copied from contract § 0.2 (the one duplication; E2E 27.9 reads the served bundles against it), `servedFor(role)` (the contract § 1.1 body), `checkFor(role)` (an `AccessCheck`), `withoutCodes(role, …)` for a bundle that lacks some (design § 12's backlog bundles). |
| `src/test/mocks/handlers.ts` | the default `GET …/organizations/:id/access` answering the administrator bundle. |
| `src/test/renderShell.tsx` | primes the access cache for the stub source's selection (administrator unless the test says `access: 'unread' \| role`), and exposes the `QueryClient` so a test can `setQueryData`. |
| `features/organizations/testRender.tsx`, `features/workitems/testRender.tsx`, `features/boards/testRender.tsx` | take `role` (default `'admin'`) and provide `AccessCheckContext` from `checkFor(role)`; `renderPage` today answers `true` for every scoped code. |
| `features/organizations/testApi.ts` | `OrganizationsTestApi` serves `GET …/access` from its dataset: the administrator bundle for a known active organization, the 404 for an unknown or deactivated one, so every existing flow (including "unknown organization → «Организация не найдена»") keeps its meaning under the gate; `teamAnswer`/`teamList` carry `isMember` (§ 2.6). |
| `features/organizations/testFixtures.ts` | `Dataset` gains optional `users` (the projection's `UserRef`s with state), `memberships`, `teamMembers` and `actingUserId`, empty by default; `exampleDataset()` stays as it is. |
| `features/organizations/testMembership.ts` (new) | `serveMemberships(data)` installs, in front of `serveOrganizations()`, the role-aware access read (contract § 1.1) and `scopedGuard()`: one catch-all `http.all` before the organization routes that applies contract § 0.1's order (404 for a non-member, 403 for a missing code) from a route→code table (contract § 4) and falls through otherwise. Pass A writes these two; lane B1 adds the nine member and team-member routes of contract § 1.2–§ 2.4 (the order of § 1.5 and § 1.6's checks, `last_admin`, the 409s, candidates cut at 20 with `moreMatched`). |

### 2.9 «Недостаточно прав»: where it shows (design § 2.3)

Every surface already prints the server's text for a 403 where it prints its other
refusals (the dialogs' `Alert`, the form's `Alert`, the slice hosts' error notices, the
add row's field error). What is new, and where each lands:

| Surface | New behaviour | Lands in |
|---|---|---|
| `FormDialog`, `ConfirmDialog` | after the re-read, if the code is gone the dialog stays with its typed values and its `Alert`, the primary disabled, «Отмена» working | pass A: P-9 (`RequiresOrganizationCode` in the shared dialogs); pass B: every page and dialog host wraps its dialogs |
| Work-item form | the form turns into the read-only form and shows the draft as typed | B: § 3.4 |
| An action with no dialog (team removal, restore, a backlog drop or move entry, «Убрать родителя», a card move) | the control disappears; a changed role replaces the refusal notice with the role-change notice, an unchanged role keeps it | A: P-6 (`claim`, the hosts' closers); B: the controls by role (§ 3.3–§ 3.5) |
| The backlog's inline add row | the row closes with its «+», the title is dropped, focus goes to the main region | B: § 3.5 |
| A read | `ErrorBanner` with «Повторить»; after the re-read the page is row 4 when its code is gone | A: § 2.3 |

Every one of them is followed by the watcher's re-read (§ 2.3), which needs no code in the
surface.

## 3. Pass B — screens

### 3.1 «Участники» (US-1)

New: `pages/MembersPage.tsx`; `paths.ts` (`MEMBERS_ROUTE =
'/organizations/:organizationId/settings/members'`, `membersPath`); the navigation
entry (`id 'members'`, label «Участники», scope `organization`, `membershipRead`,
between «Проекты» and «Процесс»); `api/membershipApi.ts` (contract § 1.2–§ 1.6:
`getMembers`, `searchMemberCandidates`, `addMember`, `changeMemberRole`, `removeMember`;
types `UserRef` (`state`: `active | inactive | unknown`), `Member`, `MemberCandidates`);
`hooks/queries.ts` (`useMembers`, `useMemberCandidates`), `hooks/mutations.ts`
(`useAddMember`, `useChangeMemberRole(organizationId, selfId)`, `useRemoveMember`).

| Component (`components/`) | Content |
|---|---|
| `lib/memberTexts.ts` (new) | the strings of design § 13 and the spec in one place: the six notices (<имя> without the mark, «Неизвестный пользователь» for an unknown user), the three confirmation texts with the unknown-user variant, the search lines; every string verbatim, none invented in a component. |
| `UserLine.tsx` | design § 3.3: name, `Chip size="small" variant="outlined"` «неактивен» (the mark is text, read aloud), «Неизвестный пользователь» in `text.secondary`; a plain-name helper `plainName(user)` (no mark) for notices, confirmation texts and the row button's name (`lib/userName.ts`). |
| `MembersTable.tsx` | `Table size="small" aria-label="Участники организации"`; columns «Имя», «Почта», «Роль», «Добавлен», «Роль изменена»; rows in the server's order; dates by `formatDateTime`, the dash «—» `aria-hidden` plus visually hidden «Роль не менялась»; the actions column and `RowActionsMenu` («Изменить роль», «Удалить из организации» in the error colour) only with `membershipWrite`. |
| `UserSearchField.tsx` | `Autocomplete` + `TextField` (P-7): `filterOptions` returns the server's answer, hint lines are disabled options, `maxLength` 100, search from 2 trimmed characters, 250 ms debounce; one `useUserSearch(scope)` picks the organization or the team search; the lines are the spec's («Введите не менее 2 символов имени или почты», «Поиск…», «Пользователи не найдены», «Показаны первые 20. Уточните запрос.», «Не удалось выполнить поиск. Попробуйте ещё раз.»); opening the list again retries a failed search. |
| `AddMemberDialog.tsx` | `FormDialog` «Добавление участника»; «Пользователь» (required) and «Роль» (`TextField select`, default «Участник», helper = the role's description); «Добавить» disabled until a user is chosen; refusals per § 3.7 below. |
| `ChangeRoleDialog.tsx` | «Изменение роли»: «Пользователь: <имя>», «Роль» preselected and focused, helper by role; «Сохранить» disabled while the role is unchanged; the self-demotion `Alert severity="warning"` («Вы потеряете права администратора. Вернуть их сможет только другой администратор.») when the own row reads «Администратор» and another role is picked. |
| `RemoveMemberDialog.tsx` | `ConfirmDialog` with `cancelAutoFocus` and `destructive`: «Удалить участника?» / «Удалить себя из организации?», the three texts of design § 3.7 (the unknown-user variant included). |

`MembersPage`: `PageHeader` «Участники» with the primary «Добавить участника» only with
`membershipWrite`; states: loading (`RegistrySkeleton`), error (`ErrorBanner`, header
stays), data — no empty state (DC-3). Own row: `member.user.id === useAuth().subject`.
Flows (focus and notices verbatim from design § 3.5–§ 3.8):

- add: success closes, re-reads, notice «Пользователь «<имя>» добавлен в организацию.»,
  focus on «Добавить участника»;
- role change: notice «Роль пользователя «<имя>» изменена.»; own change to a
  non-administrator role calls `rememberRole`, invalidates `access(o)` at once, the
  controls vanish and the focus rescue (P-10) moves focus to the main region;
- removal of another: notice «Пользователь «<имя>» удалён из организации.», focus on the
  next row's «Действия», else the previous, else «Добавить участника»;
  invalidates `members(o)` and the organization's team-member lists;
- removal of oneself: invalidates the organization lists, navigates to
  `/organizations`, then removes `access(o)` from the cache (the gate has unmounted), focus
  on the main region; the page notice
  «Вы больше не состоите в организации «<название>».» only when nothing replaces the
  organization (the precedent in `OrganizationGeneralPage`: `fallsBack` judged
  before the write); otherwise the switcher's fallback notice speaks.

All three dialogs sit under `RequiresOrganizationCode code={membershipWrite}` (P-9).

### 3.2 Team members (US-3)

`components/TeamMembersSection.tsx` takes `{ organizationId, projectId, team }` and
renders under the facts on `TeamPage` (so it follows the page wherever feat-028 puts
it; the team address is built in one function in `membershipApi.ts`):
`h2` «Участники команды» with `Button variant="text"` «Добавить в команду»
(`teamWrite`), the table named by `aria-labelledby` the heading, columns «Имя», «Почта»,
the row menu with one item «Исключить из команды» (`teamWrite`), the column omitted
for non-writers. States: loading, error (the facts stay), data, and the empty frame —
administrator: «В команде пока нет участников» / «Добавьте участников организации
кнопкой «Добавить в команду».» with the action; others: the same heading with
«Состав команды задаёт администратор организации.» — as `StateFrame headingLevel={3}`.
`components/AddTeamMemberDialog.tsx`: «Добавление в команду», «Участник организации»
(`UserSearchField`, team scope), helper «Поиск среди активных участников организации»
until a refusal replaces it. «Исключить из команды» acts at once; `RowActionsMenu`
gets an optional `busy` (disabled button with `CircularProgress size={16}`) while the
row's request runs; on success the notice «Пользователь «<имя>» исключён из команды
«<команда>».» and focus on the next row's «Действия», else the previous, else the
section's «Добавить в команду»; on failure «Участник команды не найден.» in the error
`Snackbar` (the slice host) and the table re-read. The team page keeps `TeamPage`'s
own commands under `teamWrite` (§ 3.3).

### 3.3 Role views of the existing pages (US-4, design § 6.2, § 6.3)

Every page asks the bound `useAccessCheck()` with the named constants of § 2.2; a
missing code hides the control and whatever it would leave empty. Each page's write
dialogs sit under `RequiresOrganizationCode` with the dialog's code.

| Page / file | Hidden without | Notes |
|---|---|---|
| `OrganizationGeneralPage` | «Переименовать» (`organizationWrite`), «Деактивировать» (`organizationDeactivate`) | `commands` holds only what is held; the dialogs wrap per code; once the deactivation succeeds the organization's access entry is removed (its address answers not found from now on). |
| `ProjectListPage` | «Создать проект» (`projectWrite`); the «Деактивированные» view (`projectDeactivate`) | views `[Активные]` alone → no tab strip; empty hint for non-holders «Проекты создаёт администратор организации.» without the action; the deactivated page body is only reached with `projectDeactivate` (wrapper). |
| `ProjectGeneralPage` | «Переименовать» (`projectWrite`), «Деактивировать» (`projectDeactivate`) | the «Процесс» link stays (read). |
| `TeamsPage` | «Добавить команду», the actions column (`teamWrite`) | links to the team page stay. |
| `TeamPage` (B1) | «Переименовать», «Удалить команду», «Изменить» (`teamWrite`) | plus § 3.2. |
| `AreasPage` | «Новая область», «Новая дочерняя область», the actions column (`areaWrite`) | `TreeRegistry` omits the column; the tree stays a treegrid; empty hint for non-holders «Области задаёт администратор организации.». |
| `WorkItemsPage` | «Новый рабочий элемент» (`workItemWrite`) | empty: «Рабочие элементы создают участники организации с ролью «Участник» или «Администратор».» without the action. |
| `WorkItemFormPage` | the editable form (`workItemWrite`); «Родитель» picker (`linkWrite`) | § 3.4. |
| `BacklogPage` / `BacklogTable` / `BacklogRow` | § 3.5 | |
| `BoardPage` / `BoardColumns` | «Новый элемент», the card «Ещё действия: <id>», dragging a card (`workItemWrite`) | empty «Канбан пуст» keeps its hint for everyone; the action only for holders. |
| `OrganizationListPage` | already by the instance code | § 2.7. |

Tests per page: `*.roles.test.tsx` next to each page, table-driven over
administrator, member, reader with bundles from `organizationRoles.ts`: the controls of
the column above present or absent, the empty-state strings (design § 6.3 table, verbatim),
and for one dialog per page the 403 flow (the `Alert`, the typed values kept, the
codes re-read, the primary disabled once the code is gone). The existing tests that
assumed everything allowed move to the explicit administrator role.

### 3.4 The reader's work-item form (DC-4, design § 6.5)

> **Superseded (D-24, 2026-10-13):** built as feat-029's read-only card; the `dt`/`dd` steps here and in § 4.4 no longer apply.

`workitems/components/WorkItemReadView.tsx` (new) draws the form as values: the band
(same stripe, `h1` heading line as today, the title as `Typography variant="h1"
component="p"`), the header strip, text sections (`FormSection`, `pre-wrap`), field
sections and «Связанная работа». Markup fixed for the E2E selectors (§ 4.4): every
label/value pair is a `dl` with `dt` (`subtitle2`, `text.secondary`) and `dd`
(`body1`), including the header strip; an empty value is «—» (`aria-hidden`) with the
visually hidden «не указано»; lists show their option's label, «Не указан» when empty;
numbers through `formatNumber` after `parseNumber` of the draft's text (a stored 1.5
and a typed «1.5» both read «1,5»); dates through `formatDate`; «Родитель» is a
`Link` «<идентификатор> <заголовок>» or «Нет родителя». No input, no «Сохранить», no
«Ещё действия»; the only tab stops are links.
`pages/WorkItemFormPage.tsx` `LoadedForm`: `canWrite = access(workItemWrite)`,
`canLink = access(linkWrite)`. Without `canWrite` it renders the read view *from the
draft* (`edited ?? draftOf(item)`) and keeps the error `Alert` and the notice; the
component stays mounted, so a member turned reader after a 403 sees what they typed.
With `canWrite` and no `canLink`, `RelatedWorkSection` shows the parent as text.
`lib/draft.ts` gains no change. Tests (`WorkItemFormPage.readOnly.test.tsx`): the
reader's view; the stored 1.5; a member turned reader after a 403 sees the draft; the
parent as text; tab stops; no placeholder or helper text anywhere.

### 3.5 Backlog and board (feat-027 as in main)

`BacklogTable` asks `useAccessCheck()` for `workItemWrite`, `linkWrite`,
`backlogReorder` (the last from `boardsPermissions`; the first two through the
`workitems` barrel). Derived once: `canAdd = workItemWrite && linkWrite`;
`rowActions(row) = (top-level && backlogReorder) || linkWrite`;
`rowDraggable(row) = linkWrite || (top-level && backlogReorder)`.

| File | Change |
|---|---|
| `components/BacklogRow.tsx` | props `canAdd`, `showActions`, `draggable`: no «+» when `!canAdd` (it already draws none for empty `addEntries`), no «Ещё действия: <id>» when `!showActions`, no grip and `draggable={false}` when `!draggable`. |
| `components/BacklogTable.tsx` | passes the three per row; the «⋮» menu draws the move group on `backlogReorder` (top-level rows only) and the parent items on `linkWrite`, the `Divider` only when both; `placementAt`/`dragOver`/`drop` allow row targets only with `linkWrite` and the drop line only with `backlogReorder` and for a top-level dragged row; when `canAdd` turns false while an add row is open it is closed, its title dropped, and focus goes to the main region (P-10). |
| `pages/BacklogPage.tsx` | the head's «Новый рабочий элемент» and the empty-state action only with `workItemWrite`; the reader's empty hint. |
| `components/BoardColumns.tsx`, `pages/BoardPage.tsx` | «Новый элемент», the card menu and card dragging only with `workItemWrite`. |

Tests (`BacklogPage.gating.test.tsx`, `BoardPage.roles.test.tsx`): the four bundles of
design § 12, each listing every code it holds (reader; write + link without reorder;
write + reorder without link; link alone) — what is drawn, which menu items, which
rows draggable, which drop targets; a refused drop sends nothing; the add row closing
when the code goes; the reader keeps the header buttons, chevrons, title links, team
and level drop-downs and «Показать канбан». Existing feat-027 tests run under the
administrator bundle.

### 3.6 The «Исполнитель» picker (US-5, design § 7)

No structural change: the candidates it reads are already members only (server), the
current assignee stays among its options, «Не назначен» clears, and
`workitems.workitem.assignee_unavailable` already lands on the field. Changes:
`WorkItemAssignee` (and the board and backlog row types that reuse it) gain
`isMember?: boolean` (contract § 3.3, typed and not shown — the marks are feat-017's);
`testApi` of the slice answers it. Tests: an assignee who left stays as the field's
value and saving another field sends no assignee; the refusal helper keeps the draft;
a reader gets no picker (the read view); a member turned reader gets the search's
failed line and then the read view.

### 3.7 Error-code placement (design § 9)

`lib/membershipRefusals.ts`: `placeMembershipRefusal(error, surface)` for the surfaces
`add`, `role`, `remove`, `teamAdd`, `teamRemove`, returning `{ field?, alert?, reread }`
by code only (frontend.md "Named values"), text always the server's (`apiErrorText`):

| Code | add | role | remove | teamAdd | teamRemove |
|---|---|---|---|---|---|
| `membershipAlreadyMember` | helper of «Пользователь»; members re-read | — | — | — | — |
| `membershipUserUnavailable` | helper of «Пользователь» | — | — | — | — |
| `membershipLastAdmin` | — | helper of «Роль» | `Alert` | — | — |
| `membershipNotFound` | — | `Alert`; members re-read | `Alert`; members re-read | — | — |
| `teamMemberExists` | — | — | — | helper of «Участник организации»; table re-read | — |
| `teamMemberNotInOrganization` | — | — | — | helper of «Участник организации» | — |
| `teamMemberNotFound` | — | — | — | — | error `Snackbar`; table re-read |
| `validationFailed` | `Alert` | `Alert` | — | `Alert` | — |
| `accessForbidden` | `Alert` (the codes are re-read by the watcher) | same | same | same | `Snackbar` |
| `organization.notFound` | nothing: the page is row 3 and the dialog is gone | | | | |

The user fields keep the chosen user so the administrator can pick another. The
existing placements stay: a project, team or area not found inside a found organization
(mod-002 § 12), `assignee_unavailable` (§ 3.6). `membershipRefusals.test.ts` is
table-driven over this table.

### 3.8 Tests of pass B

`MembersPage.test.tsx` (states; table, order, the marks, «—» and its hidden text;
actions only for writers; focus order), `MembersPage.add.test.tsx` (every search line,
the 2-character rule with no request, the 20-cut, inactive and unknown never offered,
current members offered and refused, both field refusals, notice without the mark),
`MembersPage.role.test.tsx` (preselect, disabled while unchanged, the self-warning, the
last-administrator helper, `not_found`, own step-down hides «Добавить участника» and every
«Действия: <имя>» with no reload and moves focus), `MembersPage.remove.test.tsx` (the
three texts, cancel, `last_admin`, `not_found`, focus rules, own removal navigates to the
list and the two notice cases), `UserLine.test.tsx`, `UserSearchField.test.tsx`,
`TeamMembersSection.test.tsx` (states, the `h3` under the `h2`, the empty frame by role,
add, immediate removal with the busy button, the 404 `Snackbar` and re-read, the
inactive and unknown rows), `membershipRefusals.test.ts`, the page tests of § 3.3–§ 3.6,
and the navigation tests (`AppLayout.access.test.tsx`, created in A-3 and extended
here: the three roles see «Общие», «Проекты», «Участники», «Процесс» in that order;
whose codes — below).
"Whose codes" (design § 12): a link into B while the remembered selection is A
(administrator in A, reader in B) shows no write control on B's page at any frame —
before B's codes are read, after, and while the address's lists fail so the selection
never follows; a switch from A to B shows none of A's controls on B's page at any frame;
a switch from «Участники» of A to B opens «Участники» of B.

### 3.9 Commit steps and lanes

Pass A, each step green on its own (`bash scripts/verify.sh`): A-1 shared pieces (§ 2.1)
and test infrastructure (§ 2.8); A-2 organizations access layer without wiring (§ 2.2);
A-3 gate, wrappers, `routes.tsx`, `AppLayout`, navigation, notice hosts (§ 2.3–§ 2.5);
A-4 default team (§ 2.6); A-5 organization list, comment sweep, conventions (§ 2.7).
Pass B: B-1 «Участники» (§ 3.1, the navigation entry last); B-2 team members (§ 3.2);
B-3 role views, work-item form, backlog, board (§ 3.3–§ 3.5); B-4 picker typing and the
refusal table (§ 3.6–§ 3.7). Lanes after A: **B1** = B-1, B-2 and `TeamPage`'s gating
(everything in `features/organizations` under `membershipApi`, `MembersPage`,
`TeamMembersSection`, `TeamPage`, `testMembership.ts`); **B2** = B-3, B-4 (the other
pages, `features/workitems`, `features/boards`). They share only the rebased `index.ts`
barrels. Every commit message `feat(mod-016): …` or `test(mod-016): …`, ≤ 72
characters, explicit paths.

Done when, per pass (`05-frontend.md` Definition of Done): every state of the
design for the pass's screens is implemented, the component tests are green,
`bash scripts/verify.sh` is green and nothing differs from `api-contract.md`. The E2E
question is answered by D-1 (a check per screen on a stand from the branch), so the
side branch of § 4 is run by QA; `status.md`'s `stage:` is the orchestrator's.

### 3.10 Keyboard, screen reader and narrow widths (design § 10, § 11)

Built with the screens, not after them. «Участники»: Tab order after the shell's stops
is «Добавить участника», then one «Действия: <имя>» per row (header cells and text
are not stops); both tables are named («Участники организации», «Участники команды»);
menus follow the existing `RowActionsMenu` (Enter, Space or ArrowDown opens on the
first item, Escape closes and returns focus to the button); a dialog opens on its
first field, Enter submits through the `form`, the first Escape closes an open
candidate list and the next the dialog, confirmations open on «Отмена»; the candidate
list is a combobox whose hint lines are disabled options the arrows skip; «неактивен»
and «Неизвестный пользователь» are text; notices are `Snackbar`s, refusals `Alert`s,
skeletons `role="status"`. Narrow widths (640 px floor, overlay below 1024 px): both
tables scroll sideways inside their own `Paper` (`TableContainer`), names truncate
with a tooltip, dates and e-mails do not wrap, dialogs are `fullWidth` and the
candidate list takes the dialog's width, the team section's «Добавить в команду» wraps
below its heading when it does not fit. Tests: a tab-order test and an Escape test on
`MembersPage`, accessible-name assertions on every control, structural assertions for
the overflow styles (jsdom measures nothing; the real measurement is E2E 27.10).

## 4. E2E (side branch `mod/016-organization-access-e2e`)

Written in parallel by another agent from this section, not by the pass A/B
developer; it rebases onto `mod/016-organization-access` after pass A (the accessible
names below exist from A and B-1) and merges back before QA. Specs are numbered 27–30
here; if another line has taken a number, the second to merge renumbers.

### 4.1 Support changes (`e2e/support/`)

| File | Change |
|---|---|
| `keycloakAdmin.ts` | `READER_GROUP_PATH` and `createEphemeralReaderUser` go (the group `/Сотрудники` is no longer in the realm); the header comments naming them follow (also `impersonation.ts` line ~146). `createProfiledUser` takes a `lastName`; new `createEphemeralMember(keycloakUrl, realm, label)` → `{ id, username, email, displayName }` with `lastName = <label> <8 hex>` (display name «E2E <label> <8 hex>», unique per run), in no group; new `setUserEnabled(keycloakUrl, realm, id, enabled)` (PUT user, within `manage-users`) for the «неактивен» case. |
| `membersApi.ts` (new) | through `organizationsApi.ts`'s `send`: `accessOf`, `members`, `addMember(request, o, userId, role)`, `changeMemberRole`, `removeMember`, `memberCandidates(request, o, text)`, `teamMembers`, `addTeamMember`, `removeTeamMember`; **`awaitInDirectory(request, o, text)`**: polls `member-candidates` as the administrator until the throwaway user appears (the projection is fed by Keycloak's event listener asynchronously; up to 60 s, fails with the text searched); **`addEphemeralMember(...)`**: create the user, wait for the directory, add with a role, return the user. |
| `refocus.ts` (new) | `refocus(page)`: `page.evaluate(() => window.dispatchEvent(new Event('visibilitychange')))` — TanStack Query's focus manager listens to exactly that event on `window` (query-core 5.102) and refetches stale queries, and headless Chromium reports `visible`. |
| `organizationsApi.ts` | `TeamDto` gains `isMember?: boolean` (the list read, contract § 3.3); `createContext` unchanged (the creator is administrator and the first team's only member). |

### 4.2 New specs

| File | Case | Scenario (stories) |
|---|---|---|
| `27-organization-members.spec.ts` | 27.1 | administrator adds a found user as «Участник»: row with «Добавлен» (the administrator's name and today's date) and «Роль изменена» «—»; the user, already signed in in a second context, sees the organization in the list after a refocus (no re-login) and opens its projects (US-1 add; US-2) |
| | 27.2 | search: one character shows the hint and sends no `member-candidates` request; no match «Пользователи не найдены»; a disabled user is not offered; a current member is offered and refused with «Пользователь уже состоит в организации» on the field (US-1 search, duplicate, inactive) |
| | 27.3 | role change from the row menu to «Читатель»: the row and «Роль изменена»; the user's next write over the API is 403 «Недостаточно прав» (US-1 role) |
| | 27.4 | removal: the confirmation text verbatim, «Отмена» changes nothing, «Удалить» removes the row and the user from the team; their assigned item keeps them; their next API request is 404 and their open page turns into «Организация не найдена» (US-1 removal; US-4 removed member; US-5 leaves) |
| | 27.5 | last administrator: alone, changing own role shows «В организации должен остаться хотя бы один администратор» on «Роль», removing oneself shows it in the confirmation; with a second administrator, stepping down shows the warning, then «Добавить участника» and every «Действия: <имя>» vanish with no reload; removing oneself lands on «Организации» (US-1 last admin, self-removal; US-4 stepping down) |
| | 27.6 | a membership already gone: another administrator removes the user through the API; «Изменить роль» then «Сохранить» shows «Участник не найден.» and the list is read again (US-1) |
| | 27.7 | a member whose account is disabled is listed with «неактивен» after the name, in «Имя» and in the «Добавлен» of rows that name them; keeps role (US-1 inactive) |
| | 27.8 | a user in no group opens `settings/general`, `settings/members` and a project page of an organization: «Организация не найдена» each; the list shows «Вы пока не состоите ни в одной организации» and no «Создать организацию»; «Настройки» opens «Языки» (US-2; US-6; US-8) |
| | 27.9 | the three roles see «Общие», «Проекты», «Участники», «Процесс» in that order; the reader sees the table without «Добавить участника» and any «Действия»; the served bundles (`GET …/access`) hold no write code for a viewer and no admin-only code for a member (US-4; anchors the hand-copied bundles) |
| | 27.10 | keyboard and width (design § 10, § 11): after the header's controls Tab reaches «Добавить участника», then one «Действия: <имя>» per row; Escape closes the candidate list before the dialog; at 640 px the table scrolls inside its own paper and the page does not (US-1; the screen's a11y) |
| `28-team-members.spec.ts` | 28.1 | add an organization member to a team: listed with the e-mail, organization role unchanged; a disabled member and a non-member are not offered («Пользователи не найдены») (US-3) |
| | 28.2 | a second add is refused «Пользователь уже состоит в команде» on the field, the user stays listed once (US-3) |
| | 28.3 | «Исключить из команды» acts at once with its notice; the user still reads the project's backlog and board; a removal already made over the API shows «Участник команды не найден.» and the table is read again (US-3) |
| | 28.4 | empty team: the administrator sees «Добавьте участников организации кнопкой «Добавить в команду».» and the action; a reader sees «Состав команды задаёт администратор организации.» and no action; the heading is `h3` under the `h2` (US-3) |
| | 28.5 | a project created through the API lists its creator as the first team's only member; deleting a team ends its memberships and leaves the other team's (US-3) |
| | 28.6 | default team: a user who is a member of «Платформа» only gets «Платформа» in the team level after choosing the project and «Бэклоги» opens its backlog; of two teams, the first in the project's order; of none, the project's first; a remembered team that exists is kept; leaving a team changes the default at the next read (US-7) |
| `29-role-views.spec.ts` | 29.1 | member: «Новый рабочий элемент» on the list, create and save a work item; none of the administrator controls on «Общие», «Проекты», «Команды», «Области», «Участники»; the empty hints «Проекты создаёт администратор организации.» and «Области задаёт администратор организации.» (US-4) |
| | 29.2 | reader: lists, backlog and board read; no «Новый рабочий элемент», «Новый элемент», «Ещё действия», grip; the work-item form as text (`dt`/`dd`), no «Сохранить», the parent a link; «Рабочие элементы создают участники организации с ролью «Участник» или «Администратор».» on an empty backlog (US-4) |
| | 29.3 | a granted role on refocus: a reader is made a member over the API; `refocus(page)`; «Новый рабочий элемент» appears with no reload and creating works (US-4) |
| | 29.4 | a 403 re-reads the codes: a member is made a reader behind the open page; pressing «Новый рабочий элемент» and submitting shows «Недостаточно прав» in the dialog with the title kept, the primary disabled, and after closing the control is gone; the role-change notice «Ваша роль в организации «…» изменилась: «Читатель».» is shown once (US-4; design § 2.3, § 2.4) |
| | 29.5 | a removed member on the backlog: the next action turns the page into «Организация не найдена»; the switcher falls back; one `GET /api/organizations` and one `…/access` follow the call's 404, and the 404 of that `…/access` starts neither again (US-4; design § 2.2) |
| | 29.6 | by address without the code: a member opening `settings/projects/deactivated` sees «Доступ ограничен» with its sentence; the administrator sees the tab «Деактивированные» (US-4) |
| | 29.7 | switch: administrator in A, reader in B; choosing B in the switcher shows no «Создать проект» on B's «Проекты», with no reload (US-4) |
| | 29.8 | a failed access read: the request is aborted once with `page.route`; the banner «Не удалось загрузить права доступа» with «Повторить»; after the retry the page and the controls show (US-4) |
| `30-assignee-membership.spec.ts` | 30.1 | the picker offers an active member and not a disabled member or a member of another organization only; typing the latter's name shows «Никого не найдено» (US-5) |
| | 30.2 | a user removed after the search is picked: «Сохранить» shows «Этого пользователя нельзя назначить исполнителем.» on «Исполнитель», the draft kept (US-5) |
| | 30.3 | an assignee who leaves stays on the form with no mark, saving the title succeeds, «Не назначен» clears; re-added as a reader they are offered again (US-5) |

US-6's migration seeding and US-8's live-realm command have no browser surface: their
runs (the opt-in live tests and the command on a task stand) are the orchestrator's
(D-19), not an E2E spec.

### 4.3 Existing specs to adapt (D-3, D-7, D-16 OP-9)

By exhaustive search, the only specs that sign in as someone other than the fixture
(`e2e-fixture`, who creates and therefore administers every organization it opens)
are 16, 19.9, 20.4 and 21.6, so no other spec relies on "any signed-in user may do
everything"; the rest change for the reasons given.

| Spec | Change |
|---|---|
| 16 | `createEphemeralReaderUser` → `createEphemeralUserWithoutGroup`; the header comment says a user in no group is the "reader" now (D-7); assertions unchanged (registry visible, no «Добавить язык», `/new` shows «Доступ ограничен») |
| 19.9 | the same helper swap |
| 19.5 | the tab-order list gains «Участники» between «Проекты» and «Процесс» |
| 20.4 | rewritten: a throwaway user added as «Читатель» sees the same definition as the fixture (`innerText` equal); a second user in no group, not added, sees «Организация не найдена» |
| 21.1 | the settings list is «Общие», «Проекты», «Участники», «Процесс» |
| 21.6 | a user in no group: the list shows «Вы пока не состоите ни в одной организации» with «Попросите администратора организации добавить вас.» and no «Создать организацию»; the direct create answering 403 `organizations.organization.create_forbidden` is kept |
| 19 (both blocks), 21, 23 | `deactivateAllOrganizations` now lists only the fixture's own organizations; confirm the "no organization context" block still starts empty on a stand where other users have organizations |
| 22–26 | no change; verify that 22.1 (the fixture as the assignee: an active member) and 23's default team (the fixture is the first team's only member) still hold |

### 4.4 Selector contract (fixed here so both sides agree)

| Element | Role and name |
|---|---|
| members table | `table` «Участники организации» (columns «Имя», «Почта», «Роль», «Добавлен», «Роль изменена») |
| team members table | `table` «Участники команды»; its section heading `heading` level 2 «Участники команды» |
| row menu button | `button` «Действия: <имя без пометки>» (an unknown user: «Действия: Неизвестный пользователь») |
| row menu items | `menuitem` «Изменить роль», «Удалить из организации»; «Исключить из команды» |
| page commands | `button` «Добавить участника», «Добавить в команду»; on an empty team an administrator has two «Добавить в команду» (the section header and the frame, design § 6.3), so a spec scopes to the frame (`.StateFrame-root`) or takes `.first()` |
| dialogs | `dialog` named by its title: «Добавление участника», «Изменение роли», «Удалить участника?», «Удалить себя из организации?», «Добавление в команду» |
| user search | `combobox` «Пользователь» / «Участник организации»; candidates `option` matched by a regular expression on the display name (the name carries its e-mail below); hint lines are disabled `option`s with the spec's text |
| role select | `combobox` «Роль»; `option` «Администратор», «Участник», «Читатель» |
| dialog buttons | «Добавить», «Сохранить», «Удалить», «Отмена», «Закрыть» |
| refusals | field refusals: the helper text of the field (`aria-invalid="true"`); others `alert` in the dialog; success notices are plain Snackbar messages that also carry `role="alert"`, so specs read them by text |
| states | skeleton `status` «Загрузка данных»; banner title «Не удалось загрузить права доступа» with `button` «Повторить»; `heading` level 1 «Организация не найдена»; `heading` level 1 «Доступ ограничен»; empty list `heading` level 2 «Вы пока не состоите ни в одной организации»; empty team `heading` level 3 «В команде пока нет участников» |
| marks | the text «неактивен» next to the name (the row cell's text); «Неизвестный пользователь» |
| read-only form | `main` holds no `textbox`, `combobox` or `spinbutton`; each field is `dt` (label) followed by `dd` (value) — `main.locator('dt', { hasText: 'Исполнитель' })`; «—» carries the hidden text «не указано» |
| role-change notice | text «Ваша роль в организации «<название>» изменилась: «<роль>».» |
| refocus | `refocus(page)` of § 4.1 |

## 5. Risks and the other lines in flight

- **tech-015-ui-kit (shared UI, in flight).** It adds a kit guard that fails on a
  direct MUI import of a part the kit wraps in new non-test sources under `features/`
  and `app/`, with an allow-list cut from a frozen commit, and it moves
  `BacklogIcon`/`ThinIcons` out of `features/boards/components`. Written against main,
  this plan's new files (`UserSearchField`, the tables, the dialogs) use raw MUI inputs
  and buttons, and `BacklogRow`/`BacklogTable` are edited where tech-015 moves
  imports. Whoever merges second rebases: if tech-015 is first, the new files switch to
  the kit parts (each MUI-direct piece is kept in one small file for that); if mod-016
  is first, tech-015 adds them to its baseline. The order is OP-1.
- **feat-029 (work-item card, in flight)** rewrites parts of `WorkItemFormPage` and the
  workitems slice; `WorkItemReadView` and the form's `canWrite` switch are the
  conflict. Same rule; OP-1.
- **feat-028 (teams to the organization, queued).** `TeamMembersSection` and the team
  address are isolated (§ 3.2); the default team uses `defaultEntry`, which does not
  care where the teams come from; the first-team rule of the project creation lapses
  with the first team (D-2), nothing in the panel.
- **Existing tests that assume the shell reads nothing.** `onUnhandledRequest: 'error'`
  and the synchronous first frame: P-12 (default handler, primed cache) covers
  `AppLayout.*`, `routes.*`, `ContextSwitcher*` and the page tests; the first visit to
  an organization's page now shows a skeleton frame, so `getBy*` right after a
  navigation in a real-route test becomes `findBy*`. The list of files expected to
  change: `AppLayout.{navigation,organizations,process}.test.tsx` (the strings
  «Организаций пока нет» and «Право на создание организаций выдаёт администратор», and
  the entries), `OrganizationListPage.test.tsx`, `routes.*.test.tsx`,
  `useOrganizationsContextSource*.test.tsx`, and every page test that used the
  allow-everything `renderPage`.
- **React Router `useParams` in a layout route.** Verified against `matchRoutes` in
  this worktree (7.18); pinned by a test so a router upgrade is noticed.
- **StrictMode** double-runs effects: the watcher's subscription and the focus
  rescue are idempotent and tested under `<StrictMode>` (as `renderPage` supports).
- **jsdom focus.** `document.activeElement` falls to `body` when a focused element is
  removed, which is what the rescue tests; refocus is `focusManager.setFocused`
  (precedent in `BacklogPage.*.test.tsx`).
- **Churn on every access event.** `AppLayout` re-renders when a served answer
  changes; the answers change on a switch, a refocus, a role change and a 403/404 —
  rare. The check reads the cache, so no consumer subscribes to the queries.
- **Contract against the real API.** MSW follows the contract; the real routes were
  read (`MembershipEndpoints.cs`: the ten routes of contract § 1–§ 2) and match.
  The first mismatch found while implementing is a blocker for the orchestrator, not a
  patch in the panel; the E2E run is the arbiter.
- **`ApiError.path`** is read by the watcher only; tests that build an `ApiError` by
  hand carry none and are treated as unscoped (unit-tested).

## 6. What the pass A/B developer must not touch

`src/frontend/e2e/` (the side branch's, § 4); `src/frontend/src/app/theme`
(restyle only through it, and not in this task); `src/backend/**`, `contracts/**`,
`deploy/**`, `scripts/**`; `api-contract.md` (a mismatch goes to the orchestrator with
a note in `status.md`), `spec.md`, the stories and `design.md` (a design gap is an open
point, not an edit); `status.md` outside lines appended under `## Plan` and `## Log`;
`review.md` and `qa.md` (other sessions); the internals of `features/dictionaries`,
`features/home`, `features/process` beyond the comment lines of § 2.7; the
`AssigneePicker`'s code. Shared-UI edits are only those of § 2.1.

## 7. Open points for the orchestrator

- **OP-1 Merge order against tech-015 and feat-029** (§ 5). Both touch the files this
  task edits; the plan assumes whoever is second rebases and that the kit guard's
  baseline or the new files absorb the difference. Deciding the order now saves a
  conflict-heavy rebase.
- **OP-2 Shared edits beyond D-17's list.** `ApiError.path`, `RequiresOrganizationCode`
  with a line in each shared dialog, `shared/lib/noticeCorner.ts`, `shared/lib/mainRegion.ts`,
  `TreeRegistry.showActions`, `RowActionsMenu.busy` (a slice component). Each is small
  and listed in § 2.1 and § 3.2; D-17 names the rest. Please confirm or name a smaller
  alternative.
- **OP-3 Two deliberate duplications** (P-7): `UserSearchField` beside `AssigneePicker`
  and a copy of `useDebouncedValue`. A debt for tech-015's kit (`PersonField`).
- **OP-4 Row 1's skeleton for process, board, backlog and the work-item form** is the
  shared one of the closest kind (P-13); the page's own skeleton follows when it mounts.
  Design § 2.1 names only the two shared kinds.
- **OP-5 E2E numbering** 27–30 is provisional (§ 4).
- **OP-6 The role-change notice waits for the organization's name** when the
  switcher's list does not hold it yet (P-11); design § 2.4 does not say.
- **OP-7 Hand-copied role bundles** in `src/test/organizationRoles.ts` (§ 2.8); there
  is no machine-readable bundle in `contracts/` to compare against. E2E 27.9 is the
  only check; a contract file would let a unit test own it.
