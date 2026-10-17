# UI kit: the panel's components and the rule

The panel has its own UI kit: the look that the reference screen set (backlog,
work item card), made reusable, in code, with a guide, a showcase page and a test
that keeps screens from drifting. This file is the guide for people and agents.
The spec it grew out of — every measurement, state and Russian string — is
[tech-015's design](../features/tech-015-ui-kit/design.md); the code is
`src/frontend/src/shared/ui` and the tokens `src/frontend/src/app/theme`. The
frontend conventions that surround it are in [frontend.md](frontend.md).

> The numbers (sizes, colours, times) are **not** repeated here: they are tokens in
> `app/theme/tokens.ts`, and a number written in two places drifts. Checked against
> the code at the end of tech-015 (2026-10-12); the table family has no showcase
> section and the coverage test does not exist yet — both arrive with tech-016
> (status.md D-6), and the places below that depend on them say so.

## 1. The rule

> A screen is built from the kit only. A part the kit does not have is added to
> the kit first — with its tests, its showcase entry and its guide block — and
> only then used. A feature never re-implements what the kit provides, never
> imports a MUI part the kit wraps, and never styles MUI's insides.

It is enforced: `src/test/kitGuard.test.ts` fails `npm test` (and so
`scripts/verify.sh`) for a new violation in `src/features/`, `src/app/` or
`src/shared/` outside `shared/ui`; § 9 says how to read a failure. What the kit does not cover is listed in § 10, so
"the kit has no such part" is a fact you can look up, not a guess.

## 2. Where things are

| What | Where |
|---|---|
| The components, one public entry | `src/frontend/src/shared/ui` — import from `shared/ui`; families in folders (`icons`, `actions`, `marks`, `text`, `fields`, `layout`, `card`, `dialog`, `table`, `feedback`) |
| Colours, sizes, type scale, the shapes of surfaces | `src/frontend/src/app/theme/tokens.ts`, read as `theme.palette.*`, `theme.layout.*`, `theme.density.*`, `theme.typography.*`, `theme.cardShadow` |
| The showcase: every component alive, in every state | the panel's `/ui-kit` — «Настройки» → «Интерфейс» → «Витрина компонентов», for administrators (`dictionaries.languages.write`); the slice `src/frontend/src/features/uikit`, its sections in `showcase/registry.ts`, one demo file each in `showcase/demos/`. The table family has no section yet (tech-016) |
| The guard, its baseline and list | `src/test/kitGuard.test.ts`, `kitGuard.baseline.ts`, `kitGuard.allow.ts`. Colours: `colourLiterals.test.ts`. The coverage test (a component with no showcase entry or no test file) arrives with tech-016 |
| The look's origin | [feat-027's design](../features/feat-027-backlog-tree-editing/design.md) (the backlog as the reference) and [mod-015's design](../features/mod-015-app-shell-design-system/design.md) (shell, screen canon, keys) |

Open the showcase before you write a screen: if a component is there, use it; if
what you need is almost there, extend it (§ 7); if it is not there, § 7 is how
it gets there.

## 3. The look in six lines

1. **One look**, the reference's: neutrals carry the screen; the accent is the
   one filled primary action per page header or dialog, the selection frame, the
   tab underline and the focus; colour is never the only carrier.
2. **Soft surfaces**: a white card with a soft glow and a grey band; no outlined
   panels.
3. **Quiet controls**: fields have no frame at rest (label above or at the left,
   value as text, a frame on hover and focus); a form that starts blank uses the
   same field with the frame always drawn and the label above it — there is no
   floating-label field in the kit; buttons are grey or, once, blue; icon buttons
   have no fill; reference glyphs are hairlines.
4. **Dense**: Segoe UI, two weights, small type, 36 px rows, 32 px controls.
5. **Focus is loud, hover is quiet**; what shows only on hover also shows when
   the pointer cannot hover.
6. **Words are Russian and final in `design.md`**; a component never invents text
   the host knows.

The legacy look (mod-015: outlined panels, grey semibold table headers, row
rules, accent-iconed text buttons, floating-label fields) is still on screens
that have not moved. Do not copy it into new code and do not extend it; the
migration is tracked in § 10.

## 4. Choosing a component

| I need… | Use | Never |
|---|---|---|
| a page's title, commands and views | `PageHeader` (the team look) | `Typography` + `Tabs` by hand; a heading element of your own (`component="h1"`, `<h2>`) |
| a page's tab title and its place in the shell, when it has no `PageHeader` (the card's page) | `usePageHead({ title, ancestors })` — it returns the `trail` for `PanelShell` | `useDocumentTitle`, `usePageChrome`, `PageChromeContext` |
| tabs for a card's sections | `BoxedTabs` + `BoxedTabPanel` | MUI `Tabs`/`Tab` |
| a text button | `ActionButton` (`primary` once; `neutral`, `subtle`, `danger`) | MUI `Button` |
| a main action with variants | `SplitButton` | `ButtonGroup` |
| an icon-only button | `IconAction` (`row`, `chevron`, `header`, `bar`) | `IconButton`, a glyph size in `sx` |
| a hairline glyph | the thin icons in `shared/ui/icons` — chevrons, add, remove, close, expand, more-vertical, refresh, undo, save, person | `createSvgIcon` in a slice |
| a menu from a button | `MenuButton`; the «⋮» is `MoreActionsMenu` | `Menu` + `MenuItem` |
| a type, a state, a person | `TypeLabel`, `StateLabel`, `CategoryMarker`, `UserAvatar`, `PersonLabel` | an icon beside a text by hand |
| a tag | `Tag`, `TagList` | `Chip` |
| a long text cut to one line | `TruncatedText` | an ellipsis `sx` |
| a value to read or edit | the fields: `InlineTextField`, `InlineTextArea`, `InlineNumberField`, `InlineDateField`, `InlineSelectField`, `InlineMultiSelectField` (several values: a filter), `InlinePickerField`, `PersonField` | `TextField`, `Autocomplete`, `InputBase`, an `<input>` or `<textarea>` of your own |
| a text that stands under a section head of its own name | `InlineTextArea` with `labelPlacement="hidden"` and the head's text as its `label` | a `top` label under the head (a second, small «Описание» would be drawn); a bare padding of your own |
| a band's label-left property with a dot or lock before its value | a field with `labelPlacement="start"` — the adornment stands in the label column by default | a dot drawn inside the value's box (the text would no longer line up) |
| a panel | `Surface` | `Paper`, `Card`, `boxShadow` |
| a titled group | `Section` — it has no margin: the `ColumnLayout` area spaces sections | a `Typography` with a border; a heading element of your own; a margin on a section |
| a body in three, two or one column | `ColumnLayout` | `useMediaQuery` |
| a work item card | `PanelShell` (page) or `DialogShell size="card"`, `CardHeaderBar`, `PropertyBand` | a hand-built band and grid |
| a modal window | `DialogShell`, `ConfirmDialog`, `FormDialog` | `Dialog` |
| a table | `TableCard`, `DataTable`, `HeadCell`, `BodyRow`, `Cell`, `TreeCell`, `RowControl`, `TablePager` | `Table*`, `TableContainer` |
| a short result message | `useNotice().show(text)` | `Snackbar` |
| an error or note that stays | `InlineNotice` (`ErrorBanner` for a load error) | `Alert` |
| an empty, denied or not-found screen | `StateFrame`, `AccessDenied`, `NotFound` | a centred `Typography` |
| loading | a skeleton of the content (`TableSkeleton`, `FieldsSkeleton`, `CardSkeleton`) | a spinner for a page |

