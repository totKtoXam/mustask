# feat-062-sight-in-work-views: backend plan

Contract: `api-contract.md` (no route or shape change; narrowing and refusals only).
Vertical slices stay separate: the shared piece is the `VisibleProjects` value in
`Application.Abstractions.Organizations`; no slice references another.

## Design

1. `VisibleProjects` (`All` | `Only(ids)`, `Sees(id)`). `AccessSet.SeenProjects()` is the
   one set form (admin: `All`, else the projects the set sees). `IOrganizationAccess`
   gets `VisibleProjectsAsync` (default `All`, so existing doubles stay valid);
   `HttpContextOrganizationAccess` overrides it from the per-request cached set.
2. `CallerAccess` carries the value; `Projects` fails closed (throws) for a non-admin
   caller without it, so a handler that forgets the sight cannot silently disclose.
3. Infrastructure: `VisibleProjectsQueries.WhereVisible` adds a parameterized
   `Contains` predicate to the statement, before grouping, counting, paging and cursor.
   No extra statements.
4. Boards: `AggregateViewReadModel` (projects in scope, rank and changed candidates,
   hidden project group = not found, directory), `BacklogMembership`/`BacklogReadModel`
   (team backlog and board, `projects` list). Handlers pass the value.
5. Sprints: `ISprintReadModel` read methods take `VisibleProjects`; live and snapshot
   queries narrow. Handlers inject `IOrganizationAccess`.
6. Reorder (R-5): `StackRankEntry` carries the project; the handler treats an item or
   anchor of a hidden project as an unknown id (409 `reorder_stale`).
7. Body-id audit: create/save team, save sprint, close destination closed (404, equal
   to unknown); the rest audited, see contract section 6.

## Tests

- Api.IntegrationTests twin tests (same data, with and without the hidden project,
  viewer vs twin viewer, shape-equal): `Boards/SightInWorkViewsTests`,
  `Iterations/SightInSprintsTests`; helper `Organizations/SightTwin`.
- Reorder hidden id test, body-id tests, administrator unchanged.
- Statement counts: `SprintStatementCountTests` (narrowed = unnarrowed pins),
  `AggregateViewStatementCountTests` (member = admin + set, 5 vs 50 projects).
- `AccessSetSightTests`: `SeenProjects`, admin `All`, fail-closed `CallerAccess`.

## Out of scope

Overview and selector offers (feat-061); frontend (no contract change, nothing to
adapt).
