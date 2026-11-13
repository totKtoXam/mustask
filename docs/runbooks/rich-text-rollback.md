# Runbook: rolling back rich-text descriptions (feat-018, ADR-0039)

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

## Rolling the code back alone is NOT safe once `Html` rows exist

(Correction 2026-11-09, code review CR-2: an earlier version of this page called
a code-only rollback safe and the roll-forward repair-free. It is neither.)

The previous release does not know the column and never writes it: its saves
change `description` only, so `description_format` keeps whatever the row had.
Against the migrated database that breaks in two ways:

- **Clearing an `Html` description fails with a 500.** The old save writes
  `description = ''` and leaves the format `Html`, which violates
  `ck_work_items_empty_description_plain`.
- **Plain text saved over an `Html` row stays marked `Html`.** After rolling
  forward the new release reads it as html: line breaks collapse and a typed
  `<b>` becomes markup.
- Also, the old panel shows an `Html` description as raw tags in a plain field
  (spec "Out of scope": there is no downgrade of html to plain). Nothing is lost
  by that alone.

Supported procedures (pick one before the old release takes traffic):

1. **Roll the migration back too** (the section below). `Down` drops the
   column and all four description checks, so the old release runs cleanly;
   the cost is the marker (see below). This is the procedure with no hidden state.
2. **Keep the migration, drop the empty-description check for the time of the
   code rollback** (`ALTER TABLE work_items DROP CONSTRAINT
   ck_work_items_empty_description_plain;`). Then, **before rolling forward**,
   repair what the old release did and put the check back:

   ```sql
   -- rows the old release touched while it ran (replace the timestamp):
   UPDATE work_items SET description_format = 'Plain'
   WHERE changed_at >= TIMESTAMPTZ '<time the old release started>'
     AND description_format = 'Html';
   ALTER TABLE work_items ADD CONSTRAINT ck_work_items_empty_description_plain
     CHECK (description_format = 'Plain' OR description <> '');
   ```

   A row of the touched set whose text the user did not change is converted to
   `Plain` too, which shows its markup as text; accept that, or export first.

Do not run the old code against `Html` rows with neither step.

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

- After procedure 1 nothing needs repair for the old release to run; after
  procedure 2 the repair above is owed before rolling forward.
- If the migration was rolled back as well and the release is deployed again,
  run the migration forward first (`dotnet ef database update`), then the code.
- The shared vocabularies (`contracts/rich-text/vocabularies.json`) are not
  data in the database; a stored class token that a later release no longer
  knows is dropped by the next save of that description, not by a read.
