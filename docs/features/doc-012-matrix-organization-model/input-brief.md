# Input — the human's brief (2026-10-16)

The task statement as the human gave it, translated from Russian; the
reference product's name is replaced by "the reference tracker" (AGENTS.md).
It is the input of stage 1, not a decision record: the human's decisions that
override parts of it are in [status.md](status.md) `## Log` — notably, § 13's
data preservation is waived (existing data may be dropped), and a project
gets a default team (set in settings, or inferred when exactly one team is
linked).

## 1. Context

An application for managing projects, tasks, teams, sprints and users already
exists, conceptually similar to the reference tracker. Its model of
organizations, projects, teams and areas follows the reference tracker's
principles. The existing architecture must be reworked to be much more
flexible — especially for organizations with several projects, teams, shared
processes and frequent moves of people between projects. This is not
greenfield: study the current implementation and refactor the existing
application, preserving its functionality.

**The main principle of the new architecture:**

- Organization — the root object and the data isolation boundary.
- Area — a universal workspace.
- Project — an independent object that owns work items.
- Team — an independent object that can take part in several projects.
- WorkItem — belongs to a project and is optionally assigned to a team.
- Sprint — a team's iteration of work, which may include items of different
  projects.

**Area, Project and Team are independent entities inside Organization.** Area
must not become a hard parent of Project or Team; Many-to-Many relations are
used between them. Do not add Department, Division, Business Unit or similar
entities — the universal Area serves these purposes.

## 2. First stage: audit of the existing application

Before changing code, study the project. Analyse: the repository structure
and stack; backend and frontend architecture; data models and ORM
configuration; existing migrations; the organization model (Organization,
Project, Team, Area, User, Membership, WorkItem, Sprint/Iteration); the
current authorization and permission model; the APIs for projects, teams and
tasks; existing pages, forms, menus and navigation; Kanban, Backlog and
Sprint Board; filtering, search and reporting; automations, notifications and
other mechanisms that use the existing hierarchy.

Analyse the actual use of `Area` and the area path in code: is it used for
grouping tasks, for task ownership by teams, for access control, for building
boards, for filtering backlogs, for analytics? If one existing entity carries
several functions, separate those duties into separate mechanisms.

Audit result — a short technical report: how the current model works; what
limitations it creates; which existing entities can be kept; which relations
and tables must be added; which relations and constraints must change; which
parts of backend and frontend the refactoring touches; the migration risks;
which existing functions must be protected from regressions.

**Do not rewrite the application from scratch.** Keep the existing stack,
architectural conventions, authorization mechanisms and infrastructure where
they are compatible with the new model. After the audit, write a sequential
implementation plan and carry it out; do not stop at documentation.

## 3. Target domain model

### 3.1. Organization

The root object. Inside an organization live users, areas, projects, teams,
organization roles, settings and access policies.

1. An organization may have any number of areas, projects and teams within
   technical limits.
2. A project need not belong to any area.
3. A team need not belong to any area.
4. An organization may work without areas at all.
5. All relations between entities respect organization boundaries.
6. A project of one organization cannot be linked to a team or an area of
   another.
7. If membership of a user in several organizations is supported, keep it.

**The organization is the tenant isolation boundary. Area is not.**

### 3.2. Area — a universal workspace

Area joins projects, teams, users and their tasks into one workspace.
Examples: a logistics department, a finance line, a digitalization program,
internal product development, platform engineering, a business initiative, a
cross-project working group. Its purpose is not fixed by the system.

Area supports: a name; a description; an optional parent area; unlimited
nesting within reasonable technical limits; links to several projects; links
to several teams; participants; roles and permissions; visibility settings;
permission inheritance; aggregated views of tasks; shared analytics and
reporting.

Example structure: Organization "Company" → Area "Logistics" (children
"Warehouse systems", "Transport systems"), Area "Finance", Area
"Infrastructure". Projects and teams are not stored physically inside the
area tree; they are linked to areas through separate relations.

Requirements: an area may exist without projects, without teams, without
participants; one project may be linked to several areas; one team may be
linked to several areas; an area may aggregate the tasks of its child areas;
tasks reachable through several areas are not duplicated; cycles between
parent and child areas are forbidden; deleting or unlinking an area does not
delete projects, teams or tasks. Link management goes through separate
`AreaProject` and `AreaTeam` mechanisms.

## 4. Project — an independent object

A project belongs directly to the organization; remove any mandatory binding
of a project to a team or an area.

1. A project may exist without a team.
2. A project may exist without an area.
3. A project may be linked to N areas.
4. A project may be linked to N teams.
5. A project owns its work items.
6. Connecting a second team does not create a duplicate of the project.
7. When a project's teams or areas change, its tasks keep their ids and
   history.
