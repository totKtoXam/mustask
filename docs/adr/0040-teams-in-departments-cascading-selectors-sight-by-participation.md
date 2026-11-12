# ADR-0040: Place every team in one department, cascade the context selectors, and let a user see only what they take part in

- Status: proposed
- Date: 2026-11-10
- Context: `doc-016-teams-under-departments`, stage 1 (business analysis): [spec](../features/doc-016-teams-under-departments/spec.md) BR-TD-1..-17, OQ-1..OQ-7; the human's stand feedback on mod-019, 2026-11-10. **Supersedes in part** [ADR-0028](0028-matrix-organization-model.md) rule 1 (`ORGANIZATION ||--o{ TEAM` and `AREA_TEAM`), [ADR-0031](0031-typed-area-tree.md) rule 4 (area–team links), [ADR-0032](0032-object-pages-and-navigation-context.md) § 5 (the selectors' offers and «Остальные»); **amends** [ADR-0029](0029-scoped-effective-permissions.md) § 6 steps 3-4, § 8 item 4 and [ADR-0038](0038-scoped-grants-project-managers-and-place-decides-creation.md) § 1 (team creation). Nothing else in them changes. The options marked "per OQ-n" are the spec's recommendations; the human's answers at gate-1 fix them, and only the human sets `accepted`, at the gate of the line that implements it (`mod-NNN-teams-in-departments`).

## Problem

A team is the organization's (ADR-0028 rule 1) and links many-to-many to departments (ADR-0031 rule 4) and projects. The human wants a strict hierarchy: a team inside one department; four header selectors that cascade from the chosen parents, a lower choice filling in its parents; and a user who sees only what they take part in. ADR-0032 § 5 deliberately hides nothing and lists non-fitting values under «Остальные»; ADR-0029 shows every «Организация» project to every member, and feat-041's three visibility values were to narrow that per project. These accepted texts contradict the request, so a new ADR records the change (README, Process 3).

## Options considered

### Option 1: keep the model, filter the selectors only

- Pros: no migration, no ADR change beyond § 5.
- Cons: a team still has no single department, so "the teams of a department" and "choosing a team chooses its department" have no answer; sight stays organization-wide. Rejected.

### Option 2: a team as a fourth node type in the area tree

- Pros: one tree holds everything; moves, depth and names reuse the tree's rules.
- Cons: teams would enter the sibling name space beside projects, the tree lock would serialize team writes, and grants on nodes (mod-019 OQ-1 A forbids them on non-departments) would need a fifth rule. Rejected.

### Option 3: a team keeps its row and gains one required department; selectors cascade; sight by participation (chosen)

- Pros: smallest model change (one required reference replaces a link relation); the cascade follows from the references; one sight rule for every surface, reusing ADR-0029's set form.
- Cons: a migration that must guess for teams with several or no department links (OQ-1); a team link now discloses a project to the team's members (OQ-4 A); department «Менеджер» grants widen again (OQ-6 A).

## Decision

Option 3.

**1. A team stands in one department.** An active team references exactly one department of its organization, required, keyed (organization, department) onto the area row of type `Department`; a deleted team references none (its deletion clears it), so the database holds "active ⇒ a department" and a deleted team never blocks a department's deletion. The area–team relation is retired with its routes. A team move changes only that reference: memberships, grants, project–team links, owned items, sprints and orders stay. A department with an active team in it is not deleted (409 `organizations.area.has_teams`, checked after the department's `FOR UPDATE`). A team creation locks the tree lock and the department row `FOR SHARE`; a team move locks, in ADR-0031 rule 6's order, the tree lock and the two department rows `FOR SHARE` (tier 1), then the team row (tier 2); so each ends against a department's deletion in one order, with no server error. Project–team links stay many-to-many and may cross departments (OQ-2 A).

