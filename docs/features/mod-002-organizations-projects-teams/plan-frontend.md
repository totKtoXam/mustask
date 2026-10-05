# Plan — frontend (mod-002-organizations-projects-teams)

Stage 6a (L task, deep pass) per
[05-frontend.md](../../ai-workflow/stages/05-frontend.md). No product code is
written in this pass. Inputs, read at the branch tip 5cbaca7, whose base is
`main` at f602024 (mod-015 c48b565 and mod-001 f602024 both in it):
[status.md](status.md) (C-1..C-4, D-a..D-x), [spec.md](spec.md),
[US-1](stories/US-1-create-organization.md)..[US-8](stories/US-8-context-switcher.md),
[design.md](design.md) (DC-1..DC-11), [api-contract.md](api-contract.md),
[frontend.md](../../conventions/frontend.md),
[ADR-0024](../../adr/0024-app-shell-and-visual-language.md),
[ADR-0026](../../adr/0026-soft-deactivation-by-status.md), mod-015's
[design.md](../mod-015-app-shell-design-system/design.md) (§ 4, § 5, § 7,
§ 9, § 10.7) and [plan-frontend.md](../mod-015-app-shell-design-system/plan-frontend.md)
(§ 4.3, § 13, D-15), mod-001's
[plan-frontend.md](../mod-001-process-seed/plan-frontend.md) (§ 6.3, D-15),
and the panel as this branch has it: `src/app` (`AppLayout`, `routes`,
`navigation/*`, `shell/*`), `shared/{ui,lib,api}`, `features/process` (the
shell's first consumer), `features/dictionaries`, `src/test`, and `e2e/`.

**Starting point.** The shell gives this task its frame: `PageHeader` with
views as tabs, `StateFrame` with `NotFound` by level, `RegistrySkeleton`,
`FormSkeleton`, `ErrorBanner`, `FormDialog`, `ConfirmDialog`, the navigation
registry (`app/navigation/model.ts`, sections «Организация» with mod-001's
«Процесс», «Проект» empty, «Справочники»), the switcher drawn from a
`WorkContextSource` (`shared/lib/workContext.ts`) with one status for the
whole switcher, and `AppLayout` feeding it `usePlaceholderContextSource()`
unless a test passes a stub. Nothing reads organizations yet, the backend of
this task is done (26 routes, 23 codes, `contracts/errors/organizations.json`),
and mod-001's demonstration organization is gone (D-h), so e2e spec 20 has no
organization to open today.

## 1. Shape of the change

- **New slice `src/frontend/src/features/organizations/`**, named after the
  backend's `Features/Organizations` (design § 16). Its `index.ts` is its
  only public entry.
- **Pages and routes, nine routes for seven pages** (design § 1): the
  organization list (two tabs, two routes), organization «Общие», «Проекты»
  (two tabs, two routes), project «Общие», «Команды», a team's page,
  «Области». `/organizations/:o` and `/organizations/:o/projects/:p` alone stay
  unrouted and answer mod-015's «Страница не найдена».
- **Registries extended:**
  1. the router, `app/routes.tsx`: one pathless layout route,
     `OrganizationsSection` (the slice's notice host, § 7.4), holding the nine
     routes, before `*`;
  2. the navigation registry, `app/navigation/model.ts`: five settings
     entries (design § 2.1);
  3. the switcher-data registry (D-n, D-o): `shared/lib/workContext.ts`'s
     contract gains a status and a retry per level and the fallback notice
     (§ 4.1); `app/shell/ContextSwitcher.tsx` draws the four rows of design
     § 3.2 and the notice; `AppLayout`'s placeholder line is swapped for this
     slice's source, handed a switch-target resolver built in
     `app/navigation/switchTarget.ts` (§ 4.6); mod-015's design § 5.4 and
     § 5.7 are amended to match (D-n);
  4. the error-code registry, `shared/api/errorCodes.ts`: the 22 codes of
     api-contract § 6 that mod-001 did not add;
  5. `shared/ui`: the new `TreeRegistry` (design § 11.2), plus small
     additive props on existing patterns (§ 9, P-3, P-7).

  The theme is not touched (C-4): every token the screens need exists
  (`layout.formColumnWidth`, `layout.contextSkeleton*`, `shell.rowSelected`).
- **Server state:** TanStack Query, keys `['organizations', <entity>, …]`
  (§ 3.3). The switcher's three levels share their keys with the pages that
  read the same lists, so one request serves both and one invalidation
  refreshes both. `retry: false` and no refetch on focus are the client's
  global defaults (`App.tsx`); the switcher's three lists opt in to a refetch
  on focus (design § 3.1, P-6).
- **Client state:** the remembered selection in `localStorage`
  (`mustask.context.<sub>`, design § 3.4) and the source's choice, pending
  switch and fallback notice inside the source hook; each dialog's fields,
  error and busy flag; the tree's expanded set, selected and focused rows;
  the slice's one notice. No global store, no copy of server data.
- **Dependencies:** none added. Every MUI component and icon named below is
  in `@mui/material` 9 and `@mui/icons-material` 9 as installed.
- **Dependency direction:** `app/` imports the slice through its barrel; the
  slice imports `shared/` and `features/process`'s barrel (the «Процесс» link
  builder) only, and reads theme values through `useTheme()` and `sx`
  callbacks. It never imports `app/` or `features/auth` internals: the token
  subject reaches the source as an argument from `AppLayout` (§ 4.3, P-4).
- **No page-side permission check** beyond the create action. Design § 1 has
  no access-denied state on these pages: until mod-016 every signed-in user
  passes, and after it a non-member gets the not-found answer the pages
  already render (the same reasoning as mod-001's DD-6). The create action is
  shown when `useAuth().can('organizations.organization.create')` holds, the
  instance-code check frontend.md names.

## 2. Files

### 2.1 `features/organizations/` (new)

| File | Holds |
|---|---|
| `index.ts` | The pages, `OrganizationsSection`, the route constants, the landing builders and `organizationsPermissions` (what `model.ts` needs), `ORGANIZATIONS_PATH`, `useOrganizationsContextSource`. |
| `paths.ts` | The nine route patterns of design § 1 and their builders (`encodeURIComponent` per id, as `processDefinitionPath` does). |
| `permissions.ts` | `organizationsPermissions`: `organizationRead`, `projectRead`, `teamRead`, `areaRead` (design § 2.1, scoped) and `organizationCreate` (instance). |
| `api/organizationsApi.ts` | The DTOs of api-contract § 1–§ 4 (`OrganizationListItem`, `Organization`, `ProjectListItem`, `Project`, `Team`, `AreaNode`, `Area`) and one function per route of § 5, ids re-encoded in the path, bodies exactly as the contract names them. |
| `api/queryKeys.ts` | The keys of § 3.3. |
| `lib/validation.ts` | The three client checks of design § 4.4: `nameError(value)`, `keyError(value)` (the key as typed: no trim, no upper-casing). |
| `lib/refusals.ts` | `placeRefusal(error, dialog)`: field or dialog alert per design § 4.4's table, keyed on `errorCode` only; `validation_failed`'s `errors.name`/`errors.key` land on their fields. |
| `lib/notFoundLevel.ts` | `notFoundLevel(error)`: `'organization' \| 'project' \| 'team' \| null` from the three not-found codes (`area.not_found` is `null`: a dialog alert, api-contract § 0.3). |
| `lib/areaTree.ts` | From the pre-ordered `AreaNode[]`: `TreeRegistry` nodes, the move picker's options (all but the node, its subtree and its parent, tree order, root marked), the default-area options (`ownerTeamId === team.id`, tree order), the owner picker's choices. |
| `context/addressContext.ts` | `addressContext(pathname)`: the organization and project ids an address carries under mod-015 D-17's prefixes (an id is carried only when a segment follows it, so `/organizations/deactivated` and `/organizations/:o` carry nothing). |
| `context/storage.ts` | Read and write `mustask.context.<sub>`; storage unavailable or a malformed value is "nothing remembered" (design § 3.4). |
| `context/resolveSelection.ts` | The pure resolution of § 4.2: lists and their statuses, the address, the remembered and chosen selection in; the effective selection, the per-level statuses and the fallback out. |
| `context/useOrganizationsContextSource.ts` | The hook: the three level queries, the storage, the address sync, `select`, `retry(level)`, the switch navigation through the resolver, the fallback notice (§ 4). |
| `hooks/` | One query hook per read (`useOrganizations(view)`, `useOrganization`, `useProjects(view)`, `useProject`, `useTeams`, `useTeam`, `useAreaTree`) and one mutation hook per write group, each doing its invalidations (§ 3.4). |
| `components/OrganizationsSection.tsx` | The pathless layout route: `<Outlet />` and the slice's one `Snackbar` (§ 7.4). |
| `components/FactsPanel.tsx` | Design § 4.2's `dl` in an outlined `Paper`. |
| `components/NameDialog.tsx` | The one name form for create and rename at every level (DC-5): title, optional text above, «Название» with its helper, current name selected, primary disabled while unchanged. |
| `components/CreateProjectDialog.tsx` | «Новый проект»: «Название» and «Ключ» (design § 7.4). |
| `components/RestoreButton.tsx` | A row's «Восстановить» with its own busy state (design § 5.5). |
| `components/DefaultAreaDialog.tsx` | Design § 9.6, with its own tree read, skeleton and failed alert. |
| `components/DeleteTeamDialog.tsx` | Design § 9.7, destructive. |
| `components/AreaDialogs.tsx` | «Новая область», «Переименование области», «Перемещение области», «Владелец области», «Удаление области» (design § 11.5). |
| `components/RowActionsMenu.tsx` | «Действия: <название>» and its `Menu` (the teams table; the tree takes its own from `TreeRegistry`). |
| `pages/OrganizationListPage.tsx` | `/organizations` and `/organizations/deactivated` (design § 5). |
| `pages/OrganizationGeneralPage.tsx` | Design § 6. |
| `pages/ProjectListPage.tsx` | `…/settings/projects` and `…/settings/projects/deactivated` (design § 7). |
| `pages/ProjectGeneralPage.tsx` | Design § 8, with the «Процесс» link from `features/process`'s barrel. |
| `pages/TeamsPage.tsx`, `pages/TeamPage.tsx` | Design § 9, § 10. |
| `pages/AreasPage.tsx` | Design § 11 on `TreeRegistry`. |
| `testFixtures.ts` | The stories' dataset (design § 17: «Альфа» with «Пример проекта» `PRM`, «Аналитика», the deactivated «Старый проект»; «Бета» with «Отчётность»; the deactivated «Гамма»; «Платформа» with «Платформа / API»), the API URLs, problem bodies per code. |
| `testApi.ts` | MSW handlers over an in-memory copy of that dataset, answering per api-contract (lists in display order, not-found from the outside in, the 409s the screens place), so flow tests (create → listed → switcher) run without a handler per step. It keeps the contract's answers, not the backend's rules in full: what a test needs and no more. |
| `testRender.tsx` | Renders a page at its route inside `OrganizationsSection`, a router, a fresh `QueryClient` and a stub session (as `renderShell` does for the shell). |

### 2.2 `app/`

| File | Change |
|---|---|
| `routes.tsx` | The layout route and the nine routes (§ 8.1). |
| `navigation/model.ts` | Five entries (§ 8.2); the doc comment's sentence on «Процесс»'s place updated (third now, fourth once mod-016's «Участники» exists). |
| `navigation/switchTarget.ts` (new) | `switchTarget(model, access, request)` and the stable callback `AppLayout` hands the source (§ 4.6). |
| `AppLayout.tsx` | The swap D-o allows and nothing else: the two lines that read `usePlaceholderContextSource()` become, below the access check they need, the resolver callback and `useOrganizationsContextSource({ enabled: contextSource === undefined, subject, resolveSwitchTarget })`; under P-4 (a) `subject` joins the existing `useAuth()` destructuring. |
| `shell/ContextSwitcher.tsx` | The per-level rows of design § 3.2 and the fallback notice of mod-015 § 5.4 (§ 4.1). |
| `shell/placeholderContext.ts` | Code unchanged; its doc comment says it is now the shell tests' static source (the product no longer imports it). |

### 2.3 `shared/`

| File | Change |
|---|---|
| `lib/workContext.ts` | The contract extension of § 4.1 and the `SwitchTargetResolver` type. Additive: every existing stub still compiles. |
| `api/errorCodes.ts` | `organizationErrorCodes` gains the 22 codes; `notFound` keeps its name (mod-001 reads it). |
| `api/tokenClaims.ts` | `subject` from `sub` ('' when absent) — under P-4 (a). |
| `ui/TreeRegistry.tsx` (new) | Design § 11.2, § 11.6 (§ 6). |
| `ui/StateFrame.tsx` | `NotFound` gains an optional prop that leaves the not-found registration out — under P-3 (a). |
| `ui/FormDialog.tsx`, `ui/ConfirmDialog.tsx`, `ui/PageHeader.tsx` | One optional prop each — under P-7 (a): `submitDisabled`; `cancelAutoFocus`; `viewsLabel` (the tabs' `aria-label`). Absent, each renders as today. |

`features/auth/AuthContext.tsx` gains an optional `subject` on
`AuthContextValue`, set from the claims, under P-4 (a). Optional, so
`renderShell`'s `authValue()` and every other stub compile unchanged.

### 2.4 Test infrastructure

| File | Change |
|---|---|
| `src/test/renderShell.tsx` | When a test passes no `contextSource`, the helper passes the placeholder source itself: one wrapper element, kept across `update()`, always calls `useStaticContextSource(PLACEHOLDER_ORGANIZATIONS)` and hands `AppLayout` the test's source or that one, so switching to a stub keeps the shell's state. mod-015's and mod-001's shell tests keep running on the placeholder dataset, byte-identical, and the real source stays disabled in them. |
| `src/test/mocks/handlers.ts` | A default `GET /api/organizations` answering `[]`: every test that mounts the real routes gets an instance with no organization unless it says otherwise. The real source then reads nothing else, so mod-001's request-count assertions (`routes.process.test.tsx`, `ProcessDefinitionPage.unreadable.test.tsx`) hold unchanged. |

### 2.5 E2E

New `e2e/specs/21-organizations.spec.ts` and `e2e/support/organizationsApi.ts`;
specs 19 and 20 reworked (§ 11, P-5).

### 2.6 Docs

- This file; a `[dev]` Log line per pass in `status.md`.
- mod-015's `design.md` § 5.4 (four rows, the fallback row's owner) and § 5.7
  (the contract) — the amendment D-n puts in this task (§ 4.7). mod-015's
  `plan-frontend.md` stays the record of that task. Its `status.md` line is
  the orchestrator's.
- `docs/conventions/frontend.md` § MUI: `TreeRegistry` joins the list of
  shared patterns, one line, in the closing pass.

**Not touched:** `app/theme/**` (C-4), the rest of `AppLayout`,
`features/process` and `features/dictionaries` code, `src/backend/**`,
`deploy/**`, `contracts/**`, `api-contract.md`, `package.json`.

## 3. The API layer (api-contract § 0–§ 6)

### 3.1 Requests

`apiFetch` for every call, ids from `useParams()` re-encoded with
`encodeURIComponent` and never validated (api-contract § 0.2). Bodies: `name`;
`name` + `key`; `areaId`; `parentId` + `name` + `ownerTeamId` (`null` for
«Наследовать от родительской области»); `parentId`; `ownerTeamId`. A `204`
returns `undefined`. Every DTO field is typed as the contract states it; the
`depth` of `AreaNode` drives the indent, `path` the pickers and the
«Область по умолчанию» cell.

### 3.2 Error codes and where they land

`organizationErrorCodes` holds the 23 codes, one to one with api-contract
§ 6; a new drift test checks both directions against
`contracts/errors/organizations.json` (the existing one checks one). The
panel branches on `errorCode` only (frontend.md "Named values"):

- `notFoundLevel` picks a page's frame from its **first read** (design § 12);
- `placeRefusal` puts a write's refusal on «Название», «Ключ», «Область» or
  «Новая родительская область», or in the dialog's `Alert`, per design § 4.4;
  the text is always `apiErrorText(error)` (the server's `detail`), or a
  field reason's own `detail` for `validation_failed`;
- after a not-found code in a dialog, closing it refetches the page, which
  then shows its frame (design § 4.4, last row).

### 3.3 Query keys

| Key | Read | Shared by |
|---|---|---|
| `['organizations', 'organizations', 'active' \| 'deactivated']` | § 1.1, § 1.2 | the list's tabs; the switcher's organization level (active) |
| `['organizations', 'organization', o]` | § 1.4 | organization «Общие» |
| `['organizations', 'projects', o, 'active' \| 'deactivated']` | § 2.1, § 2.2 | «Проекты»; the switcher's project level (active) |
| `['organizations', 'project', o, p]` | § 2.4 | project «Общие» |
| `['organizations', 'teams', o, p]` | § 3.1 | «Команды»; the owner pickers; the switcher's team level |
| `['organizations', 'team', o, p, t]` | § 3.2 | a team's page |
| `['organizations', 'areas', o, p]` | § 4.1 | «Области»; the default-area dialog |

### 3.4 Invalidation per write (design § 3.7)

| Write | Invalidates | Then |
|---|---|---|
| create organization | organizations active | open the new organization's «Проекты» (DC-6); notice |
| rename organization | organization o, organizations active | notice |
| deactivate organization | organizations active + deactivated; organization o removed | open `/organizations/deactivated`; notice per DC-7 (§ 7.4) |
| restore organization | organizations active + deactivated | notice; focus (design § 5.5) |
| create project | projects o active | notice |
| rename project | project, projects o active, areas o p (the root follows) | notice |
| deactivate project | projects o active + deactivated; project removed | open `…/settings/projects/deactivated`; DC-7 |
| restore project | projects o active + deactivated | notice; focus |
| add team | teams, areas | notice |
| rename team | teams, team, areas (owner and default names) | notice |
| set default area | teams, team, areas | notice |
| delete team | teams, areas; team removed | on its page, open «Команды»; DC-7 |
| create, rename, move area | areas, teams (a default's path can change) | notice; focus (design § 11.7) |
| set owner, delete area | areas | notice; focus |

The tree is always refetched after an area write (api-contract § 4.1: a
write can move a subtree's resolved owners), never patched in the cache.

## 4. The context source (US-8; D-b, D-n, D-o; design § 3)

### 4.1 The contract extension (`shared/lib/workContext.ts`)

```ts
interface WorkContextSource {
  status: WorkContextStatus;          // unchanged: the organization level
  /** D-n: the project and team levels, each loading or failing on its own once the level above is shown. Absent: ready. */
  levelStatus?: Partial<Record<'project' | 'team', WorkContextStatus>>;
  retry?(level?: WorkContextLevel): void; // «Повторить» of the level that failed; none: organization
  /** mod-015 § 5.4's fallback notice: the names of the selection that replaced a gone one (P-2). */
  fallback?: { id: number; names: readonly string[] } | null;
  dismissFallback?(): void;
  // organizations, selection, isReal, organizationListPath, select: unchanged
}
type SwitchTargetResolver = (request: {
  pathname: string;                   // the address on screen
  carried: WorkContextSelection;      // the levels it carries, its own ids
  next: WorkContextSelection;         // the selection after the choice
}) => string;                         // the address to open
```

The switcher, top-down, stops at the first level that is not ready
(design § 3.2): a loading level and every level below it are
`Skeleton variant="rounded"` boxes of `layout.contextSkeletonWidth` ×
`contextSkeletonHeight` (two for projects, one for teams) in one
`role="status"` «Загрузка контекста»; a failed level is the existing failed
block with «Не удалось загрузить проекты» or «Не удалось загрузить команды»
and «Повторить» calling `retry('project' | 'team')`, its text visually hidden
below 1024 px and kept in the icon's tooltip. The organization level's rows
are unchanged. The notice is MUI's `Snackbar` with `Alert severity="info"`,
6 s, bottom left: «Ранее выбранный контекст недоступен. Выбрано: » + the names
joined by « / » + «.». The copy lives in the switcher, as mod-015 § 5.4 owns
it once amended.

### 4.2 Resolution (`resolveSelection`, pure)

One function, tested by table (§ 10.8). Per level, top-down:

1. **Address.** When the address carries the level and every carried level
   is found — the level's active list holds the id — the carried levels take
   the address's ids. The address is synced once per pathname, so a later
   choice is not undone while the page stays. A level the address carries
   that is not found sets nothing, and the remembered selection is not
   written (mod-015 BR-18). A team's page carries the organization and the
   project only; its team never sets the switcher's team (design § 3.4).
   One rule for every page, this task's and mod-001's alike — P-1.
2. **Remembered or chosen.** Otherwise the level keeps its id if its list
   holds it.
3. **First entry.** Otherwise the first entry of the list, and the levels
   below take their first entries (the project's first team is its default,
   design § 3.5).

A level that is loading or failed has no effective id and keeps its
remembered one: a failure is not a deletion (design § 3.2). A level whose
list is empty is left empty («Нет организаций», «Нет проектов»).

**Fallback.** A level falls back when it had a remembered or chosen id, its
parent is the one that id was remembered under, and the ready list no longer
holds it. Then the notice is raised once every level below has settled
(ready or empty), naming the levels that hold an entry; a level that is left
empty raises nothing (design § 3.6). No notice on the first visit, nor for
levels an address sets. The page never moves on a fallback.

**Order.** The source renders the lists in the order received (api-contract
§ 0.6: the server orders them).

### 4.3 Remembering (`storage.ts`)

`localStorage['mustask.context.<sub>']` holds
`{"organizationId","projectId","teamId"}` (design § 3.4). Read once the
subject is known; written on a choice, on an address sync and on a fallback
(otherwise every reload would repeat the notice), only for levels that are
settled. No subject, no storage, or a value that does not parse: nothing is
remembered and nothing fails. `<sub>` is the token's `sub` (P-4).

### 4.4 Reads per level

- **Organizations:** `GET /api/organizations`, always (the source is enabled
  in the real shell only).
- **Projects:** of the effective organization; also of the address's
  organization while it is not yet the effective one, so a project the
  address carries can be found before anything is set.
- **Teams:** of the effective project.

Each is a `useQuery` on the key of § 3.3 with `refetchOnWindowFocus: true`
(design § 3.1, P-6), so the pages' writes reach the switcher through the
same invalidation and changes by other users show when the window regains
focus. `retry(level)` refetches that level's query.

### 4.5 Choosing (`select`)

`select(level, id)` sets the level, clears the levels below (they take their
first entries without a notice) and writes the storage. Then, if the
address carries `level` (`addressContext`), the page moves (design § 2.2):

- the source asks the resolver for the target with `next` as the new
  selection. When the address also carries the project level and the choice
  is an organization, the new organization's first project is needed first:
  the source awaits that list (`fetchQuery` on its key) and resolves after
  it; if that read fails, the target is «Главная» and the switcher shows the
  project level's failed row;
- while the move is pending the address sync is held, so the old address
  cannot pull the selection back;
- a choice at a level the address does not carry moves nothing.

### 4.6 The switch-target resolver (`app/navigation/switchTarget.ts`, D-o)

`switchTarget(model, access, { pathname, carried, next })`:

1. Find the entry whose landing, built from the address's **own** ids
   (`entry.landing(carried)`), the pathname is at or beneath
   (`entryMarker`). This is design § 2.2's "matched by the landing's pattern
   rather than by the switcher's context": a not-found frame switches as the
   page it answers, and a deactivated tab or a team's page find their entry.
2. If that entry is visible in the new context
   (`isEntryVisible(entry, access, { isReal: true, selection: next })`),
   open `entry.landing(next)`; else, or when no entry matches, «Главная»
   (`HOME_PATH`).

It reads the model and the shell's one access check, which is why `app/`
builds it and hands it to the source as a stable callback (D-o). A tab is a
view, not an entry: the landing is the active tab.

### 4.7 The amendment to mod-015's design (D-n)

In the pass that builds § 4.1, mod-015's `design.md` gains:

- **§ 5.4:** the four rows of mod-002 design § 3.2 (projects and teams
  loading and failed, with their copy), the rule "drawn from the top, stopping
  at the first level that is not ready", and the fallback row's owner changed
  from "designed here, wired by mod-002" to "rendered by the switcher from
  the source (mod-002)";
- **§ 5.7:** the contract as § 4.1 states it (a status and a retry per level,
  the fallback notice), plus a dated note naming mod-002 D-n and D-o.

## 5. Pages and their states

Every page renders `PageHeader` (or a `NotFound` frame alone), passes no
`ancestors` (design § 1: the shell derives every trail), and decides
not-found from its first read. Loading keeps the header's static title and
tabs; error is `ErrorBanner` + «Повторить» in place of the content, the
header kept without entity commands (design § 4.7).

| Page | Route | First read | States | Header | Dialogs |
|---|---|---|---|---|---|
| «Организации» | `/organizations`, `/organizations/deactivated` | the tab's list | loading `RegistrySkeleton` («Название»); error; empty per tab and right (design § 5.3, `InboxOutlined` frame `h2`); data | «Организации»; primary «Создать организацию» (`Add`) only with the create right; tabs «Активные», «Деактивированные», named «Организации по статусу» | «Новая организация»; restore inline |
| Organization «Общие» | `…/:o/settings/general` | § 1.4 | loading `FormSkeleton` 1; error; not found organization; data | «Общие»; «Переименовать» (`EditOutlined`), «Деактивировать» (`ArchiveOutlined`) once loaded | rename; «Деактивировать организацию?» |
| «Проекты» | `…/:o/settings/projects`, `…/deactivated` | the tab's list | loading («Название», «Ключ»); error; empty per tab; not found organization; data | «Проекты»; primary «Создать проект» once the list is read; tabs named «Проекты по статусу» | «Новый проект»; restore inline |
| Project «Общие» | `…/:o/projects/:p/settings/general` | § 2.4 | loading `FormSkeleton` 3; error; not found organization or project; data | «Общие»; «Переименовать», «Деактивировать» | rename (text above the field); «Деактивировать проект?» |
| «Команды» | `…/settings/teams` | § 3.1 | loading («Команда», «Область по умолчанию»); error; no empty state (a project keeps its first team); not found; data | «Команды»; primary «Добавить команду» | «Новая команда»; rename; «Область по умолчанию»; «Удаление команды» |
| A team's page | `…/settings/teams/:t` | § 3.2 | loading: title «Команда», `FormSkeleton` 2; error under «Команда»; not found team, project or organization; data | «Команда: <название>»; «Переименовать»; «Удалить команду» (`DeleteOutlineOutlined`) except for the first team | the team dialogs |
| «Области» | `…/settings/areas` | § 4.1 | loading («Область», «Владелец», 5 rows); error; root only (the treegrid, then the frame «Вложенных областей пока нет» with «Новая область»); not found; data | «Области»; «Новая область» (`Add`), «Новая дочерняя область» (`Add`, disabled until a row is selected) | the five area dialogs |

Tables are `TableContainer component={Paper variant="outlined"}` + `Table
size="small"` named per design § 13; a name cell is one line with an ellipsis,
the full name in a `Tooltip` and in the accessible name (design § 4.6); a
deactivated row's name is plain text. The first team's cell carries `Chip
size="small"` «Владеет корневой областью»; its row menu has «Переименовать»
only, any other team's adds «Изменить область по умолчанию»
(`AccountTreeOutlined`) and «Удалить команду» (`DeleteOutlineOutlined`, the
item in `error.main`). Project «Общие»'s «Типы рабочих элементов» row is
«Задаются процессом организации: » + `Link component={RouterLink}` «Процесс»
to `processDefinitionPath(o)` (D-c). The team page's «Область по умолчанию»
row carries `Button variant="text" size="small"` «Изменить» named «Изменить
область по умолчанию», or, for the first team, the caption «Корневая область
проекта. У первой команды область по умолчанию не меняется.».

**Restore** (design § 5.5, § 7.5): no confirmation; the row's button busy
with `CircularProgress`, other rows usable; on success the row leaves, the
notice, focus to the next row's «Восстановить», else the previous, else the
selected tab; on failure focus stays and the error notice carries the
server's text.

## 6. `shared/ui/TreeRegistry` (US-5; design § 11.2, § 11.6, § 11.7)

Generic and named nothing of areas (US-5):

```ts
interface TreeRegistryNode { id: string; parentId: string | null; label: string }
interface TreeRegistryAction { label: string; icon: ReactElement; onClick(): void; destructive?: boolean }
interface TreeRegistryProps<N extends TreeRegistryNode> {
  'aria-label': string;
  firstColumn: string;                          // its header
  columns: readonly { header: string; render(node: N): ReactNode }[];
  nodes: readonly N[];                          // siblings in the order given
  adornment?(node: N): ReactNode;               // after the label
  actions(node: N): readonly TreeRegistryAction[];
  selectedId: string | null; onSelect(id: string): void;
  expanded: ReadonlySet<string>; onToggle(id: string): void;
  loading?: boolean; error?: unknown; onRetry?(): void;
}
// ref: { focusRow(id: string): void } — the caller's handle for § 11.7
```

- Markup: `Table role="treegrid"`; each visible row `role="row"` with
  `aria-level`, `aria-setsize`, `aria-posinset`, `aria-selected`, and
  `aria-expanded` when it has children; the first cell indents 24 px per level
  in the spacing unit, then `IconButton` `ChevronRight`/`ExpandMore` named
  «Развернуть «…»» / «Свернуть «…»», `tabIndex={-1}`, or a gap of the same
  width; selected rows `shell.rowSelected`.
- Keyboard: the § 11.6 table — roving `tabIndex` on rows; Tab from a row to
  its «Действия» (`MoreHoriz`, `aria-haspopup="menu"`, tabbable only in the
  focused row) and out; arrows, Home/End; Enter and Space select; Shift+F10
  and ContextMenu open the menu; Escape in the menu returns to «Действия»;
  a click selects and focuses.
- States: `RegistrySkeleton` with the real headers, `ErrorBanner` with
  «Повторить».
- The expanded set and the selection are the caller's: «Области» starts with
  the root expanded and expands a new parent after a create or a move; the
  set lasts for the page visit.

## 7. Dialogs, validation, refusals, notices

### 7.1 Dialogs (design § 4.3)

`FormDialog` for every form, `ConfirmDialog` for deactivate (contained) and
delete (destructive). The first field is focused, a rename's text selected;
the primary is disabled while it has nothing to save (P-7); a confirmation
opens on «Отмена» (P-7). Busy: the primary's spinner, every field and button
disabled, Escape and the backdrop ignored. Focus returns to the opener, or
to the tree row of § 11.7 when the opener is gone. Each dialog is mounted by
its page and keeps its input when refused.

### 7.2 Client validation (design § 4.4)

Checked on submit, cleared as the field is edited: «Название должно
содержать от 1 до 100 символов» (trimmed length), «Укажите ключ проекта»,
«Ключ проекта: от 2 до 10 латинских заглавных букв и цифр, начиная с
буквы» (`^[A-Z][A-Z0-9]{1,9}$` on the key as typed). No `maxLength`. A
client refusal sends nothing.

### 7.3 Fields and pickers

- «Ключ»: `slotProps.htmlInput` `autoComplete="off"`, `spellCheck={false}`,
  `autoCapitalize="characters"`.
- Owner pickers: `TextField select`, «Наследовать от родительской области»
  first, then the active teams in the order of § 3.1 (deleted teams never
  appear: the list leaves them out); «Владелец области» preselects the
  explicit owner or «Наследовать…», helper «Сейчас: <команда>» or «Сейчас:
  <команда>, унаследовано».
- Move picker: `Autocomplete` over `areaTree`'s options, the root with the
  secondary text «Корневая область», `noOptionsText` «Ничего не найдено».
- Default-area picker: `TextField select` over the areas the team owns
  explicitly; a `Skeleton` in its place while the tree loads; an error
  `Alert` with «Повторить» inside the dialog if it fails.

### 7.4 Notices and DC-7

- **The slice's notice** — every completed action of design § 5–§ 11 and a
  failed restore — goes through `OrganizationsSection`'s one `Snackbar` (6 s,
  bottom left, plain text; `Alert severity="error"` for the failure). The
  section is the layout route around the slice's nine routes, so a notice
  outlives the move to the next page (create → «Проекты», deactivate → the
  deactivated tab, delete from a team's page → «Команды»).
- **The fallback notice** is the switcher's (§ 4.1).
- **DC-7, one notice at a time.** An action that will make the switcher fall
  back raises no notice of its own: deactivating the chosen organization
  while another is active, deactivating the chosen project while another is
  active, deleting the chosen team (`useWorkContext().selection`, the public
  contract). When the chosen organization or project was the last active
  one, its level is left empty, no fallback is raised, and the action's own
  notice shows (design § 6.4, § 8.4). As a guard for a race, the section
  closes its notice when the switcher raises a fallback.

## 8. Shell integration

### 8.1 Routes

Inside `AppLayout`'s group, before `*`:

```tsx
<Route element={<OrganizationsSection />}>
  <Route path={ORGANIZATIONS_ROUTE} element={<OrganizationListPage view="active" />} />
  <Route path={DEACTIVATED_ORGANIZATIONS_ROUTE} element={<OrganizationListPage view="deactivated" />} />
  {/* …general, projects ×2, project general, teams, team, areas */}
</Route>
```

`/organizations/deactivated` is a static segment and outranks
`/organizations/:organizationId/…` (design § 1). No gate on any route.

### 8.2 Navigation entries (design § 2.1)

| Section | id | Label | Scope | Code | Landing |
|---|---|---|---|---|---|
| «Организация» | `organization-general` | «Общие» | organization | `organizations.organization.read` | `/organizations/:o/settings/general` |
| «Организация» | `projects` | «Проекты» | organization | `organizations.project.read` | `/organizations/:o/settings/projects` |
| «Организация» | `process` (mod-001) | «Процесс» | organization | `process.definition.read` | unchanged, now third |
| «Проект» | `project-general` | «Общие» | project | `organizations.project.read` | `/organizations/:o/projects/:p/settings/general` |
| «Проект» | `teams` | «Команды» | project | `organizations.team.read` | `…/settings/teams` |
| «Проект» | `areas` | «Области» | project | `organizations.area.read` | `…/settings/areas` |

With a real context «Настройки» opens the organization's «Общие» (BR-19).
The shell derives from these alone that a deactivated tab and a team's page
are settings pages with «Проекты» and «Команды» marked `true` and the
three-segment trails of design § 1.

### 8.3 What the shell shows, mod-001's screen included

| Page | Settings navigation | Trail |
|---|---|---|
| `/organizations`, either tab | none; no entry current | none |
| any settings page of this task, its context set | «Меню настроек», its entry `page` (or `true` beneath its landing) | design § 1 |
| mod-001's «Процесс», the organization in the active list | «Меню настроек», «Процесс» `page` | «Настройки / Организация» — mod-001 D-15's second row, now the product's |
| a page opened outside the selection, before the lists confirm its context | none | none (design § 3.4) |
| «Организация не найдена», «Проект не найден» | none (the frame registers not-found) | none |
| «Команда не найдена» on a team's page | «Меню настроек», «Команды» `true` — under P-3 (a) | none |

## 9. Where design.md and the code disagree, and the resolution

| # | Design | The code as merged | Resolution |
|---|---|---|---|
| R-1 | § 3.4: this task's pages set the context "found by their first read" | the source cannot see a page's read without a page-to-source channel | one rule, the active lists, for every page — P-1 |
| R-2 | § 3.6: the fallback notice is mod-015's `Snackbar` | nothing in the shell renders it, and `AppLayout` is closed (D-o) | the source carries it, the switcher renders it — P-2 |
| R-3 | § 12: «Команда не найдена» keeps «Меню настроек» with «Команды» current | every `NotFound` registers not-found, which drops the settings navigation (mod-015 QA D-2) | an optional prop on `NotFound` — P-3 |
| R-4 | § 3.4: the key `mustask.context.<sub>` | no part of the panel exposes `sub` | P-4 |
| R-5 | § 3.1: other users' changes show "when the window regains focus" | `App.tsx` turns refetch on focus off globally | the switcher's lists opt in — P-6 |
| R-6 | § 4.3: a primary with nothing to save is disabled; a confirmation opens on «Отмена»; § 5.2, § 7.2: the tabs are named «… по статусу» | `FormDialog` has no disabled primary, `ConfirmDialog` no initial focus, `PageHeader`'s tabs are named by the title | three optional props — P-7 |
| R-7 | § 16: the resolver's shape is the frontend plan's | — | § 4.6 |
| R-8 | § 2.2: an organization choice on a project-level page opens the new organization's first project | that project is not loaded yet (one level at a time, DC-11) | the source reads it first (§ 4.5) |
| R-9 | DC-7: the fallback notice replaces the action's | the two notices live in two components | prediction plus a guard (§ 7.4) |
| R-10 | § 3.2 rows; DC-11 "rows' copy then lives in mod-015 § 5.4" | the switcher has one status | D-n's amendment (§ 4.1, § 4.7) |

## 10. Tests per story scenario

"Page test" is Testing Library + MSW (`testApi.ts` or per-test handlers)
through `testRender.tsx`, under the app theme. "Shell test" mounts the real
`AppRoutes` (or `AppLayout` with the real source) with `testApi.ts`. Every
test names its story and scenario in its `describe`, as mod-015 and mod-001
do. Scenarios that are backend only map to plan-backend § 13 and have no
frontend test.

### 10.1 US-1

| Scenario | Test file | Asserts |
|---|---|---|
| An organization is created with its process | `pages/OrganizationListPage.create.test.tsx` | «Создать организацию» → «Новая организация»; `POST` with the name; the new «Проекты» address; the notice «Организация «Пример организации» создана.» with the stored name. `ProjectListPage.test.tsx`: an empty active list shows «Пока нет проектов» and «Создать проект». Seed and creator record: backend. |
| The name is trimmed and its length checked | same | '', spaces, 101 characters: «Название должно содержать от 1 до 100 символов» on the field, `aria-invalid`, no request (request listener); «  Пример организации  » is sent and the notice uses the answer's name. |
| Unique instance-wide, deactivated included | same | `409 name_taken`: the server's text on «Название», the dialog open, the input kept. |
| Two simultaneous creations | backend; the loser's placement is the row above | — |
| A failed seed leaves nothing behind | same | `500 create_failed`: the dialog `Alert`, the name kept; a second submit answered `201` succeeds. |
| Without the right: neither offered nor accepted | `pages/OrganizationListPage.test.tsx` | no «Создать организацию» in the header or the frame, the hint «Право на создание организаций выдаёт администратор»; `403 create_forbidden` with the right in the token: «Недостаточно прав» in the dialog `Alert`. |
| Granted and revoked in the console | same | a re-render with a session that gains, then loses the code shows, then hides the action without a reload (the token renewal's effect). The console itself: backend and QA. |
| No screen grants the right | `paths.test.ts` | the slice's routes are exactly design § 1's nine; none and no entry is about rights. |
| No organization yet | `pages/OrganizationListPage.test.tsx` | both empty frames by right, `h2`, «Создать организацию» in the frame for the holder. |
| Every signed-in user sees every organization | same | a session with no code sees both rows; each name links to its «Проекты». |
| An anonymous request is refused | `app/routes.organizations.test.tsx` | an anonymous session gets the sign-in screen and sends no request to `/api/organizations…`. The 401: backend. |

### 10.2 US-2

| Scenario | Test file | Asserts |
|---|---|---|
| The organization settings | `pages/OrganizationGeneralPage.test.tsx` | the facts «Название»; «Переименовать», «Деактивировать» only after the read; loading one-line skeleton; error with «Повторить». |
| Renamed; own name in another case | same | «Переименование организации» prefilled and selected, «Сохранить» disabled until changed; «пример организации 2» is sent; the notice «Организация переименована.»; the facts and the switcher's key invalidated. |
| A taken or invalid name is refused | same | `409`: on the field; length: client. |
| Two renames to one free name | backend; placement as above | — |
| Deactivated | same; `app/AppLayout.organizations.test.tsx` | the confirmation's text verbatim, contained primary, focus on «Отмена»; on `204` the deactivated tab opens; the notice by DC-7 (own when it was the last active one, else none); in the shell the switcher falls back with its notice. |
| A deactivated organization takes no change | same | a rename answered `organizations.organization.not_found`: «Организация не найдена.» in the dialog `Alert`; closing it refetches and the page answers «Организация не найдена». |
| An unknown organization answers not found | same; `ProjectListPage.test.tsx` | a first read with the code: `NotFound` organization, no header; `not-a-uuid` and `a%2Fb` sent re-encoded. The process screen: mod-001's own tests. |
| Listed separately, no purge | `pages/OrganizationListPage.test.tsx` | the deactivated tab: names as text, «Восстановить «…»», no delete anywhere. |
| Restored exactly as it was | same | the row busy alone; the row leaves; the notice «Организация «…» восстановлена.»; focus next, previous or the tab; a refusal keeps the row and focus and shows the error notice. «Старый проект» stays deactivated: backend, and `ProjectListPage.test.tsx` lists it under the deactivated tab. |

### 10.3 US-3

| Scenario | Test file | Asserts |
|---|---|---|
| Created ready to use | `pages/ProjectListPage.create.test.tsx` | «Новый проект» with both helpers; `POST` name and key; the row and the notice «Проект «…» создан.»; the page stays (DC-6). First team and root: backend; `TeamsPage`/`AreasPage` tests start from that shape. |
| The key is required and well-formed | same | '' → «Укажите ключ проекта»; `prm`, `1PRM`, `P`, `PROJECTKEY01`, `РМК` → the format text; no request. |
| The key is unique, deactivated included | same | `409 key_taken` on «Ключ» with the server's text. |
| Two projects with one key | backend; placement as above | — |
| The name is unique in the organization | same; `ProjectGeneralPage.test.tsx` | `409 name_taken` on «Название», create and rename. |
| A deactivated project's name stays reserved | same | `409 name_taken_by_deactivated` on «Название». |
| Rename renames the root, keeps the key | `pages/ProjectGeneralPage.test.tsx` | the text above the field; the notice «Проект переименован.»; the areas key invalidated; no field or action edits «Ключ» anywhere in the slice (the caption «Задан при создании и не меняется»). |
| The project settings | same | facts «Название», «Ключ», «Типы рабочих элементов»; «Команды» and «Области» are its entries (shell test, § 10.8). |
| The «Процесс» link | same; `app/routes.organizations.test.tsx` | the link's `href` is `/organizations/<o>/settings/process`; through the real routes it opens mod-001's screen, with «Меню настроек» and the trail «Настройки / Организация». |
| A project is deactivated | same | confirmation text with name and key; the deactivated projects tab opens; DC-7. |
| A project id that names no project of the organization | same; `TeamsPage.test.tsx`, `AreasPage.test.tsx` | `organizations.project.not_found` → «Проект не найден»; `organization.not_found` → «Организация не найдена». |
| Restored | `pages/ProjectListPage.test.tsx` | as US-2's restore, «Проект «…» восстановлен.». |

### 10.4 US-4

| Scenario | Test file | Asserts |
|---|---|---|
| A second team with its own area | `pages/TeamsPage.test.tsx` | «Новая команда» with its helper; the row in the order received; the notice «Команда «Платформа» добавлена.»; teams and areas keys invalidated. |
| Unique among active teams | same; `TeamPage.test.tsx` | `409 team.name_taken` on «Название», add and rename. |
| The new team's area name must be free | same | `409 area.name_taken` on «Название». |
| Two teams of one name | backend | — |
| Renamed | same | «Переименование команды», helper «Названия областей команды не изменятся.», notice «Команда переименована.». |
| Default area among explicit areas | `components/DefaultAreaDialog.test.tsx` | options «Платформа» and «Платформа / API» only, in tree order; skeleton while loading; failed `Alert` with «Повторить»; «Сохранить» disabled until changed; `409 default_area_not_owned` on «Область». |
| The first team's default is the root | `pages/TeamsPage.test.tsx`, `TeamPage.test.tsx` | the first team's menu holds «Переименовать» only; its page has no «Изменить» and shows the caption. |
| The project's teams | `pages/TeamsPage.test.tsx` | columns, paths, the chip on the first row only, no empty state. |
| A team id that names no team of the project | `pages/TeamPage.test.tsx`; `app/AppLayout.organizations.test.tsx` | «Команда не найдена» (and the outer frames for their codes); in the shell, under P-3 (a), «Меню настроек» stays with «Команды» `true` and there is no trail. |

### 10.5 US-5

| Scenario | Test file | Asserts |
|---|---|---|
| Shown as the tree registry; the pattern names nothing of areas | `shared/ui/TreeRegistry.test.tsx`, `TreeRegistry.names.test.ts`, `pages/AreasPage.test.tsx` | the treegrid's ARIA and keyboard model (every § 11.6 row), roving `tabIndex`, `focusRow`; a source scan finds no «Област», «Владел», «Команд», `area`, `owner`, `team` in `TreeRegistry.tsx`; the page shows name, owner, actions. |
| The root comes with the project and follows it | `pages/AreasPage.test.tsx` | the root's menu holds «Новая дочерняя область» only; `409 root_is_fixed` from a direct request lands in a dialog `Alert`. |
| A tree with only the root | same | the root row, then the frame `h2` with «Новая область». |
| A child created under any node | same | under the root by «Новая область», under the selected row by «Новая дочерняя область» (disabled before a selection) and by a row's menu; «Родительская область: <путь>»; the new row selected and focused, its parent expanded; notice. |
| Sibling names unique | `components/AreaDialogs.test.tsx` | `409 area.name_taken` on «Название» for create and rename; length client-side. |
| Two creations of one sibling name | backend | — |
| Renamed | same | prefilled; the same row keeps focus; notice «Область переименована.». |
| Moves with its subtree | same | the text «… переместится вместе со всеми вложенными областями.»; `PUT …/parent`; the moved row selected under its expanded new parent; the tree refetched. |
| A free name at the destination | same | `409 area.name_taken` on «Новая родительская область». |
| Never under itself or its descendant | `lib/areaTree.test.ts`; `AreaDialogs.test.tsx` | the picker leaves out the node, its subtree and its parent; `409 move_into_own_subtree` on the picker. |
| Never leaves its project | `lib/areaTree.test.ts` | the picker lists one project's tree only; the rest is backend. |
| Only an empty leaf is deleted | same | destructive confirmation text; `409 not_empty` in the dialog `Alert`; on `204` the parent selected and focused, notice «Область «…» удалена.». |
| A leaf deleted while a child is put under it | backend; the refusals land as above | — |

### 10.6 US-6

| Scenario | Test file | Asserts |
|---|---|---|
| Created with or without an owner | `components/AreaDialogs.test.tsx` | «Владелец»: «Наследовать от родительской области» preselected, then the active teams; `ownerTeamId: null` or the team's id sent. |
| Inherits from the nearest owned ancestor | `pages/AreasPage.test.tsx` | «API» shows «Платформа» + «унаследовано», the cell's accessible text «Платформа унаследовано»; «По умолчанию» on default areas. |
| Other modules get the same answer | backend | — |
| Re-owned | `components/AreaDialogs.test.tsx` | preselection, helper «Сейчас: …», «Сохранить» disabled until changed; the tree refetched; notice «Владелец области «…» изменён.». |
| Owner cleared | same | «Наследовать…» sends `null`. |
| A move re-resolves inherited owners | `pages/AreasPage.test.tsx` | after the move the tree is read again and the refetched owners shown. |
| The owner is a team of the area's own project | `components/AreaDialogs.test.tsx` | the picker lists the project's teams only; `team.not_found` lands in the dialog `Alert`. |
| A team's default area stays with its team | same | `409 is_team_default` with `params.team` in the dialog `Alert`, for the owner and the delete dialogs. |
| The root's owner is fixed | `pages/AreasPage.test.tsx` | no «Изменить владельца» on the root. |
| A re-own or delete and a default change at once | backend | — |

### 10.7 US-7

| Scenario | Test file | Asserts |
|---|---|---|
| Deleted after a confirmation | `components/DeleteTeamDialog.test.tsx`, `pages/TeamsPage.test.tsx`, `TeamPage.test.tsx` | the text verbatim, «Удалить команду «Платформа»? Её области перейдут команде «Пример проекта». Восстановить команду будет нельзя.»; error-coloured primary, focus on «Отмена»; on `204` the row leaves (the list) or «Команды» opens (its page); the notice or, for the chosen team, none (DC-7). |
| Declining changes nothing | same | «Отмена», Escape, «Закрыть»: no request. |
| The first team cannot be deleted | `pages/TeamsPage.test.tsx`, `TeamPage.test.tsx` | no «Удалить команду»; `409 first_team_cannot_be_deleted` (a direct request) in the dialog `Alert`. |
| The deleted team's name is free, its area's is not | `pages/TeamsPage.test.tsx` | `409 area.name_taken` on «Название» of «Новая команда». |
| A deleted team answers not found | `pages/TeamPage.test.tsx` | «Команда не найдена». |
| Other modules read the deleted status | backend | — |
| A re-own and the team's deletion at once | backend; `team.not_found` placement in `AreaDialogs.test.tsx` | — |

### 10.8 US-8

| Scenario | Test file | Asserts |
|---|---|---|
| The switcher lists active organizations, projects and teams | `context/useOrganizationsContextSource.test.tsx`; `app/AppLayout.organizations.test.tsx` | the three levels in the order received, «Все организации» last, nothing deactivated or deleted (the API leaves them out). |
| The default team is the first team | `context/resolveSelection.test.ts` | a project choice selects its first team. |
| «Все организации» opens the list | `app/AppLayout.organizations.test.tsx` | the list opens; the switcher keeps «Альфа». |
| «Нет организаций» leads to the list | same | the link with no project or team level; «Организаций пока нет». |
| Settings through the shell's navigation | same; `app/navigation/visibility.test.ts` | «Меню настроек» holds «Организация» → «Общие», «Проекты», «Процесс» and «Проект» → «Общие», «Команды», «Области»; «Настройки» opens the organization's «Общие». |
| The address sets the switcher | `resolveSelection.test.ts`; `app/routes.organizations.test.tsx` | «Бета» chosen, the settings of «Пример проекта» in «Альфа» opened: «Альфа», «Пример проекта», its first team, written to storage; a deactivated organization's address: «Организация не найдена», the switcher and the storage unchanged; mod-001's process address sets its organization. |
| A project switch on a project-level page | `app/navigation/switchTarget.test.ts`; `app/AppLayout.organizations.test.tsx` | every row of design § 2.2 as an outline; in the shell «Аналитика» opens its «Общие», «Бета» its first project's «Общие» or «Главная» when it has none (after its project list is read). |
| A page addressing one team lands on its entry's page | same | from a team's page, a project choice opens «Команды» of the new project; a not-found frame switches as its page would. |
| On an organization-level page the lower levels do not move it | same | project and team choices keep organization «Общие»; an organization choice opens the new «Общие». |
| Remembered across a reload | `context/storage.test.ts`; `useOrganizationsContextSource.test.tsx` | the key `mustask.context.<sub>`; a remount shows «Альфа / Пример проекта / Платформа»; storage that throws or holds garbage remembers nothing and breaks nothing. |
| A remembered selection that is gone falls back | `resolveSelection.test.ts`; `app/shell/ContextSwitcher.levels.test.tsx` | team deleted → first team; project deactivated → «Аналитика» and its first team; organization deactivated → «Бета», its first project, its first team; the notice text and its omissions («Выбрано: Бета.»); no notice on a first visit or for an address; a failed level keeps its id. |
| Own changes without a reload | `app/AppLayout.organizations.test.tsx` | create «Дельта», rename «Бета» → «Бета 2», add «Ядро»: the levels list them with no remount; choose «Ядро», delete it: the fallback and its notice; deactivate «Альфа» from its «Общие»: the deactivated list open, the switcher on «Бета», the notice the fallback's only. |
| The lists fail to load | `app/shell/ContextSwitcher.levels.test.tsx`; `useOrganizationsContextSource.test.tsx` | the organization level's failed row (unchanged) and «Повторить» recovering; projects and teams loading and failed rows, skeleton counts, `role="status"` name, «Повторить» calling `retry('project' \| 'team')`, the text hidden below 1024 px. |

### 10.9 Unit tests

`api/organizationsApi.test.ts` (paths re-encode ids, bodies as the contract,
`204` handling), `paths.test.ts` (the nine patterns, the builders' encoding),
`lib/validation.test.ts`, `lib/refusals.test.ts` (every row
of design § 4.4), `lib/notFoundLevel.test.ts`, `lib/areaTree.test.ts`,
`context/addressContext.test.ts` (prefixes, `deactivated`, trailing segments,
encoded ids), `shared/api/errorCodes.organizations.test.ts` (both
directions against the catalog), `shared/api/tokenClaims.test.ts` gains the
`subject` cases (an added `it`, nothing edited).

### 10.10 Existing tests that change, and only these

| File | Test | Change |
|---|---|---|
| `app/navigation/visibility.test.ts` | "shows only this task's pages" | «Проект» joins the labels hidden only on the placeholder context: its section now holds built pages. |
| `app/navigation/visibility.test.ts` | "registers only built pages…" | the list becomes «Главная», «Общие», «Проекты», «Процесс», «Общие», «Команды», «Области», «Языки», and the title names them. |
| `app/navigation/visibility.test.ts` | "holds «Процесс» under «Организация»…" | the section's entries are «Общие», «Проекты», «Процесс». |
| `app/navigation/visibility.test.ts` | "is shown once the context is real, and «Настройки» then opens it first" | «Настройки» opens `/organizations/org-1/settings/general`; the title says «Общие». |
| `app/AppLayout.process.test.tsx` | "«Настройки» opens «Процесс» first, by model order" | it opens the organization's «Общие» now; the title follows. |

Every other existing test passes unedited: `renderShell` keeps the shell's
tests on the placeholder dataset (§ 2.4), the default empty organization
list keeps the real-route tests' requests as they are, the new switcher
rows render only with `levelStatus`, and `retry` keeps its call count. After
pass 9, `git diff --stat main -- '*.test.*'` shows edits to these two files
and `tokenClaims.test.ts` (added cases), plus new files.

### 10.11 Left to a browser (QA, stage 7)

The tables and the treegrid scrolling inside their `Paper` at 640 px, the
facts panel stacking below 640 px of content, the header's commands moving
into «Ещё действия», truncation with tooltips, the visible focus in the
treegrid, a cold reload remembering the selection, the realm's create right
after the D-j stand reset.

## 11. E2E (D-l)

E2E is consented (D-l), so 05-frontend.md § 7's question is answered. The
developer writes and compiles the specs in pass 10 (`npm run build`'s
`tsc -b` covers `e2e/`, plus `npx playwright test --list`); the run that
counts is QA's, on a cold stand, **after the D-j stand reset** (the realm
import gained the create right), with `stand.json` quoted in `qa.md`.

`e2e/support/organizationsApi.ts`: create an organization, project and team,
deactivate and restore, list, through the same token exchange
`dictionariesApi.ts` uses (its own copy, so specs 17–18's helper is
untouched). Names carry a run suffix: names are unique instance-wide and
nothing is purged (BR-M1-1), so every spec deactivates what it created in
`afterAll`.

**New `21-organizations.spec.ts`** (the fixture user is in
«Администраторы», whose realm role `admin` carries the create right, D-i):

| # | Case |
|---|---|
| 21.1 | Happy path, organization: the switcher's «Все организации» → «Создать организацию» → its «Проекты» with «Пока нет проектов», the notice, the switcher on it, «Меню настроек» with «Общие», «Проекты», «Процесс». |
| 21.2 | Happy path, project to areas: «Новый проект» with a key; its «Общие» → «Процесс» opens mod-001's screen with «Меню настроек» and the trail «Настройки / Организация» (mod-001 D-15); «Команды»: add «Платформа», change its default area; «Области»: create «API» under «Платформа» (inherited owner «унаследовано»), move it, delete a leaf; delete «Платформа» with the verbatim confirmation, its areas now the first team's. |
| 21.3 | Deactivate and restore: the project, then the organization; its address answers «Организация не найдена» while the switcher keeps its selection; restored, it is listed and opens again. |
| 21.4 | Failure: a taken organization name answers «Название «…» уже занято» on the field; the key `prm` is refused in the browser with no request. |
| 21.5 | Failure: the switcher's projects read refused at the network layer (`page.route(…).abort()`, as 19.8 and 20.3 do): «Не удалось загрузить проекты» + «Повторить», then the projects after the route is released. Nothing on the session flow is stubbed. |
| 21.6 | A throwaway user in no group: no «Создать организацию», the list visible; a direct `POST /api/organizations` with its token answers `403` «Недостаточно прав». |
| 21.7 | Remembered selection: a reload keeps it; after the chosen organization is deactivated through the API, a reload shows the fallback notice. |
| 21.8 | The treegrid by keyboard in a browser: arrows, Enter, Shift+F10, Escape, with a visible focus ring. |

**Reworked (P-5):**

- **Spec 20** opens an organization its `beforeAll` creates (its process is
  seeded at creation) instead of the demonstration id D-h removed; the
  expected definition is unchanged. 20.2's unknown ids stay random.
- **Spec 19** asserts the placeholder dataset in its switcher cases (19.4,
  19.5, 19.10, 19.11, 19.12) and expects «Настройки» to open «Языки» (19.3,
  19.8, 19.12 among others), which holds only with no organization context.
  Its switcher cases run on an organization, project and team the case
  creates (long names for 19.4 and 19.11), set by opening that
  organization's «Общие»; its «Языки» cases run with no active organization:
  the setup deactivates the active ones through the API and restores them in
  `afterAll`. The exact list of edited cases is settled against
  `--list` in pass 10, and the QA run proves it.

## 12. Order of work: passes that each end green

"Green" means `npm run lint`, `npm test` and `npm run build` in
`src/frontend`, plus `bash scripts/verify.sh`. Every commit builds, with an
explicit path list; before each commit the diff and its message are checked
for the reference tracker's name (C-3), the search term never written into a
file. Each pass ends with a `[dev]` Log line naming its commits. No stand is
needed until QA: every pass runs on MSW.

0. **Set-up.** `bash scripts/warm-worktree.sh`; the open points of § 13
   decided in `status.md`.
1. **Pass 1 — the contract layer, nothing visible.** `shared/api/errorCodes.ts`
   and its two-way drift test; the slice's `paths.ts`, `permissions.ts`,
   `api/*`, `lib/*`, `context/addressContext.ts`, `testFixtures.ts`,
   `testApi.ts`, with their unit tests.
2. **Pass 2 — `shared/ui`.** `TreeRegistry` with its tests and the names
   scan. Then, each as its own commit with its tests, the additive props of
   P-7 and P-3 (a).
3. **Pass 3 — the shell seam, still on the placeholder (D-n, D-o).**
   `workContext.ts`'s extension; `ContextSwitcher`'s rows and notice with
   `ContextSwitcher.levels.test.tsx`; `switchTarget.ts` and its test;
   `renderShell`'s default and `placeholderContext.ts`'s comment; mod-015's
   design § 5.4, § 5.7 amended (§ 4.7). `AppLayout` untouched; every existing
   test unchanged.
4. **Pass 4 — the context source, not yet mounted.** `tokenClaims` and the
   optional `subject` (P-4 (a)); `context/storage.ts`, `resolveSelection.ts`,
   `useOrganizationsContextSource.ts` with their tests (the bulk of US-8, on
   `testApi.ts` under a `MemoryRouter`).
5. **Pass 5 — organizations.** `OrganizationsSection`, `FactsPanel`,
   `NameDialog`, `RestoreButton`, the two organization pages, their tests.
6. **Pass 6 — projects.** `CreateProjectDialog`, the two project pages,
   their tests.
7. **Pass 7 — teams.** `RowActionsMenu`, `DefaultAreaDialog`,
   `DeleteTeamDialog`, `TeamsPage`, `TeamPage`, their tests.
8. **Pass 8 — areas.** `AreaDialogs`, `AreasPage` on `TreeRegistry`, their
   tests.
9. **Pass 9 — into the shell.** `routes.tsx`; `model.ts` with the
   `visibility.test.ts` edits in the same commit; the `AppLayout` swap with
   the default empty organization list, the `AppLayout.process.test.tsx`
   edit and `app/AppLayout.organizations.test.tsx` and
   `app/routes.organizations.test.tsx` (US-8 through the real shell, mod-001's
   trail). The § 10.10 diff check.
10. **Pass 10 — E2E and closing.** `organizationsApi.ts`, spec 21, specs 19
    and 20 reworked; `npx playwright test --list`; frontend.md's
    `TreeRegistry` line; as-built notes appended here; a last C-3 search over
    the branch diff and its messages; hand over to stage 7 (frontend QA in a
    separate session, which owes the D-j reset and the cold-stand run).

## 13. Open points for the orchestrator

Each has a recommended option. If one is still undecided when the pass that
needs it starts, that pass goes ahead with the recommendation and records it,
as mod-001's plan did with its P-2.

- **P-1. How the source knows an address's context is found (R-1).**
  - **(a) recommended:** one rule for every page — the level's active list
    holds the id — for this task's pages as for mod-001's «Процесс». No
    extra request (the lists are the switcher's own), no channel from pages
    to the source, and mod-001's request-count tests stay as they are. Con:
    design § 3.4 says this task's pages are found "by their first read"; the
    two differ only while a list is stale (another user's change not yet
    refetched), when the page answers by its own read and the switcher keeps
    its selection until the next read.
  - **(b):** this task's pages report their first read to the source through
    a slice-internal channel, other pages use the lists. Matches the
    wording; two code paths, and a page/source coupling to test.
  - Pass 4 needs it.
- **P-2. Where the fallback notice renders (R-2).**
  - **(a) recommended:** the source carries it (`fallback`,
    `dismissFallback`) and the switcher renders it as mod-015 § 5.4's row,
    within D-n's amendment of § 5.4/§ 5.7. The notice shows on any page,
    «Главная» and «Языки» included, which the US-8 reload scenario needs.
  - **(b):** a slice component mounted around every route of `AppLayout`'s
    group through `routes.tsx`. It moves mod-015's and mod-001's routes
    under a slice's layout, a slice owning shell-wide UI.
  - `AppLayout` itself is closed by D-o. Pass 3 needs it.
- **P-3. «Команда не найдена» and the settings navigation (R-3).**
  - **(a) recommended:** `NotFound` gains an optional prop that skips its
    not-found registration, used only by a team's page for the team level,
    so «Меню настроек» stays with «Команды» `true`, as design § 12 says.
    Additive in `shared/ui`, every current caller unchanged.
  - **(b):** keep the shell's rule — every not-found frame drops the
    settings navigation — and a designer's delta narrows design § 12.
  - Pass 2 needs it.
- **P-4. The token subject for `mustask.context.<sub>` (R-4).**
  - **(a) recommended:** `decodeAccessToken` gains `subject`,
    `AuthContextValue` an optional `subject`, and `AppLayout` passes it to
    the source with the swap. Two additive edits outside the slice
    (`shared/api`, the auth slice); one token read, as today.
  - **(b):** the source reads the token itself through `shared/api/session`
    (asynchronous, no auth edit, a second read path).
  - **(c):** one key per browser, `mustask.context`: deviates from design
    § 3.4 and mixes users on a shared browser.
  - Pass 4 needs it.
- **P-5. E2E specs 19 and 20 are reworked by this task (§ 11).** D-h removed
  spec 20's organization, and the real switcher replaces spec 19's
  placeholder names and changes where «Настройки» lands.
  - **(a) recommended:** rework both here, as mod-015 reworked 04, 05 and
    14; QA's cold-stand run covers the whole suite.
  - **(b):** a follow-up line, the broken cases skipped until then: the
    suite would not be green at this task's gate-3.
  - Pass 10 needs it.
- **P-6. Other users' changes when the window regains focus (R-5).**
  - **(a) recommended:** the switcher's three lists set
    `refetchOnWindowFocus: true` on their own queries (the slice's, no
    `app/` edit), as design § 3.1 says.
  - **(b):** leave the global default; other users' changes show on a
    reload, and a designer's delta narrows § 3.1.
  - Pass 4 needs it.
- **P-7. Three optional props on shared patterns (R-6).**
  - **(a) recommended:** `FormDialog.submitDisabled`,
    `ConfirmDialog.cancelAutoFocus`, `PageHeader.viewsLabel`, each additive
    with its test; every current caller renders as before. Every later
    rename dialog, confirmation and tabbed registry reuses them.
  - **(b):** none; the pages reach into the patterns' markup or the design
    loses the disabled primary, the safe focus and the tabs' names.
  - Pass 2 needs it; the default applies if it is undecided by then.

Also for the orchestrator, not a decision: mod-015's `status.md` gets its
line about the § 5.4/§ 5.7 amendment (D-n, "both status files"), which is
outside this task's write zone.

## 14. As built (stage 6b, 2026-10-04)

The open points of § 13 were still undecided in `status.md` when pass 1
started, so every pass took the recommended option: P-1 (a), P-2 (a),
P-3 (a), P-4 (a), P-5 (a), P-6 (a), P-7 (a). Commits, in order: 4fec3d4
(pass 1), d5eff89 and 9409ce8 (pass 2), 9ee75cf (pass 3), 6cb79f1 (pass 4),
13eead8 (passes 5 and 6), ec0e519 (passes 7 and 8), 0fe4d89 (pass 9),
b4f1afb (pass 10). Each built, linted and passed `npm test` before it was
made.

Where the code departs from the text above, and why:

1. **Two pairs of passes share a commit** (5 with 6, 7 with 8):
   `testRender.tsx` mounts every page of the slice, so a flow test can move
   to the page an action opens; the pairs were green together.
2. **No `removeQueries` on a deactivation or a team's deletion** (§ 3.4).
   The read of the object is marked stale with `refetchType: 'none'`
   instead: removing it made the page on screen read itself again and flash
   its not-found frame before the move to the deactivated tab or «Команды».
   The next visit reads it again.
3. **«Все организации» closes its menu.** mod-015's footer item navigated
   but left the switcher's menu open (modal), which nothing showed while the
   item was hidden. `ContextLevel`'s `footer` became a render prop given
   `close`; the switcher is this task's to extend under D-n.
4. **The create right is read through `useAccessCheck()(code,
   'instance')`**, the shell's one check, which answers `can(code)` for an
   instance code — the same answer as `useAuth().can`, without the slice
   importing `features/auth` (§ 1, dependency direction).
5. **`NotFound`'s new prop is `keepNavigation`** (P-3 (a)); with it the
   frame registers nothing with the shell. `FormDialog.submitDisabled` also
   ignores Enter in a field.
6. **The slice's notice ignores a click elsewhere** (`clickaway`): the
   click that raises the next notice would otherwise close it at once. It
   still closes after 6 s, by its close button, or for the fallback notice
   (DC-7).
7. **The address sync covers the organization and project levels.**
   `addressContext` also decodes the team-level prefix `…/teams/:t/…`, but
   no page lives there yet, so the source does not sync a team from it; the
   first page that does adds the teams read of the address's project.
8. **A team's page reads the project's teams** once its own read succeeds,
   for the first team's name in «Удаление команды» (US-7's text).
9. **«Переименование области» has no helper**: design § 11.5 gives none.
10. **E2E** (§ 11): spec 21's eight cases; spec 20 creates and deactivates
    its own organization; spec 19 runs in two groups — the «Языки» cases
    with every active organization deactivated and restored after, the
    switcher cases on two organizations the group creates («Пример
    организации 1 …» and «… 2 …», the first team named after its project),
    with 19.5's request and reload assertions turned to US-8's (the chosen
    organization's projects and teams are read; a reload keeps the choice)
    and its «Языки» tab order gaining the organization and project entries.
    Typed by `npm run build` and listed by `npx playwright test --list` (46
    tests in 20 files); not run here — QA's run on a cold stand after the
    D-j reset is the one that counts.

Tests: 1026 in 98 files under `npm test` after pass 9 (681 in 68 before
this stage). Existing tests edited: `visibility.test.ts` (4 tests) and
`AppLayout.process.test.tsx` (1), plus one added case in
`tokenClaims.test.ts` — the § 10.10 check holds.
