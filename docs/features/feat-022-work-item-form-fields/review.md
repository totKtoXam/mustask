# Code review: feat-022-work-item-form-fields

## Round 1 — whole task

Reviewed at `1054df3`, the tip of `feat/022-work-item-form-fields`, against
the merge base `fc039cf`. The range is `git diff fc039cf..1054df3`: 81
files. The code is in five commits:

- `ac9e66c`, the backend: domain, migration `20261009055512_AddWorkItemFormFields`,
  API, read models, error catalog;
- `28bdc86`, `76d4084`, `2f35bde`, the frontend: API types and draft, the
  sectioned form, «Приоритет» on the backlog row and the card;
- `87abca2`, E2E spec 25.

The rest of the range is documents.

This is the one round over the whole task, backend and frontend, with
security in its scope (status.md D-1). There is no delta round and no
separate security pass. These decisions are binding:

- D-1..D-6;
- D-7: the PATCH's "empty" shape, and the create taking none of the new
  fields;
- D-3: every signed-in user may do everything inside an active organization.

Known and decided, not reported:

- the timing of `BacklogVolumeTests`;
- the E2E run in parallel;
- the short route without QA, design or security sessions;
- «Связанная работа» last in the right column (the developer's deviation 1).
  F1 does not dispute that placement. It reports that the spec still
  describes the old one.

**Verdict: changes-requested.** No blocker, one major (F1, prose only, no
code), four minors (m1–m4). The minors are debts, each with its proposed
fix.

### What was checked and holds

- **The type is recognised by `type.appearance`, never by name, and that is
  safe with custom types.** The server reads `WorkItemTypeStates.Appearance`
  (`WorkItemTypeStates.cs`) and `WorkItemFieldSet.Of` (`WorkItemField.cs:115-125`).
  The panel reads `layoutOf` and `fieldsOf` (`fields.ts:303-312`). The test
  `WorkItemFormPage.sections.test.tsx:507` gives a bug another name and key,
  and the form still shows a bug's sections.
  - Today no process can carry a custom type. Processes are built only
    from `SeededProcess.Blueprint` (`ProcessDefinition.cs:60-90`), and the
    only process route is a GET (`ProcessEndpoints`, `:26`).
  - The token is a closed enum. Its EF converter throws on any other name
    (`AppearanceToken.cs:36-44`, `WorkItemTypeConfiguration.cs:27-30`), and
    it is unique per process (`:48-50`). So `WorkItemFieldSet.Of`'s throw
    on an unknown kind cannot be reached.
  - An unknown appearance on the panel gets no section and no field, so
    nothing is sent.
  - If release 3 lets custom types share a token
    (`WorkItemTypeConfiguration.cs:10-12`), they would share that token's
    fields. That is consistent, and it leaks nothing.
- **Values: ranges, precision, lists.** `WorkItemFieldRules.cs:69-131`
  checks:
  - range first, then precision;
  - decimals with `decimal.Round(v, 2) != v`, so «2.50» is accepted and
    nothing is rounded;
  - the integer with `Truncate`;
  - list keys as ordinal member names. A number or a comma list is not a
    key, and neither is a key in another case (tests at
    `WorkItemFormFieldsEndpointTests.cs:443`).

  The database backs these with `numeric(6,2)`, 18 check constraints and a
  `priority` default of 2 (`WorkItemsConstraints.cs`, migration `:148-236`).
  Texts are counted in UTF-16 units on both sides. The database's
  `char_length` counts code points, so it is never stricter than the domain.
  For what the binding does before these checks, see m1.
- **Target before start.** `WorkItem.CheckFields` (`WorkItem.cs:228-240`)
  takes the stored value for a date that was not sent. It reports on
  `targetDate`. It skips the check when either date has a reason of its
  own, `field_not_on_type` included.
  - The handler takes the item `FOR UPDATE` (`SaveWorkItemHandler.cs:83`)
    before it validates (`:94`), so two saves of one item check against
    what the first stored.
  - The database has `ck_work_items_target_not_before_start` (`target_date
    >= start_date`, which passes when either is NULL).
  - The browser runs the same rule on the draft (`fieldValues.ts:225-237`).
    Editing «Дата начала» clears the target's error
    (`WorkItemFormPage.tsx:234-244`).

  For the one direction no test covers, see m4.
- **`field_not_on_type`.** Every sent field is checked against the type
  first, including `{ value: null }`, `{}` and `""`. Nothing else is checked
  on that field (`WorkItem.cs:447-501`). `Save` throws if it is called with
  unchecked fields (`:261`; test `WorkItemFieldsTests.cs:394`). The panel
  never sends a field its type lacks (`fieldValues.ts:175-193`; test
  `draft.test.ts:279`). If one comes back, `refusals.ts:89` puts it in the
  `Alert` (test `WorkItemFormPage.sections.test.tsx:486`).
- **"Empty" and "unchanged" (D-7), field by field.**
  - Binding is `FieldRequest<T>(T Value)` (`WorkItemEndpoints.cs:261`).
    System.Text.Json fills a missing constructor parameter with its
    default, so `{}` binds as an empty value. I checked this in the same
    scratch program as m1.
  - A top-level `null`, or an absent member, leaves `FieldsOf` with a
    `null` member (`:171-192`), and `Apply` skips it (`WorkItem.cs:338-430`).
  - Emptying an always-set field stores its default: `priority` 2,
    `severity` 3, `valueArea` `Business`. Texts are emptied by `""`.
  - Equal values change nothing: `EqualityComparer<decimal?>` treats 2.50
    as 2.5 (`:434-444`), and «Изменён» stays.

  Tests: `WorkItemFormFieldsEndpointTests.cs:141`, `:566`, `:588`.
- **Defaults on create and in the migration.**
  - `WorkItem.Create` → `TakeDefaults` (`WorkItem.cs:323-331`).
  - The create test at `:85` checks all 17 fields per type in the create's
    answer and in the read.
  - The migration (`20261009055512_AddWorkItemFormFields.cs`):
    1. adds `priority` NOT NULL DEFAULT 2;
    2. back-fills only `severity`, `value_area` and the three texts, by
       `work_item_types.appearance` (`:135-146`);
    3. adds the checks after the back-fill.

    `changed_at` and stack ranks are untouched.
  - Down drops the 18 checks, then the 17 columns.
  - `AddWorkItemFormFieldsMigrationTests.cs:32` rolls back and re-applies.
    It then asserts, for each of the five types, the values, every other
    field NULL and `changed_at` kept.
- **The backlog and the board gain `priority`.**
  - One scalar of the already-joined item is added to the existing
    projection (`BacklogReadModel.cs:45`). There is no join and no
    statement more, so the query's shape at 5 000 items is unchanged.
  - `BoardViews.cs` passes it to top-level rows, nested rows and cards.
  - Tests: `BacklogPriorityEndpointTests.cs:52` (nested included), `:73`
    and `:89` (a reorder and a card move change no priority).
- **Authorization and IDOR are unchanged.** No route is added. The PATCH
  keeps `AuthenticatedOnly(SaveAccess)` and `RequiresOrganizationContext()`
  (`WorkItemEndpoints.cs:81-83`).
  - The handler resolves the organization, the reachable project owned by
    it, the identifier's key, the item under its lock and the type key
    (`SaveWorkItemHandler.cs:55-92`). Only then does any field reason run.
  - The reads add columns to their existing filtered projections.
  - No SQL is built from input. The migration's UPDATE and the check
    expressions are constants built from enum names (`WorkItemsConstraints.cs`).
  - There are no secrets and no new logging. Error bodies carry catalog
    texts only.
- **Error codes.**
  - Eight codes, all 400 field reasons without parameters.
  - Their `ru` texts match spec § Error codes and api-contract § 6 to the
    letter (`WorkItemErrorCatalogTests.cs:39-48`).
  - `docs/error-codes.md` is regenerated.
  - The panel's constants and drift test are at
    `errorCodes.workitems.test.ts` (25 rows).
  - The old reasons and the new ones come in one answer (test
    `WorkItemFormFieldsEndpointTests.cs:543`).
- **The panel's draft.**
  - `FieldsDraft` holds each field as its control does.
  - `differs` compares numbers as numbers: «2,50» against 2,5 is no change,
    and a non-number counts as a change (`fieldValues.ts:131-145`).
  - `parseNumber` counts decimals without trailing zeros, as the server's
    `Round` equality does (`:66-77`).
  - `sentValue` (`:152-172`) sends:
    - texts as strings;
    - every other field in `{ value }`, emptied as `{ value: null }`;
    - list keys as numbers for `priority`, `risk` and `severity`.
  - A non-number or a part-typed date is never sent but keeps «Сохранить»
    on (`draft.ts:111`), so its check can speak.
- **Sections and layout.**
  - `fields.ts:245-294` matches the spec's table per type, field order
    included.
  - The grid is `2fr 1fr` at `lg` (1280 px), and one column below it
    (`WorkItemFormPage.tsx:62-76`).
  - Below 1280 px the right column's controls are capped at
    `layout.formColumnWidth` (`:415`). The ParentPicker carries the same
    cap.
  - The DOM order is the fold order and the Tab order (tests
    `sections.test.tsx:64`, `:90`).
  - `useMediaQuery` under `createRoot` reads the real media on the first
    render, so the wide form does not flash as one column.
  - Each section is an `h2` region (`FormSection.tsx`).
- **Dates on the panel.**
  - `FieldControl.tsx:111-145` uses `type="date"`, a raised label and
    min/max of 2000–2099.
  - A part-typed date is read from `validity.badInput` on each key, as
    `INCOMPLETE_DATE`.
  - The input stays controlled at `''` while a date is incomplete. The
    browser keeps the partial entry, because React does not rewrite an
    equal value.
- **Field-level refusals.** Every reason keyed by one of the type's JSON
  names lands on that field (`refusals.ts`, `WorkItemFormPage.tsx:223`). A
  reason about a field the form lacks, and `field_not_on_type`, go to the
  `Alert`.
- **«Приоритет» on the backlog and the card.**
  - `backlogColumns.ts:21` serves both the table and the skeleton.
  - `BacklogTable.tsx:772` is in `BacklogLine`, which draws top-level and
    nested rows.
  - The card's line is `BoardColumns.tsx:433-442`, a `caption` after the
    assignee.
  - A form save reaches both pages on their next open: the backlog and the
    board queries use the default `staleTime` 0 (test
    `BacklogPage.priority.test.tsx:141`).
- **feat-001's «Связанная работа» and its parent draft.**
  - `changesOf` keeps the `parent`/`seenParentId` logic (`draft.ts:71-98`).
  - `rebaseDraft` also carries the fields per type the user touched
    (`:143-168`). The other fields come from the fresh read.
  - `RelatedWorkSection` changed only by losing its top margin.
  - The pre-existing tests the diff edits only add the column, or narrow a
    helper query to the «Описание» region. None is weakened.
- **Conventions.**
  - The rules live in Domain (`WorkItemFieldSet`, `WorkItemFieldRules`).
  - The handler stays one write slice, and the reads are `AsNoTracking`
    projections.
  - On the panel the field logic is in `lib/`, there is no cross-slice
    import beyond the existing barrels, and only MUI core is used.
  - The reference tracker is not named anywhere in the diff.
- **Tests.**
  - Backend: every server-side scenario of US-1..US-6 is named in
    `WorkItemFormFieldsEndpointTests` or `BacklogPriorityEndpointTests` and
    asserts what it names. I read both classes in full, and the migration
    test. I matched the domain tests by name and read the date-order ones.
  - Frontend: I read `WorkItemFormPage.sections.test.tsx` and
    `.planning.test.tsx` in full. I read `BacklogPage.priority.test.tsx` in
    part. I matched the unit tests by title.
  - US-1 "The form stays usable at 640 px" (no sideways scroll, no
    overlap) can only be measured in a browser. E2E 25.1 carries it.
  - US-1 "the dialog … asks only «Тип» and «Заголовок» … seven columns"
    rests on mod-003's untouched tests and on the list-row test at `:99`.
  - The one uncovered direction is m4.

### Findings

| # | Severity | Where | Finding | Proposed fix |
|---|---|---|---|---|
| F1 | major | `docs/features/feat-022-work-item-form-fields/spec.md:115` (BR-M3-30), `:148-153`, `:169-177`; `stories/US-1-sectioned-form.md:16-22`, `:34-38` | **The spec and US-1 still describe two behaviours that this diff decided otherwise.** Both decisions stand and are not disputed. The sentences are the defect: each is contradicted by a sibling file of the same diff, which stage 8 § Behavioural claims makes major regardless.<br><br>1. BR-M3-30: "a save **or creation** carrying a value for a field the item's type lacks is refused (`field_not_on_type`)". The create refuses nothing:<br>- its request has no such members (`CreateWorkItemRequest`, `WorkItemEndpoints.cs:226-227`);<br>- api-contract § 3.1 says such a member "is ignored" (`api-contract.md:122-123`);<br>- D-7 accepts that;<br>- `WorkItemFormFieldsEndpointTests.cs:99` asserts a 201 with `priority` and `remainingWork` ignored.<br><br>2. US-1 "Each type shows its own sections" lists the right column as «Планирование» plus «Классификация» or «Трудозатраты (часы)», "**And no other section is shown**". The fold scenario and spec § The form's sections and § Folding give the same order. The form also shows «Связанная работа», last in the right column:<br>- the tests assert it (`WorkItemFormPage.sections.test.tsx:56-57`, `:76-83`);<br>- the frontend Log (`status.md:46`) records the deviation from "the spec's section table [which] predates feat-001's merge".<br><br>spec.md is what QA, the E2E author and the next form features read: feat-002's «Обсуждение», feat-016's tags and mod-005's read-only form. Acting on BR-M3-30, a test of create would expect a 400. Acting on US-1, a layout would leave «Связанная работа» out. | **Prose only.** No code or test changes, since the tests already assert the built behaviour.<br><br>- BR-M3-30 → "…a save carrying a value for a field the item's type lacks is refused (`field_not_on_type`) and stores nothing. The create takes none of these fields (BR-M3-40). A member naming one is ignored, as is any member the create does not know (D-7)."<br>- US-1: in the Examples' right column, or in the step "no other section is shown", add "then feat-001's «Связанная работа»". The fold scenario's order ends with «Связанная работа».<br>- spec § The form's sections: add a note under the table, "feat-001's «Связанная работа» stands last in the right column (mod-015 § 10.3)". End § Folding's order with it. |
| m1 | minor | `docs/features/feat-022-work-item-form-fields/api-contract.md:192-196`; `src/backend/Mustask.Api/Features/WorkItems/WorkItemEndpoints.cs:229-234`; spec BR-M3-32 (`spec.md:117`) and api-contract § 1 "Nothing is rounded" | **Two binding claims overstate.** "A string where a number belongs fails binding" does not hold for a numeric string. "Never rounded" holds only up to `decimal`'s 28 significant digits.<br><br>Minimal APIs bind with `JsonSerializerDefaults.Web`, which sets `NumberHandling = AllowReadingFromString`. Nothing in `src/backend` overrides it. Checked with a scratch program under the same options:<br>- `{"value":"5"}` binds as 5 for `decimal?`, and `{"value":"3"}` as 3 for `int?`. Both would be stored, not answered with a 400. Only a non-numeric string fails, which is what the theory at `WorkItemFormFieldsEndpointTests.cs:333` uses.<br>- `{"value":2.0000000000000000000000000000001}` binds as 2.0000…0. It passes `ValidateDecimal` (`WorkItemFieldRules.cs:76`) and stores 2 instead of answering `number_too_precise`.<br><br>The panel sends neither, so nothing an agent does differs. | Weaken the sentences:<br>- "a string that is not a number (a numeric string is read as that number, as on every route of this API)";<br>- "never rounded" → "never rounded within `decimal`'s 28 significant digits".<br><br>Optionally pin the first with a positive row (`{"value":"5"}` → 200). Strict number handling for this body alone would make the PATCH the one route that differs, so it is not recommended. |
| m2 | minor | `src/backend/Mustask.Domain/WorkItems/WorkItemFieldRules.cs:100-105` (pattern from mod-003's title and description) | **A type text holding U+0000 answers 500, not a field reason.**<br>- PostgreSQL `text` cannot store NUL (SQLSTATE 22021).<br>- No rule refuses it: `ValidateText` checks only the length.<br>- No handler maps that `PostgresException`; only unique violations are mapped (`PostgresConflictDetection.cs:19`).<br><br>mod-003's title and description already behave this way. This diff adds three more texts of up to 100 000 characters with the same gap. Nothing is stored and nothing leaks, so it is input validation debt. Found by reading; not run. | One rule for every work item text: title, description and the three. Refuse U+0000 with a field reason (a new code), or strip it, and add an integration test per text. Queue it beside mod-003's debts, since the title and description share the fix. |
| m3 | minor | prose outside the diff: `docs/features/feat-001-work-item-links/spec.md:149`; `docs/features/mod-003-work-item-core/spec.md:230` | **Older specs still describe the form as one column.**<br>- feat-001: "Below «Описание», an `h2` section «Связанная работа»".<br>- mod-003 § States: "Loading: … `FormSkeleton` for «Описание»".<br><br>feat-022 re-laid the form. «Связанная работа» is now last in the right column, and loading shows one `FormSkeleton` per column. Nothing marks either sentence superseded. mod-003's and mod-006's contracts did get their Log notes in this diff; their page prose did not. | Append "superseded by feat-022 § The form's sections / § States" to both lines. Or add one Log line each to feat-001's and mod-003's `status.md`, as was done for the contracts. |
| m4 | minor | `src/backend/tests/Mustask.Domain.Tests/WorkItems/WorkItemFieldsTests.cs:235-290`; `src/backend/tests/Mustask.Api.IntegrationTests/Features/WorkItems/WorkItemFormFieldsEndpointTests.cs:293`; `src/frontend/src/features/workitems/pages/WorkItemFormPage.planning.test.tsx:218`; `src/frontend/src/features/workitems/lib/draft.test.ts:353` | **No test at any level covers "the target sent alone, earlier than the stored start".** BR-M3-35 and api-contract § 3.2 check 3 cover both directions ("the stored values standing for the date not sent"). Every test either sends the start alone against a stored target, or sends both. The code is symmetric (`WorkItem.cs:233-234`, `fieldValues.ts:225-237`), so this is a coverage gap, not a defect. | Add a domain case: stored start 2027-04-01, send `targetDate` 2027-03-31 → `target_before_start` on `targetDate`. Add the same case to `draft.test.ts`'s browser checks. |

### Behavioural claims in prose (item 6)

Direction 1 covers claims in prose the diff adds, read against the code.

- api-contract § 0, "recognised by … `type.appearance` … never by its
  Russian name … never by the key". Anchors:
  - `WorkItemField.cs:115-125`
  - `SaveWorkItemHandler.cs` (`ItemType` with `type.Appearance`)
  - `fields.ts:303-312`
  - test `sections.test.tsx:507`

  Holds. "Which the process holds fixed and unique per type" is anchored
  to `WorkItemTypeConfiguration.cs:48-50` and holds.
- § 1, "The list keys are case-sensitive and spelled exactly as above".
  Anchors: `WorkItemFieldRules.cs:124-131` and the test at `:443`
  ("development" refused). Holds.
- § 1, "Responses write a number in its shortest form". Anchors:
  `WorkItemReadModel.cs:278-281` and the test at `:203` (13.50 → 13.5).
  Holds.
- § 1 and BR-M3-32, "Nothing is rounded, clamped". Anchor:
  `WorkItemFieldRules.cs:76-95`. Holds within 28 significant digits → **m1**.
- § 1 and BR-M3-36, "Texts … stored and answered exactly as sent".
  Anchors: `WorkItem.cs:415-428` (no trim) and the test at `:474`. Holds.
- § 2, "all 17 fields … on every type … the read, the create and the
  save". Anchors: `IWorkItemReadModel.cs` (`required` init members) and the
  tests at `:85` and `:133`. Holds.
- § 3.1, "A member of § 1's names in a create body is ignored". Anchors:
  `WorkItemEndpoints.cs:226-227` and the test at `:99`. Holds, and BR-M3-30
  contradicts it → **F1**.
- § 3.2 table, "`{ "value": null }`, or `{}` empties; absent, or `null`
  leaves unchanged". Anchors:
  - `WorkItemEndpoints.cs:171-192`, `:261`
  - `WorkItem.cs:338-430`
  - tests `:141`, `:588`

  Holds.
- § 3.2, "A value equal to the stored one changes nothing — numbers compare
  as numbers". Anchors: `WorkItem.cs:434-444` and the test at `:566`.
  Holds.
- § 3.2 checks 1–3 with "one reason per field, the first that applies", and
  "not checked when either date has a reason of its own". Anchors:
  `WorkItem.cs:202-240`, `:447-501` and `WorkItemFieldsTests.cs:266`.
  Holds.
- § 3.2, "A value of the wrong JSON type fails binding … a string where a
  number belongs". Holds for non-numeric strings, wrappers, dates and
  fractions for integers (test `:336`). It fails for numeric strings →
  **m1**.
- § 3.2, "Every check runs on the item as stored under the save's lock".
  Anchor: `SaveWorkItemHandler.cs:83`, `:94`. Holds. There is no
  concurrency test; the ordering is plain in the code.
- § 3.2, the order of answers: the 400 before the area's and the parent's
  404. Anchors: `SaveWorkItemHandler.cs:94-104` and the test at `:543`.
  Holds.
- § 4, "every item that exists … the values a new item of its type takes …
  not `changedAt`". Anchors: migration `:57-62`, `:135-146` and
  `AddWorkItemFormFieldsMigrationTests.cs:32`. Holds.
- § 5, "every top-level row and every nested row … every card carries
  `priority`", and "a reorder or a card move changes no item's `priority`".
  Anchors:
  - `BacklogReadModel.cs:45`
  - `BoardViews.cs`
  - `BacklogPriorityEndpointTests.cs:52`, `:73`, `:89`

  Holds.
- § 7 and the Logs of mod-003 and mod-006, "recorded in its api-contract
  and Log". Anchor: the diff of both contracts and both `status.md`. Holds.
- spec BR-M3-30, "a save or creation … is refused" → **F1** (creation).
- spec BR-M3-31, BR-M3-34, BR-M3-37, BR-M3-38, BR-M3-39 and BR-M3-40.
  Anchors are the ones above, plus the test at `:415` (a state change
  leaves the estimates). All hold.
- spec § The form's sections, the table and "A section a type lacks is
  absent". Anchors: `fields.ts:245-294` and the test at
  `sections.test.tsx:34`. The sections hold; the table omits «Связанная
  работа» → **F1**.
- spec § Folding, "Below 1280 px … in the order … — and the right column's
  controls keep at most `layout.formColumnWidth`". Anchors:
  `WorkItemFormPage.tsx:62-76`, `:415` and the test at `:64`. The width
  holds; the order omits «Связанная работа» → **F1**.
- spec § Controls and copy (labels, placeholders, helpers, input modes,
  `select` options, «Не указан»). Anchors:
  - `fields.ts:76-225`
  - `FieldControl.tsx`
  - tests `sections.test.tsx:422`, `:474`
  - test `planning.test.tsx:366`

  Holds.
- spec § Controls and copy, the check table. Anchors:
  `fieldValues.ts:80-113`, `:204-239`, `rules.ts:18-26` and the tests at
  `planning.test.tsx:162-301`. Holds.
- spec § Saving, "«2,50» against a stored 2,5 is no change … a text that is
  not a number counts as a change … every field … disabled".
  - Anchors: `fieldValues.ts:131-145`, `draft.ts:111`,
    `WorkItemFormPage.tsx:221`.
  - Tests: `sections.test.tsx:215`, `:232`.

  Holds.
- spec § States, "a `FormSkeleton` in each column". Anchors:
  `WorkItemFormPage.tsx:166-191` and the test at `:272`. Holds.
- spec § Backlog and board. Anchors:
  - `backlogColumns.ts:21`
  - `BacklogTable.tsx:772`
  - `BoardColumns.tsx:433-442`
  - `BacklogPage.priority.test.tsx:46`, `:94`, `:123`

  Holds.
- The status.md Plan's steps 1–5 and F1–F4. Each step's claim is anchored
  by the items above. Holds.
- The frontend Log's deviations:
  - "decimals counted without trailing zeros" → `fieldValues.ts:75`;
  - "`validity.badInput` on each key" → `FieldControl.tsx:119-141`;
  - "editing «Дата начала» also clears «Целевая дата»'s error" →
    `WorkItemFormPage.tsx:239-243`.

  Holds. The test counts (+97, +68; 48, 61) were not recounted.
- Code comments:
  - `WorkItemConfiguration.cs:44-46` ("Priority defaults to 2 in the
    database too");
  - `WorkItemsConstraints.cs` ("A check passes on NULL");
  - `WorkItemReadModel.cs:150-153` ("The list leaves … the type's texts
    out");
  - `RelatedWorkSection.tsx:14-17` ("as mod-015 § 10.3 places it",
    anchored to `mod-015 design.md:939`, `:950`).

  Hold.
- No `AGENTS.md` file is in the diff.

Direction 2 covers prose outside the diff about behaviour the diff changes.

- The form's layout, with «Связанная работа» moved and one skeleton per
  column. I grepped «Связанная работа», "below «Описание»" and
  `FormSkeleton` under `docs/`. Hits:
  - feat-001 `spec.md:149` → **m3**;
  - feat-001 `spec.md:163` ("FormSkeleton covers the section"), which is
    still true;
  - feat-001 `status.md:35`, a stage plan and historical;
  - mod-003 `spec.md:230` → **m3**;
  - mod-015 `design.md:939`, `:950`, which already place it on the right.
- `priority` on backlog rows and cards, and the «Приоритет» column. I
  grepped «Приоритет», `priority` and `BacklogItem` in the specs and
  contracts of mod-003, mod-006 and feat-001. Hits:
  - mod-006 `spec.md:46` and `:172` (BR-M4-14: "Приоритет and Теги join
    them with their own lines") anticipate this task and hold;
  - mod-003 `spec.md:47`, `:313` and `:335` name the follow-up line this
    task absorbed, and are historical;
  - both contracts are annotated in the diff.

  None is stale.
- The PATCH body and `WorkItem`'s 17 fields, and the error codes. mod-003's
  contract and Log are annotated in the diff, and `error-codes.md` is
  regenerated. None is stale.
- The absorbed queue lines. `docs/backlog/withdrawn.md:18-20` already
  withdraws feat-019, feat-007 and feat-014. The amendments D-6 owes (mod-011,
  feat-015, feat-018, feat-021, doc-011) are the orchestrator's at gate-3,
  and were not swept.
- `AGENTS.md` files: no hit for «Приоритет», `priority`, «Связанная работа»
  or `appearance`.
- Not swept: the stand runbook, and any E2E index for file 25. There is no
  `e2e/README`, and the run is the executor's.

### Not read

These were not read:

- the generated migration Designer and the model snapshot;
- the frontend test doubles (`workitems/testApi.ts`, `testFields.ts`,
  `testFixtures.ts`, the boards test fixtures), beyond the save emulation
  and `defaultFields`;
- `draft.test.ts`, `fieldValues.test.ts`, `fields.test.ts`,
  `refusals.test.ts` and `workItemsApi.test.ts` beyond their titles and a
  few bodies;
- `BacklogPage.priority.test.tsx`'s first and last cases beyond a skim;
- `WorkItemFieldsTests.cs` beyond its names and the date-order block;
- E2E spec 25 beyond its header and titles. It is running now, outside
  this round;
- the Kazakh texts in `contracts/errors/workitems.json`.

Tests run: none of the repository's. One scratch console program outside
the repository checked System.Text.Json's Web defaults (m1 and `{}`).

Could not check by reading:

- how Firefox and Safari report a part-typed `type="date"` field.
  `validity.badInput` on `keyup` is exercised only in jsdom. E2E 25 runs
  Chromium;
- the layout at real widths: no sideways scroll at 640 px, and the
  ParentPicker's 256 px minimum in the right column at exactly 1280 px with
  the navigation open. Only E2E 25.1 carries these;
- m2's 500 itself. It is inferred from PostgreSQL's rule on NUL in `text`
  and from the absence of any mapping; it was not run.