**2. Who creates and moves a team (amends ADR-0038 § 1, per OQ-6 A).** `organizations.team.create` is struck as `project.create` was: a team is created in a department by a holder of `organizations.area.write` on it. The department «Менеджер» also holds `organizations.team.write`, `.team.link` and `.team.grant` on the teams standing in its department. A move needs `organizations.area.write` on both departments. `organizations.team.delete` stays the administrator's. The creation route carries its place in the body and is decided as ADR-0038 § 3 decides project creation: at step 0, on the inventory's exception list, a non-admin getting 403 for any place they lack the code on (an unknown, deleted or foreign place included), the administrator «Область не найдена.» for an unknown one and 409 `organizations.area.type_not_allowed` for a project node or a sub-area. A move's destination is a body id (ADR-0029 § 7). A team code the department gives is decided on the locked team row, so a concurrent move changes the answer and never leaves a stale pass (as mod-019 BR-SA-13). Until this § lands (its own line), team creation keeps `organizations.team.create`, the administrator's, with the place required.

**3. The selectors cascade (supersedes ADR-0032 § 5's offers, per OQ-3 A).** The selection keeps its shape (organization, department, team, project) and the invariant: department ∈ organization; team ∈ department; project linked to the team when one is chosen, else standing in the department or linked to one of its teams. Offers are the visible objects under the chosen levels above; with organization «все», every organization of the user, under a heading each. A value chooses its organization and, for a team, its department; a project keeps a fitting department or takes its own. Any chosen value left unfitting resets with «Выбор сброшен: …»; a parent replaced by the value's own is not a reset. «все» at a level resets every lower level. A repair (a remembered selection, a change seen while open) first gives a chosen team its own department, then resets, least specific first, each lower value that no longer fits (spec BR-TD-18); this replaces the reset direction of ADR-0032 §§ 5-6. «Остальные» retires. §§ 1, 2, 4 and 6 of ADR-0032 stand; the address still names the most specific object, so a cross-organization choice opens that object's organization.

**4. Sight by participation (amends ADR-0029 § 6 steps 3-4, per OQ-4 A).** For a non-admin: a project is visible when a grant reaches it, the user is a member of a team linked to it, or its «Видим всей организации» is on; a team when the user is its member, holds a grant on it or on its department, or sees a project linked to it; a department when the user holds a grant on it or sees a team or project standing in it; an organization when the user is a member. A project node and its sub-areas are visible exactly when their project is. Step 3 gains the same gate for scopes T and A: a team or a department the user does not see answers as a missing one («Команда не найдена.», «Область не найдена.»), at § 8 item 4 and before any 403, in the path and in the `teamId`/`departmentId` query alike. Step 4 applies the organization role's bundle on every visible project, team and department, and on no hidden one. Team membership still grants no code: it adds sight, and the role then applies; a project–team link likewise gives no code but gives sight (ADR-0029's "no link widens access" reads "no link adds a code"). The project value is one boolean, off at creation; feat-041's three values are not built. No value hides a project from its linked teams' members: to restrict a project to its grantees, its node's «Наследовать доступ» is switched off, the value is off and its teams are unlinked. The set form gives, per request, the visible departments, teams and projects; every surface that names or counts them joins it before paging (ADR-0029 § 7).

**5. The upgrade.** Teams with one department link take it; several or none follow OQ-1; the dropped links and any created department are logged. Existing projects' value follows OQ-5. Additive migrations only; no merged migration is edited.

**Departures from `docs/conventions/*`:** none.

## Consequences

- **Simpler:** "the teams of a department" is one reference; the cascade needs no fit table across three link kinds; one sight rule replaces three visibility values and «Участники областей».
- **Harder:** every multi-object read joins three visible sets instead of one; the offers' read across up to 10 organizations must stay within 2 s; a team link changes who sees a project, so linking and unlinking become access-relevant writes.
- **Widens:** department «Менеджер» grants already given gain team codes on their department's teams (OQ-6 A).
- **Locked in:** one department per team; the cascade's invariant; participation as the default sight.
- **Revisit when:** departments nest (the cascade then has to say whether a sub-department's objects count); a team must belong to two departments; sight must be granted to groups.
- **Index:** when accepted, the index lines of ADR-0028, -0029, -0031, -0032 and -0038 get "amended by ADR-0040".
