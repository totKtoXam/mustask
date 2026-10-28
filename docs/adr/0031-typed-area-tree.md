# ADR-0031: Make the organization's area tree a typed single tree whose project nodes are the projects' places in it

- Status: proposed
- Date: 2026-10-28
- Context: `mod-018-area-workspace`, rework to the human's typed tree (spec Decisions of 2026-10-27, BR-AW-0..15, OQ-3..OQ-13 as resolved at gate-1 on 2026-10-28; review TR-2): [spec](../features/mod-018-area-workspace/spec.md), [design](../features/mod-018-area-workspace/design.md), [api-contract](../features/mod-018-area-workspace/api-contract.md). Supersedes [ADR-0028](0028-matrix-organization-model.md) rules 2, 3, 7 and 8 and [ADR-0029](0029-scoped-effective-permissions.md) § 6 **in the parts the typed tree changes**, and nothing else in them. It goes to the human at the nearest gate ([README](README.md), Process 2); only the human sets `accepted`.

## Problem

ADR-0028 modelled the organization's areas as free, untyped nodes joined to projects by many-to-many links, and retired the
work item's area. The human's model is a single tree per organization with a type on each node: «Департамент» at the top level,
one «Проект» node per project (the project's place, with one parent, movable), and sub-areas only below a project node. The
project–area links, the item's area pointer and the free tree no longer fit, so four rules of ADR-0028 and one section of
ADR-0029 must change. An accepted ADR is not edited substantively (README, Process 3), hence this one.

## Options considered

### Option 1: keep the links, add a type on the node

- Pros: smallest change to the first build.
- Cons: a project could be linked to many areas, so it has no single place in the tree; "the project's sub-areas" and the item's
  «Область» have no owner; access reach (ADR-0029 § 6) stays a graph walk over links.

### Option 2: a typed single tree; a project is one node, sub-areas below it (chosen)

- Pros: a project has exactly one place and one path; the item's area is bounded by its project (a project node or sub-area);
  access reach later follows the parents only; no area–project relation to store, lock or keep inert.
- Cons: a data migration turns each project's old tree into a project node with sub-areas; a project cannot appear in two
  departments (the human's decision, BR-AW-0).

### Option 3: separate tables per type

- Pros: each type's columns exact.
- Cons: one tree with one name space and one move/cycle/depth check becomes three; every tree read joins them.

## Decision

Option 2, with these rules.

**1. The model.** `areas` carries `type` (`Department` 1, `Project` 2, `Area` 3) and a nullable `project_id`. One tree per
organization; the organization is not a node. A check constraint holds the shape: a department has no project and no parent; a
project node has a project and no sub-area parent; a sub-area has the project of its parent. `project_id` is unique among
project nodes (a partial unique index), so a project has one node. The sibling name space (organization, parent, normalized
name, NULLs not distinct) stays one for all types (ADR-0028 rule 5). Depth stays 10, counted from the top level.

**2. Supersedes ADR-0028 rule 2 (the table of mod-002 rules), in part.** BR-M1-9 is retired as before for the first team, but
"creating a project creates no area" no longer holds: creating a project creates its **project node** in the same transaction,
under a department or at the top level (`parentAreaId`), and renaming the project renames its node. BR-M1-12's "one tree per
project" becomes "one tree per organization, a project's subtree being the node and what is below it". BR-M1-13 is back for
sub-areas: a sub-area is deleted only with no child and no work item.

**3. Supersedes ADR-0028 rule 3 (the work item keeps no area pointer).** The item's «Область» returns, restricted to its own
project's node and the sub-areas below it (`outside_project` otherwise), with its composite key (project, area) onto the node's
tree and the area-usage delete guard (`IAreaUsage`). Ownership stays with the owning team: the area classifies, it owns
nothing. mod-003 BR-M3-23/-24 are back as they stood. The migration keeps each item's `area_id`.

**4. Area–project links are removed.** ADR-0028's `AREA_PROJECT` relation, its two indexes and its link rows are gone (a
migration drops the table; the project node is the relation). **Area–team links stay but join departments only**
(`link_type_not_allowed` otherwise). A project node's teams are feat-028's project–team links, never area–team rows.

**5. Supersedes ADR-0028 rule 7, in part (the addresses).** The area prefix `/organizations/:o/areas/:areaId/…` stays, serving
every type; a project node's page shows the project's data and links to the project, and no second address is added. Deleting,
renaming or creating a project node is done on the project, not on the area route (`root_is_fixed`).

**6. Supersedes ADR-0028 rule 8 (locks and the structure contract), in part.**
- *Lock order:* the tree lock (`area_tree_locks`), then area rows by id (the moved row `FOR UPDATE`, a destination or parent
  `FOR SHARE`), then the project row, the project's node, the teams, then the links. A locked row's presence is the lock
  statement's own answer (ADR-0028 A-9). A write beneath a project reads `projects.IsReachable` after its own lock (ADR-0026
  rule 8).
- *Item save (the old debt m-5):* a save that sets an item's area takes the project hierarchy lock first, then the item, then the area row `FOR SHARE` (the tier after the hierarchy). Area writes never lock work item rows, they only read the items that block a deletion after the area lock, so the two orders do not cross and no cycle forms. mod-007 must place the sprint tier relative to this one when it adds sprint writes.
- *Structure contract:* `RootAreaId` stays removed; the area-to-projects resolution (`ListLinkedProjectsOfAreaAsync`) is
  replaced by `FindAreaAsync` (with `Type`, `ProjectId`, `Path`), `ListAreasOfProjectAsync` and `ListAreasOfTeamAsync`.
- *Races:* "link vs area deletion" now concerns team links of a department; "project node move vs department delete" and
  "project rename vs node move" are serialized by the tree lock and the node row, and each ends in one order, no server error.

**7. Supersedes ADR-0029 § 6, in part (reach).** "The area–project links" leave the algorithm's inputs. The grants that reach
project `p` are those held on `p`, plus the area grants that reach **p's project node** (held on the node or inherited from its
parent department, with «Наследовать доступ»). Everything else in § 6 stands: additive only, no link widens access, the
"Includes sub-areas" aggregation walks the subtree. `mod-019-scoped-access` implements it; mod-018 only stores `inherits_access`.

**8. The upgrade.** One migration over the first-build schema (unmerged, so regenerated, not stacked): each project's root
becomes its project node at the top level, its descendants become sub-areas, `created_by`/`created_at` come from the project, and
items keep their `area_id`. It fails, naming the project, when a tree is deeper than 10 levels; the human shortens it and
re-runs. Down restores the shape and removes the departments and team links, which have no earlier form.

**Departures from `docs/conventions/*`:** none.

## Consequences

- A project has one place and a path; the tree shows it with its department; the project's page can show its location.
- A project cannot sit in two departments; matrix reporting by department is the team link plus the project–team link (feat-028).
- One more `type` and `project_id` to read in every tree read; the whole tree is still one statement (1 000 nodes, under 2 s).
- Access (mod-019) walks parent pointers only; no area–project relation to keep in sync.
- The first build's `area_projects`, its three routes per end and `project.link` on areas disappear; the item's area comes back,
  so mod-003's fixtures keep creating items through the project's node.
- Follow-ups owned by other lines: the Organization row of the tree and the header command name (design D-8, D-9) are frontend;
  area grants and inheritance enforcement are `mod-019`.
