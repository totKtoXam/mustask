# Frontend plan: scoped access (mod-019)

Stage 6a, L, 2026-11-09. Read: `spec.md` (BR-SA-1..19, OQ-1..11), US-1..US-8, `design.md` (D-1..D-13, OQ-D1, OQ-D2 as answered), `api-contract.md` (§§ 1-10), `plan-backend.md` §§ 6-8, `qa.md` (backend pass 2), `docs/conventions/frontend.md`, `docs/conventions/ui-kit.md`. Reference slice: `features/dictionaries`; the code lives in the existing `features/organizations` slice (design § 8: "one new slice part: `GrantsSection`"), with a small shared piece in `shared/lib`.

## 1. Decisions

| # | Decision |
|---|---|
| F-1 | **Object codes, not organization codes (BR-SA-18).** A code whose catalog `resourceScope` (from `contracts/access/role-bundles.json`) is `area`, `project` or `team` is asked of the **object's served `codes`**, never of the organization answer. `shared/lib/roleBundles.ts` wraps the JSON (instance table, org bundles, scoped bundles, catalog); `shared/lib/objectAccess.ts` gives `useObjectAccess(codes)`: an `AccessCheck` that answers an organization/instance code from the page's bound check and a narrower code from `codes` (absent until the object is read: fails closed). The drift test fails when any panel file asks the organization answer for a code whose scope is not the organization (a source scan, as `kitGuard`). |
| F-2 | **Pages gate their entry by the object's codes.** The routes of "Параметры" keep the organization *read* code in `OrganizationPage`; the page renders `AccessDenied` ("Доступ ограничен", design § 6 "Direct address") when the object's codes lack the write code. |
| F-3 | **Dialog lock** (`RequiresOrganizationCode`) gets a sibling `RequiresCode holds={…}` for object codes; a refused write still re-reads the object (F-5). |
| F-4 | **Bundle switch-over (tech-026 OP-7, ADR-0033/0038).** `src/test/organizationRoles.ts` and `shared/api/tokenClaims.ts` read `contracts/access/role-bundles.json` by a Vite JSON import (Vite resolves it outside the root; `resolveJsonModule` is on in `tsconfig.app.json` through `moduleResolution: bundler`). `organizationRoles.test.ts` is rewritten to compare with the JSON instead of the markdown tables. Public names (`ADMIN_CODES`, `checkFor`, `servedFor`, …) are kept so the ~40 test files keep compiling. |
| F-5 | **Re-read (design § 7).** Object codes live in the object's own query (`project`, `team`, `area` reads), so a re-read is an invalidation. A grant/switch write invalidates the object and its lists; a 403/404 under an object invalidates that object (extension of `useAccessWatcher`'s scoped-failure path); window refocus re-reads by the query client's default. One notice per object when the served codes differ from the last seen ones (`useObjectCodesNotice`). |
| F-6 | **D-4, the per-card flag on a shared board: no backend field is owed.** A card carries its project; `GET /projects` rows carry `codes` once per project (api-contract § 5). The board/backlog views derive `canMove` per card from the project list: `projectCodes.get(card.projectId)?.includes('workitems.workitem.write')`. Done in the second pass (§ 5). |
| F-7 | **Assignee picker (OQ-11).** `assignee-candidates` takes the optional `projectId`; the item form passes the item's project (the new-item form: the chosen project). |
| F-8 | **Grant candidates → `UserSearchField`.** `GrantCandidates.items: UserRef[]` is mapped to the field's `UserCandidate` shape in the API layer; the field is reused unchanged (new `UserSearchScope` kind `grant`). |
| F-9 | **No `shared/ui` part.** A plain MUI `Switch` + `FormControlLabel` is allowed (ui-kit § 10). Dialogs use `FormDialog`, `ConfirmDialog`, `InlineSelectField`; tables `Section` + `DataTable`. |

## 2. Slice layout (additions to `features/organizations`)

- `api/grantsApi.ts`: `GrantRow`, `GrantList`, `SwitchImpact`, and one function per route of api-contract §§ 2-3 and `getWritableAreaIds`; extensions of `Project`, `Team`, `AreaPage`, `AreaNode`, link rows (`codes`, `department`, `inheritsAccess`, `canUnlink`), `linkableFor` on the list reads.
- `hooks/grantQueries.ts`, `hooks/grantMutations.ts`: `useGrants(scope, id)`, `useGiveGrant`, `useChangeGrantRole`, `useRevokeGrant`, `useGrantCandidates`, `useInheritsAccess`, `useSwitchImpact`, `useWritableAreaIds`.
- `lib/grantTexts.ts` (design strings, plural forms of the count as a unit table), `lib/grantRefusals.ts` (error code → placement + Russian text).
- `components/GrantsSection.tsx`, `GrantDialogs.tsx` (give, change role, revoke), `InheritAccessControl.tsx`, `SwitchOffDialog.tsx`.
- `pages/AreaMembersPage.tsx` (department «Участники»), `pages/ProjectAccessPage.tsx` («Участники и доступ»); `TeamMembersSection` gains the «Роль в команде» column and the grants section below it.
- Routes/menus: `areas/:d/members`, `projects/:p/access`; entries in `app/navigation/addressModel.ts`.
- `api/errorCodes.ts`: four grant codes (the drift test compares with `contracts/errors/organizations.json`).

