# US-7: MS-7 «Дашборды, диаграммы, CSV»

- story: US-7
- depends-on: US-6
- shippable: no — a milestone, delivered by the lines listed
- status: pending

As the **product owner**, I want **MS-7 «Дашборды, диаграммы, CSV»**, so
that **a team sees its state at a glance on a dashboard fed by shared queries,
and can move work items in and out of MUSTASK in bulk through CSV**.

Delivered by `mod-009-dashboards-widgets`, `feat-006-dashboard-charts` and
`feat-005-csv-import-export`.

## Acceptance criteria

```gherkin
Scenario: Demo — a team dashboard fed by a shared query, and a CSV round trip
  Given the team «Платформа», created before dashboards shipped
  And a shared query «Открытые баги» in «Общие запросы»
  When a member opens the team's dashboards
  Then its default dashboard is already there, although nobody created it
  When the member adds a query-tile widget and a chart widget, both backed by
    «Открытые баги»
  Then the tile shows the query's count
  And the chart groups the query's results
  When the member exports «Открытые баги» to CSV
  And changes one row's «Заголовок»
  And adds a row that targets a deleted work item
  And imports the file back into the project
  Then the item that the changed row names by its «ID» has the new title
  And the change appears in that item's history
  And the row for the deleted item is rejected with its reason, while the
    other rows apply

Scenario: Guard — the team's default dashboard is protected
  Given the default dashboard of «Платформа»
  When a member looks for a way to delete it
  Then deletion is not offered
  And a delete request sent directly to the server is refused
```
