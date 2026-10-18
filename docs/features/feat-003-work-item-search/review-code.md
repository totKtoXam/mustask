# Code review — feat-003-work-item-search

## Round 1

Stage 06, one round over the whole diff `5de279f7..48a68f80` (74 files), clean context, read-only on code: no build, no test run (the QA pass runs them). Read: spec.md and US-1..US-3, review.md, design.md, api-contract.md, both plans, ADR-0030, ADR-0029 § 7 (the gate), mod-005 spec and design § 6, mod-017 api-contract § 3, mod-003 api-contract and status, `docs/conventions/backend.md` § Soft removal and `frontend.md` § Structure. This file sits beside `review.md` (which holds the requirements review, 16.6 KB) at the orchestrator's request.

**Verdict: changes-requested.** One major finding, CR-1. It is prose only (design.md), so the fix gets a delta read and no QA re-run. CR-2..CR-8 are minors: debts with their fix, owed no round.

The code does what the spec and the human's answers say. Every read of `work_items` starts from a named entry point. The keyword cannot inject anything: `strpos`, no `LIKE`. Statement counts are pinned, and the tests check what they claim to.

### Findings

**CR-1 — major (docs).** `design.md:131` says: "Filtered means any of the four has a value in the address, **a blank keyword** and a value that names nothing included". Three things in the same diff contradict it:
- BR-WS-9 ("a blank one sets no filter");
- BR-WS-11, the R-2 resolution ("a parameter with blank values only sets no filter");
- US-3 ("…the address carries no keyword, and «Очистить фильтры» is not offered").

The code follows the spec: `lib/workItemFilter.ts:60-79` drops blanks and `isFiltered` (`:105`) ignores them. A line that reuses this design for the views (feat-013, BR-WS-16) would make a blank keyword switch the band to "filtered". Fix: replace the sentence with "Filtered means any of the four has a value in the address, a value that names nothing included; a blank value is left out and sets no filter."

Resolution (fix round, frontend developer): fixed. `design.md` § 5 now reads "Filtered means any of the four has a value in the address, a value that names nothing included: a value that selects nothing is a filter that is on. A blank value is left out and sets no filter (BR-WS-9, BR-WS-11): an address whose only keyword is blank is not filtered, and «Очистить фильтры» is not offered." The code was right and is untouched. Two tests pin the sentence: `workItemFilter.test.ts` "judges an address" (`isFiltered(filterOf(...))`: blank only is false, blank beside a value that names nothing is true) and `WorkItemsPage.keyword.test.tsx` "a link whose only keyword is blank is no filter" (whole list, no «Очистить фильтры»).

**CR-2 — minor (frontend).** The panel and the server do not treat white space alike. The panel uses JS `trim()`: `valuesOf` `:51`, `filterOf` `:60`, `withFilter` `:80`, `matchesKeyword` `:170`. The server uses .NET `IsNullOrWhiteSpace`/`Trim()` (`WorkItemKeyword.cs:41,48`, `WorkItemFilter.cs:104`), and that set includes U+0085, which JS `trim()` keeps. Two crafted links show the gap:
- `?search=%C2%85оплат`: the server trims and lists two rows, but the create notice says «Он не подходит под текущий фильтр.». That breaks BR-WS-14's "never contradicts the list's next read".
- `?state=%C2%85`: the band reads «Фильтр задан» and offers «Очистить фильтры» over the whole list.

Fix: one panel helper that trims the .NET set (`\s` plus U+0085, without U+FEFF), used at those four sites. Alternatively, weaken BR-WS-14's "never".

**CR-3 — minor (docs).** `status.md:35` says api-contract.md has been "unchanged since 6ae3bed3". It has not: `d4dbf178` rewrote § 1.2's marks sentence (panel text, not the wire). The contract's header says it changes only with a status note, and there is no such note. Fix: a Log line naming the § 1.2 prose correction.

**CR-4 — minor (docs).** `spec.md:131` (Lines in flight) promises "a note in mod-017's status.md" for the restated shared filter specification. None was written. `mod-016-organization-access/api-contract.md:350` and spec BR-M1-26 still say "The «неактивен» and «не в организации» marks are feat-017's", although the list's column now carries them (OQ-2 A). feat-003 api-contract § 7 records the supersession, but mod-016's `status.md` does not. Fix: one Log line in mod-017's status.md and one in mod-016's, or drop the promise from spec.md.