## 5. The components, one block each

Every component below is built on a standard MUI part; the prop names are the
kit's one vocabulary: `label`, `tone`, `size`, `busy`, `disabled`, `readOnly`,
`error`, `hint`, `onChange(value)`, `onSelect`, `to`. The props listed are the
ones that matter; the type is in the file.

**Icons and buttons** (`actions`, `icons`)

- `ActionButton` — `label`, `tone`, `icon`, `to`, `busy`, `disabled`, `menu`.
  Default tone is `neutral`; `primary` is deliberate. *Busy*: a spinner replaces
  the icon, the button is `aria-disabled` and **stays focusable**. *Names*: the
  label. *Don't*: two `primary` in one header or dialog; a hand-made spinner; an
  accent icon on a neutral button.
- `SplitButton` — `label`, `groupLabel`, `menuLabel`, `items`, `busy`, `disabled`.
  The arrow's name must not contain the main label's word; its glyph is the thin
  chevron (never Material's filled drop-down triangle). *Disabled* wears the
  reference's clean look — `shell.splitDisabledFill` and `splitDisabledText` — not
  MUI's disabled grey, which is a darker block with a fainter label; *busy* keeps
  the tone's fill, both parts inert and focusable.
- `IconAction` — `label` (the name **and** the tooltip), `icon`, `size`, `tone`,
  `inactive` (aria-disabled, still focusable), `busy`. Name the row when many sit
  on a screen («Ещё действия: MSK-EPIC-1»). *Don't*: size a glyph yourself.
- Thin icons — chevrons, add, remove, close, expand, more-vertical, refresh,
  undo, save, person; exported with the `Icon` suffix (`ThinCloseIcon`). **The
  card's header uses these and no Material glyph** (the shell's expand and close,
  the Save group's save and its chevron arrow, refresh, undo, more-vertical, the
  empty assignee's person). Anything else: Material `*Outlined` — heavier than the
  reference's hairlines for the glyphs not yet thinned (history, link, attachment,
  comments: a recorded difference, § 10), and added to `shared/ui/icons` by the
  owner who needs them.

