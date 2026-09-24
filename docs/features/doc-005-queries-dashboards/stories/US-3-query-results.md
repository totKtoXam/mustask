# US-3: A team member works with query results

- story: US-3
- depends-on: US-1, US-2
- shippable: no — release 2/3, M6 not founded in MVP (D-4)
- status: pending

As a **team member**, I want **to view, sort and act on a query's
results**, so that **I can use a saved query as a working list, not just a
filter definition**.

## Acceptance criteria

```gherkin
Scenario: Running a query shows a live result list
  Given a saved shared query (US-2)
  When a team member opens and runs it
  Then the results reflect the current state of matching work items, not a
    snapshot taken when the query was saved

Scenario: Results respect org-wide read access, not team/board membership
  Given a work item whose Area is owned by a different team than the
    requesting member's own team
  When that member runs a shared query that matches the item
  Then the item still appears in the results — query results are not
    narrowed by board team-ownership (BR-4)

Scenario: Sorting results by a column
  Given a query's result list with multiple columns (state, assignee,
    priority)
  When a team member sorts by Приоритет
  Then the results reorder accordingly, without changing the underlying
    work items' data

Scenario: A query with no chosen columns uses the default set
  Given a newly saved query that has never had its columns changed
  When a team member runs it
  Then the results show exactly the default column set — Заголовок,
    Статус, Исполнитель, Приоритет (BR-12)

Scenario: Choosing and saving a query's result columns
  Given a team member viewing a query's results with the default column set
  When they add or remove columns from the release-2 field set (US-1) and
    save the query
  Then the chosen column set persists with the query and is used again the
    next time it is run, and by CSV export (BR-8, BR-12, US-4)

Scenario: Opening a work item from the results
  Given a query result row
  When a team member opens it
  Then the corresponding work item's full form (M3) opens

Scenario: An empty result set is shown clearly
  Given a query whose clauses match no work item
  When a team member runs it
  Then the results show a clear empty state, not an error
```
