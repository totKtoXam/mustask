# Backend plan: team sprints (mod-007)

Stage 4a. The contract is `api-contract.md`. No ADR is needed: nothing here affects every slice, changes a technology or departs
from conventions; the one new code (`iterations.sprint.manage`) follows ADR-0029/0038 exactly.

## Slices (one module, `Iterations`)

`CreateSprint`, `UpdateSprint`, `StartSprint`, `RemoveSprint`, `GetSprints`, `GetCloseSprintPreview`, `CloseSprint`, `GetSprint`
(outcome header), `GetSprintDynamics`, `GetSprintParticipants`, `GetSprintOutcomeItems`; each its own folder in
`Mustask.Application/Features/Iterations`. No lanes (economy mode, one agent): the work is sequential.
Changes outside the module: `SaveWorkItem` (the sprint pointer), the item card view (`sprint`), `WorkItemTeamOwnership`
(the reassignment clears the pointer), `PermissionCatalog` and the bundles, DI, `Program.cs`.

## Domain (`Mustask.Domain/Iterations`)

`Sprint` (state machine, dates, name, numbering input), `SprintState`, `SprintName`, `SprintDates`, `SprintErrorCodes`,
`SprintFigures` (percent, min/average/max, the rounding of BR-15 and BR-16: exact `decimal`, rounded once, half up, away from
zero), `SprintRange` (default range, the 20-sprint cap). `WorkItem.Save` gains the sprint edit and the team-change clearing.

## Database (one migration, `AddSprints`)

- `sprints`: id, organization_id, team_id (FK `teams`), number, name, normalized_name, state (text, check), start_date, end_date,
  created_at/by, closed_at/by. Unique `(team_id, number)` and `(team_id, normalized_name)`; **partial unique `(team_id)` where
  state = 'Active'** (BR-3, the database's backstop to the start's check); alternate key `(id, team_id)`.
- `sprint_counters(team_id pk, last_number)`: the team's numbers, taken under a row lock (BR-7, BR-12).
- `work_items.sprint_id` nullable, composite FK `(sprint_id, owning_team_id) → sprints (id, team_id)` (MATCH SIMPLE): the item's
  sprint is always its owning team's sprint, enforced by the database (BR-2), plus `CHECK (sprint_id IS NULL OR owning_team_id
  IS NOT NULL)`. Index `(sprint_id)`.
- `sprint_snapshot_items(sprint_id, work_item_id, project_id, type_id, number, level_id, assignee_id, story_points, is_done)`:
  the close record (BR-14). Primary key `(sprint_id, work_item_id)`; **no foreign key to `work_items`**, so a purge keeps it
  (listed as kept in `WorkItemReferenceInventoryTests` if that test needs it); FK to `sprints` cascade. Index
  `(sprint_id, level_id)`.
- Down: drops them in reverse; the `work_items` column last.

## Locks (ADR-0011, ADR-0028 rule 8)

Create: team `FOR SHARE`, counter row `FOR UPDATE`. Update/start/remove/close: team `FOR SHARE` then the sprint `FOR UPDATE`.
Close then locks the hierarchies of the sprint's projects and its items by id (`WorkItemHierarchyLocks`, as a team's unlink),
reads state categories under the lock, writes the snapshot and moves the unfinished items in set-based statements. A save that
sets a sprint takes the sprint `FOR SHARE` **before** the item's lock, so a close and such a save end in one order. Two starts
at once: the partial unique index decides, the loser answers `active_exists`.

## Reads

Every read is `AsNoTracking`, fixed statements: list (3), dynamics (3: sprint+range, one `GROUP BY sprint` over the snapshot,
levels from the process), participants (4: as above plus one `GROUP BY (sprint, assignee)`, plus the directory lookup), items
(2). Reachability: items of deactivated projects are dropped by a join to `projects` (BR-9). Statement counts are pinned by tests.

## Tests

Domain (state machine, names, dates, rounding, range), Application (CloseSprint order, validation), Api integration per route
and per story criterion, races through `Race`, a 5 000-item close and a 20 × 5 000 read as `*VolumeTests`, a statement-count test
per read, the migration up/down.