8. A project may have its own settings, roles, backlog, releases and other
   existing functions.

Case — one project, several teams: Project A has teams MARS and VENUS. Both
work on one project but may have different tasks, people, sprints, boards,
load and team roles. The Project Board shows every task of the project the
user can see, whatever the responsible team, and also the tasks with no team.

## 5. Team — an independent object

A team belongs directly to the organization and is not a child of a project.

1. A team may exist without projects.
2. A team may take part in N projects.
3. A team may be linked to N areas.
4. A team has its own members.
5. A user may be a member of several teams.
6. A team may have its own roles.
7. A team has its own backlog as a view of the tasks assigned to it.
8. A team has its own sprints.
9. A team has a board joining tasks from several projects.

Case — one team, several projects: team MARS works on projects A, B and C at
once. MARS exists in a single instance; the Team Board shows the tasks of all
three projects the current user can see; each task keeps its project; a task
view shows its source project; the team can plan tasks of different projects
into one sprint; the team's load is computed across all its projects;
unlinking the team from a project deletes no tasks.

Project ↔ Team is Many-to-Many through `TeamProject` or an equivalent of the
existing schema; linking a team to a project means its participation in the
project. **A team's participation in a project and a user's permission to see
the project are different things.** A `TeamProject` link by itself must not
bypass access policies.

## 6. WorkItem and the distribution of responsibility

Every task belongs to exactly one project. Relations: `ProjectId` —
mandatory; `OwningTeamId` — optional; `AssigneeId` — optional; `SprintId` —
optional (names adapted to the existing models). Keep the existing types and
hierarchy (Epic, Feature, User Story, Task, Bug and others); do not redesign
the work item classification if the existing model supports it.

Rules:

1. A task belongs to exactly one project.
2. A task may have no responsible team.
3. A task may have one primary responsible team.
4. The responsible team must take part in the project.
5. The assignee may be a member of several teams.
6. Moving a task between teams does not change its project.
7. Moving a person between teams keeps their task history.
8. Changing an area changes neither the ProjectId nor the WorkItemId.
9. Removing a Project ↔ Team link deletes no work items.
10. A task cannot stay in a sprint of a team that is no longer responsible
    for it.

Do not impose a rule that the assignee must be a member of the owning team:
in a matrix structure people are temporarily assigned from other teams; such
assignments are governed by access rights. When several teams deliver one
piece of functionality together, linked tasks and sub-tasks with their own
responsible teams are used. Keep the existing mechanisms of links between
tasks.

## 7. Three levels of boards

Independent views of tasks over one WorkItems collection, without copying
tasks.

### 7.1. Area Board

Shows the tasks of every accessible project linked to the area (example: Area
Logistics with projects A, B, C and teams MARS, VENUS). Filters: project,
team, assignee, task type, status, priority, sprint, date, blockers. With
child-area aggregation on, the projects of child areas count too. A project
linked to several areas counts each task once within one view.

### 7.2. Project Board

Shows the tasks of one project: for Project A with MARS and VENUS — MARS's
tasks, VENUS's tasks and tasks with no team. Teams are a filter, not a way
of splitting the project into independent copies.

### 7.3. Team Board

Shows a team's tasks from all its projects (MARS: A — 5, B — 10, C — 7 → 22
tasks, provided the user can see them), with a filter by project.

Common requirements: tasks are not duplicated; a status change shows in
every view; moving a card does not change its project without a separate
explicit action; existing workflows and statuses are supported; keep
drag-and-drop if it exists; keep the search and sorting mechanisms; support
paging or virtualization for large boards; do not load every task of the
organization into the client without need; every filter and counter respects
the user's permissions; if projects' workflows differ, display tasks on a
shared board correctly — do not overwrite projects' statuses to unify
columns.

## 8. Sprint — the team's planning

Sprints must be able to combine tasks of different projects. **Base model:
Sprint belongs to Team, not to Project.** If the current implementation is
different, study it and refactor accordingly. Case: MARS works on A, B, C;
Sprint 12 holds A — API, 5 SP; B — integration, 8 SP; C — notifications,
3 SP; 16 SP in total. Three sprints are not needed just because tasks belong
to different projects.

1. A team may have N sprints.
2. A sprint may include tasks of different projects.
3. A work item may be assigned to a sprint only if compatible with its
   responsible team.
4. Moving a task to another team requires adjusting its sprint.
5. Changes must not lose the history of completed sprints.
6. The Team Sprint Board shows the team's tasks whatever their projects.
7. The Project Board may show tasks from sprints of several teams.
8. An area may aggregate the active sprints of its linked teams.
9. If projects have releases and milestones, do not mix them with sprints.

