# US-4: A member imports and exports work items via CSV

- story: US-4
- depends-on: US-1, US-3
- shippable: no — release 3, M6 not founded in MVP (D-4); delivered by `feat-005-csv-import-export`, the only CSV owner in MUSTASK
- status: pending

As an **organization member**, I want **to export a query's results to CSV and
import work items from a CSV file**, so that **I can move data in and out
of MUSTASK in bulk**.

## Acceptance criteria

```gherkin
Scenario: Exporting query results to CSV
  Given a saved query with results (US-3) and its persisted column set
    (BR-M6-12)
  When a member, including a viewer, exports it
  Then a UTF-8 file with a BOM, semicolon-delimited, is produced containing
    exactly the query's persisted columns for every matching work item —
    export needs only read access to the query, so a viewer may export too
    (BR-M6-4, BR-M6-8)

Scenario: A file over the 5,000-row cap is refused, never truncated
  Given a query matching 5,001 work items, and a CSV import file of 5,001
    rows
  When a member exports the query, and imports the file
  Then the export is refused with a request to narrow the query, and the
    import is refused whole before any row is processed — no truncated file
    is produced and no work item is created or updated (BR-M6-8)

Scenario: Importing new work items from CSV
  Given a CSV file whose rows carry the work item fields of `doc-002`
    § Fields and no ID
  When a member imports it into a project
  Then a new work item is created per row, in its type's first state,
    through the same validation as a manual create (BR-M6-8) — one row is
    one transaction, so a row that fails validation is rejected and reported
    without blocking the rest of the file
  And a new-item row whose Статус names another state is rejected with that
    reason (`doc-002` § Fields)

Scenario: A viewer cannot import
  Given a user whose role in the organization is viewer
  When they try to import a CSV file
  Then the import is refused — only member/admin may write WorkItem
    records (doc-001 BR-5, BR-M6-8); a viewer may still export (see above)

Scenario: Importing updates to existing work items from CSV
  Given a CSV file whose rows name existing work items of the project by
    their full identifier, e.g. MSK-BUG-42
  When a member imports it
  Then the matching work items are updated field by field, one row per
    transaction, recorded in history the same way a manual edit is
    (BR-M6-8, doc-001 BR-4)
  And a row updating an item someone changed since the export still
    applies — last write wins per row, with history (doc-001 BR-11)

Scenario: Rows that target a deleted item or another project are rejected
  Given a CSV file with one row naming an item in «Удалена» and one naming
    an identifier of another project
  When a member imports it
  Then both rows are rejected with their reasons, the rest of the file is
    processed, and the deleted item is unchanged (BR-M6-8, doc-001 BR-14)

Scenario: Import is scoped to one project
  Given a CSV file
  When a member imports it
  Then every created or updated work item belongs to the single project the
    import was run against — there is no cross-project import (BR-M6-1)
```
