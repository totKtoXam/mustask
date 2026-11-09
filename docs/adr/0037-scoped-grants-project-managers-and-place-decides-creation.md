# ADR-0037: Let the place decide who creates a project, let a project's manager run its subtree, and let a department's manager run the department's projects

- Status: proposed (written at mod-019 stage 4; only the human sets `accepted`, at mod-019 gate-3, as ADR-0031 was)
- Date: 2026-11-09
- Context: `mod-019-scoped-access`, stage 4 (backend plan). The human's gate-1 answers of 2026-11-06 (OQ-3 B, OQ-4 B, OQ-8 A, OQ-10) and the spec's BR-SA-16, BR-SA-17 and § Breaks: [spec](../features/mod-019-scoped-access/spec.md), [plan-backend](../features/mod-019-scoped-access/plan-backend.md), [api-contract](../features/mod-019-scoped-access/api-contract.md). **Amends [ADR-0029](0029-scoped-effective-permissions.md) §§ 4, 6, 7 and 12 and its mod-019 delivery bullet.** ADR-0029 stays accepted; its sections are read as this ADR says where it names them, and through [ADR-0031](0031-typed-area-tree.md) rule 7 (no area–project links: "a linked area" means the project's node and its ancestors).

## Problem

ADR-0029 § 4 gives a project's «Управление» role only project codes, makes `organizations.project.create` an organization code held by the administrator alone, and gives an area's «Управление» role no project code at all. Three answers of the human at gate-1 change that, and each touches the matrix that every route, the generated test matrix and the bundle contract are built from:

- a project's manager manages the sub-areas of its own project (OQ-3 B);
- a department's manager creates projects in the department (OQ-4 B), and then must be able to finish what it began on the project it reaches (OQ-8 A);
- the scoped roles are named «Читатель» / «Участник» / «Менеджер» in every scope, the team gaining the lowest role (OQ-10).

These are changes to a matrix ADR-0029 accepted, so they are recorded as a decision and not slipped in as a plan item.

## Options considered

### Option 1: leave the matrix; the creator of a project becomes its project manager (OQ-8 C)

- Pros: widens nothing already granted; one more grant row per creation.
- Cons: a department manager would not manage a project another manager created in its department; the answer of the human was A. Rejected by the human.

### Option 2: keep `organizations.project.create` as an organization code held by the administrator, and a department manager uses a second code

- Pros: no change to the registry.
- Cons: the place is where a project lives; two codes decide one operation, the order of answers needs a rule, and a department manager would hold a code on the organization scope that no other scoped role holds. Rejected (OQ-4 B).

### Option 3: amend § 4 as the human decided (chosen)

- Pros: one code decides creation, by the place; reach does the rest; the bundles stay fixed sets.
- Cons: department «Менеджер» grants already given widen (OQ-8 A); a project's manager holds a code of another scope on a subtree (the evaluator's reach rule grows by one path).

## Decision

Option 3.

**1. § 4, the matrix.** Three changes, and the role names:

| Change | Text |
|---|---|
| Project «Менеджер» on its subtree (OQ-3 B) | `organizations.area.write` is held by the project role «Менеджер» on its project's node and on every sub-area below it, **whatever the sub-areas' switches** (the switch cuts only what comes from above). It does not hold it on the department, so it neither moves nor switches its own project's node. The node is still renamed and deleted only with the project (`409 organizations.area.root_is_fixed`). |
| `organizations.project.create` struck (OQ-4 B) | The code is not registered. A project is created in a department by a holder of `organizations.area.write` on it, and at the top level by `organizations.area.create`; the place decides. `organizations.project.write` keeps rename, description and default team. |
| Department «Менеджер» on its projects (OQ-8 A) | The department role «Менеджер» also holds `organizations.project.write`, `organizations.project.link` and `organizations.project.grant` on every project that its grants reach through the tree. It follows reach: a project node switched off, or a top-level project, gets none of it from the department. `organizations.visibility.write` stays the project role's (feat-041). **This widens grants already given.** |
| Names (OQ-10) | The stored roles of a department, a project and a team are the same three: `reader`, `member`, `manager` (UI «Читатель», «Участник», «Менеджер»). ADR-0029 § 3's `editor`, `planner` and `reader/manager` rows are replaced; no shipped code names them. Team «Читатель» holds no code beyond what every organization member reads; it only lists the person. Team `member` is ADR-0029's «Планирование», `manager` its «Управление». |