Keep the existing planning, estimation and velocity functionality, adapted to
the new model.

## 9. Users and membership

Separate: membership in the organization; membership in an area; membership
in a team; participation in a project; assignment to a work item; role
permissions. These are independent relations. One user may: be a member of
several areas; of several teams; work on several projects; have different
roles in different areas; be team lead of one team and a developer in
another; be PM of several projects; be in no team but have access to a
project. Team composition is not automatically identified with project or
area composition. Moving a person between teams loses none of their task
history, comments, estimates or other data.

## 10. RBAC and scoped permissions

Rework authorization to match the new architecture. Preferred model: **RBAC +
Scoped Permissions + Membership** — roles define the allowed actions, the
scope defines where a permission applies, membership defines a user's
belonging. Supported scopes: Organization, Area, Project, Team. Keep existing
individual permissions where needed. Example roles: Organization Admin, Area
Manager, Area Member, Project Manager, Team Lead, Team Member, Viewer. Job
titles must not be hard-wired into authorization; a role and a job title are
different things.

### 10.1. Inheritance

Area supports: inheritance from the parent area; turning inheritance off;
propagation of access to child areas; local role assignments; isolation of
protected resources. By default an area member with `workitem.read` can see
the tasks of every project linked to the area, unless the project restricts
such access. Example: in Area Logistics the head, the CTO, a project manager,
backend and frontend developers and a business analyst can all see the tasks
of the accessible projects A, B and C; editing is decided by separate
permissions.

### 10.2. Changing tasks

Example permissions: `workitem.read`, `workitem.create`, `workitem.update`,
`workitem.delete`, `workitem.assign`, `workitem.change_status`,
`project.manage`, `team.manage`, `sprint.manage`, `area.manage`,
`permissions.manage` — adapted to the existing model. Do not build a
duplicate role system on top of a working one if the existing one can be
extended.

### 10.3. Restricted projects

Project visibility: Organization; Area Members; Restricted. With
`Restricted`, reading a project's tasks needs an explicit project permission
or the provided administrative access. An `AreaProject` or `TeamProject` link
must not automatically disclose a Restricted project.

### 10.4. Several areas

Area A has access to Project X; Area B is also linked to Project X; the user
is a member of Area A only. The user gets access to Project X by Area A's
rules, if the project allows it, but does not automatically get access to
the other resources of Area B. Likewise, membership in team MARS does not
grant unlimited access to every area where that team appears.

1. All permissions are checked on the server.
2. The frontend is not the source of truth on authorization.
3. Checks are consistent across APIs, boards, lists and single tasks.
4. Restricted projects are not disclosed through counters, search,
   autocomplete or analytics.
5. Inheritance creates no cycles and no implicit widening of access.
6. Every operation has a deterministic model of computing effective
   permissions.
7. Test permission conflicts and Restricted limits separately.

First design and document the effective-permissions algorithm, then
implement it in one shared service or policy layer, so that different APIs do
not use incompatible rules.

## 11. UI/UX

Adapt the application to the new model and make the new structure clear to
the user.

### 11.1. Main navigation of the organization

Overview, Work Items, Areas, Projects, Teams, Members, Settings (names
adapted to the existing localization). Area, Project and Team are separate
navigation entries.

### 11.2. Area page

Sections: Overview, Board, Backlog, Projects, Teams, Members, Analytics,
Settings. Overview shows the linked projects, linked teams, participants,
task counts, distribution by status, active sprints, blockers. The Area Board
joins the tasks of every accessible project.

### 11.3. Project page

General info, Work Items, Board, Backlog, Teams, Releases/Milestones if they
exist, Members/Permissions, Settings. On it one can add a team, remove a team
link, link the project to an area, remove an area link, see all the
project's teams, filter tasks by team.

### 11.4. Team page

Overview, Board, Backlog, Sprints, Projects, Members, Capacity/Analytics if
supported, Settings. A project added to a team becomes available on the Team
Board within the user's permissions.

### 11.5. Creation forms

Project: required Organization, Name; optional Description, Areas, Teams,
other existing fields. Team: required Organization, Name; optional
Description, Members, Projects, Areas, other existing fields. Area: required
Organization, Name; optional Parent Area, Description, Projects, Teams,
Members, Access Settings. A user must not be forced to create an area to
start working with projects and teams.

### 11.6. UX principles

