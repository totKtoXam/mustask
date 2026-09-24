# US-4: A team member imports and exports work items via CSV

- story: US-4
- depends-on: US-1, US-3
- shippable: no — release 2/3, M6 not founded in MVP (D-4)
- status: pending

As a **team member**, I want **to export a query's results to CSV and
import work items from a CSV file**, so that **I can move data in and out
of MUSTASK in bulk**.

## Acceptance criteria

```gherkin
Scenario: Exporting query results to CSV
  Given a saved query with results (US-3) and its persisted column set
    (BR-12)
  When a team member, including a viewer, exports it
  Then a UTF-8 file with a BOM, semicolon-delimited, is produced containing
    exactly the query's persisted columns for every matching work item —
    export needs only read access to the query, so a viewer may export too
    (BR-4, BR-8)

Scenario: A file over the 5,000-row cap is refused, never truncated
  Given a query matching 5,001 work items, and a CSV import file of 5,001
    rows
  When a member exports the query, and imports the file
  Then the export is refused with a request to narrow the query, and the
    import is refused whole before any row is processed — no truncated file
    is produced and no work item is created or updated (BR-8)

Scenario: Importing new work items from CSV
  Given a CSV file with rows matching the MVP work item fields (M3) and no
    existing work item id
  When a member imports it into a project
  Then a new work item is created per row, through the same validation as a
    manual create (BR-8) — one row is one transaction, so a row that fails
    validation is rejected and reported without blocking the rest of the
    file

Scenario: A viewer cannot import
  Given a user whose organization role is viewer
  When they try to import a CSV file
  Then the import is refused — only member/admin may write WorkItem
    records (doc-001 BR-5, BR-8); a viewer may still export (see above)

Scenario: Importing updates to existing work items from CSV
  Given a CSV file whose rows reference existing work item ids in the
    project
  When a member imports it
  Then the matching work items are updated field by field, one row per
    transaction (BR-8), recorded in history the same way a manual edit is
    (M3, doc-001 BR-4)

Scenario: Import is scoped to one project
  Given a CSV file
  When a member imports it
  Then every created or updated work item belongs to the single project the
    import was run against — there is no cross-project import (BR-1)
```
