# Plan — frontend (mod-001-process-seed)

Stage 6a (L task, deep pass) per
[05-frontend.md](../../ai-workflow/stages/05-frontend.md). No product code is
written in this pass. Inputs, read at the branch tip 7cafd51, whose base holds
mod-015 (c48b565): [status.md](status.md) (C-1..C-3, D-4..D-14),
[spec.md](spec.md), [US-1](stories/US-1-process-seeded-at-organization-creation.md),
[US-2](stories/US-2-read-only-process-definition.md), [design.md](design.md)
and its prototype, [api-contract.md](api-contract.md),
[frontend.md](../../conventions/frontend.md),
[ADR-0024](../../adr/0024-app-shell-and-visual-language.md), mod-015's
[design.md](../mod-015-app-shell-design-system/design.md) (§ 2.3, § 3.4, § 4,
§ 5.5, § 5.7, § 7, § 15) and the panel as this branch has it: `src/app`
(`routes`, `AppLayout`, `navigation/*`, `shell/*`, `theme/*`), `shared/ui`,
`shared/lib`, `shared/api`, `features/dictionaries`, their tests, `src/test`
and `e2e/`.

**Which layout.** mod-015 is in the `main` this branch is based on, so this
task merges second and builds **layout B only** (D-5, D-6, BR-M2-12,
mod-015 D-12, D-16, BR-15): the screen in the shell, the appearance token and
the category colour through `app/theme`, the canon frames, and the «Процесс»
entry in the navigation registry. Layout A (the token as plain text, the
«Оформление» fact, the page-local not-found frame) is not built.

**Starting point.** The shell already gives this task everything it reads:
`theme.workTracking.typeAppearance(token)` and `categoryColour(code)`,
`layout.categoryDot`, `PageHeader`, `StateFrame` with
`NotFound level="organization"` («Организация не найдена», the hint and «На
главную»), `ErrorBanner` with its optional `title` (built for this screen,
mod-015 design § 7.1), the page chrome through which a header registers and the
shell derives the trail, and `useWorkContext()`. `app/navigation/model.ts`
reserves «Процесс» in a comment and registers nothing for it. There is no
`features/process`, no route under `/organizations/…`, and no error-code
constant file in `shared/api`.

## 1. Shape of the change

- **New slice: `src/frontend/src/features/process/`**, named after the module
  and the backend's `Features/Process` (design § Hand-offs). Its `index.ts` is
  its only public entry.
- **One route:** `/organizations/:organizationId/settings/process` inside the
  `AppLayout` group of `app/routes.tsx`, before the `*` not-found route.
- **Registries extended, three:**
  1. the router, `app/routes.tsx` (the route above);
  2. the navigation registry, `app/navigation/model.ts` (the «Процесс» entry,
     § 6.2);
  3. a new error-code registry, `shared/api/errorCodes.ts`, holding
     `organizations.organization.not_found` (frontend.md "Named values";
     api-contract § 3).
  The theme is not extended: every token and lookup the screen draws is
  already in `app/theme` (mod-015 § 2.3).
- **Server state:** one TanStack Query read, key
  `['process', 'definition', organizationId]` (design § Hand-offs),
  `staleTime: Infinity`, since an MVP process never changes (design § States,
  Content). `retry: false` and no refetch on focus are the client's global
  defaults (`App.tsx`).
- **Client state:** one ref, the "a retry was asked for" intent that drives
  focus after a retry (§ 4.2). No global store, no copy of server data.
- **Dependencies:** none added. Every MUI component and icon named below is
  in `@mui/material` and `@mui/icons-material` as installed.
- **Dependency direction:** `app/` imports the slice through its barrel;
  the slice imports `shared/` only and reads theme values through
  `useTheme()` and `sx` callbacks, never `app/` (frontend.md § MUI).
- **No writes.** The slice sends one `GET` and nothing else (BR-M2-13).

## 2. Files

### 2.1 `features/process/` (new)

| File | Holds |
|---|---|
| `index.ts` | Exports `ProcessDefinitionPage`, `PROCESS_DEFINITION_ROUTE`, `processDefinitionPath`, `processPermissions`: the page for the router, and what the navigation model needs (as the dictionaries barrel does, mod-015 plan D-20). |
| `paths.ts` | `PROCESS_DEFINITION_ROUTE = '/organizations/:organizationId/settings/process'`; `processDefinitionPath(organizationId)` builds it with `encodeURIComponent`. |
| `permissions.ts` | `processPermissions = { definitionRead: 'process.definition.read' } as const` (api-contract § 3). The navigation entry reads it; the page does not (§ 6.4). |
| `api/processApi.ts` | The DTO types of api-contract § 1.2 and `getProcessDefinition(organizationId)` (§ 3). |
| `lib/readFailure.ts` | `isOrganizationNotFound(error)`: `true` only for an `ApiError` whose `errorCode` is the not-found code (§ 3.3). |
| `hooks/useProcessDefinition.ts` | The query of § 1. |
| `hooks/useRetryFocus.ts` | Focus after a retry (§ 4.2). |
| `components/ProcessTypeCard.tsx` | One type: heading with the type mark, facts, the states table (§ 5). |
| `components/TypeMark.tsx` | The token's icon in its colour, `aria-hidden` (design point 5). |
| `components/CategoryCell.tsx` | The category marker and the label (design point 6; mod-015 § 2.3). |
| `components/ProcessDefinitionSkeleton.tsx` | The loading skeleton of five cards (§ 5.4). |
| `pages/ProcessDefinitionPage.tsx` | The page and its four states (§ 4). |
| `testFixtures.ts` | The full answer of api-contract § 1.4 (five types, 24 states, four levels, synthetic ids) and the read's URL for MSW handlers. |

