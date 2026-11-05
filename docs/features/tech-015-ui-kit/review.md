# tech-015-ui-kit — review

## Round 1

Scope: `git diff main...tech/015-ui-kit` at 42b1de6 (merge base 5614f58; main has
moved only by queue commits since, so the baseline needs no re-cut). Frontend only,
M, one round. Binding: status.md D-1..D-6 and the Plan, design.md § 3, § 5–§ 7,
§ 9–§ 13, `docs/conventions/ui-kit.md`.

Verdict: **changes-requested**. Open: 0 blocker, 3 major (R1-1..R1-3), 6 minor
(R1-4..R1-9).

Runs on this tree: `npm test` 206 of 207 files green, 3295 tests. The one red test,
`WorkItemFormPage.sections.test.tsx` feat-022 US-4 «a new bug’s two texts…», hit the
20 s limit under the full parallel load (my own runs were going at the same time).
Alone it passes in 9.8 s. That slice is not on the kit. `npm run lint` clean;
`npm run build` (tsc -b, e2e included) green; `npx playwright test --list` lists 11
scenarios of spec 28. Scratch probes and profiles were deleted, nothing of them
committed.

What holds:
- The lift keeps every value (§ 3.4: 16, 24/20, 24/16, 24/18, 16, 16/7/20, 20).
  The six boards reads and the two tests read the new keys.
- `TypeMark`: `small` is 1.25 rem = 20 px, the same as `mark.typeIcon`, and the
  `Removed` ring is still 2 px. `components.ts` and `theme.ts` change no value. On
  the backlog and board nothing moves; the team head's overflow becomes «⋮», as
  § 5.6 orders.
- The showcase is gated by `can('dictionaries.languages.write')`. The slice
  imports nothing from `shared/api` or from another slice's API, so it makes no
  request.
- The ratchet holds: a raised count, a new file, an unknown `until`, a stale entry
  and the order all fail. ALLOWED is 126, BASELINE 128.
- KIT-19, checked by hand until tech-016: every barrel component has a showcase
  entry, except the table family and the legacy flat parts, both D-6's.

### Findings

**R1-1 — major — `src/frontend/src/shared/ui/fields/InlineDateField.tsx:37-43`; `InlineDateField.test.tsx:52-68`.**
A partial edit of a filled date snaps back, and the kit never reports that the
entry is complete again. In Chromium, clearing one segment of «12.10.2026» makes
`value` `''` with `badInput` and fires `input`. The kit calls only `onIncomplete`
and leaves the prop at `'2026-10-12'`. React's controlled-input restore then
writes the old date back.

I reproduced it in jsdom with a scratch host. After `change` to `''` with
`badInput`, `input.value` is `"2026-10-12"` again, with or without `onIncomplete`.
A host that keeps an "incomplete" flag also never clears it when the user types
the same date back, because `onChange` never fires. Save then says «Введите дату
полностью.» under a complete date.

feat-022's `FieldControl.tsx:119-123` avoided this by reporting a sentinel, so
the DOM and the prop agree. feat-029's branch repeats that workaround in its host
(`card/PropertyField.tsx:140-144`). The kit's default, and the showcase's date demo,
have no workaround. § 5.7 says "an incomplete date's `value` is `''`". The kit test
pins the defect: "and no onChange".

Fix:
- On `badInput`, call `onChange('')` when `value !== ''`, then `onIncomplete()`.
- Tests: the DOM keeps `''` after a partial change, and completing it to the
  earlier date calls `onChange` with that date.

**R1-2 — major — `shared/ui/fields/fieldStyles.ts:99`, `fields/FieldFrame.tsx:303,317`, `fields/InlineSelectField.tsx:152`, `fields/InlinePickerField.tsx:190`, `actions/MoreActionsMenu.tsx:55`; status.md Log, parts 8–10, "Test technique".**
The slowness of the card in jsdom comes from the kit's `:has([aria-expanded="true"])`
rules. I profiled in Vitest, in a scratch test.

One field alone costs what MUI's own does:

| | kit | MUI |
|---|---|---|
| `InlineTextField` render, cached | 31 ms | 25 ms (`TextField`) |
| `InlineTextField` `getByRole` | 103 ms | 154 ms |
| `InlineSelectField` render | 62 ms | 65 ms (`Select`) |
| `InlineSelectField` opens | 155 ms | 143 ms |

