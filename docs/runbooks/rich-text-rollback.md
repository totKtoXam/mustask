# Runbook: rolling back rich-text descriptions (feat-018, ADR-0037)

When it applies: the release that makes the work item Описание rich text
(`descriptionFormat`, migration `AddWorkItemDescriptionFormat`) has been
deployed and something must be undone. Read the whole page before touching the
database.

## What the release changed in storage

- `work_items.description_format` (`Plain` or `Html`, default `Plain`).
  Every row that existed before keeps `Plain` and its description
  byte for byte: the migration rewrites no row.
- `ck_work_items_description_length` is widened from 100 000 to 1 000 000
  characters; `ck_work_items_plain_description_length` keeps 100 000 for
  `Plain`, and `ck_work_items_empty_description_plain` requires an empty
  description to be `Plain`.
- After the release, the panel writes `Html`. Those rows hold a reduced HTML
  string in `description`.

## Recommended: roll the code back, leave the migration

Deploying the previous release against the migrated database is safe:

- The old code does not know the column, so it ignores it (the default
  `Plain` applies to rows it inserts) and reads `description` as it always did.
- A row written as `Html` is then shown by the old panel as **raw tags in a
  plain field** (spec "Out of scope": the panel is the only client, there is no
  downgrade of html to plain). Nothing is lost; the old release's next save of
  such a description writes whatever text the user leaves, as plain.
- The old release writes `Plain` only (the column default). A description it
  has saved keeps its text; it is *not* promoted back to `Html`.
- The marker stays in the database, so the release can be rolled **forward**
  again with no data repair: every `Html` row still says so.

Do this unless there is a reason to remove the column.

## Rolling the migration back (`dotnet ef database update <previous>`)

Only if the schema itself must go. Understand the cost first:

1. **It is refused while any description is longer than 100 000 characters**
   (`RAISE EXCEPTION 'Cannot roll back AddWorkItemDescriptionFormat: …'`),
   before anything is dropped. Find the rows:

   ```sql
   SELECT id, project_id, number, description_format, char_length(description)
   FROM work_items
   WHERE char_length(description) > 100000;
   ```

   Decide per row (shorten it by hand, export it, or accept losing it); the
   migration will not truncate anything for you.
2. **It loses the marker.** After the down step, rows that were `Html` are
   indistinguishable from plain text that happens to contain tags: the
   previous release shows them as raw tags (as above) and nothing can tell them
   apart again. Rolling forward later re-creates the column with `Plain` for
   every row, so html rows would then be *read as plain text* by the new
   panel, with their markup visible. Export first if that matters:

   ```sql
   SELECT id, description FROM work_items WHERE description_format = 'Html';
   ```
3. Rows that fit stay as strings in a plain field; the 100 000-character check
   is restored.

## After a rollback of the code

- Nothing needs repair for the old release to run.
- If the migration was rolled back as well and the release is deployed again,
  run the migration forward first (`dotnet ef database update`), then the code.
- The shared vocabularies (`contracts/rich-text/vocabularies.json`) are not
  data in the database; a stored class token that a later release no longer
  knows is dropped by the next save of that description, not by a read.