`TypeMark` and `CategoryCell` stay in the slice, as design § Hand-offs puts
the two seams. The marker is a catalogue pattern (mod-015 § 2.3, § 10.6) that
mod-003's list and mod-006's board will draw too; the first of them to need it
lifts both into `shared/ui` under frontend.md's "two slices need it" rule. This
task does not move them early.

### 2.2 `app/`

| File | Change |
|---|---|
| `routes.tsx` | One `Route` for `PROCESS_DEFINITION_ROUTE` with `ProcessDefinitionPage`, inside the `AppLayout` group, before `*`. No gate on the route (§ 6.4). |
| `navigation/model.ts` | The «Процесс» entry in the «Организация» section (§ 6.2); the doc comment's "registered by whichever merges second" sentence becomes a statement that it is registered here. |

### 2.3 `shared/`

| File | Change |
|---|---|
| `api/errorCodes.ts` (new) | `organizationErrorCodes = { notFound: 'organizations.organization.not_found' } as const`, one to one with api-contract § 1.3. The dictionaries slice's inline literals are not moved (out of scope). |
| `ui/PageHeader.tsx`, `ui/ErrorBanner.tsx` | Only under P-2 (a) (§ 11): an optional `titleRef` on `PageHeader` (the `h1` takes the ref and `tabIndex={-1}` only when it is given) and an optional `retryRef` on `ErrorBanner`'s «Повторить». Both additive; every current caller renders as before. |

### 2.4 Tests

New and changed test files are listed per scenario in § 8. Briefly:
`features/process/**/*.test.ts(x)` (unit, component and page tests),
`shared/api/errorCodes.test.ts`, `app/routes.process.test.tsx`,
`app/AppLayout.process.test.tsx`, an edit to
`app/navigation/visibility.test.ts`, and, under P-2 (a), one test each in
`shared/ui/PageHeader.test.tsx` and `ErrorBanner.test.tsx`.

### 2.5 E2E

`e2e/specs/20-process-definition.spec.ts` (new) and one helper in
`e2e/support/keycloakAdmin.ts` (§ 9).

### 2.6 Docs

This file and a `[dev]` Log line. No contract, spec or convention change.
design.md's reconciliation deltas (§ 7) are the designer's to fold in, if the
orchestrator wants them folded; the code follows the reconciled values.

## 3. The API layer (api-contract § 1)

### 3.1 Types

`ProcessDefinitionResponse { id; backlogLevels: BacklogLevel[];
workItemTypes: WorkItemType[] }`, `BacklogLevel { id; name; rank; kind }`,
`WorkItemType { id; key; name; appearance; backlogLevel: { id; name };
states: State[] }`, `State { id; name; order; isInitial; category: { code;
label } }`, with the closed sets as unions:
`kind: 'portfolio' | 'requirement' | 'task'`,
`key: 'EPIC' | 'FEAT' | 'US' | 'BUG' | 'TASK'`,
`appearance: 'epic' | 'feature' | 'story' | 'task' | 'bug'`,
`CategoryCode = 'Proposed' | 'InProgress' | 'Resolved' | 'Completed' |
'Removed'` (frontend.md "Closed sets"). The theme's lookups take a `string`,
so an unexpected value at run time still renders their fallback.

### 3.2 The request

`getProcessDefinition(organizationId)` calls
`apiFetch<ProcessDefinitionResponse>(`/api/organizations/${encodeURIComponent(organizationId)}/process`)`.
The id comes from `useParams()` as the router decoded it, is never parsed or
validated (DD-4), and is re-encoded for the path, so `a%2Fb` in the address
reaches the API as `a%2Fb`, not as two segments.

A `200` whose `workItemTypes` is missing, not an array or empty throws
`EmptyProcessDefinitionError`: a process always has five types (BR-M2-8,
api-contract § 1.4), so zero is a broken answer and the page treats it as a
failed read (DD-5). Its `name` is not `TypeError`, so `apiErrorText` gives the
panel's existing «Непредвиденная ошибка. Повторите попытку.» and no new copy
is written (design T-20). A body that is not JSON fails inside `apiFetch` and
lands on the same text.

### 3.3 The one branch

`isOrganizationNotFound(error)` is `error instanceof ApiError &&
error.errorCode === organizationErrorCodes.notFound`. Never the status alone,
never the text (design § States; api-contract § 1.3). Every other failure is
a failed read: `5xx`, a `404` with another code or no body, `401` after the
client's own renewal, `403`, `503`, a network error, an unreadable or empty
answer.

## 4. The page and its states (design § States)

### 4.1 State table

`ProcessDefinitionPage` reads `organizationId` from the route and runs the
query. The page header stays mounted across loading, content and a failed
read, so the title is never re-created and the tab never flickers.