There is no extra re-render: typing 5 characters is 6 commits, against MUI's 9.
Typing creates no style either: 150 rules before and after. Container queries cost
nothing (jsdom ignores them).

The showcase card (1,129 elements, 875 rules, 42 of them `:has`):
- mounts in 2.2 s cold and 0.95 s cached;
- answers `getAllByRole('textbox', {name: 'Заголовок'})` in 954 ms and the
  `Статус` combobox in 807 ms;
- with only the 42 `:has` rules deleted, answers in 60 ms and 40 ms.

jsdom walks the subtree for each `:has` on every ancestor whose style Testing Library
reads. So the cost is the kit's, it is jsdom-only, and a browser would not feel it.
It is the likely cause of feat-029's raised timeouts (on its branch,
`WorkItemCard.test.tsx:12` 60 s, `WorkItemFormPage.assignee.test.tsx:11` 120 s) and
of the showcase test that avoids role queries. The Log line's diagnosis, "getComputedStyle over all of emotion's rules",
is not borne out: about 5 % of the rules carry about 94 % of the cost. tech-016..020
put these fields on every screen.

Fix:
- The select and the picker track open/close (`onOpen`/`onClose`) and pass `open`
  to `FieldFrame`, which sets `data-open="true"` on its root. The rules become
  `&[data-open="true"] …`.
- `MoreActionsMenu` reads `[aria-expanded="true"]` on its own trigger, not
  through `:has`.
- Add a test that no `shared/ui` source contains `:has(`.
- Correct the Log line.

**R1-3 — major — `src/frontend/src/test/kitGuard.test.ts:211-235` (`rulesFor`), `:295-309` (`attributeText`), `:481-516`.**
Two plausible doors pass the guard. I probed both with scratch files and removed them.

1. `shared/ui/**` is exempt from KIT-24, so a one-line
   `export { default as RawChip } from '@mui/material/Chip'` in any kit file gives
   every feature a raw MUI part with no site. "Add it to the kit first" is exactly
   what the guard's message tells a developer to do.
2. A heading or an intrinsic element named by a non-literal value passes:
   - ``component={`h${level}`}``, the kit's own idiom (`PageHeader.tsx:203`,
     `Section.tsx:51`, `CardHeaderBar.tsx:79`), so a slice copying it is not
     caught;
   - `component={big ? 'h1' : 'h2'}`;
   - `variant={… ? 'h1' : 'h2'}`;
   - `{...{ variant: 'h2' }}`;
   - `const TAG = 'button'; component={TAG}`.

Fix:
- Arm KIT-24's re-export half under `shared/ui`: the kit wraps a part, it never
  re-exports one.
- Read `component` and `variant` through their literal parts, as
  `colourLiterals.test.ts` `valueParts` does: the branches, and a template head
  `h` followed by a substitution.
- A fixture for each.

**R1-4 — minor — `kitGuard.test.ts:200-209, 356-530`.**
These probes also pass:
- `createElement('input')`;
- `@emotion/styled`'s `styled.button`;
- `styled` aliased (`import { styled as s }`);
- `createSvgIcon` aliased from `@mui/material/utils`;
- `useMediaQuery` aliased from `@mui/system`;
- `<Box as="button">`;
- `import * as c from '…/pageChrome'; c.usePageChrome()`. KIT-22 reads named
  imports only;
- `role="heading"`;
- `'& [class*="MuiOutlinedInput-…"]'`. KIT-15 needs `.Mui` + a capital or `Mui-`;
- ``css`box-shadow: …` ``;
- any production file named `test[A-Z]*.ts(x)`, which is exempt from every rule.
  A plausible one is `testCaseSteps.ts` for feat-031.

Fix: track the aliases by import binding, as `Typography` already is. Then:
- flag `createElement`/`jsx` with an intrinsic, `styled.<tag>` and `as=` like
  `component=`;
- read KIT-22 on namespace member access;
- match `Mui[A-Z]` in a selector-looking literal;
- replace the name pattern with the explicit list of today's helpers.

