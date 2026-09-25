# US-5: An organization member builds a dashboard with widgets and charts backed by queries

- story: US-5
- depends-on: US-2, US-3
- shippable: no — release 3, M6 not founded in MVP (D-4); founded by `mod-009-dashboards-widgets`, charts by `feat-006-dashboard-charts`
- status: pending

As an **organization member**, I want **to add query-backed widgets and
charts to a team or project dashboard**, so that **the team or project can
see live status at a glance without opening each query** (D-15).

## Acceptance criteria

```gherkin
Scenario: Every team starts with one empty default dashboard
  Given a team (M1) — newly created, or one that already existed when
    dashboards shipped
  When its dashboard page is opened for the first time
  Then a protected default dashboard already exists for the team, with no
    widgets yet (mirrors the one-board-per-team-per-level guarantee, M4)
  And a team deleted before dashboards shipped gets none (BR-M6-10)

Scenario: A project has dashboards too, but none are seeded
  Given a project with no dashboards yet (D-15)
  When a member or admin creates one
  Then it appears among the project's dashboards; unlike a team's seeded
    default, no project dashboard is protected

Scenario: Adding a query tile widget
  Given a shared query (US-2)
  When a member adds a query tile widget backed by it, on a team or a
    project dashboard
  Then the widget shows the query's current result count, live

Scenario: A personal query cannot back a widget
  Given a member who owns a personal query in «Мои запросы»
  When they try to add a widget backed by it
  Then the personal query is not offered — only a shared query may back a
    widget (BR-M6-2, BR-M6-7)

Scenario: A dashboard holds at most 10 widgets
  Given a dashboard that already has 10 widgets
  When a member tries to add an eleventh
  Then the addition is refused (spec § Non-functional requirements)

Scenario: Adding a chart widget
  Given a shared query grouped by a field, e.g. Статус
  When a member adds a chart widget of type "pie" backed by that query
  Then the chart shows the current distribution of results across the
    grouping field's values

Scenario: A widget re-runs its query on refresh
  Given a dashboard with a query-backed widget
  When the underlying work items change and the dashboard is refreshed
  Then the widget reflects the new results — it is never a cached snapshot
    (BR-M6-7)

Scenario: Soft-deleting a query used by a widget
  Given a dashboard widget backed by a query
  When that query is soft-deleted (BR-M6-9), not hard-deleted
  Then the widget keeps its place on the dashboard and shows the message
    «Запрос удалён» instead of data, until the query is restored, or a
    member re-points the widget, or removes it — the widget is never
    removed automatically

Scenario: Restoring a soft-deleted query recovers its widget
  Given a widget showing «Запрос удалён» because its query was soft-deleted
  When a member or admin restores that query (BR-M6-9)
  Then the widget resumes showing live results without needing to be
    re-pointed

Scenario: Only a team's seeded default dashboard resists deletion
  Given a team with its seeded default dashboard and one additional team
    dashboard, and a project with one project dashboard
  When a member tries to delete each
  Then the additional team dashboard and the project dashboard can be
    deleted (soft, BR-M6-9); the team's seeded default cannot (D-15)

Scenario: A deleted team's dashboards go with it
  Given team B with its default and one additional dashboard
  When an administrator deletes team B (doc-006 BR-M1-18)
  Then both dashboards are soft-deleted and no longer listed (BR-M6-10)
```
