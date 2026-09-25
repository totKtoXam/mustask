# US-5: MS-5 «Сохранённые запросы»

- story: US-5
- depends-on: US-4
- shippable: no — a milestone, delivered by the lines listed
- status: pending

As the **product owner**, I want **MS-5 «Сохранённые запросы»**, so that
**whatever a member narrows the list to inline can be saved: kept for oneself,
or shared with the organization**. Even a reader can keep their own queries.

Delivered by `doc-NNN-release-2-scope` (the release-2 entry, PR-5) and
`mod-008-queries-dashboards`.

## Acceptance criteria

```gherkin
Scenario: Demo — a viewer's personal query and a member's shared one
  Given the MVP is complete and doc-NNN-release-2-scope is merged
  And the project's areas «Платформа» and «Платформа/API»
  When a user whose role in the organization is viewer builds a query with
    the clause Область Under «Платформа» and the keyword clause «оплата»
  And saves it in «Мои запросы»
  Then running it returns the matching items of both «Платформа» and
    «Платформа/API» — the same items the MVP inline filter shows for that
    area and keyword (D-16)
  When a member saves a query in «Общие запросы» and picks the columns «ID»,
    «Заголовок» and «Статус»
  Then every member of the organization can run it and sees those columns
  When the member deletes the shared query and the organization administrator
    opens the purge list
  Then the list shows the query's owner, folder and deletion date, and not its
    name or its clauses (D-13)
  And after the administrator purges it, it cannot be restored

Scenario: Guard — a viewer writes only their own objects
  Given the viewer's personal query
  When another member opens «Мои запросы»
  Then the viewer's query is not there
  When the viewer tries to save a query into «Общие запросы»
  Then saving there is not offered
  And the server refuses such a request sent to it directly (D-11, D-12)
```