**R1-5 — minor — `src/frontend/src/test/colourLiterals.test.ts:56-69, 213-254`; `docs/conventions/ui-kit.md:354-356`.**
The guide says "a named colour, `grey.N` and `common.*` all fail". These pass:
- `textShadow: '0 0 1px black'`;
- `backgroundImage: 'linear-gradient(white, black)'`;
- `borderInline: '1px solid black'`;
- `const INK = 'black'; color: INK`;
- `import { red } from '@mui/material/colors'`, MUI's default palette by import,
  which § 9.7 means to close;
- `const { grey } = useTheme().palette`;
- `'1px solid #E0E0E0!important'`.

Fix:
- Flag `@mui/material/colors` outside `app/theme`.
- Add the shadow, image, `borderInline*/Block*` and `filter` properties.
- Accept `!` as the right boundary.
- Or narrow the sentence to what is checked.

**R1-6 — minor — KIT-20 (a reviewer's check until tech-016).**
`fields/FieldFrame.tsx`, `fields/InputField.tsx`, `actions/InlineSpinner.tsx`,
`feedback/skeletonParts.tsx` and `icons/BacklogIcon.tsx` have no test file beside
them. Each is tested through another file, but KIT-20 (§ 9.2) has no exemption, so
tech-016's coverage test fails on these five on its first run.

Fix: add beside-files, for example rename `fields.shared.test.tsx` to
`FieldFrame.shared.test.tsx`, or write in § 9.6 that KIT-20 skips files the barrel
does not export.

**R1-7 — minor — `src/frontend/AGENTS.md` (the UI kit paragraph), `docs/conventions/frontend.md` § UI kit, `docs/conventions/ui-kit.md:150-151`.**
- The first two say the showcase shows every component in every state. The table
  family (9 exported parts) has no section until tech-016 (D-6).
- ui-kit.md says a hint is "always in `aria-describedby`". While `error` is set it
  is not (`useFieldWiring.ts:53`, as § 5.7 intends).

Fix: say "(the table family from tech-016)", and "unless the error line replaces
it".

**R1-8 — minor — direction 2.**
These describe the state before this diff and are not on the owed-notes list:
- feat-027 `design.md:161-167, 362, 528-531, 565, 575`: the generic keys under
  `layout.backlogTable`;
- feat-027 `design.md:315, 536, 602`: the icons in `features/boards/components`;
- mod-015 `design.md:704`: the head's overflow as `MoreHoriz` + `Menu`. The team
  variant is now `MoreActionsMenu`.

Fix: add three «changed by tech-015» notes to the gate-3 owed list.

**R1-9 — minor — `src/frontend/src/features/uikit/pages/UiKitShowcasePage.test.tsx:66-99`.**
`expect(fetchSpy).not.toHaveBeenCalled()` runs synchronously after `render`.
`httpClient.ts:77` awaits the token before `fetch`, so a demo that read data through
the client would not be caught there. MSW's `onUnhandledRequest: 'error'` does not
fail a test either.

Fix: wait a macrotask (or use `waitFor`) before asserting, or assert that the slice
imports nothing from `shared/api`.

### Behavioural claims (item 6)

- ui-kit § 1, the guard fails `npm test`/`verify.sh` for a new violation →
  `kitGuard.test.ts:211-235, 1204-1207`, `scripts/verify.sh:554`. Holds for literal
  forms; the doors are R1-3 and R1-4.
- ui-kit § 2, `/ui-kit` for `dictionaries.languages.write`, tables later →
  `uikit/permissions.ts:11`, `routes.tsx:62`, `navigation/model.ts:186-200`.
- ActionButton busy is `aria-disabled` and focusable → `ActionButton.tsx:75-88`.
- SplitButton disabled greys and busy fill → `SplitButton.tsx:55-119`.
- IconAction `inactive` → `IconAction.tsx:75-84`.
- MenuButton keys, name and focus return → `MenuButton.tsx:88-110`.
- MoreActionsMenu `revealed={false}` → `MoreActionsMenu.tsx:47-62`.
- Fields: real `<label for>`, `aria-required` not native, placeholder attribute
  always → `FieldFrame.tsx:84-98`, `useFieldWiring.ts:44-58`, `InputField.tsx:67`.
  "Hint always described" → R1-7.
- A select is named by its label only → `InlineSelectField.tsx:94-99`.
- Section has no margin and is a named region → `Section.tsx:77-92`.
- ColumnLayout follows the container → `ColumnLayout.tsx:101-142`.
- CardHeaderBar `actionGap`; PropertyBand fixed tracks and min height →
  `CardHeaderBar.tsx:135-141`, `PropertyBand.tsx:16-35`.
- BoxedTabs «Связи (1)» and its indicator → `BoxedTabs.tsx:34-36, 79-92`.
- DialogShell: no dimming, busy never closes, the guard → `DialogShell.tsx:146-153,
  172, 260-277`.
- PanelShell: the page scrolls → `PanelShell.tsx:30-52`.
- ConfirmDialog `cancelLabel`, role `dialog` → `ConfirmDialog.tsx:54, 74`.
- Notices: one at a time, clickaway ignored, Escape, «Закрыть», throws outside a
  provider → `NoticeProvider.tsx:32-58`, `useNotice.ts:12-20`.
- InlineNotice role by severity → `InlineNotice.tsx:33-43`.
- StateFrame/NotFound `compact` → `StateFrame.tsx:69, 188-193`.
- One status per skeleton region → `skeletonParts.tsx:16`.
- ui-kit § 9, the ratchet: "no add an entry" → `kitGuard.test.ts:625-640`.
- ui-kit § 9, colour → R1-5.
- AGENTS.md and frontend.md, "every component" → R1-7.
- Log, parts 8–10:
  - 126 of 128 entries → `kitGuard.allow.ts` and `kitGuard.baseline.ts`;
  - no API request → the slice's imports;
  - 11 scenarios, type-checked → `playwright --list`, `npm run build`;
  - the role-query cost "all of emotion's rules" → R1-2.
- design § 5.7, "an incomplete date's `value` is `''`" → R1-1.
- Direction 2 (one line per behaviour the diff changed, the identifier grepped,
  and its hits):
  - The lifted keys, grepped `chevronSlot|rowControl|chevronGlyph|actionGlyph|headerGlyph`:
    feat-027 design hits → R1-8.
  - The moved icons, grepped `ThinIcons.tsx|BacklogIcon.tsx`: feat-027 design →
    R1-8. Its review and status records are history and are left.
  - The team overflow, grepped `MoreHoriz|⋯`: mod-015 design:704 → R1-8.
    E2E 26 selects rows' «Ещё действия: <ID>» only.
  - The notice host in `renderWithProviders`/`AppLayout`, grepped
    `renderWithProviders`: no stale prose.
  - Not swept: prose listing the settings navigation's sections, which now
    include the new «Интерфейс».

## Resolution

The fix pass on `tech/015-ui-kit` after round 1 (review at 0c20cfa). All three major and
all six minor findings are fixed; none is declined. `bash scripts/verify.sh` is green
(`npm test`: 213 files, 3,486 passed and 6 skipped, against 3,295 and 207 files in the review;
`npm run lint`, `npm run build` with the e2e type-check); `npx playwright test --list` shows the 11
scenarios of spec 28 — edited, type-checked, **not run**.

- **R1-1 — fixed in 25e8e4b.** A partial entry first reports `onChange('')` (unless the value is
  already empty), then `onIncomplete()`. Tests: the control keeps `''` after a partial
  change, completing it to the earlier date reports it through `onChange`, a partial entry
  that was empty reports no value, and the order is `change:`, `incomplete`. The three new
  cases fail on the old code. feat-029 can drop its workaround
  (`card/PropertyField.tsx:140-144` on its branch).
- **R1-2 — fixed in 75b508c, with a correction to the finding's numbers.**
  `FieldFrame` takes `open` and sets `data-open="true"`; the select and the picker track
  `onOpen`/`onClose`; `MoreActionsMenu` sets `data-expanded` from its trigger's
  `aria-expanded`; no rule reads `:has`. A source scan (kitGuard, 2f6b229) fails a `:has(` in
  any string or template of any source, and the Log line is corrected (a new line, status.md).
  **What I measured**, in one session on the same machine, on the showcase card (1,129
  elements), a role query as the first one after an edit: before, «Заголовок» 2.4–3.1 s and
  «Статус» 1.5–2.3 s with 85 `:has` rules in the sheet; after, 1.3–1.5 s and 1.4–1.6 s with 3
  (MUI's own, for its Select). A repeat of the same query is about 60 ms before and after.
  So the `:has` rules were about half of the cold cost, not nearly all of it, and I could
  not reproduce 60 ms / 40 ms. What is left is `getComputedStyle` matching about 1,900 rules
  against each element and its ancestors; a micro-profile of `matches()` puts most of it on
  selectors jsdom cannot parse (vendor placeholder pseudo-elements, MUI's
  `::-moz-focus-inner`), but a test-only emotion cache without the prefixer gave no clear gain on a
  noisy machine, so no cause is claimed. **Consequence: feat-029's 60–120 s timeouts should not go
  back to the default on this evidence**; measure the real card first.
  **Found while fixing it (not in the review):** the select's caret and the picker's arrow rules
  named the root's own class a second time (`'&:hover && .X'`), so they could not match and the arrow
  never showed on hover or focus; and in the picker's selector list the clear button lost its state
  and applied at rest. The rules now name the root once and repeat the state before each
  indicator. The old tests only read that a selector *contained* `:hover`; `reaches()` now asks
  whether it can reach the element, and the computed look is read at rest and on focus.
- **R1-3 — fixed in 2f6b229.** KIT-24 applies in `shared/ui`: an `export … from` a MUI module, `export
  { Chip }`, `export default Chip` and `export const RawChip = Chip` fail there, while a wrapper,
  `styled(Chip)` and a relative re-export pass. `component`, `as`, `variant`, `role` and
  the argument of `styled`/`createElement` are read through their literal parts (branches,
  `||`/`??`, a same-file `const`, a spread object, a template head), one site per rule. A fixture for
  each form named in the finding. Not caught, and said so in ui-kit.md § 9 and design § 9.2: a name
  the file cannot tell (a parameter, a call, an import).
- **R1-4 — fixed in 2f6b229, every item.** `createElement`/`jsx`, `styled.button`, an aliased or default
  `styled`, an aliased `createSvgIcon` and `useMediaQuery` (found by what the import brings), `as=`,
  a namespace import of the page chrome, `role="heading"`, an attribute selector on a MUI class,
  `box-shadow` as CSS text; the test-helper exemption is the explicit list `TEST_HELPERS` (17
  files, each checked to exist), and a production `testCaseSteps.tsx` is now read. The tree scan finds
  no new site, so the allow-list and the baseline are unchanged (126 and 128).
- **R1-5 — fixed in 0883c58, every item**, and the guide's sentence now says what is checked. More colour
  properties, a `const` that feeds one, `!` as the end of a colour, `@mui/material/colors` in any form and
  `{ colors }`, `palette.grey/common` by destructuring. The tree has no offender.
- **R1-6 — fixed in 27c3758.** `fields.shared.test.tsx` is `FieldFrame.shared.test.tsx`; `InputField`,
  `InlineSpinner`, `skeletonParts` and `BacklogIcon` have their own test files, and so do `DialogTitleBar` and
  `LoadingBox`, two older files the finding did not list that tech-016's coverage test would also have
  failed on. A loop over every `shared/ui/**/X.tsx` finds a test beside each.
- **R1-7 — fixed in 4472dc0.** `AGENTS.md` and `frontend.md` say "(the table family from tech-016)"; ui-kit.md and
  the `hint` comment say "unless the error line replaces it".
- **R1-8 — fixed in the commit that adds this section**: the three notes are on the list owed at gate-3
  (status.md Log). The feat-027 and mod-015 design documents themselves are left to the
  orchestrator, as the other owed notes are.
- **R1-9 — fixed in 041f07d.** The test waits 50 ms (a macrotask drains the token promise) before it asks
  whether `fetch` was called, and a second test reads the slice's sources for `shared/api`, `httpClient`,
  `@tanstack/react-query`, `fetch(` and `XMLHttpRequest`.
- **E2E 28 — 7bfc852.** 28.3 reads the select's caret (hidden at rest, drawn on hover and while the menu is open,
  by `data-open`) and a date cleared in part (empty until filled again). Not run.