1. Keep the existing design-system approach.
2. Do not create visual copies of one project in different areas.
3. Show the Project and the responsible Team on tasks.
4. Easy context switching: Area / Project / Team.
5. Links are added through a searchable multiselect.
6. Linked objects are shown with navigation to them.
7. Empty states.
8. Keep the interface adaptive.
9. Rework breadcrumbs so they do not create a false hierarchy.
10. Where possible, keep the user's selected filters and views.

## 12. DB refactoring

From the existing schema, design the needed changes. Expected logical
entities and relations: Organization, Area, Project, Team, User,
AreaProject, AreaTeam, TeamProject, AreaMember / scoped role assignments,
TeamMember, ProjectMember if needed, WorkItem, Sprint. This is conceptual —
do not create every listed table blindly if the existing architecture
already implements the function; reuse and extend existing entities.

DB requirements: PK/FK; uniqueness of links; correct delete restrictions;
indexes for selection by Organization, Area, Project, Team; protection
against cross-organization links; checks against area cycles; M:N support;
correct handling of a nullable TeamId; Sprint–Team ownership consistency; no
cascade deletion of tasks when a team or area is unlinked. Where an important
invariant can be enforced by the DB, use DB constraints together with domain
validation.

## 13. Migration of existing data

*Waived by the human (status.md): existing data may be dropped.* The brief
asked to keep task ids, comments, attachments, history, projects, teams and
user settings; to map the old model onto the new one; to move the existing
relations into M:N; to preserve the semantics of area paths used for
classification (without confusing classification with access); to keep the
history of completed sprints; to resolve ambiguous team–project–sprint links
by explicit rules with a conflict report; repeatable migrations, a backup, a
before/after count check, a rollback plan, legacy API checks and staged
rollout; no irreversible deletion without a separate check.

## 14. API and backend

Rework the APIs for the new model, following the project's conventions (the
URLs need not be literal). Logical operations:

- Areas: create; change; read the tree; read children; link/unlink a
  project; link/unlink a team; manage participants; manage permissions; read
  work items by area; read the area's aggregates.
- Projects: list the organization's projects; list a project's teams;
  link/unlink a team; list linked areas; read a project's tasks with filters.
- Teams: list the organization's teams; list a team's projects; link/unlink
  a project; manage members; read a team's tasks across all projects; list a
  team's sprints.
- WorkItems: create inside a project; set and change the owning team; set
  the assignee; set the sprint; read tasks by any combination of filters.

Architectural requirements: avoid N+1 queries; do not duplicate the
filtering logic by area, project and team; reusable query specifications or
services within the current stack; server-side paging; authorization
filtering before data is returned; correct counters and aggregates; keep the
existing API contracts where possible; document breaking changes explicitly.

## 15. Mandatory user scenarios

- **SC-01. A team works on three projects.** MARS linked to A, B, C: the
  team exists once; the Team Board shows the tasks of the three projects the
  user can see; tasks keep their projects; filter by project; tasks of
  different projects go into one MARS sprint.
- **SC-02. Two teams on one project.** A with MARS and VENUS: the Project
  Board shows both teams' tasks; each team sees its tasks on its Team Board,
  with access; the project exists once; the teams may use different sprints.
- **SC-03. A shared area.** Area Logistics; projects A, B, C; teams MARS,
  VENUS linked to it: the Area Board aggregates tasks; heads with the right
  area role see every accessible task; people with `workitem.read` see the
  tasks of the area's projects; a team filter does not remove tasks from the
  shared view; no duplicates.
- **SC-04. A person moves.** A user moves from MARS to VENUS: existing tasks
  stay; history is kept; permissions are recomputed; no need to move
  projects; no need to change areas to move a person.
- **SC-05. A team in different areas.** MARS linked to Area A and Area B;
  Project A is in Area A's projects, Project B is linked to Area B: the Team
  Board aggregates the accessible tasks of both projects; Area A does not
  disclose Project B's tasks automatically, nor Area B Project A's; no
  implicit widening of access through the team.
- **SC-06. A project in several areas.** Project A linked to Area A and
  Area B: one physical project; one set of work items; no duplicates; each
  area's board shows the tasks by permissions; removing one link does not
  affect the other; a Restricted project stays protected.
- **SC-07. A project without area or team.** Created without links: it is
  created; tasks can be created; the Project Board works; it does not vanish
  from the organization's list; permissions come from organization or
  explicitly assigned permissions.
- **SC-08. Nested areas.** Logistics with children Warehouse and Transport:
  the parent can aggregate the children's data; access inheritance follows
  the settings; turning inheritance off is respected; no duplicates; a cyclic
  hierarchy cannot be created.