| State | Trigger | Rendered | Not rendered | Tab | Shell effect |
|---|---|---|---|---|---|
| Loading | first open, a new `organizationId`, a retry in flight (`isPending`, or `isError && isFetching`) | `PageHeader` «Процесс» (T-01), description T-02 and T-03, `ProcessDefinitionSkeleton` | any type, state or level | «Процесс — Mustask» | header registered; trail per § 6.3 |
| Content | success with types | header, description, five `ProcessTypeCard`s in the order received (DD-2 is the server's, api-contract § 1.4) | any control | as above | as above |
| Read failed | any error that is not the not-found code, or `EmptyProcessDefinitionError` | header, description, `ErrorBanner` with `title` «Не удалось загрузить процесс» (T-19), `apiErrorText` (T-20) and «Повторить» (T-21) | «Организация не найдена», any part of a process | as above | as above |
| Organization not found | `isOrganizationNotFound(error)` | `<NotFound level="organization" />` alone: `h1` «Организация не найдена» (T-22), hint (T-23), «На главную» to `/home` (T-24) | header, description, any type, the server's `detail` | «Организация не найдена — Mustask» (T-27) | the frame registers not-found: no entry marked current, no «Меню настроек», no trail (mod-015 BR-13) |

There is no empty, empty-after-filter or access-denied state (DD-5); a `403`
is a failed read. The page has no command, so `PageHeader` gets neither a
primary action nor commands and keeps its bar's height (mod-015 § 6.2). It
passes no `ancestors`: the shell derives the whole trail (§ 6.3).

### 4.2 Focus and announcements

- The skeleton is `role="status"` named «Загрузка данных» (T-18); the alert
  announces itself (`Alert`'s `role="alert"`); the not-found frame is not an
  alert (mod-015 § 12.5).
- After «Повторить» the button unmounts under the skeleton. `useRetryFocus`
  remembers that a retry was asked for and, once the fetch settles, moves
  focus to the page title on success, or to the new alert's «Повторить» on
  another failure (design § Focus and announcements). It does nothing on a
  first load. The two targets come from P-2 (§ 11).

## 5. Components (MUI)

### 5.1 Grid and card

- `Grid container spacing={2}`, items `Grid size={{ xs: 12, lg: 6 }}`. The
  theme's `lg` is 1280 px (mod-015 § 2.5, § 15), so one column below 1280.
- `Paper component="section" aria-labelledby={headingId}`, padding in spacing
  units, no `variant` or `elevation` from the page (design point 11).
- Heading: `Typography variant="h2" component="h2"`, holding `TypeMark` then
  the type name. Layout B's level is `h2` (design point 7). The variant is the
  panel's `h2` scale (16/24, 500), not design.md's MUI `h6`, which the panel's
  type scale does not define (mod-015 § 2.4).
- Facts: `Stack component="dl" direction="row" useFlexGap` wrapping, each pair
  a `Box` with `Typography component="dt"` (`text.secondary`) and
  `Typography component="dd"` (margin 0): «Ключ» (T-04) with the key,
  «Уровень бэклога» (T-05) with `backlogLevel.name`. No «Оформление» (T-06 is
  layout A's).

### 5.2 States table

- `TableContainer` (its own horizontal scroll if a long name widens it), `Table
  size="small" aria-label="Статусы типа «<name>»"` (T-07).
- Header cells `scope="col"`: «Порядок» (T-08), «Статус» (T-09), «Категория
  статуса» (T-10).
- Rows in the order received: `state.order`; the name as `TableCell
  component="th" scope="row"` with, on the row whose `isInitial` is `true`, a
  `Chip variant="outlined"` «Начальный» (T-11) that has no `onClick`,
  `clickable` or `onDelete`, so it never takes focus; then `CategoryCell`.
- No sorting, paging or row action.

### 5.3 Type mark and category cell

- `TypeMark`: `const { icon: Icon, colour } =
  theme.workTracking.typeAppearance(appearance)`, rendered small with `sx`
  colour, `aria-hidden`. An unknown token renders the theme's neutral fallback
  and nothing throws (mod-015 § 2.3). The token's name is never text.
- `CategoryCell`: a marker, then `category.label` as the server sent it. The
  marker is a `Box` of `layout.categoryDot` with `borderRadius: '50%'`,
  `aria-hidden`: filled with `categoryColour(code)` for every code, an unknown
  one included (the theme answers `text.secondary`), and a ring for `Removed`
  (`'Removed'` is a named value of the slice), its border width
  `theme.spacing(0.25)`, the 2 px of mod-015 § 2.3 in the spacing unit. A
  circle box, not the `FiberManualRecord` / `RadioButtonUnchecked` glyphs
  design.md named (mod-015 § 15).
- The client holds no code-to-label map: api-contract § 1.2 carries the label
  (design R-26, § What the screen shows), so T-12..T-16 arrive from the server
  and T-17 has no use. No category code is ever rendered.

### 5.4 Skeleton

`ProcessDefinitionSkeleton`: one `Box role="status" aria-label="Загрузка
данных"` around the same grid, five `Paper`s, each a heading-line and a
facts-line `Skeleton`, then a `Table` with the real headers T-08..T-10 and
four rows of `Skeleton variant="rounded"`, every size in spacing units. One
status region, not five `RegistrySkeleton`s (each of which is its own status).
The pulse honours reduced motion through the theme's `motion.reducedMotion:
'system'` (mod-015); pass 2 confirms that against the installed `Skeleton`
and, if it does not hold, the skeleton passes `animation={false}` under
`useMediaQuery('(prefers-reduced-motion: reduce)')` with a test under
`setReducedMotion`.

### 5.5 Texts

Every string comes from design § Interface texts, copied verbatim:

| Ids | Where in code |
|---|---|
| T-01 | `PageHeader title` |
| T-02, T-03 | two `Typography variant="body2" color="text.secondary"` paragraphs under the header (PC-2 kept) |
| T-04, T-05, T-07..T-11 | `ProcessTypeCard` |
| T-18 | `ProcessDefinitionSkeleton`'s `aria-label` (the shared canon's words) |
| T-19 | `ErrorBanner title` |
| T-20, T-21 | `ErrorBanner` itself (`apiErrorText`, «Повторить») |
| T-22..T-24 | `NotFound level="organization"` (the shared frame's words, mod-015 § 7.2) |
| T-25 | the navigation entry's `label` |
| T-26 | derived by the shell (§ 6.3), not written by this task |
| T-27 | `PageHeader` and `NotFound` set the tab |
| T-06, T-12..T-17 | not held (§ 5.1, § 5.3) |

## 6. Shell integration

### 6.1 Route

`<Route path={PROCESS_DEFINITION_ROUTE} element={<ProcessDefinitionPage />} />`
under `RequireAuth` and `AppLayout`, so a visitor who is not signed in gets the
auth screen and the page never mounts (BR-M2-10).

### 6.2 Navigation entry

In `navigationModel.settings`, section `organization` («Организация»):

```ts
{
  id: 'process',
  label: 'Процесс',
  scope: 'organization',
  permission: processPermissions.definitionRead,
  landing: (selection) =>
    selection.organizationId ? processDefinitionPath(selection.organizationId) : null,
}
```

It is that section's first entry today and stays third once «Общие» and
«Участники» exist (mod-015 § 4.1). Settings entries carry no icon. Visibility
is the shell's: the code passes for any signed-in user (interim BR-8,
mod-015 D-18), and the entry shows only when the context is real, so on the
placeholder source it is hidden for every user (BR-M2-12). Once a real context
exists, «Настройки» opens it first, by model order (BR-19).

### 6.3 What the shell shows on this page

| When | Settings navigation | Main navigation | Trail |
|---|---|---|---|
| Before mod-002 (placeholder source, entry hidden), any id other than the placeholder organization's | none: the entry's landing in the placeholder selection is `/organizations/org-1/settings/process`, so the shell does not take this address for a settings page | kept choice, no entry current | none (the shell derives it from visible entries only) |
| Once the context is real and the address's organization is selected (mod-002 makes context addresses set the selection, mod-015 § 5.7) | «Меню настроек» with «Организация» → «Процесс» `aria-current="page"` | rail, «Настройки» `aria-current="true"` | «Настройки / Организация», plain text (mod-015 § 3.4, § 15) |
| Not found, at any time | none | no entry current | none |

The first row is what the shell as merged does, and P-1 (§ 11) is decided as
(a) by status.md D-15 (2026-10-04): the shell stays as merged. Before mod-002
this address shows no «Меню настроек», no trail and nothing marked current (a
«Языки» reader included); the trail «Настройки / Организация» shows once
mod-002 makes addresses set the selection (the second row). design.md point 2
first expected «Меню настроек» on this address before mod-002 whenever another
settings entry is visible. That would need a change to the shell's
settings-page rule, which BR-M2-12 does not allow this task, so design.md was
narrowed to these rows by a designer's delta instead (option (b) is not
taken). The second row is checked through the shell's context-source seam with
a stub source (§ 8.1), the way mod-015's tests check an organization-scoped
entry.

### 6.4 Permission

The page calls neither `useAuth().can(...)` nor `useAccessCheck()` (DD-6). The
code is in no token (D-8), so `can` would refuse everyone; and the screen has no
access-denied state (DD-5): after mod-016 a non-member is answered by the API's
not-found code, which the page already renders, so a client-side refusal would
be a second, contradicting answer. The code is declared by the slice's
`permissions.ts` and read by the navigation entry only.

## 7. Reconciliation with mod-015

design.md § "Adopting mod-015" is this stage's checklist (PC-3). Each row,
with mod-015's answer (its design § 15 and the code as merged):

| design.md derived | mod-015's answer | Built here |
|---|---|---|
| «Процесс» a settings entry; section, order, icon | third entry of «Организация»; no icon | § 6.2 |
| Trail «Настройки», one segment (T-26) | «Настройки / Организация», derived by the shell | § 6.3; the page passes no ancestors |
| Shared frames; not-found icon; error title slot | `StateFrame`, `SearchOff`, heading `h1`; `ErrorBanner title` | § 4.1 |
| Header with no command keeps its height | yes | § 4.1 |
| Token lookup by name; category colour by code; shape | `typeAppearance`, `categoryColour`; `Removed` a ring, every other code a dot, an 8 px circle box | § 5.3 |
| One column below `lg` | `lg` is 1280 px; two cards fit at 1280 beside the settings navigation | § 5.1 |
| The switcher keeps its placeholder selection before mod-002 | not answered in § 15; the shell resolves nothing from the address (mod-015 § 5.7: mod-002 does) | no «Организация не найдена» of the shell's own; the page's not-found comes only from the API |
| On a settings address, «Меню настроек» follows BR-19 with the entry hidden | not answered in § 15; the merged rule keys a settings page on the entry's landing in the current selection | § 6.3 first row; P-1 decided (a), D-15: before mod-002 no «Меню настроек», no trail, nothing current |

Further points where the code departs from design.md's wording, all settled by
the documents that came after it:

- **No category map, no T-17** (§ 5.3): the contract carries the label
  (design § What the screen shows anticipated this).
- **An unknown category code draws a filled dot in `text.secondary`**, where
  design.md said "no dot": mod-015 § 2.3, the catalogue that settles the
  marker (PC-3), says an unknown code is a filled dot. The label beside it is
  the server's, so colour is still never alone.
- **Card headings use the `h2` variant**, not `h6` (§ 5.1).
- **Reduced motion of the skeleton is the theme's**, not a page prop, unless
  pass 2 finds otherwise (§ 5.4).

## 8. Tests per story scenario

"Page test" means Testing Library + MSW, mocked per api-contract § 1, under
the app theme (the Vitest alias of mod-015). Page tests render the page
outside the shell at its route, where the access check fails closed: content
appearing there is itself the proof that the page makes no check (§ 6.4).
Shell tests mount `AppLayout` through `src/test/renderShell.tsx` or the real
`AppRoutes`.

### 8.1 US-2

| Scenario | Test file | What it asserts |
|---|---|---|
| A seeded process is shown in full | `pages/ProcessDefinitionPage.test.tsx` | Five `section`s named by their `h2`s in the order received: «Эпик», «Фича», «Пользовательская история», «Баг», «Задача». Per card: the key, the level, the table «Статусы типа «…»» with the rows in order and «Порядок» 1..n, «Начальный» on the `isInitial` row only, the label per row. The type icon is the theme's for the token (the icon's `data-testid`, e.g. `LandscapeIcon` for `epic`), and no token name is text. Each marker's computed colour equals `categoryColour(code)`; «Удалена»'s is a ring. |
|  | `pages/ProcessDefinitionPage.states.test.tsx` | The canon frames: loading is one status «Загрузка данных» with the real headers and no type name; a failed read is `ErrorBanner` with T-19 and «Повторить»; not found is the shared frame (`StateFrame-root`) with its `h1`. |
|  | `app/routes.process.test.tsx` | In the shell, through the real routes: `banner` and `main` present, exactly one `h1` «Процесс», tab «Процесс — Mustask». The colour-literal check (`src/test/colourLiterals.test.ts`) covers the new slice unchanged. |
| The Development stand shows a demonstration process | — (backend, plan-backend § 8); E2E 20.1 | The panel opens the demonstration id by address and shows the full definition. |
| An unmigrated Development stand starts without the demonstration process | `pages/ProcessDefinitionPage.states.test.tsx` (frontend half) | A `500` `http.internal_error`, which the read answers on an unmigrated schema, is a failed read, never not found. The rest is backend. |
| A failed demonstration seed does not stop the stand | — (backend) | On the screen no process means the not-found answer, covered below. |
| Every signed-in user sees the same definition | `app/routes.process.test.tsx` | The page under `authValue` with every code and with none: `main`'s text is identical. E2E 20.4 with a real token. |
|  | `pages/ProcessDefinitionPage.test.tsx` | Outside the shell (access check failing closed) the full content renders. |
| Categories are shown by their Russian labels | `pages/ProcessDefinitionPage.test.tsx` | «Задача»: «Новая» → «Новые», «Активна» → «В работе», «Закрыта» → «Завершённые», «Удалена» → «Удалённые». None of `Proposed`, `InProgress`, `Resolved`, `Completed`, `Removed` is in the document. A fixture whose label differs from the usual word shows the fixture's word: the client holds no map. |
| One state label belongs to two categories | `pages/ProcessDefinitionPage.test.tsx` | «Разрешена» reads «В работе» in «Пользовательская история» and «Решённые» in «Баг», and the two markers differ in colour. |
| An identifier with no process is not found | `pages/ProcessDefinitionPage.states.test.tsx` | A `404` carrying `organizations.organization.not_found`: `h1` «Организация не найдена», the hint, «На главную» to `/home`; no «Процесс» heading, no description, no table, no type name; tab «Организация не найдена — Mustask». A malformed id (`not-a-uuid`) and an encoded one (`a%2Fb`) are sent as they are, re-encoded (the request listener reads the path), and answer the same frame. |
|  | `app/routes.process.test.tsx` | In the shell: no «Меню настроек», no `aria-current` in any navigation, no trail; «На главную» opens «Главная». |
| The not-found code is registered with its texts | `shared/api/errorCodes.test.ts` (frontend half) | Every constant in `organizationErrorCodes` is a key of `contracts/errors/organizations.json`. The texts and the generated reference are backend tests. |
| A failed read is not taken for a missing organization | `pages/ProcessDefinitionPage.states.test.tsx` | An outline over `500` `http.internal_error`, `404` `http.not_found`, `404` with no body, `403`, `503`, a network error (`HttpResponse.error()`), a `200` that is not JSON, and a `200` with no types: each shows the alert with T-19 and «Повторить», neither «Организация не найдена» nor any type. A retry that succeeds shows the skeleton while it runs, then the content, with focus on the `h1`; a retry that fails again brings the alert back with focus on «Повторить». |
| A visitor who is not signed in sees nothing | `app/routes.process.test.tsx` | Under an anonymous session the auth screen renders, no «Процесс» heading, and no request reaches `/api/organizations/…`. The `401` without a token is backend; E2E 20.5 in a browser. |
| The read's code is registered, and nothing grants it yet | `app/navigation/visibility.test.ts` (frontend half) | The entry's `permission` is exactly `process.definition.read`. The realm checks are backend. |
| No visible entry or link leads to the screen before mod-002 | `app/navigation/visibility.test.ts` | The model holds «Процесс» under «Организация», scope `organization`, code `process.definition.read`, landing `/organizations/<id>/settings/process`. It is hidden on the placeholder context for any code and for none; visible with a real context; and «Настройки» then opens it first. |
|  | `app/AppLayout.process.test.tsx` | On «Главная», «Языки» and the process address with the placeholder source: no link whose `href` contains `/settings/process`, no text «Процесс» in any navigation. With a stub real source selecting the demonstration id on the process address: «Процесс» in «Меню настроек» with `aria-current="page"`, rail «Настройки» `aria-current="true"`, trail «Настройки / Организация». |
|  | `app/routes.process.test.tsx` | Opening the address with a seeded id shows the process. |
| Nothing on the screen changes the process | `pages/ProcessDefinitionPage.test.tsx` | In content, `main` holds no `button`, `link`, `textbox`, `checkbox`, `combobox` or `menu`; the chip has no `tabindex` and no `button` role; from the title, Tab reaches nothing inside the content. In a failed read the only control is «Повторить», in not found only «На главную». A request listener sees exactly one `GET` to the read and nothing else. |

### 8.2 US-1

No screen part (BR-M2-13): every scenario is backend and maps to plan-backend
§ 8. No frontend test.

### 8.3 Unit tests

| File | Asserts |
|---|---|
| `api/processApi.test.ts` | The path re-encodes the id; the body is returned as is; `EmptyProcessDefinitionError` for no `workItemTypes`, a non-array and an empty array; an `ApiError` passes through with its `errorCode`. |
| `lib/readFailure.test.ts` | `true` for an `ApiError` with the code; `false` for `404` with another code or no body, `500`, a `TypeError`, `EmptyProcessDefinitionError`. |
| `components/ProcessTypeCard.test.tsx` | The `section` is named by its heading; the facts are a `dl` of «Ключ» and «Уровень бэклога» with no «Оформление»; the states table has its name, `scope="col"` headers and a `th scope="row"` per row; the chip is inert; an unknown token renders the theme's fallback icon. |
| `components/CategoryCell.test.tsx` | The label is shown verbatim; the marker is `aria-hidden`, coloured by `categoryColour(code)`, a ring for `Removed`, and a filled `text.secondary` dot for an unknown code. |
| `components/ProcessDefinitionSkeleton.test.tsx` | One status «Загрузка данных», five cards, four placeholder rows each under the real headers, no type name. |
| `shared/ui/PageHeader.test.tsx`, `ErrorBanner.test.tsx` (P-2 (a) only) | With `titleRef` the `h1` is focusable through the ref and has `tabIndex -1`; without it nothing changes. `retryRef` reaches «Повторить». |

### 8.4 Existing tests that change, and only these

| File | Test | Change |
|---|---|---|
| `app/navigation/visibility.test.ts` | "registers only built pages: «Главная» and «Справочники» → «Языки»" | The expected list becomes «Главная», «Процесс», «Языки», and the title names «Процесс». |
| `app/navigation/visibility.test.ts` | "shows only this task's pages" (the three-case outline) | «Процесс» leaves the list of pages that do not exist (it exists now). The real-context case no longer forbids «Организация», whose section now holds a built page; the placeholder cases still do. |

Every other existing test passes unedited. `AppLayout.navigation.test.tsx`'s
"Pages that do not exist yet never appear — on screen" still holds, because
it runs on the placeholder source, where «Процесс» is hidden by D-18. Its
real-source case asserts only its own test entry. After pass 4,
`git diff --stat main -- '*.test.*'` shows edits to `visibility.test.ts` only,
plus `PageHeader.test.tsx` and `ErrorBanner.test.tsx` under P-2 (a), and new
files.

### 8.5 Left to a browser (QA, stage 7)

Two cards side by side at 1280 px and one column below; no sideways page
scroll at 640 px (a long name scrolls inside its table); the computed type and
category colours on the real surfaces; reduced motion of the skeleton; the
visible focus ring on «Повторить» and «На главную»; 200 % zoom. E2E 20.6
covers the first two.

## 9. E2E specs to add (D-12)

E2E is consented for this task (D-12), so the "ask before writing" step of
05-frontend.md § 7 is already answered. The developer writes the spec in
pass 5 and checks that it compiles (`npx playwright test --list`, and
`npm run build`, whose `tsc -b` covers `e2e/`). The run that counts is QA's,
against a cold stand, per 05-frontend.md § 7. Expected values are written out
from api-contract § 1.4, not read back from the panel, as spec 19 does with
its design values.

`e2e/specs/20-process-definition.spec.ts`, on the demonstration id
`d0000000-0000-4000-8000-000000000001` (api-contract § 4, D-4):

| # | Case | Asserts |
|---|---|---|
| 20.1 | Happy path | Sign in as the fixture user with `returnPath` the demonstration address; `h1` «Процесс»; five `h2`s in order; the «Задача» table's four rows and labels; «Разрешена» «В работе» on «Пользовательская история», «Решённые» on «Баг»; the five type icons visible; tab «Процесс — Mustask»; no `button` or `link` in `main`. |
| 20.2 | Not found | A fresh random UUID and `not-a-uuid`: `h1` «Организация не найдена», no table; «На главную» opens `/home`. |
| 20.3 | Failed read, then retry | The read is refused at the network layer (`page.route(…).abort()`), as spec 19.8 injects its fault: the alert with «Не удалось загрузить процесс» and «Повторить», no not-found. The route is released, «Повторить» shows the content, and the `h1` has focus. Nothing on the session flow is stubbed (`e2eSessionGuard.test.ts`). |
| 20.4 | Same definition for a user with no code | A throwaway user in no group (it holds no code at all) sees `main` text identical to the fixture user's (20.1). The user is deleted in `finally` and its absence confirmed, as spec 16 does. |
| 20.5 | Not signed in | A fresh context opens the demonstration address: the browser is sent to sign in, and no request reaches `/api/organizations/…/process` before the session exists. A `GET` of the read without a token answers `401` with no `workItemTypes`. |
| 20.6 | Layout in a browser | At 1280 px the first two cards share a top edge; at 640 px they stack, and the page does not scroll sideways. |

`e2e/support/keycloakAdmin.ts`: the user creation inside
`createEphemeralReaderUser` moves into an internal helper, and a new
`createEphemeralUserWithoutGroup` uses it without the group join.
`createEphemeralReaderUser` keeps its signature and behaviour, so specs 16 and
19 are untouched.

## 10. Order of work: passes that each end green

"Green" means `npm run lint`, `npm test` and `npm run build` in
`src/frontend`, plus `bash scripts/verify.sh`. Every commit builds, with an
explicit path list. Before each commit the diff and its message are checked
for the reference tracker's name (C-1); the search term is never written into
a file. Each pass ends with a `[dev]` Log line naming its commits.

0. **Set-up.** `npm ci` in `src/frontend` (this worktree has no
   `node_modules`). No stand is needed until QA: passes 1–4 run on MSW.
1. **Pass 1: the read, without UI.** `shared/api/errorCodes.ts` and its drift
   test; `features/process/paths.ts`, `permissions.ts`, `api/processApi.ts`,
   `lib/readFailure.ts`, `testFixtures.ts`, their unit tests. No route, no
   page, nothing visible.
2. **Pass 2: the content components.** `TypeMark`, `CategoryCell`,
   `ProcessTypeCard`, `ProcessDefinitionSkeleton` with their component tests
   under the theme. The reduced-motion check of § 5.4 happens here.
3. **Pass 3: the page and its states.** Under P-2 (a) first: the two
   additive `shared/ui` props with their tests, as their own commit.
   Then `hooks/useProcessDefinition.ts`, `hooks/useRetryFocus.ts`,
   `pages/ProcessDefinitionPage.tsx`, `index.ts`, and both page test files.
   The page exists but no route serves it.
4. **Pass 4: into the shell.** The route in `app/routes.tsx`; the entry in
   `app/navigation/model.ts` with the `visibility.test.ts` edits of § 8.4 in
   the same commit; `app/routes.process.test.tsx` and
   `app/AppLayout.process.test.tsx`. The assertions on the demonstration
   address before mod-002 follow P-1's answer.
5. **Pass 5: E2E and closing.** `e2e/specs/20-process-definition.spec.ts` and
   the `keycloakAdmin.ts` helper; `npx playwright test --list`; the § 8.4 diff
   check; a last C-1 search over the whole branch diff and its commit
   messages; hand over to stage 7 (frontend QA, a separate session, which
   runs spec 20 on a cold stand).

## 11. Open points for the orchestrator

- **P-1. The settings navigation and the trail on the process address before
  mod-002.** design.md point 2 says that, with the entry hidden, the address
  is still a settings page, so a user who can see another settings entry (a
  «Языки» reader) gets «Меню настроек» with nothing current and «Настройки»
  current in the rail. The shell as merged decides "settings page" by an
  entry's landing in the current selection (`isSettingsPage`,
  `app/navigation/visibility.ts`), and before mod-002 the selection is the
  placeholder `org-1`, never the address's id. So on the demonstration
  address the page shows no «Меню настроек» and no trail. mod-015's § 15
  answered the other six rows of the reconciliation list but not this one.
  - **(a) recommended:** take the shell's behaviour as it is. Before mod-002
    the page has no settings navigation and no trail on any id but `org-1`;
    once mod-002 makes context addresses set the selection, § 6.3's second
    row applies, and pass 4 tests it through the context seam. design.md's
    point 2 clause and T-26 («Настройки / Организация», shown once the
    context is real) are reconciled by a design delta, if the orchestrator
    wants one. Pro: no shell change, as BR-M2-12 requires; the gap closes by
    itself with mod-002. Con: for the life of the interim, the design's
    sentence does not match the screen.
  - **(b):** change the shell's settings-page rule to match a settings route
    by its pattern, independent of the selection. Pro: matches design.md now.
    Con: a shell change, which BR-M2-12 forbids this task; it would be its own
    line in mod-015's area (it touches the same matching as the queued
    `bug-003-shell-address-case-navigation`), and this task would not wait for
    it.
  - **Decided: (a)**, status.md D-15 (delegated by human 2026-10-01, recorded
    2026-10-04; closes review F1 and QA D-3). The shell stays as merged. Before
    mod-002 the process address has no «Меню настроек», no trail and nothing
    marked current; the trail «Настройки / Организация» shows once mod-002 makes
    addresses set the selection, and § 6.3's second row applies. design.md's
    point 2 clause and T-26 were reconciled by the designer's delta (344c91f,
    prototype 732b959). (b) is not taken: it is a shell change this task may
    not make, and mod-002 removes the need.
- **P-2. Focus after a retry needs two targets inside `shared/ui`.** The
  design's focus rule (§ 4.2) needs the page's `h1` focusable and a handle on
  `ErrorBanner`'s «Повторить»; `PageHeader` exposes no ref and its `h1` has no
  `tabIndex`, and `ErrorBanner` exposes no ref either.
  - **(a) recommended:** an optional `titleRef` on `PageHeader` (the `h1`
    takes it and `tabIndex={-1}` only when it is given) and an optional
    `retryRef` on `ErrorBanner`, each with a test. Additive; every current
    caller renders as before. These are shared patterns, not the shell
    (`app/`) that BR-M2-12 keeps closed, and frontend.md lets a pattern grow
    for its consumers. Every later page with a retry reuses the seam.
  - **(b):** no `shared/ui` change: the page finds the `h1` and the button
    under a wrapper ref and sets `tabindex` on the `h1` imperatively. Pro:
    touches no mod-015 file. Con: DOM reaching into shared components'
    markup, which breaks silently if their markup changes.
  - If nothing is decided before pass 3, the plan goes ahead with (a).

## 12. Stage 6b notes (as built)

Written after the five passes; the sections above are unchanged.

- **P-1 and P-2.** Neither was decided in status.md by pass 3, so both went
  ahead with (a): no shell change, and the two optional props, as their own
  commit. `AppLayout.process.test.tsx` pins § 6.3's first row (the
  placeholder source on the demonstration address: no «Меню настроек», no
  trail, nothing current) and its second row through a stub real source.
  P-1 was decided as (a) afterwards, by status.md D-15 (2026-10-04), so those
  tests stand as written; P-2 stays as the plan went ahead.
- **Reduced motion of the skeleton (§ 5.4).** Confirmed against the
  installed `Skeleton`: MUI reads the theme's `motion.reducedMotion` and
  emits `@media (prefers-reduced-motion: reduce) { animation: none }` on the
  skeleton's own class. No `useMediaQuery`; the skeleton test reads that rule
  from the document's style sheets.
- **Names that differ from § 2.1 and § 3.1, same content:** the state type is
  `StateDefinition` (the backend's word), the API path builder is
  `processDefinitionApiPath`, and the three column headers live in
  `components/stateColumns.ts`, shared by the card and the skeleton.
  `CategoryCell` renders the `TableCell` itself.
- **Tests beyond § 8.** The failed-read outline also covers a `401` after
  the client's own renewal and a `404` whose text alone reads «Организация
  не найдена.»; a first load moves no focus; the not-found frame is checked
  in the shell with a real context too (mod-015 BR-13).
- **E2E.** Spec 20 compiles under `npm run build` and lists under
  `npx playwright test --list` (six cases). It has not been run: the run that
  counts is QA's, on a cold stand (05-frontend.md § 7). `keycloakAdmin.ts`
  keeps its own formatting; only the user creation moved into a helper.

## 13. Code review round 1 fixes (as built)

Written after review round 1; the sections above are unchanged, and this
section overrides them where it says so.

- **F2, qa.md D-4 (651f27c).** The check of § 3.2 is wider and its error is
  renamed. `getProcessDefinition` throws `UnreadableProcessDefinitionError`,
  which § 3.2, § 4.1 and § 8.3 call `EmptyProcessDefinitionError`. It still
  covers an answer with no types, and now also a type that is not an object,
  a type with no `backlogLevel` object or no `states` array, and a state that
  is not an object or has no `category` object. Those are the parts the cards
  dereference. A missing text field is not checked: it renders as an empty
  cell and does not break the page. The banner text is the same (T-20).
- **F4, qa.md D-5 (bcef95f).** § 4.2 gains a third target. When a retry
  ends in «Организация не найдена», `useRetryFocus` moves focus to the
  frame's `h1`. The handle is a new optional `headingRef` on `StateFrame`
  and `NotFound`, the P-2 seam again: additive, and `tabIndex={-1}` only
  when given. A first load that finds no organization moves no focus.
  § 8.4's list of edited test files gains `shared/ui/StateFrame.test.tsx`,
  which only adds tests.
- **F1 (P-1)** was undecided when round 1 was answered, and is decided as (a)
  by status.md D-15 (2026-10-04). Nothing in the code changes under (a): the
  shell stays as merged, and `AppLayout.process.test.tsx` already pins § 6.3's
  rows 1 and 2.