Matrix rows after this ADR (only the cells that change; `R`, `M`, `G` are reader, member, manager; ADR-0029 § 4's other cells stand):

| Code | Scope | Department R M G | Project R M G | Team R M G |
|---|---|---|---|---|
| `organizations.area.write` | A | – – + | – – +, on the project's node and sub-areas only | – – – |
| `organizations.area.link`, `.area.grant` | A | – – + | – – – | – – – |
| `organizations.project.write`, `.project.link`, `.project.grant` | P | – – +, on projects the grant reaches | – – + | – – – |
| `organizations.project.create` | | struck | struck | struck |
| `boards.projectbacklog.reorder` | P | – – – | – + + | – – – |
| `boards.backlog.reorder` | T | – – – | – – – | – + + (and every organization `member`, as today) |

**2. § 6, reach.** One path is added. A **project grant reaches its project and, for codes of resource scope Area only, the project's node and every sub-area below it, whatever their switches**; it never reaches the department above. The single, set and inverse forms, and their parity test, carry the path. Everything else in § 6 stands, as ADR-0031 rule 7 reads it: the grants that reach project `p` are those held on `p` plus the department grants that reach `p`'s node (the project node's «Наследовать доступ» on, the department its parent), and a sub-area is reached through its parent while each switch on the way is on. A department's own stored switch never takes effect.

**3. § 7, the creation route.** `POST /api/organizations/{organizationId}/projects` keeps its path. Its addressed object is the place, carried in the body (mod-018 api-contract § 4), so it is on the inventory's exception list with that reason and is decided at step 0, before any lookup: a non-admin gets 403 `organizations.access.forbidden` for any place they lack the code on, an unknown, deleted, foreign or non-department place included, so nothing is disclosed. The administrator gets `organizations.area.not_found` for an unknown place and 409 `organizations.area.type_not_allowed` for a project node or a sub-area. The project node is still created in the same transaction. The creator reaches the new project through the department grant.

**4. § 12, the matrix and the inventory.** The generated matrix gains the cells above: project roles on the node and sub-area routes, department «Менеджер» on the project routes. The `POST /projects` exception is listed with its reason. The **bundle contract** (`contracts/access/role-bundles.json`) also carries the instance role→code table (ADR-0033), and replaces `src/frontend/src/test/organizationRoles.ts` (tech-026 OP-7) and the mirror `src/frontend/src/shared/api/tokenClaims.ts`; a contract test compares it with the domain.

**5. The mod-019 delivery bullet.** "`organizations.project.create` split from `.write`" is replaced by "`organizations.project.create` is not registered; the place decides (§ 3)". The rest of the bullet stands.

**Departures from `docs/conventions/*`:** none.

## Consequences

- **Simpler:** one code per creation; a department manager runs its department's projects without a second grant; the fixed-bundle model and the additive evaluator are untouched.
- **Harder:** the evaluator has one more reach path (project grant onto its node's subtree) and its tests one more column; the generated matrix grows by the project-on-node and department-on-project cells.
- **Widens:** every department «Менеджер» grant already given now holds `project.write`, `.link` and `.grant` on the projects it reaches. mod-019's `status.md` and the owners' of mod-016, feat-028 and feat-043 get a note when it merges (spec § Breaks): their "administrator only" wording for project settings and links no longer holds.
- **Locked in:** the three stored role names; the place as the decision of creation; the project grant's subtree path. A grant on a project node or a sub-area stays impossible (OQ-1 A).
- **Revisit when:** a project manager must also move its own node (it would need the area code on the department), or per-code grants are asked for (ADR-0029 Option 4).
- **Index:** when accepted, ADR-0029's index line gets "amended by ADR-0037".