- **SC-09. Unlinking a team from a project.** MARS has tasks in A; an
  attempt to unlink MARS from A detects the team's active assignments; no
  inconsistent state may arise. Provide a safe scenario: block the unlink
  with an explanation, or explicit reassignment of the tasks to another team
  or to "No team". Choose with the existing UX in mind and document it.
- **SC-10. Different workflows.** A and B have different status sets; MARS
  works on both: tasks show on the shared Team Board; statuses do not change
  automatically; transitions are validated by the source project's workflow;
  shared columns may use normalized status categories if needed.

## 16. Testing

Add or update tests within the existing test infrastructure. Cover:

- Domain: M:N Project ↔ Team; M:N Area ↔ Project; M:N Area ↔ Team;
  multiple membership of a user; organization boundaries; WorkItem–OwningTeam
  consistency; WorkItem–Sprint consistency.
- Authorization: area-level read; project-level restricted access; area
  inheritance; isolation of different areas; several roles and scopes;
  permissions after a person moves; no leaks through aggregates and search.
- Data: migrations; id preservation; no duplicates; history preservation;
  correct removal of links.
- UI/E2E: create a project without an area; create a team without a project;
  link several teams to a project; link several projects to a team; Area
  Board; Project Board; Team Board; filtering; team sprints.

## 17. Non-functional requirements

Do not worsen the existing UX; no mandatory extra steps when creating
projects; do not break functions unrelated to the refactoring; no full
reload when switching views if the architecture is a SPA; support large
lists of projects, teams and tasks; avoid sub-optimal ORM queries; ensure
tenant isolation; observable migration and authorization errors; keep the
project's coding style; no unneeded dependencies or infrastructure; no
large-scale rewrite without technical need.

## 18. Order of implementation

Work in stages, each verified: (1) analysis — study the repository, find the
current organization model, map the changes; (2) design — the target model's
ERD, the main domain invariants, the permission matrix, the
effective-permissions algorithm, the migration plan, the list of affected
components, the backward-compatibility plan; (3) backend and DB — new
relations, migrations, domain checks, application services, authorization,
API, tests; (4) frontend — navigation, Areas/Projects/Teams pages, creation
and edit forms, object links, boards, sprints, filtering; (5) migration and
compatibility — existing data, APIs, integrations, related functions; (6)
final testing — the mandatory scenarios and regression checks.

## 19. Format of results

During the work provide: As-Is Architecture (current implementation with
files and components); To-Be Architecture (new architecture and ERD); Gap
Analysis; Implementation Plan; Implemented Changes; Database Migrations;
Tests (which checks ran and their result); Remaining Issues. Do not claim a
function is implemented if the code did not change; do not claim tests
passed if they were not run; if a part cannot be implemented in the current
environment, say why.

## 20. Definition of done

- Organization stays the root object.
- Area became a universal workspace.
- Project and Team exist independently of Area.
- Project ↔ Team, Area ↔ Project, Area ↔ Team, User ↔ Team are M:N.
- A user may take part in several areas.
- A project may have several teams; a team several projects.
- The Team Board aggregates tasks of different projects; the Project Board
  shows the tasks of every team; the Area Board shows the tasks of the
  linked projects.
- A sprint supports tasks of several projects of one team.
- Scoped permissions are implemented; Restricted projects are protected;
  area inheritance and isolation work.
- Tasks are not duplicated in aggregation.
- Existing data is preserved *(waived — see the top of this file)*.
- Existing functionality did not regress.
- Backend, API and frontend work with the new model.
- The needed tests ran.

## 21. Architectural constraints — do not

1. Create a Department entity — Area plays that role.
2. Make Project a child of Team.
3. Make Team a child of Project.
4. Make Area a mandatory parent of Project or Team.
5. Limit a team to one project.
6. Limit a project to one team.
7. Tie access to tasks only to team membership.
8. Create copies of WorkItems for different boards.
9. Use the area path as the only authorization mechanism.
10. Create a sprint per project for one team without a real need.
11. Mix data of different organizations.
12. Delete existing data to simplify the migration *(waived by the human)*.
13. Complicate the model with new entities when the current ones can carry
    the relations.
14. Replace existing solutions with new libraries without need.
15. Stop at a cosmetic UI change — the architecture is reworked at every
    affected level.

**Goal:** a flexible system for managing an organization's work, where the
Organization manages organizational resources; the Area is a universal
workspace joining projects, teams, participants, permissions and reporting;
the Project manages its domain, tasks, backlog and releases; the Team manages
its people, load, tasks and sprints across projects; the WorkItem stays part
of one project but may be delivered by any connected team within
permissions; Authorization independently governs resource visibility and
user powers — supporting real matrix organizations without artificial extra
projects, teams or copies of tasks.