**CR-5 — minor (docs).** `api-contract.md:112-113` (§ 6) lists six statements for the list. For the options it lists "the project, one for the ids, one for the memberships" plus ⌈people/500⌉ lookups. As built, the list takes 7 and the options take 4 + ⌈people/500⌉, because the project check is two statements. `FilterStatementCountTests.cs:38-41` pins the real numbers. `plan-backend.md` § As built records the deviation; the contract does not. Fix: "the project and its reachability" in § 6, with a status note.

**CR-6 — minor (frontend tests).** `WorkItemsPage.assignee.test.tsx:464-505`, the measured 2 000-person picker:
- It runs about 28 s on every affected frontend run, under a 300 s timeout.
- It asserts `< 100_000` ms against a measured 25 s, which pins nothing. So `plan-frontend.md:34`'s "a floor for regressions" overstates.
- It writes to `process.stdout`.

Fix: run it only on demand (`it.runIf(process.env.MEASURE_PICKER)`), or set the ceiling near twice the measured time. Drop the stdout write.

**CR-7 — minor (docs/backend).** `WorkItemSets.Listing` (`WorkItemSets.cs:62`) is a fourth public entry point. `docs/conventions/backend.md:273-277` still says WorkItemSets "offers `Live` … `Deleted` … and `InAnyState`". The class doc was updated; the convention was not. Fix: one clause in backend.md: "`Listing`, the project's `Live` ∪ `Deleted` when a state is chosen (feat-003)". ADR-0030 stays as accepted.

**CR-8 — minor (frontend).** The fields misread while their options load:
- `StateFilter.tsx:36` and `TeamFilter.tsx:41` show «Фильтр задан» for any value. So does `AssigneeFilter.tsx:69` while `tags` is empty. Until the process, team or options read returns there are no options, so a link carrying a valid value (`?state=Новая`) reads «Фильтр задан» first and only then flips to the name. D-4 means a value no option *can* name, and that is unknown while the read is pending.
- While the options read is pending or failed, `AssigneeFilter.tsx:72` passes `options={choices}` (empty), so «Не назначен» cannot be picked. design § 2.3 says it "is always present, so the list is never empty before a search".

Fix: show «Фильтр задан» only once the field's read has succeeded. Pass `known` (which holds «Не назначен») as the options while the read is pending or failed, or weaken the design sentence.

### Behavioural claims checked (item 6, direction 1)

- BR-WS-2, AND across dimensions and OR within one → `WorkItemFilterPredicate.cs:24-58`; `WorkItemKeywordEndpointTests.Search_AllFourFilters_Combine…`.
- BR-WS-3, name compared as stored (`новая` names nothing) → default collation on `state_definitions.name`, `WorkItemFilterPredicate.cs:44`; `DeletedItemSurfaceMatrixTests` row 1 (`удалена` → 0).
- BR-WS-4 / ADR-0030 rule 2, deleted items reached only under `state` and judged by category:
  - list: `WorkItemSets.cs:62-67` → `WorkItemReadModel.cs:113`;
  - options: `WorkItemReadModel.cs:167-177`;
  - pins: matrix rows `DeletedItemSurfaceMatrixTests.cs` (`LiveOrChosenDeleted`); parity in `DeletedStateParityTests` (list and options); `WorkItemSetsTests.Listing_…` (trap names).
