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
| F-6 | **D-4, the per-card flag on a shared board: no backend field is owed.** A card carries its project; `GET /projects` rows carry `codes` once per project (api-contract § 5). The board/backlog views derive `canMove` per card from the project list: `projectCodes.get(card.projectId)?.includes('workitems.workitem.write')`. See § 5 (the part not done in this pass). |
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

## 5. Deferred (stated at the end of the stage in the Log and the report)

Anything not marked done in the Log is deferred; the report names each item and the reason.

## 6. Open items for the orchestrator or backend

- **O-1 (contract text, no code change).** api-contract § 5 and US-4 say the organization answer keeps "organization-scope codes only"; `GetOrganizationAccessHandler` still serves the whole organization-role bundle (`OrganizationRoleBundles.CodesOf(role)`; an administrator's answer lists `area.write`). The panel does not depend on either (F-1 reads object codes), so this is not a blocker; one of the two texts should change.
- **O-2.** `GET /projects` rows carry `codes` (nullable). F-6 relies on them for every active project, including ones on which the caller holds no grant. Confirm that is served for every row, not only for rows the caller has a grant on.