**Menus** (`actions`)

- `MenuButton` — `label` (the menu's name), `trigger`, `items`, `selection`
  (`none`, `radio`, `page`), `align`. Enter, Space or ArrowDown opens on the
  current entry; Escape closes and returns focus. Entry icons are neutral; a
  `destructive` entry reads in the error colour and says what it does; a `group`
  heading names the entries under it.
- `MoreActionsMenu` — a `MenuButton` with the vertical «⋮»; `label` defaults to
  «Ещё действия» (a row passes «Ещё действия: MSK-EPIC-1»); `size` is `row` or
  `bar`; `revealed={false}` draws the control transparent until hover, focus or an
  open menu (a table row's).

**Marks and people** (`marks`) — always followed by a name; colour is never the
only carrier. `TypeLabel` (`eyebrow` for the card's type line), `StateLabel`
(`clip`), `CategoryMarker` (the state dot; `size`), `UserAvatar`, `PersonLabel`
(`emptyLabel`), `Tag` and `TagList` (`onRemove`, `onAdd`). *Don't*: show a state
as a coloured chip.

**Text helpers** (`text`) — `TruncatedText` (the full text in the native hover
title), `VisuallyHidden`, `LiveRegion` (`serial` makes a repeat spoken again),
`InlineEmpty` for an empty list inside a section.

**Fields** (`fields`) — one anatomy: a label that is a real `<label for>`, a
value, a `hint` (a caption on the label's line while focused; in
`aria-describedby` unless the error line replaces it), an `error` (text; a frame, a line, `aria-invalid`),
`required` (an asterisk and `aria-required`, never the native attribute),
`readOnly`, `busy`, `disabled`, `placeholder` (always the input's own
`placeholder` attribute — only its look is hidden at rest), `emptyText`,
`startAdornment` and `adornmentPlacement` (`value`: inside the box before the
text; `label`: in the label column, the default for `start`). `labelPlacement` is
`top`, `start` (the band's label-left rows: the label column is `labelWidth`, the
value's text stands a fixed inset further in, so **every row's text starts at the
same x whatever else the row holds**; a dot or lock stands in the label column,
before the text, and the field's frame takes it in) or `hidden` (the label still
names the control: the card's title and assignee, and **a text under a section
head of its own name** — an `InlineTextArea` whose `label` is the head's text, so
no second small label is drawn and the text stands where the reference puts it).
`frame` is `hover` (default: the card — no frame at rest) or `always` (a form that
starts blank). `size` is `body`, `title` or `table`. The label's grey is
`shell.cardLabel` (checked against white and the band), a hint's and an empty
text's `text.secondary`. Kinds: text, text area (`minRows`), number (the *text*, `inputMode`),
date (`min`, `max`, `onIncomplete`: a date typed in part reports `onChange('')`
first, so the prop and the control agree and completing it reports the date
again; `onIncomplete` follows), select (`options`, `emptyLabel` makes it
optional), multi-select (`value: string[]`, `options`, `emptyText`, `loading`, `onOpen`: the filters; a multiselectable listbox that stays open while the user ticks, the value the chosen names joined by «, »), picker (`options`, `loading`, `endAction`, `clearable`), person (an avatar once
someone is set, the thin person glyph while nobody is). A select's name is its
label only. A work item's parent field is a composition of the picker with
`TypeLabel` and `StateLabel`, owned by its slice. *States*: rest, hover, focus,
error, read-only, busy, disabled, empty; no state changes the field's size.
*Don't*: set a width (the field fills its container), use the placeholder as the
label, show the hint as a permanent helper line.

**Layout and the card** (`layout`, `card`, `dialog`)

- `Surface` — `variant` (`card`, `band`), `padding`.
- `Section` — `title`, `headingLevel` (2 or 3), `count`, `actions`; a `region`
  named by its heading; **no margin of its own**; its rule is `shell.sectionRule`,
  not `divider`. `SectionHead` alone for a heading that is not a region.
- `ColumnLayout` — `main`, `side`, `extra`; the columns follow the **container's**
  width, and each area spaces its sections by `layout.section.gap`.
- `CardHeaderBar` — `appearance`, `typeLabel`, `identifier`, `headingLevel`,
  `headingId`, `title`, `meta`, `actions` (they stand `layout.card.actionGap`
  apart; the dialog's two icons are the shell's, not this bar's). `PropertyBand` —
  `blocks`, `meta`, `tabs`; the two blocks are **fixed-width** tracks
  (`bandBlockWidth`, shrinking toward `bandBlockMin` only when the card is narrow)
  with a free track and the aside after them — never `1fr` blocks; it keeps its
  height with empty rows. `TypeStripe`. `BoxedTabs` — `idBase`, `aria-label`,
  `value`, `onChange`, `tabs` (label, icon, count, `iconOnly`); a tab with a count
  is named «Связи (1)»; the selected tab's indicator is as wide as the label,
  `frame.thick` thick and `card.tabIndicatorOffset` above the strip's bottom.
- `DialogShell` — `open`, `onClose(reason)`, `size` (`confirm`, `form`, `card`),
  `title` or `header`/`band`/`alert`, `labelledBy` or `label` (the dialog's name
  when there is no heading — a card still loading), `busy` (nothing closes it),
  `onExpand`, `guard` (opt-in unsaved-changes confirmation on dismissal),
  `onExited`. **The card size draws no dimming** — its backdrop is `invisible`
  (the outside click is still caught; MUI's own large shadow separates it from the
  page; never `hideBackdrop`) — while a `ConfirmDialog` opened over it dims; its
  expand and close icons are adjacent, `card.dialogIconGap` apart, the close
  `card.dialogIconInset` from the edge. `PanelShell` — the same slots on a page,
  plus `trail` (from `usePageHead`); the page scrolls, not the card.
  `ConfirmDialog` — `destructive`, `cancelLabel`, `cancelAutoFocus`; its role
  stays `dialog`.

**Tables** (`table`) — one look: a white card with a soft glow, a sticky white
header of small grey labels, rows with no rules, a light hover, a framed
selection. `TableCard` (it owns the `<table>`: `head`, `columns`, `role`,
`bodyProps`; `maxHeightOffset` makes it scroll inside itself and the header
sticky), `HeadCell` (`sort`, `separator`, `clip`), `BodyRow` (`selected`, `cue`,
`dragging`), `Cell`, `TreeCell`, `RowControl` (revealed by the row's hover, focus
or selection; `focus="tab"` keeps it in the Tab order, `"roving"` leaves focus to
a treegrid host), `TablePager`, `TableSkeleton`. The row and cell parts pass
`role`, `tabIndex`, `aria-*` and the handlers through to their element. `DataTable`
is the plain registry with its loading, empty and error states (the error is an
`InlineNotice` with «Повторить»). A plain table has
Tab stops, not arrow navigation and no `aria-selected`; a treegrid keeps its own
keyboard model and draws with the parts.

**Notices and states** (`feedback`) — `useNotice().show(text, severity?)`: one
at a time, bottom left, clickaway ignored (Escape dismisses it, and a success or
error notice closes by «Закрыть»); never an action or a link in it. The hook needs
the `NoticeProvider` that `AppLayout` mounts (`renderWithProviders` mounts it in
tests) and throws outside one. The corner is shared with the shell's two notices
(mod-016 DC-11): the provider closes its notice when the switcher's fallback or
the role-change notice is raised, and showing one closes the role-change notice.
`InlineNotice` — `severity`, `title`, `action`; it owns **no margin** (the
container spaces it). `StateFrame` and its two variants as mod-015 § 7; `compact`
inside a dialog — an `h2`, no tab title, and (for `NotFound`) no change to the
shell's chrome. Skeletons mirror the loaded shape and are one `role="status"`
«Загрузка данных» per region.

**Two traps** the kit already fell into. MUI 9's `Typography` knows
`color="textSecondary"` and not `"text.secondary"`, which draws no colour at all —
write `sx={{ color: 'text.secondary' }}`. And jsdom cascades by order, not by
specificity, and answers no `:hover`, container query or pseudo-element: a test of
such a rule reads the stylesheet's rule, and the browser behaviour belongs in the
showcase's E2E.

## 6. Names and texts

- Interface text is Russian and final in the feature's `design.md`; code never
  invents it. The kit owns only the few defaults listed in tech-015's design § 7
  («Развернуть на весь экран», «Закрыть», «Ещё действия», «Не назначен»,
  «Продолжить редактирование», «Загрузка данных»).
- A control's accessible name is its label, never its value. A row's controls
  name their row. A hidden label is still passed.
- Names the E2E selects by do not change when a screen moves to the kit
  («Заголовок», «Исполнитель», «Статус», «Область», «Описание», «Родитель»,
  «Открыть <ID>», «Ещё действия: <ID>», «Новый рабочий элемент»…).

## 7. Adding a new pattern to the kit

You need something the kit does not have — a new field kind, a new surface, a
repeated arrangement that two screens will share. Do not build it in a slice.

1. **Look first.** Search the showcase and § 4. Ask whether an existing part
   with a prop is the answer; most are.
2. **Look at the reference.** Measure it (§ 8). A new dimension that two parts
   must agree on, a test pins, or the reference fixes becomes a **token** in
   `app/theme/tokens.ts` with a pin in `theme.test.tsx`; anything else is the
   spacing unit inside the component. A colour is a token, always.
3. **Write it in `shared/ui/<family>/`**, on the standard MUI part, with the
   states it needs (rest, hover, focus, disabled, read-only, loading, error — as
   each applies), forwarding `ref` and `aria-*`/`data-*`, its Russian names from
   the design, no import from `features/` or `app/`.
4. **Test it** beside it (`<Name>.test.tsx`): names, roles, states, the tokens it
   reads. What jsdom cannot show (hover, container queries, sticky) goes in the
   showcase E2E.
5. **Export it** from `shared/ui/index.ts`; **show it** — a section in
   `features/uikit/showcase/registry.ts` (its demo in `showcase/demos/`, every state
   alive, its name in `covers`; the coverage test that will fail without it arrives
   with tech-016 — add the entry regardless); **describe it** in § 5 here.
6. **Guard it.** If it replaces a MUI part, add that part to a rule in
   `kitGuard.test.ts`; its current users go into the allow-list with the task
   that removes them.
7. `bash scripts/verify.sh` is green; the design the feature owns says which kit
   part is new and why (a reviewer reads it there).

A pattern that is used once, on one screen, and is not likely to be shared, is
still built in the kit when it is a *kit-grade* part (a field, a control, a
surface). A layout that only that screen has stays in its slice, made of kit
parts.

## 8. Checking a screen against the reference

The acceptance of a screen that claims the reference's look is a side-by-side
look, not a feeling.

1. Take the reference screenshot — it lives **outside the repository** and is
   never copied into it; the reference tracker is never named in anything
   written (AGENTS.md). Note its pixel ratio (the human's window is 1536 × 790
   CSS px at 1.25: CSS px = device px ÷ 1.25).
2. Put the screen at the same CSS size and compare, element by element: the head's
   rhythm, row and control heights, type size and weight, greys, the state of
   each control at rest, hover, focus and selected. Hues come from the theme,
   not from the picture (a browser paints saturated colours off); greys and
   sizes come from the picture.
3. Record every difference **by token name** in the task's review or QA file.
   Each is a defect to fix or a deviation written in the feature's `design.md`
   with its reason (feat-027 § 8 is the model). A deviation that is the kit's is a
   change to the kit (§ 7), not a local override.
4. Check it at the supported widths (down to 640 CSS px; the overlay navigation
   below 1024), with the keyboard only (every control reachable, focus visible,
   Escape closes the top layer), and read aloud by names alone (the controls'
   Russian names, § 6).

## 9. The guard test

`src/test/kitGuard.test.ts` scans non-test sources under `src/features/`,
`src/app/` (not `app/theme`) and `src/shared/` (not `shared/ui`, where only KIT-14,
KIT-18 and KIT-24 apply) with the TypeScript compiler, so a comment or a string of copy is
never mistaken for code. It fails on:

- an import of a MUI part the kit wraps (`TextField`, `Autocomplete`, `Input*`,
  `Button`, `IconButton`, `ButtonBase`, `Menu`, `MenuItem`, `Popover`,
  `Snackbar`, `Alert`, `Chip`, `Paper`, `Card`, the `Table*` family, `Dialog*`,
  `Modal`, `Skeleton`, `CircularProgress`, `Avatar`, `Tabs`, `Tab`,
  `Breadcrumbs`…) — rules KIT-1..13;
- `createSvgIcon` outside `shared/ui/icons` and `app/theme` (KIT-14);
- styling MUI's insides: a string containing `.Mui` + a capital, `Mui-` + a
  letter or an attribute selector on a MUI class (`[class*="MuiChip-"]`), or an
  import of any `*Classes` (`svgIconClasses`…) (KIT-15);
- `boxShadow` — a surface of your own, also written as CSS text
  (a `css` template with `box-shadow: …`) (KIT-16; `elevation` is not checked);
- `useMediaQuery` in a feature, whichever package it comes from — columns are
  `ColumnLayout`'s (KIT-17);
- `shared/` importing `features/` or `app/` (KIT-18);
- a kit component with no showcase entry (KIT-19) or no test file (KIT-20) — **not
  armed yet**: the coverage test that reads the showcase registry arrives with
  tech-016, and until then a reviewer checks both;
- a heading element made by hand — `<h1>`–`<h6>`, `component="h2"`, `role="heading"`,
  a `Typography variant="h2"` with no `component` (KIT-21; `variant="h2"
  component="p"` is fine); `usePageChrome`, `PageChromeContext` or
  `useDocumentTitle` outside the kit, also read off a namespace import
  (KIT-22; the shell, `app/AppLayout.tsx`, and the showcase's inert-chrome demo,
  `PageHeadDemo.tsx`, are the two named seams);
- an intrinsic `<input>`, `<textarea>`, `<select>`, `<button>`, `<table>`,
  `<dialog>`, or a `component=` / `as=` / `styled()` / `styled.button` /
  `createElement()` naming one (KIT-23). A name that is not one literal is read through
  its literal parts: the branches of a condition, either side of `||` / `??`, a
  `const` it names, a spread object, and a template's head (`` component={`h${level}`} ``
  is a heading). A name the file cannot tell is not caught, and review reads it;
- a door round the rules: `import * as … from '@mui/material'`, a re-export, a
  `require()` or dynamic `import()` of it (KIT-24) — **in the kit too**: a kit file
  wraps a MUI part and never hands one on (`export { default as RawChip } from
  '@mui/material/Chip'`, `export { Chip }` of an imported part, `export const RawChip =
  Chip`), because "add it to the kit first" must not end in a raw part every feature can
  import;
- a relational `:has(` selector in any source: in jsdom each such rule walks the
  subtree for every element whose style is read, which made every role query on the
  card slower. Put the state in an attribute the component sets (`data-open`,
  `data-expanded`) and select on that.

Test helpers that are not `*.test.*` are exempt by an explicit list in the guard
(`TEST_HELPERS`), not by their name: a new helper is added there, in a diff a reviewer
sees.

**Reading a failure.** The message names the file, the line, the rule and the
kit part to use instead. Use it. If the part you need is not in the kit, § 7 —
do not work around the guard.

**The allow-list** (`kitGuard.allow.ts`) holds today's violations — **an exact
count of sites per file and rule** — each with the id of the task that removes it
(`until`: one of tech-016 … tech-020, the migrations of § 10). It is cut from a frozen baseline (`kitGuard.baseline.ts`, the scan of one
commit; if another task merges to main before the kit does, the baseline is re-cut
once from the new main and the re-cut is logged in the task's `status.md`)
and **only shrinks**: a new violation fails, **a listed file that gains one more
site fails too**, and a stale entry (a count above what the file now has) fails,
so you lower or remove the entry when you fix the file. There is no «add an
entry»: an entry for a file the baseline does not know, or a count above the
baseline's, fails — fix the code, or add the part to the kit first. The goal is
an empty list (tech-020 requires it). One exception is on record: mod-016, built in
parallel with the kit and merged after it, re-cut the baseline at its rebased
commit 554bb6e so that its pre-kit screens join the list like the pages before
them (mod-016 D-24, the human's decision of 2026-10-13). Colour has its own test,
`src/test/colourLiterals.test.ts` — a colour anywhere in a literal (a shorthand, a
gradient, a shadow, `…!important`), a named colour, `grey.N` and `common.*` as the value
of a colour property (also through a `const` that feeds one), `palette.grey` and
`palette.common` (also by destructuring) and an import of `@mui/material/colors` all
fail; dimensions are checked by review, which holds them to tokens and the spacing unit.

## 10. What is not in the kit yet, and the legacy

**Not in the kit** — a known gap with an owner; until it is built, the plain MUI
part is allowed and nothing else is: `Switch`, `Checkbox`, `Radio`,
`FormControlLabel` (until a form needs one); a neutral grey note panel; the grey
«Follow» group; a comment composer; links, history and attachments lists; tag
editing (the card's meta row, through `Tag`); the board's tile as a surface; the
dark avatar fill, the grey trail and hairline switcher chevrons (the shell); the
hairline history, link, attachment and comments glyphs (the owner of each tab adds
its glyph to `shared/ui/icons`; until then Material `*Outlined` is a recorded
difference); the date field's hidden mask outside Chromium-family browsers.
Allowed without a rule: `Box`, `Stack`, `Typography`, `Link`, `Divider`, the
`List*` family, `Tooltip`, `Collapse`, `Fade`, `Backdrop`, `Drawer`, `AppBar`,
`Toolbar`, `styled`, `alpha`, `useTheme` and the icons of `@mui/icons-material`.

**Legacy** — the mod-015 look, on screens that have not moved: the registries and
the tree of areas, forms and dialogs, the shell. They move in follow-up tasks,
one family each: **tech-016** the backlog and board (with the table family's
showcase section and the coverage test), **tech-017** registries and
trees, **tech-018** forms and dialogs (every form field becomes the framed field
with the label above), **tech-019** the shell, and **tech-020**, which deletes the
theme's legacy defaults and the default variant of `PageHeader` and requires an
empty allow-list. Until then
`frontend.md`'s older paragraphs on secondary commands and "Shared patterns" are
the legacy look; where they and this guide disagree about a kit component, this
guide wins.
