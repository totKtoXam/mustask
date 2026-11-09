# Backend plan: scoped access (mod-019)

Stage 04, L, 2026-11-09. Read: `spec.md` (BR-SA-1..19, OQ-1..10 answered), stories US-1..US-8, `design.md` (D-1..D-13, OQ-D1, OQ-D2 as answered), ADR-0029 as amended by **ADR-0038** (`proposed`, written before any code; accepted by the human at gate-3), ADR-0027, ADR-0031, ADR-0033. Contract: `api-contract.md`. The module is large and the work is delivered in the steps of § 3, each ending green; the steps are the cut lines of the spec's Decomposition.

## 1. Decisions of this plan

| # | Decision |
|---|---|
| P-1 | **One role enum for the three scopes**: `GrantRole` = `reader`, `member`, `manager` (UI «Читатель», «Участник», «Менеджер»), stored as text; the scope is the table. Consistent with the organization codes `admin`, `member`, `viewer`. |
| P-2 | **Three tables, one per object kind** — `area_grants`, `project_grants`, `team_grants`: `(object_id, user_id)` primary key, `organization_id`, `role`, `granted_by`, `granted_at`. Composite keys `(organization_id, user_id)` onto the organization membership (**cascade**: removing the membership ends the grants, a concurrent write loses by the key) and `(organization_id, object_id)` onto the object's alternate key (cascade for areas, which are hard-deleted; teams are soft-deleted and their deletion handler ends the grants). A single polymorphic table was rejected: no foreign key to the object, no cascade. |
| P-3 | **`PermissionScope` becomes `PermissionSource`** (ADR-0029 § 2); a new `ResourceScope` (`Organization`, `Area`, `Project`, `Team`) is a property of every organization code in the catalog, per ADR-0029 § 4 as amended by ADR-0038. |
| P-4 | **The evaluator's single form is a pure domain function**: `EffectivePermissions.CodesOn(role, target scope, reaching grants)` — the union of the organization bundle (codes of the target's resource scope) and the bundles of the reaching grants (codes of the target's resource scope), additive, order-independent. Reach is decided by a read model (one statement over the caller's grants and the object's chain), not by the domain function. A project grant also reaches its node and sub-areas for area-scope codes (ADR-0038 § 2). |
| P-5 | **The route policy grows a resource scope**, not a second mechanism: `RequiresOrganizationPermission(code, ResourceScope scope)` carries the scope; the requirement resolves the route value of that scope (`areaId`, `projectId`, `teamId`) inside the organization. An id that does not resolve is evaluated against the organization bundle alone (ADR-0029 § 8 last paragraph, today's answer order), and the handler answers the not-found. A route that does not declare a scope behaves as today. |
| P-6 | **Self-grant is a 400** with its own code (`organizations.grant.self_grant`, field `userId`), the grant's `changed` conflict is a 409; both are in the contract. |
| P-7 | **The bundle contract** `contracts/access/role-bundles.json` is hand-written, compared with the domain by a test (domain wins on a mismatch: the test fails). Frontend switch-over is stage 6 (see § 5). |
| P-8 | **Lanes: no.** The grant use cases share one repository and one policy change; the one developer works them in order. The reference use case is `GiveGrant`. |

## 2. Slices (Organizations module)

| Slice | Route | Note |
|---|---|---|
| `GetGrants` | `GET /{areas,projects,teams}/{id}/grants` | one use case, scope in the query; project reads add the inherited rows |
| `GiveGrant` | `POST …/grants` | reference use case |
| `ChangeGrantRole` | `PUT …/grants/{userId}` | |
| `RevokeGrant` | `DELETE …/grants/{userId}` | |
| `SearchGrantCandidates` | `GET …/grant-candidates` | excludes the caller and the holders |
| `SetInheritsAccess`, `GetSwitchImpact` | § 3 of the contract | step 4 |
| `CreateProject` (place decides), `LinkTeamsTo*`, `Unlink*`, tree and project reads | §§ 4-6 | steps 5-6 |

Domain: `ResourceScope`, `GrantRole`/`GrantRoles`, `ScopedRoleBundles`, `ReachingGrant`, `EffectivePermissions`, `AreaGrant`, `ProjectGrant`, `TeamGrant` (factories validate; `Replace(role, by, at)`), `GrantErrorCodes` in `OrganizationErrorCodes`.

## 3. Delivery steps (each ends with a green build and its tests)

| Step | Content | Stories |
|---|---|---|
| 1 | Domain: `ResourceScope`, `PermissionSource` rename, catalog gains `ResourceScope`, the three `*.grant` codes, `GrantRole`, scoped bundles, evaluator (unit and property tests: order independence, idempotence, additivity, project-grant-on-subtree), migration `AddScopedGrants`, grant entities and configurations | US-1, US-3 core |
| 2 | The policy's resource scope (P-5) and the grant slices on the three scopes, with endpoint tests per scenario of US-1; the error codes; the bundle contract and its test | US-1, US-6 contract |
| 3 | Reach through the tree (single form read model for projects: the project node's chain), the inherited rows of a project's grants | US-2 (reads) |
| 4 | The switch's route, rules, impact read, locked-row decisions (BR-SA-13), the switch state in the node and project reads | US-2 |
| 5 | The route policy on every organization route: scopes declared, `boards.projectbacklog.reorder`, `boards.backlog.reorder` rescoped, the inventory test per scope, the performance test (statement count at 10 and 10 000 grants) | US-3, US-6 |
| 6 | Links under grants (`canUnlink`, filtered offers, both-end codes), project creation by place, the tree under project and department managers, the served codes per object and `writableAreaIds` | US-4 server, US-7, US-8 |

Order inside a step: migration → domain → slice → integrations; tests with the code, a comment on each linking the story criterion.

## 4. Migration `AddScopedGrants` (additive)

Three new tables, two composite foreign keys each, a plain index `(organization_id, user_id)` on each for "the caller's grants" (tens of rows among 10 000), no existing table touched. Down drops the three tables. A department's stored `inherits_access` stays as it is (BR-SA-6).

## 5. Notes for the later stages

- **Frontend (stage 6):** switch `src/frontend/src/shared/api/tokenClaims.ts` and `src/frontend/src/test/organizationRoles.ts` to import `contracts/access/role-bundles.json` at build time (Vite JSON import); `PermissionScope` in `src/frontend/src/shared/lib/access.ts` and `app/navigation/types.ts` becomes `instance` plus the four resource scopes (ADR-0029 § 2). Not done here.
- **Owners' notes at merge (P-10):** spec § Breaks lists them; the orchestrator writes them.
- **ADR-0038** is `proposed`; its text is the authority for BR-SA-16/-17. The code in steps 1-4 does not depend on its acceptance beyond the bundle cells it names.
- **No Keycloak change of any kind**; grants live in the MUSTASK database.

## 6. Refinements of the second pass (steps 4-6)

- **Where each second code is decided.** A route is scoped when it names one object of the code's scope (`{areaId}`, `{projectId}` or `{teamId}`); the scoped policy resolves that object. A route whose object is the *place in the body* (`POST /projects`, `POST /areas`) or whose rule needs *either* end of a link (both `DELETE` department–team links) declares `RequiresOrganizationPermissionDecidedInHandler(code, reason)`: a membership-only policy, a stated reason, and the handler decides at step 0 through `IOrganizationAccess.HoldsOnAsync`. The inventory test lists these four routes exactly, both ways.
- **Non-disclosure.** An unknown or malformed id is passed on as `Guid.Empty` (or the parsed guess), so a non-admin gets 403 and an administrator passes to the 404.
- **Set form.** `IOrganizationAccess.SetAsync` loads the caller's grants and the tree once per request (`AccessSet`); per-row `canUnlink`, `codes` on a project, team or area read and the filtered pickers read it, never a statement per row.
- **`boards.projectbacklog.reorder`** (Project scope) replaces `boards.backlog.reorder` on the project backlog reorder. Organization «Участник» and project «Менеджер» and «Участник» hold it, so nobody who could reorder loses it.
- **`writableAreaIds`** cannot be added to the tree, which is a bare array the panel consumes; it is a sibling read `GET /areas/writable-ids` (declared `organizations.area.read`), an array of ids computed from the set form. api-contract § 5 says so.
- **Deferred, with the reason.** (a) A department «Менеджер» creating a project and naming teams in the same request still needs the organization-level link codes (administrator only): the project is created, the link step stays with the administrator until a later change moves the team codes to the scoped form. (b) The per-card flag of a shared board (US-3, «moves only the cards the user may change») is not done in this pass: the card move is judged by the route's code on the project. The statement-count test at 10 and 10 000 grants is done (`ScopedAccessStatementCountTests`: the check takes 3 statements and the set form 4 at both sizes).
- **Timing budget.** `UnlinkTeamVolumeTests` passed alone and in the full run after the scoped check (it failed once in pass 1 on a loaded machine, 5.6 s); its 5 s budget is unchanged. `AggregateViewVolumeTests` failed once under a parallel run (9.8 s against 2 s) and passed alone in 2 m 9 s, as it did in the full run; it is machine load, not the check.

## 7. Status at the end of this pass

See the Log in `status.md`: what is implemented, what waits for the next steps.