- BR-WS-5, purge lands on the list it came from, else unfiltered, by replace → `WorkItemsPage.tsx:273` (state mark), `WorkItemFormPage.tsx:145-150`, `listOrigin.ts`; `WorkItemFormPage.landing.test.tsx`.
- BR-WS-6, options are the live assignees plus the deleted items' assignees under a chosen Removed name, in that order → `WorkItemReadModel.cs:157-190`; `UserRefOrder.Order`; `Options_*` endpoint tests.
- BR-WS-7 / OQ-2 A, one helper for the options and the column → `Marks.tsx` `MarkedAssigneeName`, `assigneeFilterOptions.ts:41-82` (both `userNameWithMarks`); `WorkItemReadModel.cs:408` (`isActive`).
- BR-WS-9, the keyword:
  - trim, first non-blank → `WorkItemKeyword.cs:41,48`;
  - 255, line breaks, NUL match nothing → `:50`, `WorkItemFilterPredicate.cs:70-72`;
  - `%` `_` `\` literal through `strpos`, and «ё» → `WorkItemFilterPredicate.cs:83-84`;
  - identifier ASCII-only and the addressed project's → `WorkItemKeyword.cs:75`;
  - pins: `WorkItemKeywordEndpointTests`.
- BR-WS-10 / NFR non-disclosure, one status and body for every kind of unnamed value → `WorkItemFilterNonDisclosureTests` (byte-equal bodies).
- BR-WS-11, one writer by replace that keeps other parameters, page 1 on any change → `useWorkItemFilter.ts:57`, `WorkItemsPage.tsx` (`filterContent` keyed page); `useWorkItemFilter.test.tsx`.
- BR-WS-13, both clears remove all four and focus «Поиск» → `WorkItemsPage.tsx:175-178`.
- BR-WS-14, notice judged by all four → `workItemFilter.ts:198-230`. "Never contradicts" holds except for the U+0085 case → CR-2.
- D-3, applied at 250 ms, on Enter, on leave → `KeywordFilter.tsx:72,81`, `useDebouncedValue.ts:4`; `KeywordFilter.test.tsx`.
- D-4, «Фильтр задан» for a value no option names → the three `emptyText` lines. Wrong while the options load → CR-8.
- Design § 2.3, «Не назначен» always present → not while the options read is pending or failed → CR-8.
- Design § 5 "a blank keyword … included" → contradicted by the code and the spec → CR-1.
- NFR query shape, statements constant with rows and values → `FilterStatementCountTests` (7; 4 + ⌈n/500⌉). The contract prose understates these → CR-5.
- NFR speed, 2 s at 100 000/5 000/500/2 000 → `WorkItemFilterVolumeTests` (asserts per request, and the answers' counts).
- api-contract § 5, inventory 59 → 60 and the OpenAPI operation → `OrganizationAccessInventoryTests.cs:237`, `OrganizationAddressedRoutes.cs:100`, `WorkItemsOpenApiTests.cs:25`.
- Retired test named with rule ids → `WorkItemFormPage.membership.test.tsx:103-108` (mod-016 design § 7 superseded by feat-003 OQ-2 A, BR-WS-7).
- status.md:35, "api-contract unchanged since 6ae3bed3" → false → CR-3.
- status.md:34-35, "No E2E spec breaks" → holds, see the E2E sweep below.

### Direction 2 (behaviour the diff changes, prose outside it)

- **List row `assignee.isActive` and the column's marks.** Grepped "marks are feat-017's", "reads like any other" and "isMember" under `docs/features/mod-00{3,5}`, `mod-016`, `mod-017`.
  - mod-003: noted.
  - mod-016 api-contract:350, spec BR-M1-26, design:752, US-1:121: stale for the list → CR-4.
- **`GET …/workitems` reaching deleted items under `state`.** Grepped "no filter to list deleted", "feat-003" and "no caller".
  - mod-005 spec BR-DR-6 and § Surfaces: accurate, they name feat-003.
  - mod-005 design § 6: amended in the diff.
  - mod-005 plan-backend:173 ("`Deleted` has no caller in this line"): historical, true for its line.
- **WorkItemSets entry points.** Grepped "WorkItemSets" in `docs/conventions`, `docs/adr`: backend.md:273-277 → CR-7. ADR-0030 is accepted and untouched.
- **The shared filter specification.** mod-017 api-contract § 3 anticipates feat-003. The status note the spec promises is missing → CR-4.
- **Not swept:** feat-040's branch, which shares `WorkItemsPage.tsx`, `queryKeys.ts` and `WorkItemFilterPredicate`. It is in flight and not in main; whichever of the two merges second reconciles.

### E2E (standing NO: not run, not edited)

Swept the specs that meet the list, the band or the column:
- `22-work-items` (22.1 changes fields on the card page, not the list);
- `27-work-item-card` (card-scoped locators);
- `27-organization-members.spec.ts:458-461` (the ex-member's row now reads «… (не в организации)»; `toContainText(name)` and `not.toContainText('неактивен')` both still hold);
- `29-role-views` (table and create button only);
- `30-assignee-membership` 30.3 (card page, unchanged until feat-017);
- `19-shell-in-a-browser` (the «Фильтры» group there is the languages registry's).

None is expected to break.

### For the human

- **D-1 A, the 2 000-person picker.** The kit is not virtualised. In jsdom it takes about 25 s to open with 2 001 options, which is no browser number. QA owes the stand timing. Recommended: keep D-1 A unless the stand shows the open over about 1 s; then D-1 B (the plain multi-select) is the fallback the design names. Not a code finding.

## Delta read 1

Stage 06, the one delta read (M cap), clean context. Read `ee77b2a4..84a3e873` (`a0fd8a14`, `94bc4ea5`, `84a3e873`; 8 files) against D-F1 and D-F3 (qa.md) and CR-1, plus what those lines touch. Checkout `wf_604530c7-e3a-7`, branch `feat/003-work-item-search` at `84a3e873`.

**Verdict: approve.** D-F1, D-F3 and CR-1 are resolved, and nothing nearby broke. One new minor, DR-1 (docs). No round is owed.

### The three ids

- **CR-1: resolved.** `design.md:131` now agrees with BR-WS-9, BR-WS-11 and US-3:50. The code is untouched, as round 1 said. Two tests pin the sentence, both green:
  - `workItemFilter.test.ts:102`: blank only is false, and blank beside a value that names nothing is true;
  - `WorkItemsPage.keyword.test.tsx:122`: the whole list, no «Очистить фильтры».
- **D-F1: resolved.** `WorkItemsPage.tsx:117-120` stores a new content with page 0 by a render-phase `setPaging`. `filterContent` returns a string (`workItemFilter.ts:120`), so the comparison is by content and the extra render settles at once. Pinned by `WorkItemsPage.qa.test.tsx:59,76,92`. On the pre-fix page, `:59` and `:76` fail and `:92` (page kept under an unchanged filter) passes: the tests pin both the fix and its limit.
- **D-F3: resolved.** `KeywordFilter.tsx:86-90` sets the text on leaving only when trimming changes it. A real trim is a state change, which renders before the next event, so it leaves nothing queued. Pinned by `WorkItemsPage.qa.test.tsx:150,165,181,196,213`. On the pre-fix field the first four fail and `:213` (spaces around the keyword) passes, as qa.md says. D-F4's `it.fails` (`:236`) still passes: D-F4 stands, a minor.

### Nearby

- Nothing outside `features/workitems` imports `KeywordFilter`.
- The fallback `paging.content === content ? … : 0` (`WorkItemsPage.tsx:121`) is now redundant. Harmless.
- ESLint and Prettier are clean on the two changed sources and the three changed tests.

### Runs (2026-10-19, this checkout)

| What | Result |
|---|---|
| `npx vitest run` on PQ, `WorkItemsPage.{keyword,filters}`, `KeywordFilter`, `lib/workItemFilter` | 5 files, 116 passed |
| PQ alone, with `KeywordFilter.tsx` and `WorkItemsPage.tsx` taken from `ee77b2a4` (restored afterwards, `git status` clean) | 6 failed (D-F1 ×2, D-F3 ×4), 8 passed |

### New finding

**DR-1: minor (docs).** Outside the two defect entries, qa.md still reads as it did before the fix:
- `:83`, `:84`, `:105` and `:107` mark D-F1 and D-F3 ✘;
- `:130` says every frontend defect's test is `it.fails`;
- `:175` reads "majors: 2 … result: fail until the two majors are fixed".

A gate-3 reader of the verdict line sees a QA that fails. Fix: a line under the frontend verdict, "After the fix round (`a0fd8a14`): majors 0, result pass; the D-F1 and D-F3 tests are plain". Then flip the four ✘ to ✔ (fixed).

### Behavioural claims checked (item 6)

- Direction 1:
  - `design.md:131` → `filterOf`/`isFiltered`, the two CR-1 tests.
  - qa.md D-F1 Resolution, "the first two fail on the old code" → the pre-fix run.
  - qa.md D-F3 Resolution, "without the fix the first four fail" and "D-F4 still stands" → the pre-fix run, `:236` green.
  - The comment at `WorkItemsPage.tsx:112-114` → `:118-120`.
  - The comment at `KeywordFilter.tsx:82-85` → `:87-89`.
  - The CR-1 Resolution, "two tests pin the sentence" → both exist and pass.
- Direction 2, the page on a return to an earlier content. Grepped "page 1", "first page" and "keeps the page" in mod-017, mod-020 and this line's docs. The hits all say any change shows page 1, which the fix makes true: mod-017 api-contract:188, US-5:34 and design:151; this line's design:103 and plan-frontend:22.
- Direction 2, the blur handler. Grepped "blur" and "leaving" in design.md and plan-frontend.md. design:55 and plan-frontend:35 still hold.
- Not swept: feat-040's branch (in flight, not in main).

### Still open, for the human at gate-3

All minors, debts, no round owed:
- Code review: CR-2..CR-8 (round 1) and DR-1.
- QA: D-B1, D-F2, D-F4, D-D1 (the same gap as CR-4).
- Security: S-1, S-2.

D-1 A: QA timed the 2 000-person picker at 0.35–0.39 s to open on a quiet machine and 0.77 s under load (qa.md F-8). That is under round 1's 1 s threshold, so D-1 A stands.
