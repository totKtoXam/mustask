# Plan (backend): mod-022-projects-in-departments

Route L from stage 4. Requirements: `doc-016-teams-under-departments` (`spec.md` BR-TD-3, -4 project part, -6, -19 steps 2-3,
-20; stories US-5, US-2 steps 2-3), decisions in [ADR-0040](../../adr/0040-teams-in-departments-cascading-selectors-sight-by-participation.md).
Contract: [api-contract.md](api-contract.md). The approach is mod-021's ([plan](../mod-021-teams-in-departments/plan-backend.md)): one
migration, the transform in one SQL block, a log table, tests on a populated fixture.

## Lanes

None: one connected change (the migration, the move rule, the link rule, the creation rule) done in one sequence.

## Migration `20261117221218_ProjectsInDepartments` (one transaction, all or nothing)

1. `project_place_upgrade_log` (kept, not mapped; kinds `department_created`, `project_placed`, `link_cut`; the cut row carries the
   number of items made «Без команды»).
2. `ProjectsInDepartmentsUpgrade.Script`, per organization: step 2 (each top-level project node, active or deactivated, goes to the
   department holding most of its linked teams as mod-021 placed them, ties by department name; else «Проекты без департамента»,
   reused when present, created and coloured by the BR-TD-20 rule otherwise; a clashing non-department node of that name stops the
   migration), then step 3 (every link whose ends stand in different departments is cut like feat-028 BR-OL-8 to «Без команды»:
   default team cleared, the team's places on the project's items dropped, the items' owner and sprint cleared in any state, the link
   removed; `changed_at` untouched, no history row). Every placement, created department and cut link is a log row and a `RAISE NOTICE`
   with names; one notice per organization sums up the counts.
3. `ck_areas_type_shape` is rebuilt so a project node must have a parent (the database refuses a project at the top level for good).

Idempotence: the script selects only what is still to do (top-level project nodes, links across departments), so a second run finds
nothing and logs nothing; a test runs the script a second time. Colours: a project keeps its colour (BR-TD-20, no recolouring on a
move); only the department the upgrade creates is coloured.
Down: relaxes the check and drops the log. It does not return a project to the top and does not restore a cut link (as mod-021's Down).

## Domain and slices

| Slice | Change |
|---|---|
| `Area.MoveUnder` | a project node to the top level: 409 `organizations.area.project_needs_department` |
| MoveArea | a project node to another department while any team is linked: 409 `organizations.area.project_has_teams`, read after the node's lock; the department it already stands in is a no-op |
| CreateProject | no department: 400 field `parentAreaId` reason `organizations.project.department_required`, nothing created (no place is judged for it); `teamIds` must stand in the department: 409 `organizations.team.other_department` |
| LinkTeamsToProject, LinkProjectsToTeam, AddTeam (`projectIds`) | the project node locked `FOR SHARE` (new `IAreaRepository.LockProjectNodeForShareAsync`), its department read under that lock, equal to the team's: else 409 `organizations.team.other_department` |

Import (mod-024) creates no project (it writes into existing projects), so BR-TD-6 needs nothing there.

## Concurrency

A link takes the project node `FOR SHARE`, a move holds it `FOR NO KEY UPDATE` and reads the links after: they conflict, so a link
against a move ends in one order (the link refused or the move refused). Shares never wait for each other, so no cycle with the team
locks (a team move takes no node row beyond the destination department).

## Tests

Domain (`AreaTests`), Application (handlers on fakes), Infrastructure (`ProjectsInDepartmentsMigrationTests`: populated fixture,
logging, idempotence, all-or-nothing, Down/Up, schema check; mod-021's migration tests now stop at their own migration), Api integration
(creation, move, links, races, error codes; existing tests adjusted to the required department), error-code catalog and generated
reference. Only the affected projects run locally.
