# US-2: A team member saves a personal query and shares one with the project

- story: US-2
- depends-on: US-1
- shippable: no — release 2/3, M6 not founded in MVP (D-4)
- status: pending

As a **team member**, I want **to save a query privately or share it with my
project**, so that **I, or my team, can reuse it without rebuilding it every
time**.

## Acceptance criteria

```gherkin
Scenario: Saving a personal query
  Given a team member has built a query (US-1)
  When they save it without sharing it with the project
  Then the query appears only in that user's own "Мои запросы" folder,
    visible to nobody else (BR-2)

Scenario: A viewer saves, edits and deletes their own personal query
  Given a user whose organization role is viewer
  When they save a query into their own "Мои запросы", later edit it, and
    later delete it
  Then every one of these operations succeeds — ownership, not organization
    role, is the check (D-14); this is the one write a viewer may perform
    anywhere in MUSTASK

Scenario: Sharing a query with the project
  Given a member (not viewer) has built a query
  When they save it into "Общие запросы"
  Then every member of the project's organization, viewers included, can
    see and run it (BR-3)

Scenario: A viewer cannot create a shared query
  Given a user whose organization role is viewer
  When they try to save a query into "Общие запросы"
  Then the save is refused; the same viewer can still save the query to
    their own "Мои запросы" (BR-3, D-9)

Scenario: A member promotes their own personal query to shared
  Given a member owns a personal query in "Мои запросы"
  When they move it into "Общие запросы"
  Then the query becomes an ordinary shared query governed by BR-3 — any
    member or admin can now edit or delete it, not only the original
    owner (BR-11)

Scenario: A viewer cannot promote their own personal query
  Given a viewer owns a personal query in "Мои запросы"
  When they want it shared with the project
  Then they cannot promote it themselves, because promoting writes into a
    shared folder (BR-3); a member or admin must recreate or share it for
    them (BR-11)

Scenario: Organizing shared queries into folders
  Given a member creates a sub-folder under "Общие запросы"
  When they move a shared query into it
  Then every member of the project's organization sees the query under the
    new folder (BR-3)

Scenario: Deleting a non-empty shared folder is refused
  Given a shared folder that still contains queries or sub-folders
  When a member tries to delete it
  Then the deletion is refused until the folder is empty

Scenario: A deleted shared query is soft-deleted and can be restored
  Given a member deletes a shared query
  When another member or admin opens the restore option before an
    administrator purges it by hand
  Then the query is soft-deleted on delete, not hard-deleted, and
    reappears in "Общие запросы" exactly where it was on restore (BR-9,
    D-13)
```
