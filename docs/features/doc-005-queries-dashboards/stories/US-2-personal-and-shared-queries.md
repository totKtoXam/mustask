# US-2: A member saves a personal query and shares one with the project

- story: US-2
- depends-on: US-1
- shippable: no — release 2/3, M6 not founded in MVP (D-4); founded by `mod-008-queries-dashboards`
- status: pending

As an **organization member**, I want **to save a query privately or share it with my
project**, so that **I, or my team, can reuse it without rebuilding it every
time**.

## Acceptance criteria

```gherkin
Scenario: Saving a personal query
  Given a member has built a query (US-1)
  When they save it without sharing it with the project
  Then the query appears only in that user's own «Мои запросы» folder,
    visible to nobody else (BR-M6-2)

Scenario: A viewer saves, edits and deletes their own personal query
  Given a user whose role in the organization is viewer
  When they save a query into their own «Мои запросы», later edit it, and
    later delete it
  Then every one of these operations succeeds — the viewer's bundle holds
    doc-001's personal-object code, limited to their own objects (D-14);
    this is the one write a viewer may perform in release 2

Scenario: Sharing a query with the project
  Given a member (not viewer) has built a query
  When they save it into «Общие запросы»
  Then every member of the project's organization, viewers included, can
    see and run it (BR-M6-3)

Scenario: A viewer cannot create a shared query
  Given a user whose role in the organization is viewer
  When they try to save a query into «Общие запросы»
  Then the save is refused; the same viewer can still save the query to
    their own «Мои запросы» (BR-M6-3, D-9)

Scenario: A member promotes their own personal query to shared
  Given a member owns a personal query in «Мои запросы»
  When they move it into «Общие запросы»
  Then the query becomes an ordinary shared query governed by BR-M6-3 — any
    member or admin can now edit or delete it, not only the original
    owner (BR-M6-11)

Scenario: A viewer cannot promote their own personal query
  Given a viewer owns a personal query in «Мои запросы»
  When they want it shared with the project
  Then they cannot promote it themselves, because promoting writes into a
    shared folder (BR-M6-3); a member or admin must recreate or share it for
    them (BR-M6-11)

Scenario: Organizing shared queries into folders
  Given a member creates a sub-folder under «Общие запросы»
  When they move a shared query into it
  Then every member of the project's organization sees the query under the
    new folder (BR-M6-3)

Scenario: Deleting a non-empty shared folder is refused
  Given a shared folder that still contains queries or sub-folders
  When a member tries to delete it
  Then the deletion is refused until the folder is empty

Scenario: A deleted shared query is soft-deleted and can be restored
  Given a member deletes a shared query
  When another member or admin restores it before the organization
    administrator purges it
  Then the query was soft-deleted, not hard-deleted, and it reappears in
    «Общие запросы» exactly where it was (BR-M6-9, D-13)

Scenario: The organization administrator purges without reading
  Given another user's personal query, soft-deleted by its owner
  When the organization administrator opens the purge list
  Then the entry shows its owner, its folder and its deletion date, and
    neither its name nor its clauses (BR-M6-9)
  And purging it removes it for good, while nothing lets the administrator
    open or restore it

Scenario: Only the organization administrator purges
  Given a soft-deleted shared query
  When a user whose role in the organization is member looks for a purge
  Then no purge is offered, and a purge request sent directly is refused
    (BR-M6-9)
```