## 3. Screens and states (design.md)

| Screen | States |
|---|---|
| Grants section (department, project, team) | loading `TableSkeleton`, error banner with «Повторить», empty (three texts, manager line), after filter, not found, reader (no header command, no menu column), inherited muted rows with «Откуда», own row, 403 on a write |
| Grant, role, revoke dialogs | field errors, scope/type notice, grant gone, 403 → «Недостаточно прав», object gone → closes onto NotFound |
| Inherit switch + read-only fact, per node type | loading, error, busy, 403 |
| Switch-off dialog | loading, error with retry, count plural forms, zero, self-lockout, given-grants table (hidden at N = 0), 403 |
| «Создать проект» | org «Проекты» and department «Проекты», places by writable ids + `area.create`, absent with no place, races |
| Link tables / pickers | per-row `canUnlink`, `linkableFor` on the pickers |

## 4. Order of work (each step ends green: ESLint, `tsc -b`, Vitest on its files)

1. This plan. 2. Bundle switch-over and error codes. 3. API layer and hooks. 4. `objectAccess` and the existing pages moved to object codes. 5. Grants section, dialogs, pages, routes, menus, team page. 6. Switch control and dialog. 7. Create project by place, `canUnlink`, `linkableFor`. 8. Assignee `projectId`. 9. `status.md` Log.

## 5. Second pass (the items first deferred) and what stays open

| # | Item | Done in the second pass |
|---|---|---|
| X-1 | **Work-item and board surfaces read object codes.** | `useProjectCodes` (organizations barrel) turns `GET /projects` into a map from project id to codes. `ProjectAccess` (workitems) provides one project's codes to the work-item list, the item form and the card dialog, and holds the page on its skeleton until the rows are read. Boards: `useBoardAccess` (write and link: any shown project holds the code; reorder: the project's `boards.projectbacklog.reorder`, or the team read's `boards.backlog.reorder`). The per-card `canMove` of F-6 (D-4) is judged by the card's own project in `BoardColumns`, `AggregateBoardView` and, per group, `AggregateBacklogView`. The section entries and the route gates of the projects, work items, boards and backlogs ask the organization read every member holds (design § 3: the entries are present for everyone); the shell's «Параметры» entries read the chosen object's codes (`useObjectCodes`, `NavigationEntry.object`) and need the organization read too. Pages whose rows carry no codes (the organization's teams, the project list's link dialog, the tree's link of teams) ask the role floor (`RoleFloorAccess`, from `role-bundles.json`). The test doubles serve the organization answer with the organization-scope codes only, and the caller's codes on the project rows, the team read and the area read (`servedObjectCodes`). |
| X-2 | `useAccessWatcher` invalidates the object on a scoped 403/404 (F-5). | `scopedFailureOf` names the object (project, team or area, and whether the call was its own read); the watcher reads that object and the project rows again (`rereadObject`), except for the object's own failed read, which would loop. A role change also reads the project rows again: they were read under the old role. |
| X-3 | The BR-SA-18 guard. | `src/test/scopeGuard.test.ts` scans the production sources: a file that names a code whose catalog `resourceScope` is not organization must be registered with the file that provides the object's codes, and a stale entry fails too; negative fixtures prove it fails. |
| X-4 | `AreasPage` row commands limited to the writable ids. | The row commands and the dialogs' lock ask `useWritableAreaIds` per row. |
| X-5 | A 200-row grants list test. | `GrantsPages.volume.test.tsx`: 200 grants on a project, every row drawn, the filter narrows to 10, the row menu works. |

Still open, and why:

- **The team backlog table and the team-place project picker are coarse.** A team's backlog holds rows of several projects: the table's controls (write, link, reorder by drag) show when any shown project allows them, not per row, and the project picker of `CreateWorkItemDialog` on a team place is not narrowed to the projects the caller can write in. The server refuses what the caller may not do, and the refusal re-reads the codes (F-5). A per-row flag needs the row's project codes in the row model; it is a follow-up for the team views, not a correctness gap.
- **A holder of a grant on a single object** is under-offered on lists whose rows carry no codes (the role floor); the object's own page offers the grant's controls.

Done: F-1..F-9, X-1..X-5, and steps 1-9 of section 4.

## 6. Open items for the orchestrator or backend

- **O-1 (contract text, no code change).** api-contract § 5 and US-4 say the organization answer keeps "organization-scope codes only"; `GetOrganizationAccessHandler` still serves the whole organization-role bundle (`OrganizationRoleBundles.CodesOf(role)`; an administrator's answer lists `area.write`). The panel does not depend on either (F-1 reads object codes), so this is not a blocker; one of the two texts should change.
- **O-2.** `GET /projects` rows carry `codes` (nullable). F-6 relies on them for every active project, including ones on which the caller holds no grant. Confirm that is served for every row, not only for rows the caller has a grant on.
