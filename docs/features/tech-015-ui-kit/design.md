# Design: The panel's UI kit (tech-015)

Stage 3 artifact of the short route (`status.md` D-3): the kit's contract. It
makes the look that feat-027 matched to the reference screen reusable, adds what
feat-029's work item card needs, and fixes the rule that a screen is built from
the kit only. Tokens, components, states, every Russian string the kit owns,
the guard test and the showcase page are here, and nowhere else; the living
guide for people and agents that grows out of it is
[`docs/conventions/ui-kit.md`](../../conventions/ui-kit.md).

- **Inputs.** `status.md` D-1..D-3 are binding: components in code under
  `src/frontend/src/shared/ui`, a written guide, a showcase page in the panel
  for administrators, no new library (Storybook declined), tests per component
  and a guard test, and the rule «build from the kit only; a new pattern goes
  into the kit first». The approved look is
  [feat-027's design](../feat-027-backlog-tree-editing/design.md) and its tokens
  in `src/frontend/src/app/theme`; the base is
  [mod-015's design](../mod-015-app-shell-design-system/design.md) (shell, screen
  canon, menus' keys, notices). The card's needs are feat-029's
  `design.md` § 10 (on its branch, `feat/029-work-item-card-reference`); § 13
  maps its working names to the kit's. **That list — «What the card asks the
  kit to add or change» — was taken in by the last designer pass (2026-10-12,
  `status.md` D-5)**: where the card's measures come from the reference's
  pixels they win over this file's first draft, and where the two designs name
  one thing differently the kit's family convention keeps one name and § 13
  lists every rename, so the card's owner follows it.
- **The reference** is the reference tracker's backlog and work item card, seen
  in two screenshots that live outside the repository and are never copied into
  it; the tracker is never named (mod-015 BR-16, tech-001 D-1). Numbers taken
  from them are in feat-027 § 2–§ 4 and feat-029 § 0 and § 2 and are not
  repeated here except where the kit fixes them as tokens.
- **Stage 3 fixes structure, components, states and texts, not pixel promises**
  (`docs/ai-workflow/stages/03-ux-design.md`). The measurements below are the
  look to reach, in tokens; the acceptance look is a side-by-side check
  (`ui-kit.md`, «Checking a screen against the reference»).
- **No prototype.** The showcase page (§ 10) is the prototype and the
  acceptance surface: every component alive, in every state.
- **Review.** A separate reader checks this file before any code (D-3,
  feat-027 D-8's lesson).
- **The data of every example** is feat-029's: project key `MSK`, team
  «Платформа», the story `MSK-US-2` «Письмо со ссылкой».

## 0. Decisions of this design

Taken by the designer, for the reader to check and the human to overrule. The
three questions only the human could answer are in § 15, with their answers
(`status.md` D-4, D-5).

| # | Decision | Why |
|---|---|---|
| K-1 | **One look: the reference's.** The mod-015 look (outlined panels, grey semibold table headers, row rules, accent-iconed text buttons, floating-label fields) is *legacy*: it stays on screens that have not moved (§ 8), is not extended, and no new kit component draws it (the existing `PageHeader` keeps its default variant until tech-020). **No theme default of an existing MUI component changes in this task** — only tokens and kit components are added — so no other page is re-styled here (D-2 OUT). The switch of the theme's defaults is the last migration step (§ 8, tech-020) | D-2: «re-styling other pages» is out; one look is the goal, so the legacy has an end |
| K-2 | **Where the kit lives.** `shared/ui`, new components in family folders (`icons`, `actions`, `marks`, `text`, `fields`, `layout`, `card`, `dialog`, `table`, `feedback`) and one public entry, `shared/ui/index.ts`. The existing flat files stay where they are until their family is rebuilt (§ 8); the barrel re-exports them. Slices import the kit through `shared/ui` and nothing else of it | frontend.md already puts wrappers in `shared/ui`; the barrel is what the coverage test (§ 9) reads |
| K-3 | **One field family, two frames.** `InlineTextField` and its siblings (§ 5.7) draw the borderless field of the card; the same components with `frame="always"` draw the framed field of forms and dialogs (label above, a 1 px frame at rest). There is no second, outlined field in the kit | a blank form with no frame is unusable; one anatomy (label, hint, error, required, read-only) keeps both looks one family |
| K-4 | **One button.** `ActionButton` has four tones (`primary`, `neutral`, `subtle`, `danger`), owns the busy spinner and the menu attributes; `SplitButton` and `IconAction` complete the set. A busy button stays focusable (`aria-disabled`), so a keyboard user does not lose their place | six hand-written busy spinners and two tone systems today (§ 2) |
| K-5 | **One menu.** `MenuButton` owns the anchor, the keys, the attributes, the name and the focus return; `MoreActionsMenu` is its «⋮» preset. Entries' icons are neutral | ten hand-written menus drift in keys and labelling (§ 2) |
| K-6 | **Tables are parts, plus one declarative helper.** `TableCard`, `HeadCell`, `BodyRow`, `Cell`, `TreeCell`, `RowControl`, `TablePager`, `TableSkeleton` compose any table (the backlog's tree, drag and add row included); `DataTable` is the plain registry built from them and owns its loading, empty and error states | the backlog is too specific for a generic grid, the registries are not |
| K-7 | **Tokens by component family**, not by feature: feat-029's `layout.workItemCard.*` becomes `layout.card`, `layout.columns`, `layout.section` and `layout.field` (map in § 13); feat-027's generic `layout.backlogTable.*` is lifted (§ 3.4). A dimension is a token only when two components must agree on it, a test pins it, or it was measured on the reference; everything else is the spacing unit inside the kit component | frontend.md "Named values": a literal with a meaning gets a name, coincidentally equal values are not merged |
| K-8 | **Dialogs.** `DialogShell` and `PanelShell` are new. `ConfirmDialog` gains `cancelLabel` now; `ConfirmDialog` and `FormDialog` are rebuilt on the shell in migration (§ 8, tech-018), because the shell changes every dialog's close glyph and layout. The unsaved-changes guard is opt-in on the shell | the card needs the shell at once; re-styling every dialog is not this task |
| K-9 | **One notice host.** `NoticeProvider` is mounted once in `AppLayout`; `useNotice().show(text, severity?)` replaces the five hosts. The duration is a token, `noticeDurationMs` (6000), bottom left, clickaway ignored, one at a time | five copies with three dismissal rules today |
| K-10 | **The showcase** is a page of the new slice `features/uikit` at `/ui-kit`, a settings entry «Интерфейс» → «Витрина компонентов», visible by one existing instance permission, `dictionaries.languages.write` (§ 10, § 15 Q-1 → (a), `status.md` D-5). Its sections are a registry the coverage test checks against the barrel | D-1: a showcase inside the panel for administrators |
| K-11 | **The guard is a ratchet with a frozen baseline.** The rules of § 9 fail `npm test` for a new violation in `features/`, `app/` and `shared/` outside `shared/ui`. Today's violations sit in an allow-list that records **an exact count per file and rule**, only shrinks, and is cut from the scan of the commit `bd3bb22`, not from the tree of the day: a higher count fails as new, a lower one as stale, an entry for a file created after the baseline fails, and every entry names the migration that removes it. **The guard lands in plan part 1** of the kit's branch, so the kit's own later parts, feat-029 on top of the branch and the first migration are checked from their first line; main receives it with the kit's one squash (K-16) | the kit cannot be enforced against code that predates it any other way; a list cut late grandfathers whatever a parallel task merged first, and a list with no count lets a listed file grow |
| K-12 | **What the reference has and the kit does not yet** is listed (§ 8.4) with an owner, so a missing piece is a known gap, not a surprise. Nothing is built "for later" that the task's brief does not name (tags are named) | the places kept (feat-029 BR-C-18) are slots, not stand-ins |
| K-13 | **Page heads and the shell's seam are the kit's.** A heading element (`h1`–`h6`) made by hand fails the guard (KIT-21), and so do `usePageChrome`, `PageChromeContext` and `useDocumentTitle` used outside the kit (KIT-22): a page names its tab, registers with the shell and gets its trail through `usePageHead` (§ 5.6) | the brief asks the guard to catch a feature hand-rolling a page head, and the hand-rolled heads of today are all `Typography component="h1"`/`"h2"` |
| K-14 | **A confirmation stays `role="dialog"`.** `ConfirmDialog` keeps MUI's default role; feat-029 § 8's `alertdialog` is corrected in § 13, not built in the kit | about a hundred existing selectors name `dialog` (81 lines in Vitest, 21 in the E2E specs) and none names `alertdialog`; a role change would rewrite them all for no user-visible gain, and § 6 promises a rebuilt screen's tests keep their selectors |
| K-15 | **The reference's pixels beat the first draft.** feat-029's measures taken from the reference (the label column 82 and the value's text at label + 88, the state dot's centre at label + 64 and 19 before the name, blocks 253 wide, the dialog's icons 2 apart and 5 in, the toolbar's 11, the tab indicator 2 thick and 4 up, the greys `#707070`, `#EAEAEA`, `#F0F0F0`, `#959595`, the text's 15 above and 8 in) replace the draft's where they differ. Where the two designs name one thing differently the kit's family convention keeps **one** name (`adornmentPlacement="label"`, not `"gutter"`; `field.textAreaInset`, not `textPadX`; `ThinSaveIcon`, not `ThinSave`) and § 13 lists every rename | feat-029 § 0 and § 2 explain how the numbers were taken (ratio 1.25); a measure written twice with two values is a defect waiting for the side-by-side |
| K-16 | **One squash, in the order of `status.md` D-5.** The kit's ten parts are one task and merge as **one squash, before feat-029**; part 1 does not merge alone. If another task merges to main first, the guard's baseline is re-cut from the new main by § 9.3's procedure and the re-cut is logged in `status.md`. The follow-up migrations are the queued tasks **tech-016 … tech-020** (§ 8.2) | one task, one squash (git.md); a baseline cut from an older tree must not grandfather a newcomer silently |

## 1. Principles — the look in words

1. **One look, quiet.** Neutrals carry the screen: the page `#F8F8F8`, surfaces
   white, text `#242424` and `#616161`, a field's and the card's label `#707070`,
   rules `#E0E0E0` and a section's `#EAEAEA`. Everything else appears only for a
   reason.
2. **Dense.** Page title 20 px, card title 17, section head 16, text 14
   (13 in table bodies), labels and captions 12. A row is 36 px, a control
   32, a table header and a tab 40. The grid is 8 px, with 4 px as its half step.
3. **Segoe UI, two weights.** 400 and 600; the family has no 500 face
   (feat-027 § 2.1). Capitals only by CSS (the card's type line), so the DOM
   text and the accessible name keep their case.
4. **One accent.** `#0078D4` is the filled primary action (at most one per
   page header or dialog), the selected row's frame, the tab underline and
   the focus frame. A link's text is `#0C5598` (the accent as text is 4.26:1,
   under the 4.5:1 floor). Semantic colours (error, warning, success, info)
   mean something; type and category colours belong to work-tracking marks;
   **colour is never the only carrier** — a name or a text sits beside it.
5. **Surfaces are soft.** A white card with a soft glow (`theme.cardShadow`,
   the theme's 4 px corners) on the grey page; a grey band
   (`background.default`) for properties. No outlined panels in this look.
6. **Controls are quiet.** Fields have no frame at rest — a small grey label,
   the value as text — and show one on hover and focus. Buttons are grey
   (neutral) or, once per page or dialog, blue (primary); icon buttons have no
   fill. Forms that start blank use the same field with its frame drawn.
7. **Hairline glyphs.** The reference draws its glyphs as 1 px lines. The kit's
   thin set (1.5 units in the 24-unit box) covers the structural ones —
   chevrons (the split button's arrow is one), «+», «−», «✕», «⤢», «⋮» — and the
   ones the card's toolbar and the kit's own parts draw: save, refresh, undo,
   person. **Every glyph of the card's header is one of these eleven** — no
   Material glyph stands in that header. Every other glyph is
   Material *Outlined*, 18 px in a bar, 16 in a row: a known difference from the
   reference, listed with its owner in § 8.4.
8. **Focus is loud, hover is quiet.** The 2 px accent ring (or frame) marks
   keyboard focus and clears 3:1 on every surface; the hover frame is
   decoration (1.5–2:1) and never the only sign of anything. What shows only on
   hover — a field's frame, caret and placeholder look — also shows when the
   pointer cannot hover (`@media (hover: none)`). A table row's controls are
   revealed by the row's hover, focus and selection; with no hover a tap selects
   the row, and so shows them (§ 5.11, § 6).
9. **Honest limit.** A field with no frame at rest is the reference's look and
   is accepted (D-1); a label and the value's text identify it, the focus frame
   carries the 3:1, and `frame="always"` exists for anything that starts blank.
10. **Words.** Russian, sentence case, final in design.md; the ellipsis
    character «…» ends an entry that opens a dialog («Изменить родителя…»);
    one verb per action («Сохранить», «Закрыть»); no exclamation marks.
11. **Motion** is MUI's own and honours `prefers-reduced-motion` (mod-015
    § 12.7); nothing in the kit animates for its own sake.

## 2. Inventory of today's UI and what replaces it

Two readers walked `src/frontend/src` (read-only, at `main` bd3bb22). The
findings that shape the kit:

- **Two visual languages coexist.** mod-015's registry look covers every page
  but «Бэклоги» and «Канбан»; feat-027's reference look lives only in
  `features/boards` and in `PageHeader variant="team"`. feat-027 raised the
  weight scale panel-wide, so the registries' grey headers (`MuiTableCell.head`
  = `subtitle2`) became semibold, further from the reference than before.
- **Kit-grade pieces are trapped in slices** and cannot be shared under the
  slice rule: boards' `ThinIcons`, `BacklogIcon`, `BacklogCell`,
  `backlogRowStyle`, `StatusRegion`, `Notice`, `TeamMenu`, `LevelMenu`;
  workitems' `FormSection`, `Marks`, `Notice`, `FieldControl`, `HeaderChrome`;
  organizations' `RowActionsMenu`, `TruncatedName`, `requiredField`.
- **Per-call overrides fight the theme**: `MuiIconButton.sizeSmall` forces a
  20 px glyph (four slices re-override it), `MuiTableCell.head` is grey and
  semibold (the backlog replaces it), two row hovers (`#F0F0F0`, `#F8F8F8`).
- **Literals**: no colour literal outside the theme (the guard holds); every
  dimension is a literal or a spacing multiple chosen per call. § 3.5 lists
  them and their tokens.

| # | Pattern | Today (where) | Gap against the reference | Kit |
|---|---|---|---|---|
| 1 | Page head, command bar | `PageHeader` (default 16/8 spacing, padded tabs; `variant="team"` 28/12/40/20, flush tab), `HeaderChrome` re-implements the registration | only the team variant matches; the flush tab is local `sx`; overflow ⋯ with accent icons | `PageHeader` (§ 5.6), `usePageHead` |
| 2 | Tabs | `PageViews` (links) only | no boxed, icon or count tabs | `BoxedTabs` (§ 5.6) |
| 3 | Buttons | `MuiButton` variants; accent-iconed text, outlined, grey only in the team head; spinner re-written in `ConfirmDialog`, `FormDialog`, `RestoreButton`, `AddChildRow`, `VersionConflictPanel` | two tones and a split button | `ActionButton`, `SplitButton` (§ 5.2) |
| 4 | Tables | registry look (outlined, grey 36 px header, 600 labels, row rules, 14 px, `#F0F0F0`) in five registries; backlog look in `features/boards` | white 40 px header, 400 labels, separators, 13 px, no rules, framed selection | table family (§ 5.11) |
| 5 | Tree rows | `TreeRegistry` (24 px indent, filled chevrons, always-visible ⋯ column) and the backlog's title cell | 16 px indent, hairline chevrons, controls revealed on hover | `TreeCell`, `RowControl` |
| 6 | Type marks, state dots | `TypeMark` 20, gap 8 (workitems); compact 16, gap 6 (backlog) | 16 / 6 | `TypeLabel`, `StateLabel` (§ 5.4) |
| 7 | Icons, icon buttons | thin set in boards; `sizeSmall` overrides ×4 | one hairline set; no ⤢ | `icons`, `IconAction` (§ 5.1, § 5.2) |
| 8 | Menus | ten, each with its own anchor, keys, naming | neutral entries, one keyboard model | `MenuButton` (§ 5.3) |
| 9 | Dialog shell | `DialogTitleBar`, `ConfirmDialog` (xs), `FormDialog` (sm) | no near-full-window size, no ⤢, no stripe | `DialogShell`, `PanelShell` (§ 5.10) |
| 10 | Card header | workitems band with a 4 px stripe; the board card's stripe | bar 10 px, type line, id + title, actions | `CardHeaderBar`, `TypeStripe` (§ 5.9) |
| 11 | Fields | outlined floating-label `TextField` in 14 files; `InputBase` title; `AddChildRow` 28 px | borderless, label above or at the left | fields (§ 5.7) |
| 12 | Section heads | `FormSection` (slice), settings title, a copy in `RelatedWorkSection` | 16/600 head over a rule | `Section` (§ 5.8) |
| 13 | Body columns | `useMediaQuery` 8/4 grid in the form | three columns by the card's own width | `ColumnLayout` (§ 5.8) |
| 14 | Notices | five hosts, `6000` in five places, three dismissal rules | — | `NoticeProvider` (§ 5.12) |
| 15 | Inline notes | `ErrorBanner` plus three hand-written `Alert`s, margins by caller | container owns spacing | `InlineNotice` (§ 5.12) |
| 16 | Empty, error, not-found, loading | `StateFrame` (outlined action), `ErrorBoundary` own markup, `LoadingBox`, five skeleton shapes | skeleton mirrors the loaded look; no page spinner | `StateFrame`, skeletons (§ 5.12) |
| 17 | Panels | outlined `Paper` ×11 (filter band, form column, home) | glow card | `Surface` (§ 5.8) |
| 18 | Shell frame | top bar, trail, switcher, navigation, user menu | trail neutral grey, dark avatar, hairline chevrons | stays in `app/`, rebuilt from the kit in tech-019 |
| 19 | Truncation | five copies, `title` vs `Tooltip` | ellipsis and native title | `TruncatedText` (§ 5.5) |
| 20 | Live region, hidden text | boards `StatusRegion`, `mixins.visuallyHidden` | — | `LiveRegion`, `VisuallyHidden` (§ 5.5) |
| 21 | Avatars | 32 px menu, 24 px inline | dark disc, white initials | `UserAvatar`, `PersonLabel` (§ 5.4) |
| 22 | Chips, tags | filled/outlined `Chip`s ×6 files | grey and pale-blue pills with ✕ and «+» | `Tag`, `TagList` (§ 5.4) |
| 23 | Required markup | `requiredField.ts` and an inline copy; the title has `aria-required` and no mark | — | `required` on every field |
| 24 | Read-only facts | `FactsPanel`, `ProcessTypeCard`, the language form | label–value pairs like the band's | field `readOnly` |

Other findings, kept for their owners: `docs/conventions/frontend.md`
"Shared patterns" lists neither `WorkTrackingMarks`, `PageTrail`,
`DialogTitleBar`, `FormFooter` nor `LoadingBox`, and still says secondary
commands are text buttons (feat-027 § 3.5 superseded that for two heads and
never edited it); mod-015 design § 2.1 and § 2.3 still show the old accent and
type colours; `shared/ui/ErrorBanner.tsx`, `ErrorBoundary.tsx`,
`LoadingBox.tsx` and `RegistrySkeleton.tsx` carry Russian code comments against
the language rule (fixed when those files move, § 8).

## 3. Tokens

`app/theme/tokens.ts` stays the only source of appearance; `theme.ts` puts
everything on the theme object; slices and the kit read it through `useTheme()`
or `sx` callbacks (frontend.md "Theme"). `src/test/colourLiterals.test.ts`
keeps the colour rule, **strengthened in plan part 1 together with the tokens**
(§ 9.7: a colour inside a shorthand, a named colour and the default palette's
`grey.N` / `common.*` no longer pass); dimensions are checked by
`theme.test.tsx` pins (§ 3.6) and by review.

**When a dimension is a token.** (a) Two components must agree on it; (b) a
test pins it; (c) it was measured on the reference and a QA reader compares it.
Otherwise it is the spacing unit (8 px, half step 4) written inside the kit
component. A feature never writes a pixel number for something a token names.

### 3.1 Kept as they are

`colourRoles` (application background `#F8F8F8`, navigation trio, content
`#FFFFFF`, text `#242424` / `#616161`, accent `#0078D4`, `accentDark`
`#0C5598`, divider `#E0E0E0`, `inputBorder` `#8A8A8A`, `tableHeader`,
`rowHover`, `rowSelected` `#E5F0FA`, `addAction` `#8A8886`, `buttonNeutral`
`#EBEBEB`, `buttonNeutralHover` `#E2E2E2`), `semanticColours`,
`typeColours`, `categoryColours`, `shellPalette`, `breakpointValues`
(640 / 1024 / 1280), `mediumWeight` 600, `typeScale`, `cardShadow`,
`fontFamily`, `htmlFontSize`, `baseFontSize`, `tooltipEnterDelayMs` 500,
`focusRing` {2, 2}, `density` {registryRowHeight 36, controlHeight 32,
tabHeight 40}, and the layout keys of the shell (`topBarHeight`, `navigationWidth`,
`railWidth`, `settingsNavigationWidth`, `sidePaneWidth`, `formColumnWidth`,
`boardColumnWidth`, `boardColumnCollapsedWidth`, `authCardWidth`,
`avatarSize` 32, `categoryDot` 8, `contextNameMaxWidth`, `trailSegmentMaxWidth`,
`contextSkeleton*`, `selectedMarkerWidth`, `dividerWidth` 1).

### 3.2 New: colour, type, shape, time

| Token | Value | Used by | Note |
|---|---|---|---|
| `palette.shell.fieldHover` | `#C8C8C8` | the field's hover frame, the hover frame of a card control | 1.67:1 on white, 1.58:1 on `#F8F8F8`: inside 1.5–2:1 and under `inputBorder` (3.45:1). Decorative — the label names the field and the focus frame carries 3:1 (`theme.test.tsx` pins the range, not the hex) |
| `palette.shell.cardLabel` | `#707070` | a field's label (every placement), the band's labels and the band's meta line | the reference's label grey (`#707070`–`#737373` on its pixels). **4.95:1 on white and 4.66:1 on `background.default`**, both over the 4.5:1 floor; `#737373` is 4.46:1 on the band and is **not used**. `contrast.test.ts` pins both pairs at ≥ 4.5, so a lighter grey fails. `text.secondary` (`#616161`) stays for hints, placeholders, `emptyText` and every other secondary text |
| `palette.shell.sectionRule` | `#EAEAEA` | the rule under a `Section`'s head | the reference's (1.20:1 on white, lighter than `divider`'s 1.32:1): a decorative line, exempt from the 3:1 rule for graphics because nothing is lost without it — the head and the content carry the structure. Tab lines and table rules stay `divider` (the reference's tab lines are `#DADADA`, a recorded difference, § 5.6) |
| `palette.shell.splitDisabledFill`, `.splitDisabledText` | `#F0F0F0`, `#959595` | the disabled `SplitButton` (§ 5.2): the reference's clean «Save» | 2.63:1 — a disabled control is exempt from the contrast floor (WCAG 1.4.3), and the pair is the reference's own; `contrast.test.ts` records it as an exempt pair and `theme.test.tsx` pins the two hexes. **Not MUI's disabled grey**, which is a darker block with fainter text (§ 5.2) |
| `typeScale.cardTitle` → `typography.cardTitle` | 17 / 24 / 400 | the card's id and title | a custom variant, declared in the module augmentation like `tableCell` |
| `shapeRadius` (exported; `shape.borderRadius` reads it) | 4 | every kit surface and control | was the literal at `theme.ts:75`; the 2 px radius of an outlined header button is half of it (`borderRadius: 0.5` in `sx`) |
| `noticeDurationMs` → `theme.noticeDurationMs` | 6000 | `NoticeProvider` | replaces five copies |

### 3.3 New: layout groups

All in px unless noted; all on `theme.layout`, declared in the module
augmentation. A group's key list is the contract; the tests of § 3.6 pin it.

| Group | Keys and values | Used by |
|---|---|---|
| `frame` | `thin` 1, `thick` 2 | the width of a control's or a row's frame — a field's hover/rest and focus/error frames, a row's selection and drop frames, the `Removed` ring. Not `dividerWidth` (a rule between surfaces): equal today, different roles |
| `iconAction` | `row` {box 24, glyph 16}, `chevron` {24, 20}, `header` {24, 18}, `bar` {32, 18} | `IconAction`; every target is ≥ 24 px (WCAG 2.5.8) |
| `mark` | `typeIcon` 20, `typeIconCompact` 16, `categoryDotField` 10, `nameGap` 6, `avatarInline` 24, `personGlyph` 16 | `TypeLabel`, `StateLabel`, `CategoryMarker`, `UserAvatar`, `PersonLabel`, `PersonField`; one gap for every mark-and-name pair (the reference's 6; mod-003's 8 goes with the migration); `personGlyph` is the empty person's `ThinPersonIcon` inside the 24 px `avatarInline` slot (two parts must agree: `PersonLabel` and `PersonField`). `layout.categoryDot` 8 and `layout.avatarSize` 32 keep their places beside it |
| `inlineSpinner` | 16 | `ActionButton` busy, a field's busy adornment |
| `pageHead` | `top` 28, `viewsGap` 12, `bottom` 20, `titleGap` 10, `titleIcon` 24, `trailGap` 8, `panelTop` 16 | `PageHeader` (the team rhythm: 28 + 32 row + 12 + 40 tab row + 20 = 132; `trailGap` is the space between a trail drawn above the title and the title); `PanelShell` (`panelTop` above a page's panel, `trailGap` below its trail, § 5.10) |
| `table` | `headerHeight` 40, `minHeight` 240, `treeIndent` 16, `titleLabelInset` 36, `separatorHeight` 20, `gripHitWidth` 16, `gripDotOffset` 7, `gripMarkHeight` 20, `gripDot` 2, `gripPitch` 6, `draggedOpacity` 0.6 | the table family (§ 5.11). The row is `density.registryRowHeight` 36 |
| `field` | `padY` 4, `box` 28, `titleBox` 32, `gap` 2, `textInset` 6, `textAreaInset` 8, `textAreaTop` 15, `adornmentGap` 19, `textMinRows` 3 | the field family (§ 5.7): a value box is the text's line plus 2 × `padY` (20 + 8 = 28; the title's 24 + 8 = 32); label 16 + `gap` + box + `gap` = **48** whether the field is empty or filled; a single-line text starts at `textInset` (the 1 px frame and 5 of padding), a multi-line text at `textAreaInset` (the frame and 7 of padding) — with the box at the section's inset 8 that is 16 from the section's edge, as the reference sets it (feat-029 § 2, § 4.7), where a single-line value stands at 14. **`textAreaTop` is the space above a text that stands under a head of its own name** (`InlineTextArea` with `labelPlacement="hidden"`, § 5.7): it replaces the label's line there, so with the rule (1) and `section.bodyTop` (4) the box starts 20 below the rule's top edge and the first line's cap top is 30 below it (1 + 4 + 15 + `padY` 4 + 6 down to the cap). **`adornmentGap` is the space between a label-column adornment's right edge and the value's text** (§ 5.7): 19, the reference's, which puts a 10 px dot's centre at label + 64. These are the card's `textPadX` (= `textAreaInset`), `textTop` (= `textAreaTop`) and `bandAdornmentGap` (= `adornmentGap`), one name each in the family's convention (§ 13) |
| `section` | `headGap` 12, `bodyTop` 4, `gap` 32, `inset` 8 | `Section` (head 24 + 12 + the 1 px rule + 4 = 41 before the first field), and the content inset of the card's fields |
| `columns` | `gutter` 12, `top` 16, `bottom` 24, `gap` 16, `sideWidth` 320, `threeMin` 1040, `twoMin` 680 | `ColumnLayout`: tracks `minmax(0,1fr) 320 320` at ≥ `threeMin` (the first column is then at least 1040 − 640 − 32 = **368**), `minmax(0,1fr) 320` at ≥ `twoMin` (344 at the least), one column below |
| `card` | `dialogShare` 0.9, `typeBar` 10, `typeBarCompact` 4, `headerInset` 18, `rightInset` 8, `headerPadTop` 4, `headerRowGap` 8, `headerPadBottom` 8, `actionGap` 11, `dialogIconInset` 5, `dialogIconGap` 2, `bandMinHeight` 76, `bandPaddingTop` 6, `bandRow` 32, `bandRowGap` 4, `bandLabelWidth` 82, `bandBlockWidth` 253, `bandBlockMin` 240, `bandBlockGap` 32, `tabStripHeight` 36, `tabPadX` 10, `tabIndicatorOffset` 4, `stackBelow` 720 | `DialogShell`/`PanelShell`, `CardHeaderBar`, `PropertyBand`, `BoxedTabs`, `TypeStripe`. The header is `headerPadTop` 4 + 3 rows of `density.controlHeight` 32 + 2 × `headerRowGap` 8 + `headerPadBottom` 8 = **124**. `actionGap` is the space between row 3's actions (the Save group and each icon button: pitch 32 + 11 = 43); `dialogIconInset` is the distance of the ✕ box from the card's right edge and `dialogIconGap` the space between ⤢ and ✕ (pitch 34). **`bandLabelWidth` is the label column — the label's own width; the value's box starts where it ends and the value's text stands `field.textInset` 6 further in, so the text is 88 from the label's left edge in every row** (§ 5.7). `bandBlockWidth` is a block's fixed width (253 + `bandBlockGap` 32 = the reference's pitch 285); `bandBlockMin` is the least a block shrinks to and the width it keeps when the blocks stack. `tabIndicatorOffset` is how far the selected tab's indicator stands above the strip's bottom (its thickness is `frame.thick` 2) |

### 3.4 Lifted from `layout.backlogTable`

feat-027's table group mixes the table's own measures with generic ones. The
generic ones move to the kit's groups and are deleted from `backlogTable`; the
boards slice reads them from the new place in the same change (a rename of the
reads, listed below). What stays in `backlogTable` is the page's: the column
widths and `viewportOffset`.

| Was (`layout.backlogTable.…`) | Now | Value |
|---|---|---|
| `treeIndent` | `layout.table.treeIndent` | 16 |
| `chevronSlot`, `chevronGlyph` | `layout.iconAction.chevron.box`, `.glyph` | 24, 20 |
| `rowControl`, `actionGlyph` | `layout.iconAction.row.box`, `.glyph` | 24, 16 |
| `headerButton`, `headerGlyph` | `layout.iconAction.header.box`, `.glyph` | 24, 18 |
| `typeIcon` | `layout.mark.typeIconCompact` | 16 |
| `gripHitWidth`, `gripDotOffset`, `gripMarkHeight` | `layout.table.*` | 16, 7, 20 |
| `separatorHeight` | `layout.table.separatorHeight` | 20 |
| `hierarchyColumn`, `orderColumn`, `typeColumn`, `titleColumnMin`, `stateColumn`, `effortColumn`, `businessValueColumn`, `valueAreaColumn`, `viewportOffset` | stay | 72, 68, 184, 400, 104, 64, 64, 104, 204 |

Reads to rename (grep `backlogTable`): `AddChildRow`, `BacklogHeadRow`,
`BacklogRow`, `backlogRowStyle`, `BacklogTable`, `TeamHead`,
`shared/ui/WorkTrackingMarks`, and the tests `theme.test.tsx` and
`BacklogPage.look.test.tsx`. `PageHeader.team.test.tsx` keeps reading
`viewportOffset`. The move is its own early commit (plan part 1), so a merge
with feat-029's boards edits stays mechanical.

### 3.5 Every hard-coded value the inventory found, and its place

| Where it lives now | Value | Becomes |
|---|---|---|
| `PageHeader.tsx:175` (team) `pt 3.5`, `pb 2.5`, views row `mt 1.5`, title gap `1.25` | 28, 20, 12, 10 | `layout.pageHead.top`, `bottom`, `viewsGap`, `titleGap`. The default variant's `pt 2 / pb 1` and `gap 1` are legacy: untouched, deleted with the variant (tech-020) |
| `PageHeader.tsx:416` tab `px 0, mr 2, pt 2, pb 0.75` | flush tab, 40 px row | inside the kit's page tabs (spacing units); a test computes the row = `density.tabHeight` |
| `TeamHead.tsx:127,129` icon `fontSize: 24`; `:232` `ml: -0.5` | 24; −4 | `layout.pageHead.titleIcon`; the −4 px pull belongs to `PageHeader`'s `titleControl` slot |
| `PageHeader.tsx:178` trail `mb 1`; `HeaderChrome.tsx:21` `pt 2` and the band's `mt 2` above the card (`WorkItemFormPage`) | 8; 16; 16 | `layout.pageHead.trailGap`; `layout.pageHead.panelTop` (inside `PanelShell`); `usePageHead` replaces `HeaderChrome` (§ 5.6). A page's trail above the card is now 16 + trail + 8 (was 16 + trail + 16): the one deliberate change, to match `PageHeader`'s own trail |
| `DialogTitleBar.tsx:22,30,31` `pr 7`, top 1.5, right 2 | — | the shell's two icons (§ 5.10): `layout.card.headerPadTop` above, `dialogIconInset` and `dialogIconGap` |
| `ConfirmDialog.tsx:72`, `FormDialog.tsx:84`, `AddChildRow.tsx:208`, `VersionConflictPanel.tsx:49` spinner `spacing(2)` / `16` | 16 | `layout.inlineSpinner` |
| `ConfirmDialog.tsx:59`, `ErrorBanner.tsx:37`, `WorkItemFormPage` `mt 2`, `BacklogPage` `Box mb 2`, `AnonymousScreen mb 3` — margins by caller | 16 / 24 | none: `InlineNotice` has no margin; the container (a `Stack` gap, a section) owns it |
| `FormPanel.tsx:29` `p 3`, `FilterBar.tsx:27` `px 2 py 1.5 mb 2` | 24; 12 × 16 | `Surface padding="form"` and `padding="dense"` |
| `FormSkeleton.tsx:27,29`, `RegistrySkeleton.tsx:38`, `WorkItemFormPage` `FormLoading` 10 / 28×5 / 7 | — | none: a skeleton's sizes derive from the component it mirrors (`layout.field`, `layout.table`) |
| `StateFrame.tsx:71-77` `pt 8`, `pb 4`, `px 2`, `gap 1` | — | stay inside `StateFrame`; its compact variant is `pt 2`, `pb 2` |
| `TreeRegistry.tsx:350` outline `spacing(0.25)` | 2 | `focusRing.width` |
| `TreeRegistry.tsx:361,376,416` indent `3*(level-1)`, chevron box `3.5`, actions column `6` | 24, 28, 48 | `layout.table.treeIndent` 16, `layout.iconAction.chevron.box` 24, `RowControl` in the title cell — a look change, so it moves with tech-017 |
| `WorkTrackingMarks.tsx:63` ring `spacing(0.25)`; `TypeMark` default `fontSize small` | 2; 20 | `layout.frame.thick`; `layout.mark.typeIcon` |
| `Marks.tsx` (workitems) gap `1`, avatar `spacing(3)` | 8; 24 | `layout.mark.nameGap` (6), `layout.mark.avatarInline` |
| `BacklogHeadRow.tsx:63` `TITLE_LABEL_INSET '36px'`, `:84` `1px solid`, `:85` radius `shape/2`, `:116` `pl 2` | 36; 1; 2; 16 | `layout.table.titleLabelInset`; `layout.frame.thin`; `IconAction outlined`; spacing |
| `backlogRowStyle.ts:51-64` `inset 2px / 1px / -1px`, `:20` `DRAGGED_OPACITY 0.6`, `:124-125` grip 2px / 6px / 1px | 2, 1; 0.6; 2, 6, 1 | `layout.frame.thick/thin`; `layout.table.draggedOpacity`; `gripDot`, `gripPitch`; `layout.dividerWidth` |
| `BacklogTable.tsx:977` `minHeight: 240` | 240 | `layout.table.minHeight` |
| `AddChildRow.tsx:215-217` height 28 and its `(36 − 28)/2` margin | 28 | `layout.field.box` (the field's own box; the margin goes with the row's padding) |
| `BoardColumns.tsx:400`, `WorkItemFormPage.tsx:88` stripe `spacing(0.5)` | 4 | `layout.card.typeBarCompact` through `TypeStripe` |
| `WorkItemFormPage` `gridSx` (`mt 3`, `gap 3`, 2fr/1fr at lg), band `px 2 py 1.5` | — | `ColumnLayout`, `CardHeaderBar`, `layout.columns`, `layout.card` |
| `FormSection.tsx` `pb 1`, `mb 2`; `RelatedWorkSection` copy | 8, 16 | `layout.section` |
| the pickers' `minWidth` `spacing(24)` / `spacing(32)` | 192, 256 | none: a kit field fills its container; the band's blocks have `layout.card.bandBlockWidth` and `bandBlockMin` |
| `NOTICE_DURATION_MS = 6000` in four files, `autoHideDuration={6000}` | 6000 | `theme.noticeDurationMs` |
| `LanguagesRegistryPage` filter widths `spacing(19)`, `spacing(12)` | 152, 96 | stay the slice's (spacing units) |
| `theme.ts:75` `shape.borderRadius: 4`; `components.ts` `MuiOutlinedInput` focused `borderWidth: 2` | 4; 2 | `shapeRadius`; `focusRing.width` (a one-line theme fix, no look change) |
| `components.ts:84` `MuiIconButton.sizeSmall` glyph 20 and padding 4; `:32` button padding; `:141` cell padding; `:214,219` dialog padding | — | legacy theme defaults: untouched now, removed in tech-020. `IconAction` uses MUI's medium size, so the `sizeSmall` override never applies to a kit part |
| `MuiTableRow` hover `action.hover` `#F0F0F0` vs the backlog's `background.default` | — | the kit row's hover is `background.default`; the registry hover dies with the legacy table (tech-017) |
| `mixins.visuallyHidden` `1px` / `-1px` | — | stays (the standard technique); `VisuallyHidden` uses it |
| the shell: `AppLayout` `px {2, 3}`, `pb 3`; `TopBar`; `SettingsNavigation`; `UserMenu`; `SkipLink` | — | unchanged until tech-019 |

### 3.6 What `theme.test.tsx` pins

Every new key's value, and the relations that keep the design honest:

- the card header: `headerPadTop + 3 × density.controlHeight + 2 × headerRowGap + headerPadBottom = 124`;
- the header's right edge, measured on the reference: `rightInset + 2 × iconAction.bar.box + 2 × actionGap + iconAction.bar.box / 2 = 110` (the refresh button's centre from the right edge; its neighbours' are 24 and 67), `iconAction.bar.box + actionGap = 43`; and the dialog's icons: `dialogIconInset + iconAction.bar.box / 2 = 21` (the ✕'s centre), `iconAction.bar.box + dialogIconGap = 34` (the pitch; the ⤢'s centre is then 55);
- the band: `bandMinHeight ≥ bandPaddingTop + 2 × bandRow + bandRowGap` (74 ≤ 76) and `tabStripHeight ≤ bandMinHeight`; `bandBlockMin ≤ bandBlockWidth`; `bandBlockWidth + bandBlockGap = 285` (the pitch) and `headerInset + bandBlockWidth + bandBlockGap = 303` (block B's label from the card's left edge);
- the band's label column: `bandLabelWidth + field.textInset = 88` (every value's text from its label's edge) and `bandLabelWidth + field.textInset − field.adornmentGap − mark.categoryDotField / 2 = 64` (the state dot's centre from the label's edge);
- the tab strip: `tabIndicatorOffset = 4`, `frame.thick = 2` and `tabIndicatorOffset + frame.thick < tabStripHeight`;
- the columns: `twoMin < threeMin`; `threeMin − 2 × sideWidth − 2 × gap ≥ 360`;
- the page head: `top + controlHeight + viewsGap + tabHeight + bottom = 132` and `backlogTable.viewportOffset = topBarHeight + 132 + 24`;
- the field: `box = body1.lineHeight + 2 × padY`; `titleBox = cardTitle.lineHeight + 2 × padY`; `caption.lineHeight + gap + box + gap = 48`; `textInset < textAreaInset`; `adornmentGap` 19, `textAreaInset` 8 and `textAreaTop` 15 (the reference's measures, § 5.7); `mark.personGlyph ≤ mark.avatarInline`;
- the page head's trail: `trailGap` and `panelTop` (8 and 16);
- the section: `h2.lineHeight + headGap + dividerWidth + bodyTop = 41`;
- every `iconAction` box ≥ 24;
- `fieldHover`'s contrast against white and `background.default` is within [1.5, 2) and under `inputBorder`'s (a case in `contrast.test.ts`); **`cardLabel` ≥ 4.5:1 on white and on `background.default`** (the same file: a lighter grey fails); the hexes of `cardLabel` `#707070`, `sectionRule` `#EAEAEA`, `splitDisabledFill` `#F0F0F0` and `splitDisabledText` `#959595`, with `sectionRule` lighter than `divider`; the last pair recorded in `contrast.test.ts` as exempt (a disabled control);
- `noticeDurationMs`, `shapeRadius`, `typography.cardTitle` (17 / 24 / 400);
- the old `backlogTable` generic keys are gone (a regression test for the lift), and `theme.test.tsx`'s 1060 ≤ 1135 / 1060 > 975 width check still holds.

## 4. How the kit is built

### 4.1 Folders and the public entry

```
src/frontend/src/shared/ui/
  index.ts          the public entry: every kit component and its prop types
  icons/            ThinIcons.tsx, BacklogIcon.tsx
  actions/          ActionButton, SplitButton, IconAction, MenuButton, MoreActionsMenu
  marks/            TypeLabel, StateLabel, UserAvatar, PersonLabel, Tag (TagList)
  text/             TruncatedText, VisuallyHidden, LiveRegion, InlineEmpty
  fields/           the field frame (internal) and the seven fields
  layout/           Surface, Section (SectionHead), ColumnLayout
  card/             CardHeaderBar, PropertyBand, BoxedTabs (BoxedTabPanel), TypeStripe
  dialog/           DialogShell, PanelShell
  table/            TableCard, DataTable, HeadCell, BodyRow, Cell, TreeCell, RowControl, TablePager, TableSkeleton
  feedback/         NoticeProvider (useNotice), InlineNotice, FieldsSkeleton, CardSkeleton
  <the existing flat files>   PageHeader, PageTrail, StateFrame, ErrorBanner, ConfirmDialog,
                    FormDialog, FormPanel, FilterBar, TreeRegistry, SortableHeaderCell,
                    RegistrySkeleton, FormSkeleton, WorkTrackingMarks, … until § 8 moves them
```

### 4.2 Rules for whoever writes a kit component

1. **Built on MUI's standard part** — each spec below names it. A part MUI has
   is never re-drawn; the kit wraps it, sets its look from tokens and fixes its
   behaviour and names.
2. **Appearance only from the theme.** No colour literal (the guard test), no
   pixel number for what a token names (review), spacing units for the rest.
   Inside the kit, reaching into MUI's classes (`.MuiSvgIcon-root`) is allowed;
   in a feature it is a violation (§ 9, KIT-15).
3. **Layers.** The kit imports `shared/lib`, `shared/api` types and the theme
   object through `useTheme()`; never `features/` or `app/` (machine-checked,
   § 9, KIT-18).
4. **Props speak one vocabulary.** `label` (the accessible name, drawn or not),
   `tone`, `size`, `busy`, `disabled`, `readOnly`, `error` (the text), `hint`,
   `onChange(value)` (the value, not the event), `onSelect` (a menu entry),
   `to` (a link). Unknown `data-*` and `aria-*` pass through to the root
   control.
5. **Every interactive part** forwards a `ref` to its focusable element and
   shows its keyboard focus with the theme's ring or, for a field, its focus
   frame.
6. **States are part of the component** — rest, hover, focus, disabled,
   read-only, busy/loading, error, as each applies; a state in the spec is a
   state in the test and in the showcase.
7. **Names and texts are Russian and come from this file** (§ 7); a component
   with a default text says so; a text the host knows is a prop with no default.
8. **Done means**: the barrel exports it; a `<Name>.test.tsx` stands beside it;
   the showcase shows every state (§ 9, KIT-19/20); `ui-kit.md` has its block;
   `bash scripts/verify.sh` is green.
9. **jsdom limits.** jsdom evaluates no container query, no `:hover`, no
   `@media (hover: none)`. Component tests assert structure, attributes, roles,
   names and the styles jsdom does compute; what only a browser can show
   (hover frames, container-query columns, sticky headers) is the showcase
   E2E's (§ 11).

## 5. Components

Each block: **When**, **Built on**, **Anatomy**, **Props**, **States**,
**Names** (Russian, the pattern the E2E and tests select by), **Do / don't**.
`(Pn)` is the plan part (`status.md`) that builds it.

### 5.1 Icons — `shared/ui/icons` (P2)

**When.** A glyph the reference draws as a hairline. Anything else is a
Material `*Outlined` icon from `@mui/icons-material`.

**Built on.** MUI's `createSvgIcon` (no dependency), one open stroked path in the
24 × 24 box, `fill="none"`, `stroke="currentColor"`, `strokeWidth` 1.5 — the way
feat-027 § 4.4 drew the chevrons. They move here from `features/boards`
(`ThinIcons.tsx`, `BacklogIcon.tsx`); the boards' copies are deleted and its
imports repointed in plan part 2. **The exports keep the `Icon` suffix the code
already has** (`createSvgIcon(…, 'ThinChevronDown')` is exported as
`ThinChevronDownIcon`, as `BacklogIcon` is); the table and the showcase's
`covers` (§ 10.3) use the export names. Each is a `React.memo(forwardRef(…))`
object, not a function — the coverage test counts it as a component (§ 9.6).

| Export | Path (24 × 24) | Used for |
|---|---|---|
| `ThinChevronDownIcon` | `M4.5 8.5 12 16l7.5-7.5` | expanded, menu indicator, drop-downs, **the `SplitButton`'s arrow** — at the button's 18 px glyph its ink is about 11 × 6, the reference's 12 × 6; never Material's filled `ArrowDropDown` (exists) |
| `ThinChevronRightIcon` | `M8.5 4.5 16 12l-7.5 7.5` | collapsed tree row (exists) |
| `ThinAddIcon` | `M12 5v14M5 12h14` | «+» (exists) |
| `ThinRemoveIcon` | `M5 12h14` | «−» of the hierarchy header (was Material `Remove`, heavier than the row's «+») |
| `ThinCloseIcon` | `M6 6l12 12M18 6 6 18` | «✕»: the dialog's «Закрыть», a tag's remove |
| `ThinExpandIcon` | `M14 4h6v6M20 4l-7 7M10 20H4v-6M4 20l7-7` | «⤢»: «Развернуть на страницу» |
| `ThinMoreVerticalIcon` | filled dots at (12, 6), (12, 12), (12, 18), radius 1.5: `M12 7.5a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3zm0 6a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3zm0 6a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3z` | «⋮» — the one filled glyph of the set |
| `ThinRefreshIcon` | `M19.5 12a7.5 7.5 0 1 1-2.2-5.3M13.3 6.7h4v-4` | «Обновить»: a circle open at the upper right, an arrowhead corner at its end |
| `ThinUndoIcon` | `M9 14 4.5 9.5 9 5M4.5 9.5H15a4.5 4.5 0 0 1 0 9h-4` | «Отменить все изменения»: a left arrowhead and a stem that turns back |
| `ThinSaveIcon` | `M5 4.5h11.5l3 3V19a.5.5 0 0 1-.5.5H5a.5.5 0 0 1-.5-.5V5a.5.5 0 0 1 .5-.5zM8 4.5V9h7V4.5M7.5 19.5v-6h9v6` | the Save button's icon: a disk with its shutter and its label |
| `ThinPersonIcon` | `M12 11.5a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7zM5 19.5c0-3.3 3.1-5.5 7-5.5s7 2.2 7 5.5` | the empty person of `PersonLabel` and `PersonField`: a head and open shoulders, **drawn at `layout.mark.personGlyph` 16 px inside the 24 px slot** (`text.secondary`), where an assigned person is a `UserAvatar`. The reference's ink is **9 × 12 CSS px** (11 × 15 device) — the target of the developer's side-by-side check, the path's proportions are adjusted to reach it |
| `BacklogIcon` | `M8 6h13v2H8zM3 11h18v2H3zM9 16h12v2H9z` | the team head's glyph at 24 px (exists) |

The reference draws **every** glyph of the card as a hairline (save, refresh,
undo, «⋮», «⤢», «✕», the history, link and attachment tabs, the person, the
comments). The kit thins those that something in this task draws: the toolbar
(`ThinRefreshIcon`, `ThinUndoIcon`), the Save button (`ThinSaveIcon`) and the
person (`ThinPersonIcon`) — Material's `SaveOutlined`, `Refresh`, `Undo` and
`PersonOutline` are strokes of about 1.5 px at 24 px and a 24 px person is 16 px
of ink against the reference's 9 × 12 — **so the card's header draws exactly
these eight and no Material glyph**: `ThinExpandIcon` and `ThinCloseIcon` (the
shell's two icons), `ThinSaveIcon` (the Save group's), `ThinChevronDownIcon` (its
arrow), `ThinRefreshIcon`, `ThinUndoIcon`, `ThinMoreVerticalIcon` and
`ThinPersonIcon`. Material `*Outlined` stays outside the header, where nothing in
this task thins it: a menu entry's icon («Копировать ссылку») and a link in the
body («Открыть <ID>»). **History, link, attachment and comments glyphs are not
built** — nothing of this task draws them; the tab's host passes an icon to
`BoxedTabs`, and until its owner adds `ThinHistoryIcon`, `ThinLinkIcon`,
`ThinAttachIcon` or `ThinCommentIcon` here it passes Material `*Outlined`, a
known and recorded difference (§ 8.4).

`CrownIcon` and `StoryIcon` stay in `app/theme`, reached only through
`theme.workTracking`. The developer checks each new glyph at its size beside
the reference (feat-027 V-5; § 14); the paths are the starting point.

**States.** None of its own: a glyph is `aria-hidden` and takes its colour from
the button that holds it (`IconAction`, § 5.2).
**Names.** None; the button names it.
**Do / don't.** Do take the hairline glyph for every glyph in the table. Don't
draw another `createSvgIcon` in a slice (KIT-14); don't mix a Material filled
glyph and a thin one in one row of controls.

### 5.2 Buttons — `shared/ui/actions` (P2)

#### `ActionButton`

**When.** Every text button: a command, a dialog's primary and cancel, a retry,
a state frame's action.
**Built on.** MUI `Button` (`RouterLink` when `to`).

```ts
type ActionTone = 'primary' | 'neutral' | 'subtle' | 'danger';
interface ActionButtonProps {
  label: string;                       // the name; drawn
  tone?: ActionTone;                   // default 'neutral' — blue is deliberate
  icon?: ReactElement;                 // leading
  to?: string;                         // renders a link
  onClick?: (event: MouseEvent<HTMLElement>) => void;
  type?: 'button' | 'submit';          // default 'button'
  busy?: boolean;                      // the work this button started is running
  disabled?: boolean;                  // unavailable for a reason of state
  menu?: { open: boolean; hideIndicator?: boolean };  // opens a menu
  size?: 'default' | 'compact';        // 32 px / the inline retry's small
  fullWidth?: boolean;
}
```

| Tone | MUI | Look |
|---|---|---|
| `primary` | `contained`, `primary` | `#0078D4` fill, white label (4.53:1). **The page header's or dialog's only filled button** |
| `neutral` | `contained`, `inherit` | `shell.buttonNeutral` fill, `buttonNeutralHover` on hover, `text.primary` label and icon — the reference's grey secondary |
| `subtle` | `text`, `inherit` | no fill, `action.hover` on hover, `text.primary` label and icon (the mod-015 text button with an *accent* icon is legacy) |
| `danger` | `contained`, `error` | `error.main` fill — an irreversible action only (mod-015 § 9.4) |

Label `typography.button` (14/20/600); height `density.controlHeight` 32
(`compact`: no minimum, for a retry inside a notice); icon 18 px.

**States.** *Rest.* *Hover* — the tone's hover fill. *Focus* — the theme's
2 px ring, offset 2. *Disabled* — MUI's disabled look, native `disabled`, out of
the Tab order (the clean Save group of `SplitButton` has the reference's own
look, below). *Busy* — a `CircularProgress` of `layout.inlineSpinner` replaces
the icon (or stands where an icon would), `aria-busy="true"` and
`aria-disabled="true"`, the click is ignored, **the button stays focusable**:
a user who pressed Enter on «Сохранить» keeps their place. *Menu open* —
`aria-expanded="true"`; a trailing `ThinChevronDownIcon` unless `hideIndicator`
(`aria-haspopup="menu"` stays, so a picker with no drawn chevron is still
announced).
**Names.** The `label`. With `menu`: the same, plus `aria-haspopup="menu"` and
`aria-expanded`.
**Do / don't.** Do give each page header or dialog at most one `primary`; show
a pristine «Сохранить» `disabled`, not hidden. Don't use MUI `Button` in a
feature (KIT-2); don't build a busy spinner by hand; don't put an accent icon on
a neutral button.

#### `SplitButton`

**When.** A main action with a short list of variants: «Сохранить» with
«Сохранить и закрыть».
**Built on.** MUI `ButtonGroup variant="contained" disableElevation`: the main
button and an arrow button, the arrow's menu by `MenuButton` (§ 5.3).

```ts
interface SplitButtonProps {
  label: string; icon?: ReactElement;
  tone?: 'primary' | 'neutral';        // default 'primary'
  type?: 'button' | 'submit'; onClick?: () => void;
  busy?: boolean; disabled?: boolean;
  groupLabel: string;                  // role="group" name — «Сохранение»
  menuLabel: string;                   // the arrow's name — «Ещё варианты сохранения»
  items: MenuEntry[];                  // § 5.3
}
```

**Anatomy.** 32 high (`density.controlHeight`): the main part (an optional
18 px `icon`, the label in `typography.button`) and the arrow part, whose glyph is
**`ThinChevronDownIcon` at 18 px** (ink about 11 × 6: the reference's chevron is
12 × 6) and never Material's filled `ArrowDropDown`. The arrow part is about as
wide as it is high (the reference's group is 112 × 32: a 81 label part and a 31
arrow part) — a figure for the side-by-side, not a token.
**States.** Enabled, disabled (both parts), busy (the main button as above, both
parts inert, focus kept, **the tone's enabled fill kept**: the work is running, the
button is not unavailable), menu open (the arrow `aria-expanded`). Pristine is
`disabled` and wears **the reference's clean look: fill
`shell.splitDisabledFill` `#F0F0F0`, label and glyphs `shell.splitDisabledText`
`#959595`** (2.63:1; a disabled control is exempt from the contrast floor), for
both tones. `SplitButton` draws this itself; the theme's `MuiButton` defaults do
not change (K-1). **It is not MUI's disabled grey**, which this design's first
draft took for the reference's: MUI's contained button, disabled, is
`rgba(0,0,0,.12)` — `#E0E0E0` on white — with a label of `rgba(0,0,0,.26)` that
reads `#A6A6A6` over that fill (1.84:1): a darker block with a fainter label than
the reference's clean «Save». (feat-029 § 2 writes `#BDBDBD` for that label; that
is the label over white, not over its own fill.)
**Names.** The group `groupLabel`; the main button its `label`; the arrow
`menuLabel` — **it must not contain the main label's word**, so a query for
the main button's name finds it alone (a test pins this); the items are
`menuitem`s.
**Do / don't.** It is the page's only filled button (`primary`); `neutral` is for
a split button that is not the page's main action. Don't give the arrow an
accessible name that repeats the label.

#### `IconAction`

**When.** Every icon-only button: a row's «+» and «⋮», a tree chevron, the
header's two square buttons, a toolbar button of the card, a dialog's
«Развернуть на страницу» and «Закрыть».
**Built on.** MUI `IconButton` at MUI's **medium** size (the legacy
`MuiIconButton.sizeSmall` override never applies), with its own box and glyph
from `layout.iconAction`, inside a `Tooltip`.

```ts
interface IconActionProps {
  label: string;                       // aria-label AND tooltip text
  icon: ReactElement;
  size?: 'row' | 'chevron' | 'header' | 'bar';   // default 'bar'
  tone?: 'default' | 'muted' | 'add';  // glyph text.primary | text.secondary | shell.addAction
  outlined?: boolean;                  // 'header' only: 1 px text.primary square, radius ½ shape
  onClick?: (event: MouseEvent<HTMLElement>) => void;
  inactive?: boolean;                  // aria-disabled, focusable, no action, muted
  disabled?: boolean;                  // native disabled
  busy?: boolean;                      // spinner replaces the glyph
  tooltip?: boolean;                   // default true; a tree chevron passes false
}
// every `aria-*`, `data-*`, `onKeyDown`, `tabIndex` and the `ref` pass through to the
// button, so a MenuButton's trigger props (§ 5.3) can be spread on it and a treegrid
// row's roving tabindex (§ 5.11) can drive it
```

| Size | Box / glyph | Used by |
|---|---|---|
| `row` | 24 / 16 | a row's «+» and «⋮» (`RowControl`) |
| `chevron` | 24 / 20 | the tree chevron, the team chevron |
| `header` | 24 / 18 | the hierarchy header's «+» «−» (`outlined`) |
| `bar` | 32 / 18 | the card's toolbar (`ThinRefreshIcon`, `ThinUndoIcon`, `ThinMoreVerticalIcon`), the dialog's two icons (`ThinExpandIcon`, `ThinCloseIcon`) |

**States.** *Rest* — glyph `text.primary` (the theme's grey `action.active` is
not used: the reference's glyphs are near-black). *Hover* — `action.hover`
fill. *Focus* — the theme ring. *Inactive* — `aria-disabled="true"`, glyph and
border `text.secondary` (6.2:1), still in the Tab order, the click does
nothing (the backlog's idle expand/collapse and a write in flight). *Disabled* —
native, `text.disabled`. *Busy* — as `ActionButton`. The tooltip shows after
`tooltipEnterDelayMs` on hover and on keyboard focus, and also on a disabled
button (the kit wraps it).
**Names.** The `label`: «Развернуть на страницу», «Закрыть», «Обновить»,
«Отменить все изменения», «Ещё действия», «Добавить: Фича (MSK-EPIC-1)»,
«Развернуть MSK-EPIC-1». A row's control names its row.
**Do / don't.** Don't override a glyph's size with `sx` or `.MuiSvgIcon-root`
(KIT-15); don't use an icon button where a labelled button fits; don't leave
the `label` generic when many sit on a screen — name the row.

### 5.3 Menus — `shared/ui/actions` (P2)

#### `MenuButton`

**When.** Any button that opens a list of choices: a drop-down of links, a
choice list, the card's «Ещё действия», a split button's arrow, the user menu
and the switcher's levels (when the shell is rebuilt, tech-019).
**Built on.** MUI `Menu` and `MenuItem`; `ListSubheader` for a group.

```ts
type MenuEntry =
  | { label: string; icon?: ReactElement; onSelect?: () => void; to?: string;
      destructive?: boolean; disabled?: boolean; current?: boolean }
  | { group: string; items: MenuEntry[] }      // a heading and its entries
  | 'divider';
interface MenuButtonProps {
  label: string;                       // the menu's name (and the trigger's, by the host)
  trigger: (props: MenuTriggerProps) => ReactElement;  // ActionButton or IconAction
  items: MenuEntry[];
  align?: 'start' | 'end';             // 'end': the menu's right edge on the trigger's
  selection?: 'none' | 'radio' | 'page';
  disableRestoreFocus?: boolean;       // the host moves focus itself after a choice
}
// MenuTriggerProps: ref, onClick, onKeyDown, 'aria-haspopup': 'menu',
// 'aria-expanded', 'aria-controls' — spread them on the trigger.
```

**Behaviour** (mod-015 § 12.4, one model for every menu). Enter, Space or
ArrowDown on the trigger opens the menu with focus on the `current` entry (or
the first); ArrowUp/ArrowDown move, Home/End jump, typing a letter jumps; Enter
chooses; Escape closes and **returns focus to the trigger** (unless
`disableRestoreFocus`); a choice closes the menu. `selection="radio"`: entries
are `menuitemradio` with a check mark on the current one (the switcher's
levels); `"page"`: the current entry is `selected` with `aria-current="page"`
(a team or level drop-down); `"none"`: plain `menuitem`s.
**Look.** The MUI menu on `background.paper`; entries 14/20; icons in
`text.secondary` — **neutral** (the accent-iconed overflow of mod-015 § 6.1 is
legacy); a `destructive` entry's text and icon `error.main`; a `group` heading is a
`ListSubheader` (`caption`, `text.secondary`) and **each entry under it carries
its id in `aria-describedby`**, because a screen reader skips the subheader
while the arrows walk the items (feat-027 F-12).
**States.** Closed, open (trigger `aria-expanded`), entry hover/focus
(`action.hover`), `current`, `disabled` (skipped by arrows), `destructive`.
**Names.** The menu: `aria-label` = `label`. The trigger: by the host.
**Do / don't.** Don't write `Menu` or `MenuItem` in a feature (KIT-3); don't
name a menu «Меню»; don't rely on the destructive colour alone — the label says
what it does.

#### `MoreActionsMenu`

**When.** The «⋮» of a card, a row or a toolbar.
**Built on.** `MenuButton` with an `IconAction` of `ThinMoreVerticalIcon`.

```ts
interface MoreActionsMenuProps {
  label?: string;                      // default «Ещё действия»; a row passes «Ещё действия: MSK-EPIC-1»
  items: MenuEntry[];
  size?: 'row' | 'bar';                // default 'bar'
  revealed?: boolean;                  // a table row control: shown on hover, focus, selection
  disableRestoreFocus?: boolean;
}
```

**States** as `MenuButton` and `IconAction`. **Names:** the button and the menu
both read `label`. **Don't** build a «⋯» (horizontal) overflow: the reference's
is vertical.

### 5.4 Marks and people — `shared/ui/marks` (P3)

**When.** Wherever a work item's type, a state, a person or a tag is named.
Colour is never the only carrier: each mark is followed by a name.

| Component | Props | Look | Names |
|---|---|---|---|
| `TypeMark` (exists, primitive) | `appearance`, `compact?`, `colour?` | the type's icon in its colour via `theme.workTracking`; 20 px, 16 px compact (`layout.mark`) | `aria-hidden` |
| `TypeLabel` | `appearance`, `name`, `size?: 'default' \| 'compact'`, `selected?`, `eyebrow?` | `TypeMark` + `layout.mark.nameGap` 6 + the name; `compact` is the table's 16 px / `tableCell`; `selected` takes the icon to `primary.dark` (6.6:1 on `rowSelected`); `eyebrow` is the card's type line: the compact icon, `caption`, capitals **by CSS**, `primary.dark`, no letter-spacing | the name as text; the DOM text keeps its case |
| `CategoryMarker` (exists; the «state dot») | `code`, `size?: 'row' \| 'field'` | a dot of `layout.categoryDot` 8 (`row`) or `layout.mark.categoryDotField` 10 (`field`) by the category's colour; `Removed` is a ring of `layout.frame.thick` | `aria-hidden`; never drawn without the state's name |
| `StateLabel` | `categoryCode`, `name`, `size?: 'row' \| 'field'` (default `'row'`: the dot's size, as `CategoryMarker`), `clip?: boolean` | dot + `nameGap` + name; `clip` is an ellipsis with the full name in `title` | the name |
| `UserAvatar` | `name`, `size: 'menu' \| 'inline'` | MUI `Avatar`, initials from `shared/lib/initials`, 32 / 24 px (`avatarSize`, `mark.avatarInline`), `avatarInitials` type; its fill is the theme's (the reference's is darker: shell task tech-019) | `aria-hidden` beside a name |
| `PersonLabel` | `name: string \| null`, `size?: 'inline' \| 'menu'` (default `'inline'`), `emptyLabel?` | avatar and name; `null` shows a `ThinPersonIcon` (`mark.personGlyph` 16 px, centred in the 24 px `avatarInline` slot) and the empty text in `text.secondary` | «Не назначен» by default; «Неизвестный пользователь» when the host has an id and no name (host text) |
| `Tag` | `label`, `tone?: 'neutral' \| 'accent' \| 'success' \| 'warning'`, `variant?: 'filled' \| 'outlined'`, `onRemove?` | MUI `Chip size="small"`, a full pill; `neutral` on `shell.buttonNeutral`, `accent` on `shell.rowSelected` (the backlog's pale-blue pills), `success`/`warning` MUI's tints (registry statuses «Активен», «изменилось»); the label `typography.tableCell` 13/20; `onRemove` adds a `ThinCloseIcon` | the label; the remove button «Удалить тег «<label>»» |
| `TagList` | `tags: {id, label, tone?}[]`, `onRemove?`, `onAdd?`, `addLabel?` | tags wrap with a gap of 8 px; a trailing 24 px ghost «+» (`ThinAddIcon`, `buttonNeutral` fill, radius ½ shape) when `onAdd` is given — the place kept for the tags feature | «Добавить тег» (the «+»); the list is a `list` |

**States.** A tag: rest, hover and focus only when it has `onRemove` (the
remove button takes the ring); disabled when its host is read-only (no remove,
no «+»). The others are static.
**Do / don't.** Don't show a state as a coloured chip (the reference never
does): a dot and a name. Don't draw a type icon without its name nearby
except in the eyebrow, which names it in text. Don't use `Chip` in a feature
(KIT-6).

### 5.5 Text helpers — `shared/ui/text` (P3)

| Component | Props | Behaviour |
|---|---|---|
| `TruncatedText` | `children: string`, `hint?: 'title' \| 'tooltip'` (default `'title'`), `to?`, `look?: 'plain' \| 'link'`, `component?` | one line, `min-width: 0`, an ellipsis; `title` carries the full text (the reference's native hover title) or a MUI `Tooltip` after 500 ms; with `to` it is a router link — `plain` is `color: inherit`, underlined on hover only (the backlog's title); `link` is the theme's link colour. The DOM text is whole, so the accessible name is complete |
| `VisuallyHidden` | `children`, `component?` | `theme.mixins.visuallyHidden`; a hidden description or label |
| `LiveRegion` | `text: string`, `serial?: number` | `role="status"`, visually hidden; every message replaces the text node (keyed by `serial`), so a repeated message is spoken again |
| `InlineEmpty` | `children` | `body2`, `text.secondary`: an empty list or section («Дочерних элементов нет.»). Not a `StateFrame`: no centred frame inside a section |

**Don't** copy an ellipsis `sx` into a slice, or use `title` and `Tooltip` for
the same kind of text in one screen; one position (the title attribute) is the
default.

### 5.6 Page head and tabs

#### `PageHeader` — `shared/ui/PageHeader.tsx` (exists) (P5)

**When.** The first block of every page: the title as the page's only `h1`
(and the browser tab), at most one primary action, secondary commands, and the
page's views.
**Built on.** `Typography`, `ActionButton`-looking `Button`s (rebuilt on
`ActionButton` in tech-017), `Tabs` with `Tab component={RouterLink}`,
`MoreActionsMenu` for the overflow.

```
(28)                                          [trail — below 1024 px, above the title]
[icon] Title ⌄                                      [Primary] [Secondary] [Secondary] [⋮]   ← 32
(12)
View 1   View 2                                                         [viewsEnd]       ← 40
(20)
```

**The kit's page head is the team rhythm of feat-027 § 3.2**, in `layout.pageHead`
(28 / 32 / 12 / 40 / 20); the title group's icon, `h1` and drop-down 10 px apart;
the tab flush with the content's left edge, its underline the label's width, the
label 8 px above it (40 px row). The props are the ones it has
(`title`, `ancestors`, `primaryAction`, `commands`, `views`, `viewsLabel`,
`viewsEnd`, `titleIcon`, `titleControl`, `titleRef`, `documentTitle`,
`variant`), plus:

| Addition (part 5) | Meaning |
|---|---|
| `headingLevel?: 1 \| 2 \| 3` | the title's level, default 1. A page head shown inside a region (the showcase) takes the level below its section's |
| `variant="team"` is the kit look | the default variant (16/8 spacing, padded tabs, accent-iconed text secondaries, ⋯ overflow) is **legacy**: frozen, deleted with `variant` when the last page leaves it (tech-020) |
| in `variant="team"` the overflow is `MoreActionsMenu` (⋮, neutral icons) and a command's default tone is `neutral` | the default variant keeps ⋯ and accent icons until it is deleted |
| `usePageHead(options)` → `{ trail }` | the registration, the tab title and the trail's placement, extracted so a page with no `PageHeader` — the card's page — has the same seam; `PageHeader` calls it. Contract below |

**`usePageHead`** (its own file, `usePageHead.ts`: ESLint's `react-refresh` rule):

```ts
interface PageHeadOptions {
  title?: string;                       // the tab's title; absent: the tab is left alone (the page is still loading)
  ancestors?: readonly TrailSegment[];  // the page's own trail segments, as PageHeader's prop
  documentTitle?: string;               // when the tab says something other than `title`
}
function usePageHead(options: PageHeadOptions): { trail: ReactElement | null };
```

It (1) names the tab (`useDocumentTitle(documentTitle ?? title)`), (2) registers
the head with the shell in a layout effect keyed by the serialised `ancestors`, so
a new array on every render registers once, and (3) returns **`trail`: the ready
`PageTrail` element** when the shell asks for the trail above the page
(`trailPlacement === 'header'`, below 1024 px) and the trail is not empty,
otherwise `null` (the top bar draws it). The *host* places the element:
`PageHeader` draws it above its title with `layout.pageHead.trailGap` below it;
a page that has no `PageHeader` passes it to `PanelShell`'s `trail` slot (§ 5.10),
which owns the spacing — so the `pt: 2` literal of today's `HeaderChrome` has no
heir and `HeaderChrome` is deleted by feat-029. Outside the shell (a test, the
auth card) the context is inert and `trail` is `null`.

**States.** *Loading:* the head stands complete at once; the primary is absent
until its data is read. *Permissions:* a command without its code is not
rendered; the bar keeps `controlHeight` so a reader's head is as high as an
administrator's. *Narrow* (640–1023): the trail stands above the `h1`; secondary
commands that do not fit move into «Ещё действия», last first; the primary
never moves. *Error / empty:* the head stays; the page body shows the state.
**Names.** The `h1` is the title; the views' tablist is named by `viewsLabel`
(«Вид страницы» when the page has one) or by the title; each tab is its
`label`; the overflow is «Ещё действия».
**Do / don't.** One `h1` per page and one `PageHeader`; don't build a second
head from `Typography` and `Tabs`; don't put two `primary` commands in it. The
guard catches the two ways to hand-roll a head by machine: a heading element of
your own (`Typography component="h1"`, `<h2>` — KIT-21) and a page's own
registration with the shell (`usePageChrome`, `PageChromeContext`,
`useDocumentTitle` — KIT-22). A page title is `PageHeader`; a title with no
head — a page whose head is the card's — is `usePageHead` plus `CardHeaderBar`;
a titled group is `Section`; an empty or refused page is `StateFrame`.

#### `BoxedTabs` — `shared/ui/card/BoxedTabs.tsx` (P5)

**When.** The sections of a card («Детали», «История», «Связи», «Вложения»),
at the right end of the band. Not for page views (those are addresses:
`PageHeader views`).
**Built on.** MUI `Tabs` and `Tab`; a count is a small `Chip`.

```ts
interface BoxedTab {
  value: string;
  label: string;                       // the name; drawn unless iconOnly
  icon?: ReactElement;
  count?: number;                      // a grey pill after the label
  iconOnly?: boolean;                  // the label is the name and the tooltip
}
interface BoxedTabsProps {
  idBase: string;                      // from useId(); links tabs and panels
  'aria-label': string;                // the tablist's name
  value: string;
  onChange: (value: string) => void;
  tabs: BoxedTab[];                    // one tab is a valid strip
}
function BoxedTabPanel(props: { idBase: string; value: string; selected: boolean; children: ReactNode }): ReactElement;
```

**Anatomy.** A strip `layout.card.tabStripHeight` 36 high, right-aligned, its
right end `layout.card.rightInset` 8 from the card's edge, against the band's
lower edge; 1 px `divider` lines on the top and the sides of every tab, none at
the foot (the reference's are `#DADADA`, six steps darker: decorative, no token
of its own, a recorded difference); each tab `tabPadX` 10 each side, `min-width: 0` and `min-height`
`tabStripHeight` (MUI's 90 px width floor and the theme's `density.tabHeight` 40 for
`MuiTab` would otherwise widen a one-tab strip and make it 4 px too high); the
selected tab's label 600
`text.primary`; other labels 400 `text.secondary`; a count is a `buttonNeutral`
pill; an icon is 18 px. **The indicator** is a `primary.main` line
`layout.frame.thick` **2 px thick and as wide as the label**, not the tab, with its
foot **`layout.card.tabIndicatorOffset` 4 px above the strip's bottom** — the
reference's underline stands 4 px above the band's lower edge, directly under
the label. MUI's own indicator is `bottom: 0` and tab-wide, so the kit overrides
both (an override or the label's own underline; inside the kit either is allowed,
the result is what is pinned).
**States.** Rest, hover (`action.hover`), focus (the ring), selected, `disabled`
(rare). **Keys:** Left/Right move focus, Home/End jump, Enter or Space selects
(manual activation, MUI's default).
**Names.** The tablist: the `aria-label` («Разделы рабочего элемента»). A tab:
its `label`, with « (<count>)» appended when it has a count («Связи (1)»), set as
the tab's `aria-label` so the pill's bare digit is never the name by itself; an
`iconOnly` tab shows its label as a tooltip. The panel is
`role="tabpanel"`, `aria-labelledby` its tab, tab `aria-controls` the panel.
**Do / don't.** Don't render a panel for a tab that is not selected; don't use
it for navigation between addresses; a lone tab is fine.

### 5.7 Fields — `shared/ui/fields` (P4)

**When.** Every value a person reads or edits: the card's properties and texts,
a dialog's or form page's inputs, a filter, the add row's title, a read-only
fact. Seven fields share one anatomy (the internal field frame): a label, the
value, a hint, an error, the same states, the same names.

| Field | MUI base | Value | Specifics |
|---|---|---|---|
| `InlineTextField` | `InputBase` | `string` | one line; `size`, `maxLength`; a value that does not fit shows `text-overflow: ellipsis` at rest (the card's long title), and scrolls as an input does while the field has focus |
| `InlineTextArea` | `InputBase multiline` | `string` | `minRows` (default `layout.field.textMinRows` 3), grows with its text; `size="body"` only; its text starts at `layout.field.textAreaInset` 8 (a single-line text, at `textInset` 6). **With `labelPlacement="hidden"` it is a text under a head of its own name** (the card's «Описание», «Критерии приёмки»): its box begins `layout.field.textAreaTop` 15 below the section's `bodyTop`, where a `top` label's line would have been, so the first line's cap top is 30 below the head's rule and the text 16 from the section's edge, as the reference sets it; the `label` is the head's own text (the accessible name stays «Описание») and **no second, 12 px «Описание» is drawn under the head**. The field's block is `textAreaTop` + the box + `gap` 2, so the next head's cap top is 127 below the rule for a text of up to `textMinRows` 3 lines |
| `InlineNumberField` | `InputBase` (`type="text"`) | `string` — the *text*, so a half-typed number survives; the host parses | `inputMode: 'decimal' \| 'numeric'`; the host shows «,» and no trailing zeros |
| `InlineDateField` | `InputBase type="date"` | `'YYYY-MM-DD' \| ''` | `min`, `max`; the browser's own control; the empty mask (дд.мм.гггг) and the calendar button are hidden until hover or focus — the technique is under *The date field's mask*; `onIncomplete?` is called on a key-up while the entry is partial (feat-022), **after `onChange('')` when the value was not already empty** (review R1-1): a partial date's value is `''`, so the prop and the control agree and completing the date reports it through `onChange` again |
| `InlineSelectField` | `Select` + `MenuItem` | one `value` of `options` | `options: {value, label, startAdornment?}[]`; `emptyLabel?` adds a first option with that label («Не указан») — its presence is what makes the field optional; `renderValue?` |
| `InlinePickerField` | `Autocomplete` | an option or `null` | `options`, `getOptionLabel`, `isOptionEqualToValue?`, `renderOption?`, `loading?`, `loadingText?`, `noOptionsText?`, `onInputChange?`, `filterOptions?`, `endAction?` (a link or button after the value — «Открыть MSK-FEAT-6»); the input is always present: at rest it shows the chosen option's text, on focus the text is selected for searching |
| `PersonField` | `InlinePickerField` | a person or `null` | a start slot `layout.mark.avatarInline` 24 wide: an assigned person is a `UserAvatar` 24 with the initials, **nobody is a `ThinPersonIcon` at `layout.mark.personGlyph` 16, `text.secondary`**, centred in the same slot; the empty value reads `emptyText` («Не назначен») in `text.secondary` at rest. In the card's header (the field at `headerInset` 18) the slot's centre is 36 and the name's text 54 from the card's left edge: `textInset` 6 + the slot 24 + `mark.nameGap` 6 |

The parent field of a work item is **a composition, not a kit component**: an
`InlinePickerField` with a `TypeLabel` start adornment, `renderOption` of a
`TypeLabel` + identifier + title + `StateLabel`, and `endAction` the «Открыть»
link. `features/workitems` (`ParentPicker`) owns it; the kit supplies every part.

```ts
interface FieldProps {
  label: string;                                  // always set: it names the control, drawn or not
  labelPlacement?: 'top' | 'start' | 'hidden';    // default 'top'; 'hidden': not drawn — the title, the assignee, a text under a head of its own name
  labelWidth?: number;                            // 'start' only; default layout.card.bandLabelWidth 82: the label column; the value's text stands textInset (6) further in, at 88
  size?: 'body' | 'title' | 'table';              // 14/20 | cardTitle 17/24 | tableCell 13/20
  frame?: 'hover' | 'always';                     // default 'hover' (card); 'always' = the form look
  hint?: string;                                  // a caption on focus; always in the description
  error?: string;                                 // the text; error frame, the line, aria-invalid
  required?: boolean;                             // an asterisk and aria-required, never native
  readOnly?: boolean;
  busy?: boolean;                                 // read-only look, text not greyed, aria-busy
  disabled?: boolean;                             // only where the value can never be edited
  placeholder?: string;                           // ALWAYS the input's `placeholder` attribute; only its look follows the states
  emptyText?: string;                             // shown at rest when empty, text.secondary
  startAdornment?: ReactNode;                     // a dot, an icon, a 24 px avatar
  adornmentPlacement?: 'value' | 'label';         // default 'label' when labelPlacement="start", else 'value'; 'label' means nothing for top/hidden
  id?: string;
}
// InlineTextField:   FieldProps & { value: string; onChange(value: string): void; maxLength?: number }
// InlineTextArea:    FieldProps & { value; onChange; minRows?: number }
// InlineNumberField: FieldProps & { value: string; onChange(text: string): void; inputMode: 'decimal' | 'numeric' }
// InlineDateField:   FieldProps & { value: string; onChange(value: string): void; min?: string; max?: string; onIncomplete?(): void }
// InlineSelectField<V extends string>: FieldProps & { value: V | ''; onChange(value: V | ''): void;
//                      options: { value: V; label: string; startAdornment?: ReactNode }[]; emptyLabel?: string;
//                      renderValue?: (option: { value: V; label: string }) => ReactNode }
// InlinePickerField<O>: FieldProps & { options: O[]; value: O | null; onChange(option: O | null): void; … }
// each also takes inputProps (inputMode, aria-*, data-*, min, max) and a ref that focuses the input.
```

**Anatomy.**

```
labelPlacement="top"                          labelPlacement="start" (the band)
 Label ····················· hint (on focus)   Статус   ●  Новая         ← the dot stands in the
 ┌ value ───────────────────────────────┐     Область     Платформа     ← label column; both texts
 └──────────────────────────────────────┘                                  start at 88
 error line (caption, error.main)

 `start`, x from the label's left edge:
   Статус   0 ·· label ·· 53 ┌ 59 ● 69 ··19·· 88 Новая ┐        the box takes in the dot
   Область  0 ·· label ····· 82 ┌ 88 Платформа ┐               an ordinary box begins at 82
```

The label is a real `<label for>`, `caption` 12/16 `shell.cardLabel`, above the
value (`top`), in a column at its left (`start`, the band's rows:
`layout.card.bandRow` 32 high, the value box centred), or visually hidden
(`hidden`: the card's title and assignee, and a text under a head of its own name
— the name is still the label). The
value box is `layout.field.box` 28 high (`titleBox` 32 for `size="title"`) with
`padY` 4; the text starts `textInset` 6 in (`textAreaInset` 8 for
`InlineTextArea`). Label 16 + `gap` 2 + box 28 + `gap` 2 = **48 whether empty or
filled**: typing into an empty field moves nothing. A frame is an inset shadow of
`layout.frame` width, so **no state changes the box**.
**The label column** (`start`). `labelWidth` — by default `layout.card.bandLabelWidth`
**82** — is the label's own width: the value's box starts where it ends and the
text stands `textInset` 6 further in, so **every row's value text is 88 from the
label's left edge, whatever else the row holds**, as it is in the reference («New»
and «MT» stand +88 and +89.6 CSS px from their labels). The column is the label's,
not the box's: the box never begins before it, except for the label-column
adornment below.
**The adornment.** `startAdornment` is a dot, an icon or an avatar. With
`adornmentPlacement="value"` (the default for `top` and `hidden`: the assignee's
person or avatar) it is drawn inside the box before the text, `mark.nameGap` 6 from
it. With `"label"` (the default for `start`: the state's dot, a place kept's lock) it
stands **in the label column**: its right edge `layout.field.adornmentGap` **19**
before the value's text — a 10 px dot is at 59–69, **its centre at label + 64**, the
reference's — and **the box takes it in**: the frame of hover, focus and error
starts `textInset` before the adornment's left edge (53 for the dot) and wraps the
dot and the text together, so the field reads as one control. The label is clipped
with an ellipsis where the box begins («Статус» ends near 38 against the 53 it
has; a label that needs more belongs in a `top` field). So «Новая» starts where
«Платформа» does — in the reference the state dot and the Reason row's lock stand
inside the label's 88 px, never in the value (were the dot an ordinary adornment in
a box that begins at 82, the text would stand further right than its neighbours').
**The hint** shows as a `caption` on the label's own line, right-aligned, while
the field has focus (`top`); it is always in `aria-describedby`. It covers
nothing and moves nothing (feat-029 had it float under the value, over the next
field's label — § 13). **It never wraps and never pushes the label**: the label
keeps its width, the hint takes the rest of the line (`flex: 1 1 0`, `min-width:
0`, `white-space: nowrap`, `text-overflow: ellipsis`), so in a column too narrow
for both it is cut with «…» — its full text stays in the DOM, so
`aria-describedby` reads it whole. For `start` and `hidden` it floats under the
value on an opaque background and `z-index` above its neighbours.

**States.**

| State | Look and behaviour |
|---|---|
| Rest, `frame="hover"` | no frame, no fill, no caret; the value is text; an empty field is blank (its `emptyText`, if any, in `text.secondary`); a select's caret and a date's calendar button are hidden; the placeholder **attribute is present** and its text is transparent (*The placeholder*, below) |
| Rest, `frame="always"` | a 1 px `shell.inputBorder` frame (3.45:1); the placeholder shows; the label stays above (never floating) |
| Hover | a 1 px `shell.fieldHover` frame (`always`: `text.primary`), the caret and the placeholder's text appear; the box does not change |
| Focus | a 2 px `primary.main` frame (`focusRing.width`), the caret and the placeholder's text show, the hint shows; a select opens on Enter, Space or ArrowDown; a picker selects its text |
| Error | `error` set: a 1 px `error.main` frame at rest and hover, 2 px on focus; the error line under the box (`caption`, `error.main`, `role="alert"`), which **does** move what is below; `aria-invalid="true"`; the line replaces the hint as the description; editing clears nothing by itself (the host clears it) |
| Read-only | the value as text; no hover frame, no caret; reachable by Tab, `aria-readonly`; a read-only select or picker does not open |
| Busy | the read-only look with the text colour unchanged (not the grey of `disabled`); `aria-busy="true"`; edits refused |
| Disabled | `text.disabled`, native `disabled`; only where the value can never be edited |
| Loading | a picker with `loading`: its list shows `loadingText` («Загрузка…»); the input stays usable |
| Touch (`hover: none`) | the hover look (the frame, the placeholder, the caret) is also the rest look, because there is no hover to discover it by |

**The placeholder** is **always** the input element's own `placeholder` attribute,
in every state and in the DOM, whatever the frame (a select has none). Only its
look follows the states: at rest in `frame="hover"` the CSS makes it transparent
(`::placeholder { opacity: 0 }`); hover, focus, `@media (hover: none)` and
`frame="always"` show it. The existing E2E specs that read the attribute with no
hover or focus — `25-work-item-fields` (lines 142 and 304 `'Не оценено'`, line
270 the text area's «Опишите, по каким признакам история считается готовой») and
`26-backlog-tree-editing` (line 1080 `'Введите заголовок'`, the add row that
moves to `InlineTextField` in tech-016) — therefore pass unchanged. It is never the
label and never the name; a screen reader may read it as a description, so it
never says what the label does not. The shared field suite pins the attribute
in every state (§ 12).

**The date field's mask.** The browser's own `type="date"` stays. At rest and
empty its mask «дд.мм.гггг» and its calendar button are hidden by CSS, so an
empty date reads blank like its neighbours and **nothing moves**:
`::-webkit-datetime-edit` takes `opacity: 0`, and
`::-webkit-calendar-picker-indicator` `opacity: 0` — kept in the layout and
clickable, not `display: none`. The component decides when: it sets
**`data-empty="true"` on the input while `value === ''` and no `error` is set**,
and the hide rule is `[data-empty="true"]:not(:focus)` with the field not hovered.
Hover, focus, `hover: none` and `frame="always"` show both; a filled date shows
its text and hides only the button until hover or focus; a set `error` (a partial
entry, feat-022's «Введите дату полностью.», raised by `onIncomplete`) keeps the
mask visible, so the user sees what they typed (an incomplete date's `value` is
`''`). These are Chromium-family pseudo-elements; in Firefox and Safari the native
mask and button stay visible at rest — the field works and nothing moves, an
accepted difference (§ 8.4). jsdom pins the attribute (empty, filled, error);
E2E 28.3 reads the computed `opacity` of both pseudo-elements at rest, on hover
and on focus.

**Names.** The control's accessible name is its label **only**: `<label for>` on
an input; for `InlineSelectField` MUI would add the shown value, so the kit
sets `SelectDisplayProps['aria-labelledby']` to the label's id. Roles:
`textbox` (text, number, text area), `combobox` (select, picker, person); a
date by its label. `required` draws « *» (`aria-hidden`) after the label, so the
name stays «Заголовок»; `aria-required="true"` is set and the native
`required` never is (the form is `noValidate`; the checks are the host's).
`aria-describedby` = the hint's id, or the error line's while `error` is set.
**Do / don't.** Do pass a `label` even when `labelPlacement="hidden"`. Do give a
text that stands under a section head of its own name `labelPlacement="hidden"`
with the head's text as its `label`; don't use `top` there — a second 12 px
«Описание» would be drawn under the head and everything below would stand about
16 px lower than the reference's. A text area with no head of its own name takes a
visible `top` label: `hidden` on it means «under a head» and leaves `textAreaTop`
of space above it. Do use `frame="always"` for a form that starts blank — a
dialog's «Название», a filter — and the default for editing a record in place.
Don't wrap a field in
`FormControl`, don't set its width (it fills its container; the host sizes the
container), don't use the placeholder as the label, don't show a hint as a
permanent helper line, don't use MUI `TextField`, `Autocomplete`, `InputBase`
or `FormHelperText` in a feature (KIT-1). A read-only fact (`dl`-style) is a
`readOnly` field in a band or a section.

### 5.8 Surfaces, sections, columns — `shared/ui/layout` (P5)

#### `Surface`

**When.** Any panel on the page: the card, a band, a form's column, a
filter band, the home card.
**Built on.** MUI `Paper` at `elevation={0}`.

```ts
interface SurfaceProps {
  variant: 'card' | 'band';
  padding?: 'none' | 'dense' | 'form';  // default 'none': 0 | 12 × 16 | 24
  component?: ElementType;               // e.g. 'form', 'section'
  children: ReactNode;
}
```

`card`: white, `theme.cardShadow`, `shapeRadius`, no outline — the reference's
soft glow; `band`: `background.default`, no shadow, square. The board's tile
(`Card elevation 1`) is not a surface of the kit yet (§ 8.4).
**Don't** write `Paper`, `Card` or a `boxShadow` in a feature (KIT-7, KIT-16; the
`elevation` those take is not a rule of its own).

#### `Section` and `SectionHead`

**When.** A titled group in a card's body: «Описание», «Планирование»,
«Связанная работа». `SectionHead` alone is for a heading that is not a region
(a board column's head).
**Built on.** `Typography` (`h2`), a rule, a `<section>`.

```ts
interface SectionProps {
  title: string;
  headingLevel?: 2 | 3;                  // default 2
  count?: number;                        // «Дочерние элементы (3)»-style, after the title
  actions?: ReactNode;                   // right of the title
  divider?: boolean;                     // default true: the 1 px rule, shell.sectionRule
  children: ReactNode;
}
interface SectionHeadProps {             // the head alone: the heading, its count and actions, the rule
  title: string;
  headingLevel?: 2 | 3;                  // default 2
  count?: number;
  actions?: ReactNode;
  divider?: boolean;                     // default true
  id?: string;                           // the heading's id: what a Section's aria-labelledby points at
}
```

`Section` is `<section aria-labelledby={id}>` holding a `SectionHead` (it
generates the `id`) and its children inset by `layout.section.inset`.

**Anatomy.** The head is `typography.h2` (16/24/600), `layout.section.headGap`
12 below it a `layout.dividerWidth` rule in **`shell.sectionRule`** (`#EAEAEA`, the
reference's — not `divider`), then `bodyTop` 4, then the
content inset `layout.section.inset` 8 at both sides: 41 px from the head's
top to the first field. A `Section` is `<section aria-labelledby>` — a `region`
named by its own heading; `headingLevel={3}` is `typography.h3`.
**Who owns the 32 px between sections: the area, not the section.** A `Section`
has **no outer margin at all** — none above, none below, none after its last
field — so nothing doubles (grid and flex items do not collapse margins) and no
trailing 32 sits on top of the panel's `columns.bottom` 24. The
`layout.section.gap` 32 between consecutive sections is the **`ColumnLayout`
area's**: each area is a flex column with that gap (below), and the same value
separates the areas when they stack. A host never writes a gap number for
sections; sections are placed in a `ColumnLayout` area (the showcase page is a
`ColumnLayout` with `main` alone), and a `Section` stacked by anything else is not
a supported arrangement.
**States.** None of its own. **Names.** The title.
**Do / don't.** Don't copy the head's markup (`FormSection` and the copy in
`RelatedWorkSection` go: a heading element of your own is KIT-21); don't draw
the rule with `borderBottom: 1, borderColor: 'divider'` in a feature (a section's
rule is `sectionRule`, not `divider`); don't give
a `Section` a margin, and don't space two sections with `Stack` or `Box` of your
own.

#### `ColumnLayout`

**When.** The body of a card: the texts, the property sections and the related
work, in three, two or one column — decided by the container's own width, not
the window's.
**Built on.** CSS grid and CSS container queries (`container-type:
inline-size`) written in `sx` (`'@container kit-columns (min-width: …)'`).

```ts
interface ColumnLayoutProps {
  main: ReactNode;                       // the texts
  side?: ReactNode;                      // the property sections
  extra?: ReactNode;                     // related work and the kept places
  threeMin?: number; twoMin?: number;    // default layout.columns.threeMin 1040 / twoMin 680
}
```

| Container width | Columns | Tracks and areas |
|---|---|---|
| ≥ `threeMin` 1040 | three | `minmax(0,1fr) 320px 320px`, `column-gap` `columns.gap` 16; areas `main side extra` |
| ≥ `twoMin` 680 | two | `minmax(0,1fr) 320px` and rows `auto 1fr`, so `extra` follows `side` directly while `main` spans both; areas `main side` / `main extra`, `row-gap` `section.gap` 32 between `side` and `extra` |
| below | one | `main`, `side`, `extra` stacked, `row-gap` `section.gap` 32 between |

**Structure: two elements, because an element cannot query itself.** The outer
element is the **container** — `container: kit-columns / inline-size` — and
carries the padding: `columns.top` 16 above, `gutter` 12 at the sides, `bottom` 24
below, `scrollbar-gutter: stable` where it scrolls; it is never the grid. The
**grid is its child**, and the `@container kit-columns (min-width: …)` rules are
written on that child. The thresholds read the container's **content width**,
inside the gutters. Each grid area (`main`, `side`, `extra`) is a flex column,
`gap: layout.section.gap`, `min-width: 0` — that is where two consecutive
sections get their 32 px (§ 5.8, `Section`). A column with no content is not
rendered and takes no track: with `extra` absent the wide layout is `main side`
(two tracks), with `side` and `extra` absent it is `main` alone. **No
`useMediaQuery` is left in a slice** for columns (KIT-17). At 1440 px with the
navigation expanded the container's content width is 1128 px: 456 / 320 / 320.
The first column's floor at three columns is 368 px.
**States.** None. **Names.** None (the sections name themselves).
**Do / don't.** Don't decide the columns from the window; don't nest a
`ColumnLayout` in a column; don't put the container and the grid on one element.

### 5.9 The card — `shared/ui/card` (P5)

The card is a `DialogShell` or a `PanelShell` (§ 5.10) holding, top to bottom, a
`CardHeaderBar`, a `PropertyBand`, an optional notice and the body. The two
parts below read the shell's container (`kit-card`: `container: kit-card /
inline-size`, declared by `DialogShell size="card"` and by `PanelShell` on their
outer Paper or Surface — and by nothing else, § 5.10), so they belong in one;
outside a shell they draw their wide layout. Their narrow rules are
`@container kit-card (max-width: …)` against `layout.card.stackBelow` 720.

#### `CardHeaderBar`

**When.** The head of a card: the type, the identifier, the title, the actions.
**Built on.** `Typography`, `TypeStripe`, `TypeLabel`, `Box`; the title is an
`InlineTextField size="title"` the host passes.

```ts
interface CardHeaderBarProps {
  appearance: string;                    // the type token: the bar's colour, the eyebrow's icon
  typeLabel: string;                     // «Пользовательская история»
  identifier: string;                    // «MSK-US-2»
  headingLevel: 1 | 2 | 3;               // 1 on the page, 2 in the dialog, 3 inside a showcase section
  headingId?: string;                    // the dialog's aria-labelledby target
  title: ReactNode;                      // an InlineTextField size="title", label «Заголовок» hidden
  meta?: ReactNode;                      // row 3, left: the assignee (a PersonField, label hidden), kept places
  actions?: ReactNode;                   // row 3, right, layout.card.actionGap apart: SplitButton, IconActions, MoreActionsMenu
}
```

```
▌ [icon] ПОЛЬЗОВАТЕЛЬСКАЯ ИСТОРИЯ MSK-US-2                                  (4)            row 1, 32
▌ MSK-US-2   Письмо со ссылкой________________________________           (8)            row 2, 32
▌ (person) Анна Смирнова   ·· places kept ··      [Сохранить|▾] [⟳] [↶] [⋮]  (8)         row 3, 32
```

The bar (`TypeStripe`, `layout.card.typeBar` 10) stands at the left edge over the
header's height **only** (the band is not coloured). The content starts at
`headerInset` 18, ends `rightInset` 8 from the right. Rows: 4 + 32 + 8 + 32 +
8 + 32 + 8 = **124** (`layout.card`, § 3.6). Row 1 is `TypeLabel eyebrow`
(icon 16, «<Тип> <ID>» as **one text** in capitals by CSS, `primary.dark`) — the
heading: `h1` on the page, `h2` in the dialog; the DOM text and the accessible
name keep the case. Row 2 is the identifier in `typography.cardTitle` 17/24/400
(not a link, no wrap), a 16 px gap, then `title`. Row 3: `meta` fills the left
and shrinks, `actions` sit at the right **`layout.card.actionGap` 11 apart** — the
Save group and each icon button, so the 32 px boxes have a pitch of 43 — the last
box `rightInset` 8 from the edge: the three icon buttons' centres are 24, 67 and
110 from the right edge, as the reference's are (§ 3.6 pins the sum). The
dialog's ⤢ and ✕ are not this bar's; they are drawn over the right end of row 1
(§ 5.10). Below `layout.card.stackBelow` 720 (the shell's width) row 3 wraps:
`meta` on its own line, `actions` under it at the right.
**States.** None of its own; the slots carry theirs (the title's error line
shows under row 2 and moves what is below).
**Names.** The heading: «<Тип> <ID>». While the card loads or fails there is no
header to carry it, so the dialog is named by `DialogShell`'s `label`
(«Рабочий элемент <ID>», the host's text, § 5.10) and the page by `CardSkeleton`'s
`heading` (§ 5.12). The slots are named by their own parts.
**Do / don't.** Don't colour the band; don't turn the capitals into typed
capitals; the dialog's «Развернуть на страницу» and «Закрыть» are the shell's,
not this bar's.

#### `PropertyBand`

**When.** The grey strip under the header: label-left properties in two blocks,
a stamp at the top right and the section tabs at the bottom right.
**Built on.** `Surface variant="band"` and CSS grid.

```ts
interface PropertyBandProps {
  blocks: [ReactNode, ReactNode?];       // each a stack of fields with labelPlacement="start"
  meta?: ReactNode;                      // top right: «Изменён: 05.10.2026 14:30»
  tabs?: ReactNode;                      // bottom right: a BoxedTabs, against the lower edge
}
```

```
┌ PropertyBand — background.default, at least 76 high ───────────────────────────┐
│ Статус   ●   Новая             Область      Платформа            Изменён: …       │
│ ·· Причина ··                  ·· Итерация ··                                    │
│                                                        ┌ Детали ┬ ⟲ ┬ ⚭ 1 ┐       │
└──────────────────────────────────────────────────────────────────────────────────┘
  block A (rows of 32, 4 apart)  block B                  the aside: meta on top, tabs below
```

`background.default`, `bandMinHeight` 76, padding `bandPaddingTop` 6 · `rightInset`
8 · 0 · `headerInset` 18. **Four grid tracks, `column-gap` `bandBlockGap` 32**:

```
grid-template-columns: minmax(bandBlockMin, bandBlockWidth) minmax(bandBlockMin, bandBlockWidth) minmax(0, 1fr) auto
                       └──── block A ────┘                 └──── block B ────┘                 └ free ┘      └ aside ┘
```

the two blocks are **fixed**, `bandBlockWidth` 253 (253 + 32 = the reference's
pitch of 285: block A's label stands 18 and block B's 303 from the card's left
edge, `theme.test.tsx` pins both); the free track takes the rest and is empty; the
aside is `auto` and right-aligned — `meta` at the top (`caption`, `shell.cardLabel`,
6 down), `tabs` at the bottom. The room left is **not** shared out to the blocks:
a `1fr` block would put «Область» about 335 px right of the reference's. The
blocks shrink toward `bandBlockMin` 240 only where the card is too narrow for
253 + 253 and the aside (a card width between 720 and about 790); then `meta` ends
in an ellipsis and the tab strip never shrinks (a strip of more tabs than the
single «Детали» is wider — its owner re-checks `stackBelow`). With one block the
second track is not drawn. Rows are `bandRow` 32 high with `bandRowGap` 4 between;
the label column is `bandLabelWidth` **82** and **every row's value text is 88
from the label's left edge** — the adornment of a field (the state's dot, centred
at label + 64; a place kept's lock) stands in the label column, 19 before the
text, and the field's box takes it in (§ 5.7). **The band keeps its 76 px with its
rows empty**, so a place kept for a property arrives without moving what is
below (74 of content, 2 of slack). Below `stackBelow` 720 the blocks stack in
one column of `bandBlockMin` at least and the aside goes under them, and the
minimum height is dropped.
**States.** None of its own. **Names.** None: its fields name themselves.
**Do / don't.** Keep to two blocks; a field in it is `labelPlacement="start"`;
don't put the tabs in a block.

#### `TypeStripe`

**When.** The coloured bar of a work item surface: the card's header (10 px)
and the board's card (4 px, § 8.4).
**Built on.** A `span` filled from `theme.workTracking.typeAppearance(token).colour`.

```ts
interface TypeStripeProps { appearance: string; size?: 'card' | 'compact' }   // layout.card.typeBar | typeBarCompact
```

`aria-hidden`; colour is never the only carrier — the type's name stands in
the type line or the card's text.

### 5.10 Dialogs and the page panel — `shared/ui/dialog` (P5)

#### `DialogShell`

**When.** Any modal window over a page: the work item card (`size="card"`), a
form in a dialog, a confirmation. A page that hosts the same card uses
`PanelShell` (below).
**Built on.** MUI `Dialog`, `DialogContent`, `DialogActions`.

```ts
type DialogCloseReason = 'escape' | 'backdrop' | 'close';
interface DialogShellProps {
  open: boolean;
  onClose: (reason: DialogCloseReason) => void;
  size: 'confirm' | 'form' | 'card';     // MUI xs | sm | 90 % × 90 % (layout.card.dialogShare)
  title?: string;                        // a plain title: the h2 with «Закрыть» (confirm, form)
  header?: ReactNode;                    // fixed area: a CardHeaderBar (card)
  band?: ReactNode;                      // fixed area: a PropertyBand
  alert?: ReactNode;                     // fixed area between the band and the panel: an InlineNotice
  lead?: ReactNode;                      // a paragraph under the title (confirm, form)
  children: ReactNode;                   // the panel: scrolls
  footer?: ReactNode;                    // the actions (confirm, form); the card has none
  labelledBy?: string;                   // the heading's id when there is no `title` (the card's headingId)
  label?: string;                        // the dialog's name when there is no heading to point at (a card still loading or failed: «Рабочий элемент MSK-US-2») — aria-label
  busy?: boolean;                        // no dismissal, controls inert
  onExpand?: () => void;                 // draws «Развернуть на страницу»
  guard?: { dirty: boolean; title: string; text: ReactNode; discardLabel: string; cancelLabel?: string };
  fullScreenBelow?: 'md' | false;        // default 'md' for the card (1024 px)
  onExited?: () => void;                 // after it has closed: the host restores focus if the opener is gone
}
```

```
┌ Paper 90 % × 90 %, radius shapeRadius, elevation 24, no backdrop ──┐
│ [⤢][✕]  absolute: top 4, ✕ 5 from the right, ⤢ 2 to its left,      │
│         32 × 32 each (first in the DOM)                            │
│ header   ← CardHeaderBar                       fixed               │
│ band     ← PropertyBand                        fixed               │
│ alert    ← InlineNotice (when set)             fixed               │
│ panel    ← children …                          flex 1, min-height 0,│
│                                                overflow-y auto,     │
│                                                scrollbar-gutter    │
│                                                stable               │
└────────────────────────────────────────────────────────────────────┘
```

**The card's Paper** (`size="card"`): MUI `maxWidth={false}`, `width` and
`height` `layout.card.dialogShare × 100` % of the dialog container (which is the
window), **`margin: 0`**, **`maxHeight: none`** (MUI's Paper caps it at
`100% − 64px`, which would break the 90 %), `display: flex; flex-direction:
column`, radius `shapeRadius` (0 when full screen). **This Paper — and the
`PanelShell`'s Surface — declare the container `kit-card`** (`container: kit-card
/ inline-size`) and **nothing else does**: `CardHeaderBar` and `PropertyBand` read
it with `@container kit-card (…)`, which an element cannot do on itself, so the
Paper is their parent and never one of them. `size="confirm"` and `"form"` never
declare it: their Paper's width follows its content (`maxWidth` xs / sm, no fixed
width), and a width fixed by `inline-size` containment cannot follow content — it
would collapse to nothing.

**The backdrop of the card draws nothing.** The reference dims nothing: the
page behind the card keeps its full brightness and only the card's large shadow
separates it. So `size="card"` sets MUI `Dialog`'s backdrop slot to **`invisible`**
(`Backdrop invisible`): the backdrop element is still rendered, so **a click
outside the card still reaches `onClose('backdrop')`** (and the guard), but it
paints no fill; the Paper keeps MUI's own **elevation-24 shadow** (no token, no
`boxShadow` of the kit's own). It is **not `hideBackdrop`** — that removes the
backdrop and the outside click with it — and not the default `rgba(0,0,0,.5)`
(the theme has no `MuiBackdrop` override), which would grey the page to half
brightness. `size="confirm"` and `"form"` keep the default dimming, **and so does a
`ConfirmDialog` opened over the card: a question dims** — the whole window, the
card included. Nothing else about modality changes: the page behind is still
inert, the trap holds and a screen reader's world is the same; the transparent
backdrop is a visual fact only.

**The two icons** (`size="card"`): an `IconAction size="bar"` each — `ThinExpandIcon`
«Развернуть на страницу» (when `onExpand` is given) and `ThinCloseIcon` «Закрыть»,
32 × 32, glyph 18, tooltips — **adjacent**, `layout.card.dialogIconGap` **2** apart
(pitch 34), the ✕ box `dialogIconInset` **5** from the Paper's right edge (the ✕'s
centre 21 from it, the ⤢'s 55), `headerPadTop` 4 from the top, so both stand on row 1's
centre line. They are the first controls in the DOM. (This design's first sketch,
«right 8» with touching boxes, is replaced: it put the ✕'s centre 24 from the edge
and the pair at a pitch of 32 against the reference's 34.) `size="confirm"` and
`"form"` carry the one «Закрыть» in the title's row, placed by their rebuild
(tech-018).

**Behaviour.**
- *Open.* Focus goes to the dialog's own frame, in no field; the trap holds
  Tab inside; a Select's list or a menu open above it takes Escape first (MUI's
  top-modal rule), then the dialog.
- *Close.* «Закрыть», Escape and the backdrop call `onClose` with their reason.
  With a `guard` whose `dirty` is true they first open the confirmation
  (below) and call `onClose` only if the user chooses to discard. **While `busy`
  none of them closes** and the two icons are inert. Focus returns to the
  opener (MUI), or the host puts it elsewhere in `onExited`.
- *Expand.* `onExpand` is a plain callback: the host navigates to the page
  carrying its edits. It is never guarded.
- *Guard.* A `ConfirmDialog destructive` over the card: the title and text are
  the host's (the item's identifier belongs in the text), the primary is
  `discardLabel`, the cancel is `cancelLabel` (default «Продолжить
  редактирование») and holds the initial focus, so Enter never discards;
  Escape is cancel. It is the kit's only unsaved-changes guard — **leaving the
  page by navigation is not guarded** (mod-015 D-22 stands).
- *Stacking.* A `ConfirmDialog` or a menu opens above the card; a notice
  (`NoticeProvider`) is above all of them.
- *Full screen.* Below `md` (1024 px) the card is full screen, square, no
  margin; the fixed areas and the panel behave as before; the card's own width
  then decides its inner layout (§ 5.8, § 5.9).
- `size="confirm"` and `"form"` are `Dialog` at `maxWidth` xs (444) and sm, with
  `title`, `lead`, the panel as `DialogContent` and `footer` as `DialogActions`
  (their rebuilt `ConfirmDialog`/`FormDialog`, tech-018).

**States.** Closed, open, busy, guard open, full screen, with and without
`alert`. A loading card keeps its frame and the two icons (the host passes the
skeleton as `children`); a not-found or failed card keeps them too.
**Names.** The dialog is `role="dialog"` (MUI's default, for every size and for
`ConfirmDialog`: K-14), modal, `aria-labelledby` the heading (`title`'s own, or
`labelledBy`) or, when there is no heading — the card while it loads or has failed,
whose skeleton and error frame carry none — `aria-label` = `label`. Exactly one of
`title`, `labelledBy`, `label` is given (a dev-mode warning and a test pin it). The
two icons are «Развернуть на страницу» and «Закрыть», each with its tooltip.
**Do / don't.** Don't write `Dialog` in a feature (KIT-9); don't add a second
close button; don't catch Escape yourself; don't close a busy dialog; don't name
a loading card's dialog with a hidden heading of your own (`label` is the way).

#### `PanelShell`

**When.** The same card on a page: the shell's trail above, then one white
panel on the application background.
**Built on.** `Surface variant="card"`.

```ts
interface PanelShellProps {
  trail?: ReactNode;                     // usePageHead(...).trail — the trail above the panel, when the shell wants it there
  header?: ReactNode; band?: ReactNode; alert?: ReactNode; children: ReactNode;
}
```

The same slots as the dialog, in a `Surface` (`theme.cardShadow`,
`shapeRadius`) that **declares the container `kit-card`** (`container: kit-card /
inline-size`; a block-level panel takes its width from the page, so containment
costs it nothing); **the page scrolls, not the card** — the header, the band and the
tab strip scroll with it, nothing is fixed, and the body has no inner scrollbar.
No «Развернуть на страницу» and no «Закрыть» (the trail leads back). The space
above is the shell's, not the host's: `layout.pageHead.panelTop` 16, then the
`trail` when there is one with `trailGap` 8 below it, then the panel.

#### `ConfirmDialog` and `FormDialog` (exist) (P5)

`ConfirmDialog` gains **`cancelLabel?: string`** (default «Отмена»), so the
card's confirmations read «Продолжить редактирование». It keeps `destructive`
(the primary is `danger`), `cancelAutoFocus`, `busy`, `error`, and Escape =
cancel. Nothing else changes now — **including its role: it stays `dialog`, not
`alertdialog`** (K-14; feat-029 § 8 reads `alertdialog` and is corrected in § 13);
both are rebuilt on `DialogShell` in tech-018 (their close glyph becomes
`ThinCloseIcon` and the busy spinner `ActionButton`'s).

### 5.11 Tables — `shared/ui/table` (P7)

**When.** Any table. The reference look is the only one: a white card with a soft
glow, a sticky white 40 px header of small grey labels, 13 px rows with no
lines, a light hover, a framed selection.
**Built on.** MUI `Table`, `TableHead`, `TableBody`, `TableRow`, `TableCell`,
`TableSortLabel`, `TablePagination` — all of them only here.

```
┌ TableCard: a Surface card that scrolls inside itself ───────────────────────┐
│ HeadCell │ HeadCell          │ HeadCell      │ HeadCell     ← white, 40, sticky │
│──────────────────────────────────────────────────────────────  ← 1 px rule  │
│ ⌄ [icon] Title ⋮             │ ● Новая       │ 20           ← BodyRow, 36     │
│ ⌄ [icon] Title               │ ● В работе    │              ← hover #F8F8F8   │
│ ▌[icon] Title ⋮              │ ● Новая       │ 20           ← selected         │
└──────────────────────────────────────────────────────────────────────────────┘
   no row rules · selected: #E5F0FA with a 2 px accent frame on the left, 1 px elsewhere
```

| Part | Props | Look and behaviour |
|---|---|---|
| `TableCard` | `aria-label`, `role?: 'table' \| 'treegrid'` (default `'table'`), `head` (the `HeadCell`s of the one header row), `children` (the `BodyRow`s), `columns?: { width?: number \| string }[]`, `minWidth?: number`, `bodyProps?`, `foot?`, `maxHeightOffset?: number`, `minHeight?`, `ref` | **the part that owns the `<table>`** — no feature writes `Table`, `TableHead` or `TableBody` (KIT-8). A `Surface card` and the scroll container (`overflow: auto`, `position: relative` — hidden descriptions are absolute and must not stretch the page; the `ref` is this element) holding the `Table` (`aria-label`, `role`), its `colgroup` when `columns` is given (then `table-layout: fixed`, `width: 100%`, `min-width` = `minWidth`: the backlog's fixed columns), a `thead` with the one header row (`head`) and a `tbody` (`children`; `bodyProps` are its attributes and handlers — the backlog's drag events), then `foot` (`TablePager`) below the table. `maxHeight: calc(100dvh − maxHeightOffset)` when given and never under `layout.table.minHeight` 240; `scroll-padding-top` = `table.headerHeight` so a focused row clears the sticky header. A sticky header needs `maxHeightOffset`. **`role="treegrid"`** gives the table, its header row, `HeadCell`s and `Cell`s the explicit roles `treegrid`, `row`, `columnheader`, `gridcell` (a context the parts read), as `TreeRegistry` has them today |
| `DataTable<R>` | § below | the plain registry from the parts |
| `HeadCell` | `label?`, `children?`, `kind?: 'cell' \| 'hierarchy' \| 'title'`, `width?`, `minWidth?`, `align?`, `separator?`, `clip?`, `sort?: { direction: 'asc' \| 'desc' \| false; onSort(): void }`, `aria-label?`, and the other attributes of a `th` | `th scope="col"`, `background.paper`, `top: 0`, `table.headerHeight` 40, a 1 px `divider` rule under the row; the label `caption` 12/16/400 `text.secondary`; `kind="hierarchy"` pads the cell 16 px at the left (the two square buttons), `kind="title"` insets the label by `table.titleLabelInset` 36 so it stands over the tree's icons; `aria-label` on the `th` replaces the content-derived name («Иерархия» — the two buttons inside would otherwise be part of it); `separator` draws a 1 px × `table.separatorHeight` 20 `divider` line at the right edge, centred (**its width is the string `${dividerWidth}px`**, because `sx` reads a bare 1 as 100 % — feat-027 V-1); `clip` is an ellipsis with the full label in `title`; `sort` adds a `TableSortLabel`, `aria-sort` follows, first activation ascending; `children` takes custom content (the hierarchy header's two buttons) |
| `BodyRow` | `selected?`, `onSelect?`, `cue?: 'dropAllowed' \| 'dropRefused' \| 'dropLineTop' \| 'dropLineBottom'`, `dragging?`, `children`, **and every other attribute of a `tr`** | `tr`, `density.registryRowHeight` 36, no bottom border; states in the next table; `RowControl`s inside it are revealed by this row's hover, focus-within and selection. **Pass-through:** `role`, `tabIndex`, `id`, `aria-*` (`aria-level`, `aria-setsize`, `aria-posinset`, `aria-expanded`, `aria-selected`), `data-*`, `onClick`, `onKeyDown`, `onFocus`, the drag handlers and the `ref` reach the `tr`; the part sets none of them itself |
| `Cell` | `padding?: 'cell' \| 'tree' \| 'hierarchy'`, `clip?`, `align?`, **and every other attribute of a `td`** | `td`, `padding 0 8px` (`tree`: 0 4px; `hierarchy`: 0 8px 0 12px, `position: relative` for the drag grip), `typography.tableCell` 13/20, `text.primary`, centred vertically; `clip` an ellipsis with `title`; `role`, `tabIndex`, `aria-*`, `data-*` and the handlers pass through (a `gridcell` role is implied by `TableCard role="treegrid"`) |
| `TreeCell` | `depth`, `expandable`, `expanded?`, `onToggle?`, `toggleLabel?`, `toggleTabIndex?: number`, `icon?`, `trailing?`, `children`, **and every other attribute of a `td`** | the title cell: `depth × treeIndent` 16, a chevron slot of `iconAction.chevron.box` 24 (an `IconAction size="chevron"` of `ThinChevronRightIcon`/`ThinChevronDownIcon`, `aria-expanded`, no tooltip, `tabIndex` = `toggleTabIndex` — a treegrid passes `-1`: its row is the stop; a leaf keeps an empty `aria-hidden` box so icons of one depth line up), 4 px, the `icon` (a compact `TypeMark`), 4 px, the title (`TruncatedText`), and a 24 px slot at the right edge for `trailing` (reserved even when empty, so a long title never runs under a control) |
| `RowControl` | `IconAction`'s props with `size="row"`, plus `focus?: 'tab' \| 'roving'` (default `'tab'`) | 24 px box, 16 px glyph; `opacity: 0` at rest — **not** `visibility` or `display`, so it stays focusable — and 1 on this row's hover, focus-within (the focused row itself counts), selection, or its own `aria-expanded="true"`; on `hover: none` only on the selected row. **`focus="tab"`: always in the Tab order** (`tabIndex` untouched). **`focus="roving"`: `tabIndex={-1}`** unless the host passes one — the host keeps the roving tabindex (the current row's control gets `0`), owns the arrow keys and the focus (`ref`, `onKeyDown`), and the part does nothing about focus; the reveal rule is the same |
| `TablePager` | `page`, `rowsPerPage`, `count`, `onPageChange` | MUI `TablePagination` with `rowsPerPageOptions={[]}` in the card's foot (a `divider` rule above), «1–10 из 25» from `ruRU` |
| `TableSkeleton` | `headers: string[]`, `rows?: number` (default 5) | the **real header labels** and skeleton rows in the same card; `role="status"` «Загрузка данных», the rows `aria-hidden` |

**Row states** (`BodyRow`; the behaviours — drag, selection, a write in flight —
are the slice's, the look is the kit's):

| State | Look |
|---|---|
| Rest | `background.paper`; row controls transparent |
| Hover | `background.default` (`#F8F8F8`); controls shown |
| Keyboard focus | the 2 px ring on the focused control; the row as on hover |
| Selected | `shell.rowSelected` and a `primary.main` frame drawn as inset shadows: `layout.frame.thick` 2 on the first cell's left, `thin` 1 at the top and bottom of every cell and at the last cell's right; the compact type icon `primary.dark`; controls shown. The part never sets `aria-selected` (invalid on a plain table row); a treegrid host passes it through, where it is valid |
| Dragging (source) | the row's content at `layout.table.draggedOpacity` 0.6 |
| Drop allowed | a `frame.thick` `primary.main` frame, background `action.selected` |
| Drop refused | a `frame.thick` dashed `error.main` frame |
| Drop line | a 2 px `primary.main` line above or below the row |
| Write in flight | controls `inactive` (`aria-disabled`), none draggable |

**`DataTable<R>`** is for the plain registry:

```ts
interface DataColumn<R> {
  key: string; header: string;
  width?: number | string; minWidth?: number; align?: 'left' | 'right';
  separator?: boolean; clip?: boolean;
  sort?: { direction: 'asc' | 'desc' | false; onSort(): void };
  render: (row: R) => ReactNode;
}
interface DataTableProps<R> {
  'aria-label': string;
  columns: DataColumn<R>[]; rows: R[]; rowKey: (row: R) => string;
  selectedKey?: string | null; onSelect?: (key: string) => void;
  maxHeightOffset?: number; minHeight?: number;
  loading?: boolean;                     // no rows yet: TableSkeleton with the real headers
  error?: string; onRetry?: () => void;  // a read error
  empty?: StateFrameProps;               // the frame that replaces the table when there are no rows
  pager?: TablePagerProps;
}
```

| State | What the user sees |
|---|---|
| Loading, no rows | `TableSkeleton` with the real column labels, in the card |
| Empty | `empty`'s `StateFrame` **replaces the card**; the page's filter bar and head stay (mod-015 § 7.3) |
| Read error, no rows | `ErrorBanner` (the server's text, «Повторить») replaces the card |
| Failed re-read, rows shown | the table stays (rows, selection, scroll); the `ErrorBanner` stands above the card; a successful retry removes it |
| Permissions | a page decides before it renders the table (the shared `AccessDenied` frame) |

**Names.** The table: `aria-label` (e.g. «Бэклог», «Реестр языков»); headers
`columnheader`; a sortable header exposes `aria-sort`; a row's controls name their
row («Добавить: Фича (MSK-EPIC-1)», «Ещё действия: MSK-EPIC-1»); a tree's chevron
«Развернуть MSK-EPIC-1» / «Свернуть MSK-EPIC-1» (`toggleLabel`). **Keyboard:** a
plain table with Tab stops, no arrow-key row navigation (feat-001 D-9). **The
treegrid model of `TreeRegistry` is kept as it is** (WAI-ARIA treegrid: rows with
`role="row"`, a roving `tabIndex`, `aria-level` / `aria-setsize` / `aria-posinset` /
`aria-expanded` / `aria-selected`, a chevron out of the Tab order, the actions
button on the roving stop, the arrow keys) and tech-017 moves it onto the parts **through
their pass-through**: `TableCard role="treegrid"`, `BodyRow` with its row
attributes, handlers and `ref`, `TreeCell toggleTabIndex={-1}`, `RowControl
focus="roving"` with the host's `tabIndex`. The parts add no keyboard behaviour of
their own, so tech-017's promise — TreeRegistry draws with `TreeCell` and `RowControl`
— does not break the keyboard model; its `TreeRegistry.*.test` suites are the
safety net and stay green unchanged.
**Do / don't.** Don't write `Table*` or `TableContainer` in a feature (KIT-8);
don't draw row rules or a grey header; don't set `maxHeightOffset` for a table
that does not scroll inside itself; don't use `aria-selected` on a plain row.

### 5.12 Notices, empty, error and loading states — `shared/ui/feedback` (P6)

#### `NoticeProvider` and `useNotice`

**When.** A short result message: «Изменения сохранены.», «Рабочий элемент
MSK-US-24 создан.».
**Built on.** MUI `Snackbar` (and `Alert` for the two coloured severities).

```ts
function useNotice(): { show: (text: string, severity?: 'plain' | 'success' | 'error') => void };
// <NoticeProvider> is mounted once in AppLayout; renderWithProviders mounts it in tests.
```

Bottom left, `theme.noticeDurationMs` 6000, **clickaway ignored**, one at a time —
a newer notice replaces the shown one, and the same text again is announced
again (keyed by a serial). `plain` is the Snackbar's own message; `success` and
`error` are an `Alert` of that severity. The notice is `role="alert"`. It
survives a route change (the page that raised it may be gone). The shell's
fallback notice (the switcher's info `Alert` with a close) is an `InlineNotice`,
not a notice.
**States.** Hidden, shown, replaced. **Names.** Its text.
**Do / don't.** Don't write `Snackbar` or an `autoHideDuration` in a feature
(KIT-4); don't put an action or a link in a notice (use `InlineNotice`); don't
raise a notice for an error that belongs at its field.

#### `InlineNotice`

**When.** An error, a warning or a note that stays in the layout: a refusal that
lands on no field, a load error with «Повторить», a read-only note, a version
conflict.
**Built on.** MUI `Alert` (standard variant), `AlertTitle`.

```ts
interface InlineNoticeProps {
  severity: 'error' | 'warning' | 'info';
  title?: string;
  children: ReactNode;                   // the text
  action?: { label: string; onClick: () => void; ref?: Ref<HTMLButtonElement> };  // «Повторить»
}
```

**It owns no margin:** the container (a `Stack` gap, a section, the shell's
fixed `alert` area) spaces it. `error` is `role="alert"`; `warning` and `info`
are `role="status"`. The action is an `ActionButton tone="subtle"
size="compact"`. `ErrorBanner` (exists) becomes `InlineNotice severity="error"`
plus `apiErrorText` in tech-017; until then it stays. A neutral grey note panel (the
reference's side-section text with links) is **not built yet** (§ 8.4).
**Names.** The title and the text. **Don't** write `Alert` in a feature (KIT-5);
don't stack margins around it.

#### `StateFrame`, `AccessDenied`, `NotFound` (exist) (P6)

The mod-015 canon (§ 7) is unchanged: an icon, a heading, a hint and at most
one action, centred in the area it replaces; `h1` and the tab title when it
replaces the page, otherwise `h2`. Additive for the card:

| Addition | Meaning |
|---|---|
| `StateFrame compact?: boolean` | for a frame inside a dialog or a panel: `pt 2`, `pb 2`, an `h2`, **no tab title** (it implies not `fullPage`) |
| `NotFound({ level, compact?, hint?, action?, headingRef?, keepNavigation? })` | the dialog's not-found says «Проверьте адрес или закройте окно.» with the action «Закрыть» (`hint`, `action: { label; onClick }`, both the host's) |

**`NotFound` with `compact` forwards it to `StateFrame` and leaves the page
alone.** Today `NotFound` always renders `fullPage` — its heading is the page's `h1`
and the browser tab takes it — and, unless `keepNavigation`, registers
not-found with the shell (`usePageChrome().registerNotFound()`), which marks no
navigation entry current and hides the settings navigation. Inside a card dialog
over «Бэклоги» that would rename the backlog's tab and take its shell its
marker, against feat-029 § 4.1 («the browser tab keeps the host page's title»).
So with `compact`: **not `fullPage`; an `h2`; no `useDocumentTitle`; no
`registerNotFound`** (compact implies `keepNavigation`; the chrome is the host
page's). Without `compact` nothing changes: `fullPage`, `h1`, the tab, the
registration. `AccessDenied` is unchanged. A component test renders
`NotFound compact` inside a `DialogShell` in the shell (`renderShell`) and asserts
the tab title, the navigation's current entry and the settings navigation are the
same as before it rendered, and that its heading is an `h2`.

The frame's outlined accent-icon action is legacy; the kit look (a grey or a
blue `ActionButton`) arrives with tech-017, together with folding the crash screen
(`ErrorBoundary`) in as `kind="crash"`.

#### Skeletons

**When.** Loading. The canon is a skeleton in the shape of the loaded content,
never a page spinner (`LoadingBox` is retired in tech-020).
**Built on.** MUI `Skeleton variant="rounded"` inside a `role="status"
aria-label="Загрузка данных"` wrapper — **one `status` per region**, the shapes
`aria-hidden`. Sizes derive from the component each mirrors (`layout.field`,
`layout.card`, `layout.table`), not from tokens of their own.

| Component | Mirrors |
|---|---|
| `TableSkeleton` (§ 5.11) | the table's real header and five rows |
| `FieldsSkeleton` `{ count, labelPlacement? }` | `count` fields of 48 px (label 16 + value 28) |
| `CardSkeleton` `{ heading? }` | the card: the header (a 220 × 16 line, a 60 % × 24 title, a circle and a 160 × 16 line, a 112 × 32 block), the band (two label-and-value pairs, a 160 × 16 meta line), the body in three generic columns (a 24 high head and six lines; a head and three 46 high fields; a head and two fields) — the type is not known yet (feat-029 § 6). `heading?: { text: string; level: 1 \| 2 }` adds a visually hidden heading of that text and level: the **page** passes `{ text: 'Рабочий элемент MSK-US-2', level: 1 }`, so it has its `h1` while loading; the **dialog** passes none and is named by `DialogShell`'s `label` (§ 5.10) |

`RegistrySkeleton` and `FormSkeleton` stay until tech-017 and tech-018 rebuild them on the
two above. **Names.** «Загрузка данных».

## 6. Accessibility — what holds for the whole kit

The per-component names are in § 5; these are the rules across them.

- **Names the E2E and the tests already select by are kept**: «Заголовок»,
  «Исполнитель», «Статус», «Область», «Описание», each field's own label,
  «Родитель», «Открыть <ID>», the list «Дочерние элементы (n)», «Новый рабочий
  элемент» (a button and the menu it opens), «Иерархия», «Развернуть на один
  уровень», «Ещё действия: <ID>», «Добавить: <Тип> (<ID>)». A kit component
  must produce them; a rebuilt screen's tests do not change their selectors.
- **A control's name is its label**, never its value: a select's combobox is
  named by its label only (§ 5.7). A hidden label is still a label.
- **Keyboard.** The Tab order is the DOM's. Menus: mod-015 § 12.4 (Enter, Space
  or ArrowDown opens on the current entry; arrows, Home/End, typing; Enter
  chooses; Escape closes and returns focus). Tabs: arrows, Home/End, Enter or
  Space selects. A dialog: Escape closes the top-most layer only (a list, a menu,
  a confirmation, then the dialog); Enter in a single-line field submits the
  card's form. A table is plain: Tab stops, no arrow navigation (a treegrid
  keeps its own keyboard model, § 5.11). A confirmation is a `dialog` (K-14).
- **Focus** is the theme's 2 px accent ring (offset 2) on a control, the 2 px
  accent frame on a field; both clear 3:1 on every surface (white 4.53, band
  4.26, the grey button's fill and the selected row's tint by the contrast test).
  The hover frame is decoration: 1.5–2:1, and never the only sign of anything.
  A dialog opens with focus on its frame, traps Tab, and gives focus back to
  its opener (or where the host puts it in `onExited`); a refused field takes
  focus with its error.
- **Pointer and touch.** Every target is ≥ 24 × 24 (the smallest `IconAction`);
  a field's hover look (frame, caret, placeholder) also shows when `hover: none`;
  a row's controls are revealed by hover, focus and selection, and with no hover
  a tap selects the row, which shows them (principle 8).
- **Colour is never the only carrier**: a state's dot has its name, a type's icon
  its name (or the eyebrow's text), an error its text, a selected row its frame.
- **Live regions.** A skeleton is one `role="status"` «Загрузка данных» per
  region with its shapes `aria-hidden`; a notice and an error line are
  `role="alert"`; `LiveRegion` is `role="status"` and re-announces a repeat;
  a `warning` or `info` `InlineNotice` is `role="status"`.
- **Reduced motion**: the dialog's fade and the navigation's width change follow
  `prefers-reduced-motion` (the theme's `motion` setting); the kit adds no
  animation.
- **Zoom and width.** Supported down to 640 CSS px (frontend.md); the kit's
  parts do not claim reflow below it (mod-015 D-8). At 640 the card is one
  column and nothing is cut.
- **Contrast pairs the kit adds to `contrast.test.ts`**: a field's and the
  band's label (`shell.cardLabel`, `#707070`) on white (4.95) and on
  `background.default` (4.66), both ≥ 4.5 so that a lighter grey fails; a hint or
  an empty text (`text.secondary`) on the same two; the eyebrow (`primary.dark`)
  on white; the error line and frame (`error.main`) on white; a tag's text on
  `buttonNeutral` and on `rowSelected`; the focus frame on white, the band and the
  grey button; `fieldHover` within its range (§ 3.6). **Recorded as exempt, not
  asserted:** the disabled `SplitButton`'s `splitDisabledText` on
  `splitDisabledFill` (2.63) — a disabled control is outside the floor — and
  `sectionRule`, a decorative line.
- **The transparent backdrop of the card** (§ 5.10) is a visual fact only: the
  page behind stays inert, the trap holds, Escape and the outside click close the
  card as before, and a `ConfirmDialog` over it dims.

## 7. Interface texts the kit owns

Final, Russian. Strings a host knows (a field's label, a notice's message, a
dialog's question) are the host's and are quoted in its own design; the kit has
**no default text for them**. What the kit does own:

| Component | Text |
|---|---|
| `DialogShell` | the two icons «Развернуть на страницу», «Закрыть» (names and tooltips); the guard's default cancel «Продолжить редактирование» |
| `ConfirmDialog` | `cancelLabel` default «Отмена» (unchanged) |
| `MoreActionsMenu` | default name «Ещё действия» |
| `Tag` | the remove button «Удалить тег «<тег>»» |
| `TagList` | the add button «Добавить тег» |
| `PersonLabel` | empty value «Не назначен» (default) |
| skeletons, `TableSkeleton` | «Загрузка данных» |
| `InlineNotice` | a retry button's label is the host's («Повторить» for `ErrorBanner`) |
| `InlinePickerField` | defaults «Загрузка…» (`loadingText`) and «Ничего не найдено» (`noOptionsText`); the host overrides them (the area picker's «Не удалось загрузить области. Попробуйте ещё раз.» is its own) |
| `BoxedTabs` | a count is appended to a tab's name as « (<n>)» |
| `StateFrame`, `AccessDenied`, `NotFound` | unchanged: mod-015 § 7.2 |
| `TablePager` | «1–10 из 25» and the arrows' names from MUI's `ruRU` |
| the showcase | § 10 |

## 8. Migration

### 8.1 Existing parts and where they go

| Existing | Becomes | By |
|---|---|---|
| `features/boards/components/ThinIcons.tsx`, `BacklogIcon.tsx` | `shared/ui/icons` (the boards copies deleted, imports repointed) | tech-015, part 2 |
| `shared/ui/PageHeader.tsx` | the kit head (§ 5.6): `headingLevel`, `usePageHead`, the team look's overflow | tech-015, part 5; the default variant deleted in tech-020 |
| `shared/ui/ConfirmDialog.tsx` | gains `cancelLabel` | tech-015, part 5 |
| `shared/ui/StateFrame.tsx` | gains `compact` and `NotFound({ level, compact, hint, action })` (compact: an `h2`, no tab title, no `registerNotFound`, § 5.12) | tech-015, part 6 |
| `app/AppLayout.tsx` | mounts `NoticeProvider` | tech-015, part 6 |
| `app/navigation/model.ts`, `app/routes.tsx` | the showcase's entry and route | tech-015, part 8 |
| `features/workitems`: `FormSection`, the `RelatedWorkSection` copy, `FieldControl`, `AssigneePicker`, `AreaPicker`, `StatePicker`, `ParentPicker`, `WorkItemFormPage`'s band, grid and `FormLoading`, `Notice`, `HeaderChrome`, `Marks` (what the card touches) | `Section`, the kit fields, `CardHeaderBar`, `PropertyBand`, `ColumnLayout`, `CardSkeleton`, `useNotice`, `usePageHead` (`HeaderChrome` is deleted; the trail goes to `PanelShell`), `TypeLabel`, `StateLabel`, `UserAvatar`, the thin toolbar, Save and person glyphs; the hand-rolled `h1`/`h2` of the page and its sections go (KIT-21) | feat-029 |
| `features/boards`: `BacklogTable`, `BacklogHeadRow`, `BacklogRow`, `BacklogCell`, `backlogRowStyle`, `AddChildRow`, `TeamHead`, `LevelMenu`, `StatusRegion`, `Notice`, `BoardColumns` | the table family, `IconAction`, `RowControl`, `TreeCell`, `InlineTextField`, `MenuButton`, `LiveRegion`, `useNotice`, `TypeStripe`, `MoreActionsMenu`; the `.Mui` selectors and the `1px` literals go | tech-016 |
| `shared/ui`: `RegistrySkeleton`, `SortableHeaderCell`, `TreeRegistry`; the registries of dictionaries, organizations, process, the work item list; `organizations` `RowActionsMenu`, `TruncatedName`; `ErrorBanner`, `StateFrame`'s action, `ErrorBoundary`'s markup | `DataTable` and `TableSkeleton`, `HeadCell sort`, `MoreActionsMenu`, `TruncatedText`, `InlineNotice`, the kit's buttons, `StateFrame kind`; `TreeRegistry` draws with `TableCard role="treegrid"`, `BodyRow`, `TreeCell` and `RowControl focus="roving"` **through their pass-through and keeps its own treegrid keyboard model** (§ 5.11) | tech-017 |
| `shared/ui`: `FilterBar`, `FormPanel`, `FormSkeleton`, `FormDialog`, `ConfirmDialog`, `DialogTitleBar`; every dialog and form of organizations, dictionaries, workitems, boards; `organizations/components/requiredField.ts` | `Surface`, the fields with `frame="always"` and `required`, `FieldsSkeleton`, `DialogShell` | tech-018 |
| `app/shell`: `TopBar`, `ContextSwitcher`, `UserMenu`, `SettingsNavigation`, `MainNavigation`; `shared/ui/PageTrail`; the plain screens `features/home` and `features/auth` (one panel, a button, a notice, a head) | `MenuButton`, the thin chevron, `TruncatedText`, a neutral grey trail, the dark avatar fill, `useNotice`, `Surface`, `ActionButton`, `InlineNotice`, `usePageHead` | tech-019 |
| theme legacy defaults; `LoadingBox`; Russian code comments | deleted / translated | tech-020 |

### 8.2 The follow-up tasks

The human chose the pace on 2026-10-12 (`status.md` D-4, § 15 Q-2: the other
pages move **family by family**), and the orchestrator queued five tasks, in this
order. Their queue lines and this design's first draft called them M-1 … M-5; **the
ids below are what every later document, the guard's `until` (§ 9.3) and the
status files name.** Each is a task of its own because it changes what a person
sees, and the human reviews the diff by looking. All depend on tech-015;
tech-020 on the other four too.

| Task | Scope | Safety net | Look change |
|---|---|---|---|
| **tech-016** `ui-kit-m1-boards` — the backlog and the board on the kit | the boards slice, first, because the kit is already visible there | E2E 26 and the Vitest suite, unchanged selectors | none, side by side: it is the reference's look; the add row's 1 px accent frame at rest becomes the kit's field frame unless the look check says otherwise |
| **tech-017** `ui-kit-m2-registries` — registries and trees | dictionaries, organizations lists, process, the work item list, `TreeRegistry`, `ErrorBanner`, `StateFrame` | each page's tests and E2E 15–22 | yes: grey 600 header → white 400, row rules out, 14 → 13 px, outlined → glow, ⋯ → ⋮, 24 → 16 px tree indent |
| **tech-018** `ui-kit-m3-forms` — forms and dialogs, **the label above the frame** | `FormDialog`, `ConfirmDialog`, `FormPanel`, `FilterBar`, the language form, the organizations' forms | tests and E2E 16–18, 21 | yes: floating-label outlined fields → the kit's framed field with the label above (`frame="always"`, § 15 Q-3); close glyph; footers unchanged |
| **tech-019** `ui-kit-m4-shell` — the shell | top bar, switcher, user menu, trail, settings navigation, notices; the home page and the auth card (`features/home`, `features/auth`) | `AppLayout.*.test.tsx`, E2E 19 | yes: neutral grey trail, dark avatar, thin chevrons |
| **tech-020** `ui-kit-m5-legacy-out` — the legacy out | `PageHeader`'s default variant and `variant`, the theme's `MuiIconButton.sizeSmall`, the grey `MuiTableCell.head` and `MuiTableRow` hover (so a raw MUI table converges), `LoadingBox`, the Russian comments; the guard's allow-list must be empty | the guard test itself | none: nothing is left on the legacy look |

feat-029 consumes the kit and is not one of them.

### 8.3 Theme defaults that change only in tech-020

`MuiIconButton.styleOverrides.sizeSmall` (padding 4, glyph 20),
`MuiTableCell.head` (grey, `subtitle2`) and `sizeSmall` padding, `MuiTableRow`
`height` and hover `action.hover`, `MuiButton` `text`/`outlined` accent-icon
variants (once no `Button` is left outside `ActionButton`), `MuiDialogTitle`
and `MuiDialogActions` padding (when `DialogShell` owns them). Until then they
are the legacy look, and no kit part depends on them (§ 4.2).

### 8.4 What the reference has and the kit does not yet

A known gap, with an owner. Not built because nothing needs it now (K-12).

| Part | Where the reference has it | Owner |
|---|---|---|
| a neutral grey note panel with text and links | the card's side sections (deployment, development) | the feature that shows one; none queued — `InlineNotice` gets a `neutral` severity then |
| the grey «Follow ⎸⚙» split group | the card's actions | none queued (doc-011) |
| a comment composer and a discussion list | the card's body | feat-002 |
| links and attachments lists, history | the card's tabs | feat-015, mod-004 — through `BoxedTabs` |
| hairline history, link, attachment and comments glyphs (`ThinHistoryIcon`, `ThinLinkIcon`, `ThinAttachIcon`, `ThinCommentIcon`) | the card's tab icons and the comments count — the reference draws every glyph as a hairline (§ 5.1) | the owner of each tab or count adds its glyph to `shared/ui/icons` (feat-015, mod-004, feat-002); until then it passes a Material `*Outlined` icon, visibly heavier — a recorded difference, looked at in the visual check (§ 14) |
| the empty date's hidden mask and calendar button in Firefox and Safari | the field's rest look | not claimed: those are Chromium-family pseudo-elements; the native mask stays visible there and nothing moves (§ 5.7). E2E runs Chromium only |
| tag editing | the card's meta row | feat-016 — through `Tag` and `TagList` |
| a switch, checkbox or radio field | the language form's «Активен» | tech-018, when the first form needs it; `Switch`, `Checkbox`, `Radio` and `FormControlLabel` are allowed meanwhile (§ 9.5) |
| the board's tile as a surface | `BoardColumns` cards (`Card elevation 1`) | tech-016 (`Surface variant="tile"`) |
| the dark avatar fill, the grey trail, hairline switcher chevrons | the reference's top bar | tech-019 |
| a page-level «leave with unsaved changes?» guard | — | not planned (mod-015 D-22) |
| reflow below 640 px, a dark theme | — | not claimed |

### 8.5 Documentation

This task adds the guide, its pointers in `frontend.md` and
`src/frontend/AGENTS.md`, and nothing else in the conventions. Owed by the
migrations: `frontend.md` "Shared patterns" (the list is stale) and "One accent"
(secondary commands are grey or plain, not accent-iconed text buttons) when tech-017
lands; mod-015 design § 2.1, § 2.3 and § 2.4 carry a short «changed by feat-027 /
tech-015» note (the orchestrator notes both at gate-3 with the other owed notes).

## 9. The rule and its guard test

### 9.1 The rule

> A screen is built from the kit only. A part the kit does not have is added
> to the kit first — with its tests, its showcase entry and its guide block — and
> only then used. A feature never re-implements what the kit provides, never
> imports a MUI part the kit wraps, and never styles MUI's insides.

It goes into `docs/conventions/frontend.md` (a paragraph under "MUI"),
`src/frontend/AGENTS.md` and `docs/conventions/ui-kit.md`.

### 9.2 What is checked

`src/test/kitGuard.test.ts` (run by `npm test` and `scripts/verify.sh`; **it lands
in plan part 1**, § 9.3) scans the sources with the TypeScript compiler, as
`colourLiterals.test.ts` does, so a comment or a string of copy is never taken
for code.

**Scope.** Non-test files under `src/features/**`, `src/app/**` except
`app/theme/**`, and **`src/shared/**` except `shared/ui/**`** — a wrapper placed
in `shared/lib`, `shared/api` or `shared/config` is a feature's code in disguise
and is checked like one — for KIT-1..17 and KIT-21..24; KIT-18 reads all of
`src/shared/**`. Exempt: `*.test.*`, `src/test/**` and test helpers named
the files of the guard's explicit `TEST_HELPERS` list (a name pattern was a door:
a production `testCaseSteps.tsx` took the exemption — review R1-4); `shared/ui/**`
(the kit is where MUI is wrapped) for every rule but KIT-14, KIT-18 and **KIT-24,
which shuts the doors in the kit too** — a kit file wraps a part and never hands one
on (`export … from` a MUI module, `export { Chip }`, `export default Chip`,
`export const X = Chip` — review R1-3); and two named seams, each in the guard's own `EXEMPT` list with its
reason, for KIT-22 only: `app/AppLayout.tsx` (it *provides* the page chrome) and
the showcase's inert-chrome demo `features/uikit/showcase/demos/PageHeadDemo.tsx`
(§ 10.3).

**What a site is** — the unit the allow-list counts (§ 9.3). For a rule that names
MUI parts or hooks (KIT-1..13, KIT-22): the **import binding** plus **every
other reference to its local name** in the file (a JSX tag, `styled(Name)`,
`component={Name}`, a type query), the closing tag and property names excluded;
the part is recognised by the module's name whatever the local name
(`import B from '@mui/material/Button'`, `import { Button as B } from
'@mui/material'`, `import { default as B } from '@mui/material/Button'`), and an
import of the same name from any other module is not MUI's. So one import and
two `<Button>`s is **3 sites**, and a third `<Button>` is the 4th. For a pattern
rule: each occurrence — a `createSvgIcon` call (KIT-14), a literal or an import
(KIT-15), a `boxShadow` property (KIT-16), a `useMediaQuery` call (KIT-17), an
element, attribute or `styled()` call (KIT-21, KIT-23), an import, export,
`require()` or dynamic `import()` (KIT-24).

| Rule | Forbidden (an import from `@mui/material` or `@mui/material/<Name>`, unless a pattern is named) | Use instead | Baseline at `bd3bb22`: files / sites |
|---|---|---|---|
| KIT-1 | `TextField`, `Autocomplete`, `Select`, `Input`, `InputBase`, `OutlinedInput`, `FilledInput`, `NativeSelect`, `TextareaAutosize`, `InputAdornment`, `InputLabel`, `FormLabel`, `FormHelperText`, `FormControl` | the fields (§ 5.7) | 14 / 59 — TextField 14 files, Autocomplete 4, InputAdornment 2, InputBase 1, FormHelperText 1 |
| KIT-2 | `Button`, `ButtonGroup`, `IconButton`, `ButtonBase`, `ToggleButton`, `ToggleButtonGroup`, `Fab` | `ActionButton`, `SplitButton`, `IconAction` | 20 / 54 — Button 12, IconButton 9 |
| KIT-3 | `Menu`, `MenuItem`, `MenuList`, `Popover`, `Popper` | `MenuButton`, `MoreActionsMenu` | 14 / 55 — MenuItem 14, Menu 8 |
| KIT-4 | `Snackbar`, `SnackbarContent` | `useNotice` | 5 / 10 |
| KIT-5 | `Alert`, `AlertTitle` | `InlineNotice`, `ErrorBanner` | 9 / 22 |
| KIT-6 | `Chip` | `Tag` | 6 / 12 |
| KIT-7 | `Paper`, `Card`, `CardContent`, `CardHeader`, `CardActions`, `CardActionArea`, `CardMedia` | `Surface`, `TableCard`, `FormPanel` | 12 / 28 — Paper 11, Card 2, CardContent 1 |
| KIT-8 | `Table`, `TableContainer`, `TableHead`, `TableBody`, `TableRow`, `TableCell`, `TableFooter`, `TableSortLabel`, `TablePagination`, `Pagination`, `PaginationItem` | the table family | 14 / 164 — TableRow 11, TableCell 11, Table 9, TableHead 9, TableBody 9, TableContainer 7, TablePagination 2 |
| KIT-9 | `Dialog`, `DialogTitle`, `DialogContent`, `DialogContentText`, `DialogActions`, `Modal` | `DialogShell`, `ConfirmDialog`, `FormDialog` | 1 / 3 — `features/dictionaries/components/DefaultLanguageDialog.tsx` (`DialogContentText`) |
| KIT-10 | `Skeleton`, `CircularProgress`, `LinearProgress` | the skeletons, `ActionButton busy` | 10 / 26 — Skeleton 7, CircularProgress 4 |
| KIT-11 | `Avatar`, `AvatarGroup` | `UserAvatar` | 2 / 4 |
| KIT-12 | `Tabs`, `Tab` | `PageHeader views`, `BoxedTabs` | 0 |
| KIT-13 | `Breadcrumbs` | `PageTrail` | 0 |
| KIT-14 | pattern: a `createSvgIcon` call outside `shared/ui/icons` and `app/theme` | `shared/ui/icons` | 2 / 4 (the boards' icons; 0 after part 2) |
| KIT-15 | pattern: **styling MUI's insides** — a string or template literal containing `.Mui` + a capital (`'& .MuiSvgIcon-root'`) **or `Mui-` + a letter** (`'&.Mui-focusVisible'`, `.Mui-disabled`); an import of any name ending `Classes` (`svgIconClasses`, `outlinedInputClasses`, `generateUtilityClasses`) from `@mui/material` or `@mui/material/<Name>` — `` `& .${svgIconClasses.root}` `` is the same selector by another road | the kit's own sizes and states | 8 / 11 (all `.Mui` + capital; no `Mui-` and no `*Classes` today) |
| KIT-16 | pattern: a `boxShadow` (or `'box-shadow'`) object property or JSX attribute whose value is not `none` / `0`. **`elevation` is not checked**: a Paper or Card is already KIT-7, and `<AppBar elevation={0}>` (the shell's top bar) is allowed (§ 9.5) and could never leave the list | `Surface` | 2 / 5 — `BacklogTable.tsx` 1, `backlogRowStyle.ts` 4 |
| KIT-17 | pattern (`features/**` only): `useMediaQuery` | `ColumnLayout`, container queries inside the kit | 1 / 1 (`WorkItemFormPage`, feat-029) |
| KIT-18 | `shared/**` importing `features/**` or `app/**` | the layer rule (frontend.md "Slice rules") | 0 |
| KIT-19 | every PascalCase component export of `shared/ui/index.ts` — a function, a class, or a React `memo` / `forwardRef` / `lazy` object (§ 9.6) — is named by a showcase entry (`covers`) or by `NOT_SHOWN` with a reason | a showcase entry | — |
| KIT-20 | every kit component file `shared/ui/**/X.tsx` has a `X.test.tsx` or `X.<topic>.test.tsx` beside it | a test | 3 files: LoadingBox, DialogTitleBar, WorkTrackingMarks (given tests in the parts that touch them) |
| KIT-21 | pattern: **a heading element made by hand** — an intrinsic `<h1>`–`<h6>`; a `component` prop (or a `styled('h1')`) naming `h1`–`h6`; a `Typography` whose `variant` is `h1`–`h6`, `subtitle1` or `subtitle2` and which has no `component` (MUI renders those as `h1`–`h6`). `Typography variant="h2" component="p"` — a heading *style* on a non-heading element, as in `HomePage`, `SettingsNavigation` and `TopBar` — stays allowed | `PageHeader`, `Section` / `SectionHead`, `StateFrame`, `CardHeaderBar` | 6 / 7 — `AnonymousScreen` (h1), `WorkItemFormPage` (h1), `BoardColumns` (h2), `ProcessTypeCard` (h2), `FormSection` (h2), `RelatedWorkSection` (h2 and h3: 2 sites). h4–h6 are checked too: none today, and no kit part uses them |
| KIT-22 | `usePageChrome`, `PageChromeContext` and `useDocumentTitle`, imported from any module outside the exempt seams (§ 9.2) | `usePageHead` (§ 5.6) | 2 / 6 — `AnonymousScreen` (`useDocumentTitle`), `HeaderChrome` (`usePageChrome`, `useDocumentTitle`); `AppLayout` is exempt |
| KIT-23 | **intrinsic form, button, table and dialog elements**: a JSX `<input>`, `<textarea>`, `<select>`, `<button>`, `<table>` or `<dialog>`; a `component` prop on any element, or a `styled()` call, naming one of them (`<Box component="input">`, `<Typography component="button">`, `styled('button')`) | the fields, `ActionButton` / `IconAction`, the table family, `DialogShell` | 0 (the two `TableCell component="th"` belong to KIT-8) |
| KIT-24 | **a door round KIT-1..13**: a namespace import (`import * as M from '@mui/material'`), a re-export (`export … from`), a `require()` or a dynamic `import()` of `@mui/material` or `@mui/material/<Name>` (`@mui/material/styles` excepted); and an import from any `@mui/*` package other than `@mui/material`, `@mui/icons-material` and `@mui/system` (`@mui/lab`, `@mui/x-*`: a new dependency, a human's call) | the named kit part | 0 |

**The offender column is the designer's independent scan** of `bd3bb22`
(`src/frontend/src`, the test files and helpers excluded): it totals **128
(file, rule) entries**. It is indicative for a reader and **the generator's
cross-check** (§ 9.3): the baseline's totals per rule must equal it, and a
difference is a bug in a rule, explained or fixed before the list is committed.
The authority is the baseline file, not this table. (The first draft's table had
KIT-16 at 3 files where its rule matched 4 — `BacklogTable`, `BoardColumns`,
`backlogRowStyle`, `TopBar` — and KIT-9 at 0 where `DefaultLanguageDialog`
imports `DialogContentText`; both are corrected here, and KIT-16 no longer reads
`elevation`.)

**Widened by review round 1 (R1-3, R1-4), the baseline unchanged** — the scan of the
tree finds no new site, so the list needs no entry:

- *A name that is not one literal.* `component`, `as`, `variant`, `role`, a
  `styled(…)` argument and the first argument of `createElement` / `jsx` are read
  through their literal parts (the branches of a condition, either side of `||` /
  `??`, the right of `&&`, a `const` the expression names in the same file, an
  object spread into the element, and the head of a template that goes on with a
  substitution — `` `h${level}` `` is `h`, a heading). One site per rule per
  attribute, however many branches. A value the file cannot tell (a parameter, a
  call) is not caught; review reads it.
- *Other roads to the same element.* `<Box as="button">`; `createElement('input')`,
  `React.createElement(…)`, `jsx('h2', …)`; `styled.button` and an aliased or default
  `styled` (`import styled from '@emotion/styled'`, `{ styled as s }`); `role="heading"`
  (KIT-21).
- *Aliases are found by what the import brings*, not by the spelling of the call:
  `createSvgIcon` from `@mui/material/utils` under another name (KIT-14), `useMediaQuery`
  from `@mui/system` or `@mui/material/useMediaQuery` (KIT-17), and a namespace import of
  the page chrome (`chrome.usePageChrome()`, KIT-22).
- *Insides and shadows.* An attribute selector on a MUI class
  (`'& [class*="MuiOutlinedInput-root"]'`) is KIT-15; a `box-shadow:` written as CSS text
  (`css` template) that is not `none` / `0` is KIT-16.
- *The test helpers* are an explicit list (`TEST_HELPERS`, 17 files), checked to exist.
- *The kit's own rule, not a KIT rule.* No source — kit, shell or slice — holds a
  relational `:has(` selector in a string or a template (review R1-2): every such rule
  made each role query on the card slower in jsdom. The state goes in an attribute the
  component sets (`FieldFrame`'s `data-open`, `MoreActionsMenu`'s `data-expanded`).

The colour rule stays in `colourLiterals.test.ts`, **strengthened** in part 1
(§ 9.7). Dimensions stay with review and the `theme.test.tsx` pins; the guard
cannot tell a spacing step from a magic number, and does not pretend to.

### 9.3 The allow-list — a ratchet with a frozen baseline

Today's offenders are listed in `src/test/kitGuard.allow.ts`; the guard fails
for any violation **not** in it, **and for any entry that does not match the
code exactly**:

```ts
export const UNTIL = ['tech-016', 'tech-017', 'tech-018', 'tech-019', 'tech-020', 'feat-029'] as const;
export const ALLOWED: readonly {
  file: string;   // under src/, forward slashes: 'features/boards/components/BacklogTable.tsx'
  rule: string;   // 'KIT-8'
  count: number;  // the sites of that rule in that file (§ 9.2) — exactly
  until: (typeof UNTIL)[number];  // the task that removes it: the five queued migrations (§ 8.2) or the card
  reason: string;
}[] = [
  // { file: 'features/boards/components/BacklogTable.tsx', rule: 'KIT-8', count: 6, until: 'tech-016', reason: 'the backlog table predates the kit' },
];
```

Beside it stands the **baseline**, `src/test/kitGuard.baseline.ts`:
`BASELINE_COMMIT = 'bd3bb22'` and `BASELINE: { file; rule; count }[]`, the scan of
that commit and nothing else. The list is the baseline's copy that migrations
lower; the baseline never changes.

The test has five checks besides the scan:

1. **Exact match.** For each (file, rule) the scan finds N sites. With no entry,
   every one is a new violation. An entry with `count` < N fails —
   «N − count new site(s) at lines …» — so **a listed file cannot gain another
   hand-rolled `TextField`, `Menu` or heading unnoticed**. An entry with
   `count` > N fails as *stale*: «lower it to N» (to 0: remove the entry).
2. **Bounded by the baseline.** Every entry's (file, rule) is in `BASELINE` and its
   `count` is at most the baseline's. So **no entry can name a file created after
   the baseline**, and no count can be raised to let a new violation through: the
   message is «not in the baseline — fix the code, not the list».
3. **`until` is checked, not free text.** It is one of `UNTIL` — the five queued
   migrations, **tech-016 … tech-020**, and `feat-029` — so a typo is a type
   error and a new owner is a change to the design, not to the list; and a task
   whose `docs/features/<id>-*/status.md` reads `stage: complete` is *overdue* —
   its entries must already be gone (a task with no folder yet is not complete).
   `reason` is non-empty.
4. **Sorted** by file, then rule, so a diff shows exactly what changed.
5. **Every listed file exists.**

There is no «addition with a reviewer's yes»: the baseline leaves no room for one.
A new violation is fixed with the kit; a gap in the kit is closed in the kit first
(§ 9.1). The list only shrinks, to zero (tech-020 requires it empty).

**How the first list is made (plan part 1), and why it is not made later.**
Generating it at the end — from whatever main holds then — would grandfather
every violation a parallel task merged first, and feat-029 would start (after
part 6) before the guard existed. So:

- **The baseline is cut from the commit, not from the tree:** a scratch worktree
  of `bd3bb22` (`git worktree add --detach <dir> bd3bb22`) and the scan run over
  its `src/frontend/src` — the test takes its root from `KIT_GUARD_ROOT` and, with
  `KIT_GUARD_EMIT=baseline`, prints the `BASELINE` file instead of asserting.
  The scratch worktree is removed afterwards.
- **It is cross-checked** against § 9.2's last column: the totals per rule must
  equal it (128 entries). A difference is a bug in a rule, not in the table.
- **`ALLOWED` starts as the baseline's copy** — `until` and `reason` filled by the
  folder table below — and is then brought to the tree **by lowering only**.
- **The guard lands in part 1** of the kit's branch, rules KIT-1..18 and KIT-21..24
  all armed, so the kit's own later parts and feat-029 on top of the branch are
  checked from their first line; main receives it with the kit's one squash, and
  the migrations that follow (tech-016 …) start under it. (KIT-19 and KIT-20 are
  the coverage test of part 9.) Each later part that moves code lowers the entries
  it touches in the same commit — part 2 empties KIT-14, for one.
- **The baseline is frozen.** A change to it other than deleting lines is a
  review stop. The one exception is the orchestrator's, and it is the merge order
  of `status.md` D-5: **if another task merges to main before the kit's squash**,
  the tree holds legitimate newcomers (a dialog with a `TextField`, a new `Menu`),
  so the baseline is **re-cut from the new main by this same procedure** — a
  scratch worktree of the new main's commit, the scan with `KIT_GUARD_EMIT=baseline`,
  the cross-check of the totals against the old baseline's plus the newcomers'
  sites — `BASELINE_COMMIT` moves to it, `ALLOWED` is regenerated from the new
  baseline and lowered to the tree again (the newcomers' entries get their `until`
  from the folder table below), and **the re-cut is logged in `status.md`**: the old
  and the new commit and the newcomers' entries. The list never absorbs a newcomer
  silently, and a violation that arrives *after* the guard is merged fails
  `verify.sh` like any other.
- A new MUI part gets a new rule when a kit component starts to wrap it; its
  lines are added to the baseline and the list in the same commit, from the scan
  of that commit's parent.

**`until` by folder** — who removes what, so every entry gets its owner by rule,
not by guess:

| Where | `until` |
|---|---|
| `features/boards/**` | `tech-016` |
| `features/workitems`: `WorkItemFormPage`, `FieldControl`, `AssigneePicker`, `AreaPicker`, `StatePicker`, `ParentPicker`, `FormSection`, `RelatedWorkSection`, `HeaderChrome`, `Marks`, `Notice` | `feat-029` |
| `features/workitems`: `WorkItemsPage`, `TypePickerMenu` | `tech-017` |
| `features/workitems/components/CreateWorkItemDialog.tsx` | `tech-018` |
| `features/dictionaries`: `LanguagesRegistryPage` | `tech-017` |
| `features/dictionaries`: `LanguageFormPage`, `DefaultLanguageDialog`, `VersionConflictPanel` | `tech-018` |
| `features/organizations`: the pages, `OrganizationsSection`, `RowActionsMenu`, `RestoreButton`, `FactsPanel` | `tech-017` |
| `features/organizations`: the dialogs (`AreaDialogs`, `CreateProjectDialog`, `DefaultAreaDialog`, `NameDialog`) | `tech-018` |
| `features/process/**` | `tech-017` |
| `app/**`, `features/home`, `features/auth` | `tech-019` |

### 9.4 The scan's own tests

As in `colourLiterals.test.ts`: `it.each` fixtures that it **catches** — one per
way round a rule, so a hole cannot reopen unnoticed — and fixtures that it **lets
through**:

- *Catches.*
  - **Imports:** a default and a named import of each part, with an alias
    (`{ Button as B }`) and as `{ default as B }`, and each name added to a
    rule here (`Input`, `NativeSelect`, `TextareaAutosize`, `InputAdornment`,
    `FormLabel`, `ButtonBase`, `ToggleButton`, `ToggleButtonGroup`, `Fab`,
    `MenuList`, `Popover`, `Popper`, `Pagination`, `Modal`); a file under `shared/lib`
    (the widened scope).
  - **Counts:** one import and two `<TextField>`s is 3 sites; a third `<TextField>`
    in a file whose entry says 3 fails as new; an entry of 4 for 3 sites fails as
    stale; an entry for a file absent from the baseline fails; an entry whose `until`
    is not in `UNTIL` fails.
  - **Doors (KIT-24):** `import * as M from '@mui/material'`,
    `export { Button } from '@mui/material'`, `require('@mui/material')`,
    ``import('@mui/material/Button')``.
  - **MUI's insides (KIT-15):** `'& .MuiSvgIcon-root'`, `'&.Mui-focusVisible'`,
    `` `& .${svgIconClasses.root}` `` with `import { svgIconClasses } from
    '@mui/material'`.
  - **Surfaces and icons:** `boxShadow:` in `sx` and `boxShadow={1}`;
    `createSvgIcon`; `useMediaQuery` in a feature.
  - **Intrinsic elements (KIT-23):** `<input>`, `<textarea>`, `<select>`,
    `<button>`, `<table>`, `<dialog>`, `<Box component="input">`,
    `<Typography component="button">`, `styled('button')`.
  - **Heads and the seam (KIT-21, KIT-22):** `<h1>`, `<h3>`,
    `<Typography component="h2">`, `<Typography variant="h2">` with no
    `component`, `styled('h2')`; an import of `usePageChrome`, of
    `PageChromeContext`, of `useDocumentTitle`.
- *Lets through.* The same words in a comment, in copy, in a JSX text; `Box`,
  `Stack`, `Typography`, `Link`, `Tooltip`, `Switch`; an import from
  `@mui/material/styles` or `@mui/icons-material/…`; `<AppBar elevation={0}>`
  and `<Drawer elevation={16}>`; `<Typography variant="h2" component="p">`;
  `<form>`, `<a>`; `boxShadow: 'none'`; `import { Button } from '../kit'` (not
  MUI's); a closing tag (not a second site); the same MUI import under
  `shared/ui`; `useDocumentTitle` in `app/AppLayout.tsx` and in
  `PageHeadDemo.tsx` (the two named seams).
- *Not vacuous.* The scan must see `features/boards`, `features/workitems`, `app/`
  and `shared/lib`, and the baseline must hold at least one entry of every rule
  that has an offender today.

### 9.5 What is allowed because the kit does not cover it

No rule, no list entry: `Box`, `Stack`, `Typography`, `Link`, `Divider`, `List`,
`ListItem`, `ListItemButton`, `ListItemText`, `ListItemIcon`, `ListSubheader`
(outside menus), `Tooltip`, `Badge`, `Collapse`, `Fade`, `Backdrop`, `Drawer`,
`AppBar` (with or without `elevation`: the shell's top bar is one), `Toolbar`,
`Switch`, `Checkbox`, `Radio`, `FormControlLabel`, `styled`, `alpha`, `useTheme`
and the icons of `@mui/icons-material`. Using one in a layout is normal; using
one to draw a pattern the kit has is a review finding where the guard cannot
tell: a `Stack` of `Typography` that *looks* like a section head but carries no
heading element, or a `Box` with a border that *looks* like a panel. What it
**can** tell it does — a heading element (KIT-21), a page's own registration with
the shell (KIT-22), an intrinsic field, button or table (KIT-23).

### 9.6 The coverage test (KIT-19, KIT-20)

> **Moved to tech-016 (status.md D-6, 2026-10-12):** `kitCoverage.test.ts` is not part
> of tech-015 — it arrives with the table family's showcase section; the registry's
> `covers` is filled in meanwhile, and a reviewer checks KIT-19 and KIT-20 until then.

`src/test/kitCoverage.test.ts` imports `shared/ui/index.ts`, takes every
PascalCase export **that is a component**, and checks each is named in the
`covers` of some entry of the showcase registry
(`features/uikit/showcase/registry.ts`, exported from the slice's `index.ts` as
`showcaseSections`) or in `NOT_SHOWN` (`{ name, reason, until }`, ratcheted like
the allow-list: today the legacy parts that tech-017/tech-018 replace). So a new kit
component cannot land without its showcase entry, and the showcase cannot show a
component the kit does not export. The same file checks KIT-20 by the file
system.

**What counts as a component.** A function or a class — **and a React exotic
object**: a value whose `$$typeof` is `Symbol.for('react.memo')`,
`Symbol.for('react.forward_ref')` or `Symbol.for('react.lazy')`. MUI's
`createSvgIcon` returns `React.memo(React.forwardRef(Component))`
(`@mui/material/SvgIcon/createSvgIcon.js:30`), an *object*, so a filter on
`typeof value === 'function'` skips every thin icon, `BacklogIcon` and any kit
component wrapped in `memo` or `forwardRef` — and coverage would pass without
checking them. Hooks (`use*`), types, constants and plain objects (a context,
a token) are not components. The test pins both sides: **a fixture proves a
`memo(forwardRef(…))` export is caught** until it is covered, a fixture that a
non-component PascalCase export (a context, a config object) is not asked for
coverage, and the non-vacuous check — the scan must see a plain function
(`IconAction`), a `memo(forwardRef)` object (`ThinChevronDownIcon`) and a
`forwardRef` field.

### 9.7 The colour rule (`colourLiterals.test.ts`, strengthened in part 1)

Today the test matches only a string literal that **is** a hex colour
(`^#…$`) or **starts with** `rgb(` / `hsl(`; a colour inside a shorthand passes
(`'1px solid #E0E0E0'`, `` `0 0 0 1px rgba(0,0,0,.1)` ``, `'linear-gradient(#fff,
#000)'`), and so do a named colour (`'white'`) and a default-palette path that
bypasses the tokens (`'grey.300'`, `'common.black'`). The kit counts on this test
for its colour rule (§ 4.2, rule 2), so it is extended **in part 1, with the
tokens** — and, because the scan of `bd3bb22` finds **no offender outside
`app/theme` for any of the new matches** (and no false alarm on the Russian
copy), it needs **no allow-list**.

1. **A colour anywhere in a literal.** A hex colour (3, 4, 6 or 8 digits) or a
   colour function (`rgb`, `rgba`, `hsl`, `hsla`, `hwb`, `lab`, `lch`, `oklab`,
   `oklch`, `color-mix`) *anywhere* in a string literal, a no-substitution
   template, or the head and each span of a template — bounded on the left by the
   start, a space, a comma or `(`, and on the right by the end, a space, a comma,
   `)` or `;`. So the three shorthands above are caught; `'#main-content'`,
   `'#12'` and an address are not. A literal containing a Cyrillic letter is copy
   («Задача #123 создана»): only the whole-literal rule of today applies to it.
2. **A named colour or a default-palette path as the value of a colour
   property.** In an object property or a JSX attribute named `color`, `bgcolor`,
   `backgroundColor`, `background`, `border` and its four sides, each `…Color`,
   `outline`, `outlineColor`, `fill`, `stroke`, `caretColor`,
   `textDecorationColor`, `accentColor`, `boxShadow`: a CSS colour keyword
   (`white`, `black`, `red` … the CSS Color 4 names; `transparent`, `inherit`,
   `currentColor` and `none` are not colours to name) as the whole value or as a
   word of a shorthand (`'1px solid white'`); `grey.N` / `grey.AN` or `common.*`
   (`'grey.300'`, `'common.black'`); and, anywhere outside `app/theme`, the
   property access `palette.grey` / `palette.common` (`theme.palette.grey[300]`).
   The theme's own roles (`'primary.main'`, `'text.secondary'`, `'divider'`,
   `'shell.rowSelected'`) are what the kit uses and pass.
3. **Fixtures** (`it.each`, as today): *catches* `border: '1px solid #E0E0E0'`,
   `` boxShadow: `0 0 0 1px rgba(0,0,0,.1)` ``, `'linear-gradient(#fff, #000)'`,
   `color: 'white'`, `<Box bgcolor="grey.300">`, `borderColor: 'common.black'`,
   `theme.palette.grey[300]`, `fill="black"`; *lets through* `'#main-content'`,
   `'Задача #123 создана'`, a comment, `color: 'primary.main'`,
   `color: 'inherit'`, `background: 'transparent'`, `fill: 'currentColor'`,
   `border: '1px solid'` (the colour from `theme.palette.divider`), and a colour
   word outside a colour property (`{ label: 'white' }`).
4. **Widened by review round 1 (R1-5)**, so that «a named colour, `grey.N` and
   `common.*` all fail» is true as the guide says it: the colour properties also
   include `textShadow`, `backgroundImage`, `borderInline*`, `borderBlock*`,
   `borderImage`, `columnRule`, `textDecoration`, `filter` and `backdropFilter`;
   a `const` that feeds a colour property is read (`const INK = 'black'; color: INK`);
   `!` ends a colour (`'#E0E0E0!important'`); an import of `@mui/material/colors` (MUI's
   default palette by import, in any form) and `import { colors } from '@mui/material'`
   fail; and `palette.grey` / `palette.common` are caught when taken by destructuring
   (`const { grey } = useTheme().palette`, `const { palette: { common } } = theme`).
   The scan of the tree finds no offender, so there is still no allow-list.

## 10. The showcase page

### 10.1 Route, access, navigation

| | |
|---|---|
| Slice | `src/frontend/src/features/uikit` — `pages/UiKitShowcasePage.tsx`, `showcase/registry.ts`, `showcase/demos/*.tsx`, `permissions.ts`, `paths.ts`, `index.ts` |
| Address | `/ui-kit` (`UI_KIT_PATH`), inside `AppLayout`, outside any work context; `app/routes.tsx` registers it |
| Gate | `<RequirePermission any={[uiKitPermissions.showcaseRead]} fallback={<AccessDenied />}>`, as the languages registry does. Anonymous: mod-015's auth card |
| Permission | `uiKitPermissions.showcaseRead` is **one existing instance code** — `dictionaries.languages.write`, held by the realm roles `admin` and `dictionaries-admin` — so no backend, realm or stand change and no stand is reset (decided: § 15 Q-1, D-5; the alternative, a new code, and its cost are there). It is one constant: changing the answer later changes one line |
| Navigation | a new settings section «Интерфейс» with one entry «Витрина компонентов» (`scope: 'instance'`, the same permission, landing `UI_KIT_PATH`), **last** in `navigationModel.settings`, so «Настройки» still lands on the first visible entry as before. Without the code the entry is not shown (mod-015 BR-7) and a direct address answers `AccessDenied` («Доступ ограничен»), no request |
| Trail | the shell's «Настройки / Интерфейс»; the page passes no `ancestors` |
| Data | none: the page makes **no API request**; every demo is fed from local data (§ 10.4) |

### 10.2 Layout

Built from the kit: `PageHeader variant="team"` (title only), then a
`ColumnLayout` — `main` the sections, `side` the contents (sticky, `top` = the
top bar's height + 16).

```
Настройки / Интерфейс
Витрина компонентов
Здесь каждый компонент … Наведите курсор и нажмите Tab … docs/conventions/ui-kit.md.
┌ main ───────────────────────────────────────────────┬ side ─────────────────────┐
│ Section «Цвета»                                      │ Содержание                │
│   usage sentence                                     │  Цвета                    │
│   [demo group: caption + live component]  …         │  Шрифт и размеры текста   │
│ Section «Шрифт и размеры текста» …                   │  …                        │
│ …                                                    │                           │
└──────────────────────────────────────────────────────┴───────────────────────────┘
```

Each section is a `Section` (`h2` = its title): the usage sentence (`body2`,
`text.secondary`), then **demo groups** — a caption (`caption`,
`text.secondary`) over a live component, laid out in a grid
(`repeat(auto-fill, minmax(240px, 1fr))`, gap 16). A states matrix is a group of
demos, one per state, captioned «В покое», «Наведение (наведите курсор)»,
«Фокус (нажмите Tab)», «Нажато», «Отключено», «Неактивно», «Занято», «Ошибка»,
«Только чтение», «Пусто», «Загрузка», «Меню открыто». States that need a pointer
or a key are not faked: the caption says what to do. Dialogs and menus open
from a button, for real. The contents list is a `nav` named «Содержание
витрины»; each entry a link to `#<section id>`. **The heading outline stays
sane:** the page has exactly one `h1`, a section's title is an `h2`, and a demo
that carries a heading of its own is one level lower — the `PageHeader` and
`CardHeaderBar` demos take `headingLevel={3}`, and the card demo's own
`Section`s `headingLevel={3}` too (a heading inside a framed demo is not part of
the page's outline, but must not add an `h1`).

### 10.3 The sections

The order of the page. `Covers` is what KIT-19 checks. Titles and usage
sentences are final.

| # | id | Title (h2) | Usage sentence | Covers | Demo groups |
|---|---|---|---|---|---|
| 1 | `colours` | Цвета | Все цвета панели берутся из темы. Цвет никогда не бывает единственным носителем смысла: рядом всегда текст или значок. | — (tokens) | swatches by group — «Основа», «Акцент и ссылки», «Смысловые», «Типы работ», «Категории состояний» — each with the token's name and its value read from the theme at run time |
| 2 | `typography` | Шрифт и размеры текста | Шрифт Segoe UI, два начертания: обычное и полужирное. | — (tokens) | every `typeScale` entry set in a sample line «Съешьте ещё этих мягких французских булок», captioned with its size / line / weight |
| 3 | `sizes` | Размеры и отступы | Размеры берутся из темы или из шага сетки в 8 пикселей. | — (tokens) | a table of `theme.layout`, `density` and `frame` values: group, key, px |
| 4 | `icons` | Значки | Тонкие значки — для знаков, которые в образце нарисованы линией; остальные — контурные значки Material. | `ThinChevronDownIcon`, `ThinChevronRightIcon`, `ThinAddIcon`, `ThinRemoveIcon`, `ThinCloseIcon`, `ThinExpandIcon`, `ThinMoreVerticalIcon`, `ThinRefreshIcon`, `ThinUndoIcon`, `ThinSaveIcon`, `ThinPersonIcon`, `BacklogIcon`, `IconAction` | the eleven thin glyphs at 24 px with their names; `IconAction` in its four sizes; states: «В покое», «Приглушённо», «Неактивно», «Занято», «Отключено», outlined |
| 5 | `buttons` | Кнопки | На странице и в окне одна синяя кнопка; остальные серые или без заливки. | `ActionButton`, `SplitButton` | the four tones × «В покое», «Отключено», «Занято», «Меню открыто»; a split button: enabled, disabled (the reference's clean look, `splitDisabledFill` / `splitDisabledText`), busy |
| 6 | `menus` | Меню | Меню открывается клавишей Enter, пробелом или стрелкой вниз; Esc закрывает его и возвращает фокус на кнопку. | `MenuButton`, `MoreActionsMenu` | a text button with `selection="page"`; one with `radio`; a «⋮» with a group, a divider, a disabled and a destructive entry |
| 7 | `marks` | Типы, состояния и люди | Тип работы и состояние всегда подписаны текстом; цвет и значок только помогают. | `TypeMark`, `TypeLabel`, `CategoryMarker`, `StateLabel`, `UserAvatar`, `PersonLabel` | the five types, default and compact, selected, eyebrow; the five categories, 8 and 10 px, with a clipped long name; avatars 32 and 24; a person and «Не назначен» |
| 8 | `tags` | Теги | Тег — короткая метка работы; удалять и добавлять теги можно, только когда это разрешено. | `Tag`, `TagList` | the four tones, filled and outlined; a removable tag; a list with «+»; a read-only list |
| 9 | `text` | Длинный текст | Длинный текст обрезается многоточием, а полный текст показывается подсказкой. | `TruncatedText`, `VisuallyHidden`, `LiveRegion`, `InlineEmpty` | a long title in a narrow box, plain and link looks; an empty list «Дочерних элементов нет.» |
| 10 | `page-head` | Заголовок страницы | Название страницы, команды и виды страницы; на странице он один. | `PageHeader` | a head in a framed box (`headingLevel={3}`, an inert chrome, `documentTitle` = this page's), with a primary, a grey secondary, a «⋮» overflow and two views |
| 11 | `tabs` | Вкладки карточки | Вкладки разделов карточки: значок, число и подпись. Одна вкладка — тоже допустимая полоса. | `BoxedTabs`, `BoxedTabPanel` | a single tab; four tabs with icons and counts; the panel linked to its tab |
| 12 | `fields` | Поля ввода | Поле без рамки: подпись сверху, значение как текст, рамка только при наведении и фокусе. | `InlineTextField`, `InlineTextArea`, `InlineNumberField`, `InlineDateField`, `InlineSelectField`, `InlinePickerField`, `PersonField` | § 10.4's field table: every kind, and the states «В покое», «Подсказка (нажмите Tab)», «Ошибка», «Только чтение», «Занято», «Отключено», «Пусто», plus the framed form look, the title size, the band's label-left placement (with and without a label-column adornment) and a text under a head of its own name (`labelPlacement="hidden"`) |
| 13 | `sections` | Разделы и колонки | Раздел — заголовок, линия и содержимое. Колонки перестраиваются по ширине самого блока, а не окна. | `Surface`, `Section`, `SectionHead`, `ColumnLayout` | a `Section` with an `h3` one; a `ColumnLayout` in three fixed-width boxes of 1100, 700 and 500 px |
| 14 | `card` | Карточка | Шапка, серая полоса свойств и тело карточки; на странице они в одной белой панели. | `CardHeaderBar`, `PropertyBand`, `TypeStripe`, `PanelShell` | the `MSK-US-2` card as a `PanelShell`: header (the toolbar 11 apart; the Save group, clean and enabled), band with its fixed blocks, the state field's dot in the label column and a one-tab strip, body in columns with «Описание» as a text under its head; a bug in its colour; the narrow (stacked) form in a 600 px box |
| 15 | `dialogs` | Окна | Окно карточки, подтверждение и окно с формой. Занятое окно закрыть нельзя. | `DialogShell`, `ConfirmDialog`, `FormDialog` | buttons «Открыть окно карточки», «Открыть окно с защитой от потери изменений», «Открыть занятое окно», «Открыть подтверждение», «Открыть окно с формой»; the card's window draws no dimming, the confirmation over it dims |
| 16 | `tables` | Таблицы | Белая карточка с мягкой тенью; заголовок остаётся при прокрутке; выбранная строка в рамке. | `TableCard`, `DataTable`, `HeadCell`, `BodyRow`, `Cell`, `TreeCell`, `RowControl`, `TablePager`, `TableSkeleton` | a tree table (§ 10.4); a sortable registry with a pager; «Загрузка», «Пусто», «Ошибка» |
| 17 | `notices` | Уведомления | Короткое сообщение внизу слева исчезает через 6 секунд; ошибки, которые остаются на месте, показываются в самой странице. | `NoticeProvider`, `InlineNotice`, `ErrorBanner` | buttons «Показать уведомление», «Показать успех», «Показать ошибку»; the three inline severities; an error with «Повторить» |
| 18 | `states` | Пустые, ошибочные и загружаемые состояния | Каждый экран одинаково показывает загрузку, пустоту, ошибку и отсутствие доступа. | `StateFrame`, `AccessDenied`, `NotFound`, `FieldsSkeleton`, `CardSkeleton` | empty, filtered, denied, not found, compact in a box; the field and card skeletons |

`NoticeProvider`'s `useNotice` is shown by the buttons of section 17 and is a
hook, so it is not in `covers`. `NOT_SHOWN` at the start: `SortableHeaderCell`,
`RegistrySkeleton`, `FormSkeleton`, `FilterBar`, `FormPanel`, `TreeRegistry`,
`LoadingBox`, `ErrorBoundary`, `PageTrail` (legacy or shell parts, each with its
`until` and reason). `PageHeader`'s demo needs an inert chrome: the section wraps
it, **in `showcase/demos/PageHeadDemo.tsx` and nowhere else**, in
`PageChromeContext.Provider` with a no-op registration, so the page's own head,
trail and tab title are not touched. That file is one of the guard's two named
seams for KIT-22 (§ 9.2); the page's own head is a plain `PageHeader`.

### 10.4 The demo data

Russian, fixed, local to `features/uikit/showcase/data.ts`; the project key
`MSK`, team «Платформа», the story `MSK-US-2`.

| Data | Content |
|---|---|
| types (appearance → name) | `epic` «Эпик», `feature` «Фича», `story` «Пользовательская история», `bug` «Баг», `task` «Задача» |
| states (category → name) | `Proposed` «Новая», `InProgress` «В работе», `Resolved` «Решена», `Completed` «Завершена», `Removed` «Удалена» |
| people | «Анна Смирнова», «Борис Ли» |
| areas | «Платформа», «Платформа / Вход», «Платформа / Отчёты» |
| the tree table | «Вход и регистрация» (Эпик, Новая, 20) → «Вход через SSO» (Фича, В работе) → «Письмо со ссылкой» (Пользовательская история, Решена) → «Ссылка не открывается» (Баг, Новая) and «Шаблон письма» (Задача, Завершена); columns «Порядок», «Тип», «Заголовок», «Статус», «Трудоёмкость» |
| the registry table | «Код», «Эндоним», «Порядок»: `ru` «Русский» 1, `kk` «Қазақ тілі» 2, `en` «English» 3, 25 rows in the pager for «1–10 из 25» |
| a long title | «Описать end-to-end сценарии использования текущей системы и вынести неизвестные правила отдельным списком вопросов» |

**The fields** — each demo's label is unique **within its section** and none is
a substring of another, so a test scopes by the section's `region` and finds a
field by its name (the card demo of section 14 reuses «Заголовок», «Исполнитель»,
«Статус», «Область», «Описание» and «Приоритет» in its own region):

| Demo | Field and label | Value | Notes |
|---|---|---|---|
| title | `InlineTextField size="title"`, «Заголовок» (hidden) | «Письмо со ссылкой» | the card's title |
| rest | `InlineTextField`, «Тема» | «Письмо со ссылкой» | |
| hint | `InlineNumberField`, «Оценка в пунктах» | 3 | `inputMode="decimal"`, hint «От 0 до 9 999» |
| error | `InlineTextField`, «Срок» | «5 октября» | error «Введите дату полностью.» |
| read-only | `InlineTextField`, «Автор» | «Борис Ли» | `readOnly` |
| busy | `InlineTextField`, «Заметка» | «Короткая заметка» | `busy` |
| disabled | `InlineTextField`, «Код» | «MSK-US-2» | `disabled` |
| empty | `InlineTextField`, «Комментарий» | — | placeholder «Добавьте комментарий» |
| framed | `InlineTextField frame="always"`, «Название команды» | — | `required`, placeholder «Введите название» |
| text area | `InlineTextArea`, «Описание» | «Пользователь получает письмо со ссылкой для входа.» | hint «До 100 000 символов»; a visible `top` label |
| text under its head | `InlineTextArea labelPlacement="hidden"`, «Описание» — in the card demo (section 14), under the `Section` head «Описание» | the same text | the head names it, no second label is drawn; its box starts `textAreaTop` below the head's rule (E2E 28.11) |
| number | `InlineNumberField`, «Трудоёмкость» | 20 | `inputMode="numeric"` |
| date | `InlineDateField`, «Дата начала» | 2026-10-05 | `min` 2000-01-01, `max` 2099-12-31 |
| date, empty | `InlineDateField`, «Целевая дата» | — | hint «Не раньше даты начала»; the mask and the calendar button are hidden at rest (E2E 28.3) |
| select | `InlineSelectField`, «Приоритет» | «2» | options «1»–«4», hint «1 — самый высокий» |
| optional select | `InlineSelectField`, «Риск» | — | `emptyLabel` «Не указан» |
| state | `InlineSelectField labelPlacement="start"`, «Статус» | «Новая» | `startAdornment` a 10 px dot, in the label column (`adornmentPlacement` defaults to `'label'` for `start`): the dot's centre at label + 64, 19 before the text, the field's frame taking it in; options the five states. Next to the card demo's «Область», whose text must start at the same x, 88 from its label (E2E 28.11) |
| picker | `InlinePickerField`, «Область» | «Платформа» | `loading` variant «Загрузка…»; no-options «Ничего не найдено» |
| person | `PersonField`, «Исполнитель» | «Анна Смирнова» | with an avatar |
| unassigned | `PersonField`, «Наблюдатель» | — | `emptyText` «Не назначен» |

### 10.5 The page's own texts

| Where | Text |
|---|---|
| `h1`, browser tab, navigation entry | «Витрина компонентов» (tab: «Витрина компонентов — Mustask»); settings section «Интерфейс» |
| Intro | «Здесь каждый компонент интерфейса показан вживую и во всех состояниях. Наведите курсор и нажмите Tab, чтобы увидеть наведение и фокус. Правила и описание каждого компонента — в docs/conventions/ui-kit.md.» |
| Contents | `h2` «Содержание», `nav` named «Содержание витрины» |
| Section titles and usage | § 10.3 |
| Dialog demos | card: the demo card of section 14 in a `DialogShell size="card"`; guard: title «Закрыть без сохранения?», text «Несохранённые изменения рабочего элемента MSK-US-2 будут потеряны.», «Закрыть без сохранения» / «Продолжить редактирование»; confirm: title «Удалить тег?», text «Тег «Discovery» будет удалён из рабочего элемента MSK-US-2.», «Отмена» / «Удалить»; form: title «Новая команда», fields «Название команды» (required), «Описание», «Отмена» / «Создать» |
| Notice demos | «Изменения сохранены.», «Рабочий элемент MSK-US-24 создан.», «Не удалось сохранить изменения.» |
| Inline notices | error «Не удалось загрузить данные.» with «Повторить»; warning «Рабочий элемент изменён другим участником.»; info «Рабочий элемент доступен только для чтения.» |
| State frames | empty «Бэклог пуст» / «Добавьте рабочий элемент — он встанет в конец бэклога.» / «Новый рабочий элемент»; the canon's others as mod-015 § 7.2 |

### 10.6 States of the page

*Loading:* none — no request. *Empty:* none. *Error:* a demo that throws is
caught by the shell's error boundary like any page. *Permissions:* § 10.1.
*Narrow:* below the container's two-column threshold `ColumnLayout` stacks the
page — the sections, then the contents list; the demo grids reflow; nothing cuts
and the page does not scroll sideways (every section stays reachable by
scrolling, E2E 28.10). The page is for people on a desktop browser; it claims
nothing below 640.

## 11. E2E for the showcase

One file, `e2e/specs/28-ui-kit-showcase.spec.ts` (the next free number after
feat-029's 27, taken from the directory when written). It needs the stand only
to sign in: the page makes no request. Selected by role and name, as every
spec; no `data-testid`.

| # | Scenario |
|---|---|
| 28.1 | The fixture user (full administrator) opens «Настройки» → «Интерфейс» → «Витрина компонентов»; the `h1` and the tab title read «Витрина компонентов»; the contents `nav` lists the 18 section titles of § 10.3 in order, and each link leads to a `region` named by that title |
| 28.2 | A user with `dictionaries.languages.read` only (spec 16's throw-away reader) gets «Доступ ограничен» at `/ui-kit` and has no «Интерфейс» entry; the throw-away user is deleted in a `finally` |
| 28.3 | Fields (in the «Поля ввода» region): for «Тема» and «Оценка в пунктах» the box's bounding size is **equal at rest, hover and focus**; the hover shows a frame, the focus a 2 px accent frame, the hint appears while focused; «Срок» has `aria-invalid="true"` and its error line is its description; «Автор» is `aria-readonly`; «Статус» is a `combobox` named «Статус», opens on Enter, Esc closes it; «Комментарий» (empty) has its `placeholder` attribute «Добавьте комментарий» **at rest, with no hover and no focus**, and the placeholder's computed text colour is transparent at rest and visible on hover and on focus; «Целевая дата» (empty): the computed `opacity` of `::-webkit-datetime-edit` and of `::-webkit-calendar-picker-indicator` is 0 at rest and 1 on hover and on focus, and the box's size does not change; «Дата начала» (filled) shows its text at rest |
| 28.4 | Buttons and menus: a busy button keeps focus and ignores a click; the «⋮» menu opens with Enter, Space and ArrowDown, closes on Escape with focus back on the trigger, and its destructive entry reads in the error colour; the split button's disabled parts read the computed fill `splitDisabledFill` and label colour `splitDisabledText` (`#F0F0F0`, `#959595`), not MUI's, and an enabled `primary` one the accent |
| 28.5 | Tabs: the strip's arrows move focus, Enter selects, the panel is linked to its tab (`aria-labelledby`); **the selected tab's indicator is 2 px thick, as wide as its label (±1) and its foot 4 px above the strip's bottom (±1)** |
| 28.6 | Dialogs: «Открыть окно карточки» opens a `dialog` named «Пользовательская история MSK-US-2» whose header and band stay while the panel scrolls; **its backdrop has a transparent computed `background-color`, the page behind is as bright as before it opened, the window keeps a `box-shadow`, and a click outside still reaches the close path (with the guard and an edit it opens the confirmation); the confirmation over it has the dimmed backdrop**; Esc closes it and focus returns to the button; with the guard and an edit, Esc opens «Закрыть без сохранения?» and a second Esc closes **only** the confirmation; «Открыть занятое окно» ignores Esc and the backdrop; the ✕'s centre is 21 and the ⤢'s 55 from the window's right edge (±1) |
| 28.7 | Columns: the three boxes of section 13 show three, two and one columns (the computed `grid-template-columns` of each); **two consecutive sections of one column are 32 px apart** (the first one's bottom to the second's top, by bounding box), in the two-column box `side` and `extra` are 32 px apart, and the last section's bottom to the panel's bottom is the panel's 24 — no margin doubled, no trailing 32 |
| 28.8 | Notices: «Показать уведомление» shows the text bottom left, a second press replaces it, and it is gone after 6 s |
| 28.9 | Tables: the header is `position: sticky`; a click selects a row (a frame, `rowSelected`); a row's «+» and «⋮» are transparent at rest, visible on hover and on focus, and always in the Tab order |
| 28.10 | At 640 × 800 the page does not scroll sideways and every section is reachable |
| 28.11 | The card demo's band and texts, in CSS px at 1536 × 790 (±1): the value text of «Статус» and of «Область» **start at the same x, 88 from their labels' left edges**; the state dot's right edge stands `adornmentGap` (19) to the left of that x and its centre is 64 from the label's edge, inside the label column; on hover the state field's frame starts `textInset` (6) before the dot; block A's label is `headerInset` (18) and block B's 303 from the card's left edge (the blocks' pitch is 285, not shared out over the room left); the multi-line «Описание» of the fields section starts its text 2 px further in than a single-line value of the same section (`textAreaInset` 8 against `textInset` 6); **the text under its head in the card demo has its textarea's top `bodyTop` + `textAreaTop` (19) below its section's rule, its text 16 from the section's left edge, and no visible label of its own** (the textbox is still named «Описание») |
| 28.12 | The card demo's header, by the reference's measures (±1): the three icon buttons' centres are 24, 67 and 110 from the card's right edge; the type bar is 10 wide and as high as the header (124) and the band is not coloured; the assignee's empty glyph is centred 36 and its text starts 54 from the card's left edge, and an assigned person's avatar has the same centre |

jsdom cannot show a hover, a container query or a sticky header; these scenarios
are what proves them (§ 4.2, rule 9).

## 12. Tests per component

Vitest, Testing Library, the app theme (`src/test/testingLibrary.tsx`), one
`<Name>.test.tsx` beside each component; `renderWithProviders` gains
`NoticeProvider`.

| Part | Test pins |
|---|---|
| 1 tokens, **the guards** | § 3.6 (the reference's relations included: the header's right edge, the dialog's icons, the band's blocks and label column, the tab strip, the new greys' hexes); `contrast.test.ts` new pairs (§ 6); **`colourLiterals.test.ts` strengthened with its catch and let-through fixtures (§ 9.7), green on the tree**; **`kitGuard.test.ts` with its fixtures (§ 9.4), the baseline and the list (§ 9.3), green on the tree** — the five checks, the cross-check against § 9.2's totals |
| 2 icons, actions, menus | each of the twelve icons renders `aria-hidden` with its path; `IconAction`: the four sizes' box and glyph from the tokens, the label is the name and the tooltip, `inactive` is `aria-disabled` and focusable and ignores the click, `busy` swaps the glyph for a spinner, `tabIndex` and the other attributes pass through, no `.MuiSvgIcon-root` size leaks from `sizeSmall`; `ActionButton`: four tones' classes, busy keeps focus and ignores a click, `menu` sets `aria-haspopup` and `aria-expanded`, `to` renders a link; `SplitButton`: group and arrow names, the arrow's name does not contain the label, **the arrow's glyph is `ThinChevronDownIcon`, the disabled parts' computed fill and label are `splitDisabledFill` / `splitDisabledText` (not MUI's grey) for both tones, busy keeps the tone's fill and both parts inert with focus kept**; `MenuButton`: Enter, Space, ArrowDown open on the current entry, arrows and Home/End, Escape returns focus, `radio` and `page` selection attributes, a group's id in each entry's `aria-describedby`, `destructive` colour, `disableRestoreFocus` |
| 3 marks, text | `TypeLabel` sizes and `eyebrow` (the DOM text keeps its case), `CategoryMarker` 8 / 10 and the `Removed` ring, `StateLabel clip`, `UserAvatar` sizes, `PersonLabel` null state, `Tag` tones and the remove button's name, `TagList` «+»; `TruncatedText` title / tooltip / link looks, `LiveRegion` re-announces a repeat, `InlineEmpty` |
| 4 fields | one shared suite run over the seven fields: label is a real `<label for>` and the control's name; `required` draws the asterisk, sets `aria-required` and never `required`; `error` sets `aria-invalid`, the line is the description and replaces the hint; `readOnly` is focusable and refuses input; `busy` is read-only with `aria-busy` and an unchanged text colour; `disabled`; `labelPlacement` hidden keeps the name; no size change across states by the computed box; **the `placeholder` prop is the input's `placeholder` attribute in every state — rest, hover, focus, `frame="always"`, read-only, busy — and its text is transparent at rest in `frame="hover"`** (the E2E specs 25 and 26 read the attribute with no hover); `frame="always"` shows the placeholder; **`labelPlacement="start"` puts the adornment in the label column (`adornmentPlacement` `'label'`, the default) and `"value"` inside the box; the single-line text starts at `textInset`, a text area's at `textAreaInset`; **in the `start` placement every value's text stands `bandLabelWidth` + `textInset` (88) from the label's edge, with and without an adornment; the label-column adornment's right edge is `adornmentGap` (19) before the text, its centre for the 10 px dot 64 from the label's edge, and the box begins `textInset` before it; an `InlineTextArea` with `labelPlacement="hidden"` draws no label element (the textbox is named by its `label`), leaves `textAreaTop` above its box and its block is `textAreaTop` + the box + `gap`; `PersonField`'s empty slot is a `ThinPersonIcon` of `personGlyph` in the `avatarInline` slot and a `UserAvatar` once set**; a single-line value shows `text-overflow: ellipsis` at rest; the hint is a single line with an ellipsis and its full text in the description**; per kind: select's name is the label only, the optional select's `emptyLabel`, the date's min/max and `onIncomplete` and **`data-empty` (true for `''` with no error, false when filled or in error)**, the number keeps its text, the picker's loading, empty and `endAction`, `PersonField`'s `emptyText` |
| 5 card, layout, dialog | `Surface` variants and paddings; `Section` is a region named by its heading, levels 2 and 3, **and has no margin (computed `margin` 0 on every side); its rule is `shell.sectionRule`**; `ColumnLayout` renders only the given columns in the DOM order main, side, extra, **as an outer padded container with the grid as its child (the `container-type` and the grid are never on one element), each area a flex column with `gap` = `layout.section.gap`** (the tracks and the measured 32 px between two sections are E2E 28.7); `CardHeaderBar`'s heading level and the eyebrow's name, the row structure, **`actions` `actionGap` apart**; `PropertyBand` keeps its min height with empty rows, renders `meta` and `tabs`, **has the grid tracks `minmax(bandBlockMin, bandBlockWidth)` twice, `minmax(0, 1fr)` and `auto` with a `bandBlockGap` column gap, and draws no second block track when it is given one block**; `BoxedTabs` names, counts, icon-only tooltip, arrows, panel linkage, **the indicator's computed height 2 (`frame.thick`) and `bottom` 4 (`tabIndicatorOffset`), as wide as its label, and a tab's `min-width` 0**; `DialogShell`: roles and names (`aria-labelledby` the heading, or `aria-label` = `label` when there is none — a loading card), focus on open and return, Escape / backdrop / close reasons, busy refuses all three, the guard's flow (dirty → confirm; cancel keeps; discard calls `onClose`; Enter on the confirmation does not discard), `onExpand`, full screen below `md`, stacking under a `ConfirmDialog` (which is `role="dialog"`), the card Paper's `margin` 0 and `maxHeight` none and the `kit-card` container on it **and not on the `confirm` and `form` sizes**; **the card's backdrop is `invisible` (its computed `background-color` is transparent, not `hideBackdrop`) and a click on it still calls `onClose('backdrop')`; `confirm` and `form` keep the default dimming, and so does a `ConfirmDialog` over the card; the Paper keeps its elevation shadow; the two icons are `dialogIconGap` apart and the ✕ `dialogIconInset` from the edge**; `PanelShell` slots, its `trail` slot and the `kit-card` container on its Surface; `ConfirmDialog.cancelLabel`; `PageHeader.headingLevel`; `usePageHead`: the tab title, one registration per ancestors value, `trail` an element below 1024 px with a non-empty trail and `null` otherwise, inert outside the shell; the team overflow |
| 6 feedback | `useNotice`: one at a time, a replacement, the same text again re-announced, 6 s, clickaway ignored, the three severities; `InlineNotice` roles per severity and the action's `ref`; `StateFrame compact` (an `h2`, no tab title); `NotFound` overrides (`hint`, `action`); **`NotFound compact` rendered inside a `DialogShell` in the shell (`renderShell`): the browser tab's title and the navigation's current entry and the settings navigation are the same as before it rendered, `registerNotFound` is never called, and its heading is an `h2`; without `compact` it is still the `h1` and registers**; the skeletons: one `status`, shapes `aria-hidden`; `CardSkeleton heading` renders a visually hidden heading of the given level |
| 7 tables | `TableCard`: offset and floor, the structure it renders (`table` > `colgroup` / `thead` > `tr` > the `head` / `tbody` > the rows, `foot` below), `bodyProps`, `ref`, **`role="treegrid"` gives the table, header row, `HeadCell`s and `Cell`s `treegrid` / `row` / `columnheader` / `gridcell`**; `HeadCell` label, `scope`, separator width string, `clip` title, `aria-sort` and the first activation; `BodyRow` selection frame, cue frames, dragging opacity, **and the pass-through of `role`, `tabIndex`, `aria-level`, `aria-expanded`, `aria-selected`, `onKeyDown`, `onFocus` and the `ref` — the part sets none of them**; `Cell` padding kinds, clip and pass-through; `TreeCell` indent, chevron name and `aria-expanded`, **`toggleTabIndex={-1}` takes the chevron out of the Tab order**, leaf box, trailing slot; `RowControl`: `focus="tab"` is in the Tab order at `opacity: 0`, **`focus="roving"` has `tabIndex` -1 and honours a `tabIndex` the host passes**, both revealed by the row's focus; `DataTable` states (loading, empty, error, stale error) and names; `TablePager`; `TableSkeleton` real headers |
| 8 showcase | the registry's ids are unique and every section has a title and a usage sentence; the page renders under permission and answers `AccessDenied` without it; the navigation entry's visibility; the contents match the sections; no network request |
| 9 coverage | `kitCoverage.test.ts` (§ 9.6): KIT-19 with its `memo` / `forwardRef` fixtures and the non-vacuous check, KIT-20; the guard of part 1 re-run on the migrated tree (the list lowered, never raised) |

## 13. feat-029: names, refinements and what it inherits

feat-029's `design.md` § 10 named the parts by role and asked this design to
name them for good; its first table, «What the card asks the kit to add or
change» (eleven items), was **taken in on 2026-10-12** (`status.md` D-5): items 1–11
are in §§ 3, 5, 6, 10–12 above, with the card's measures where they were taken
from the reference's pixels (K-15). The map of names:

| feat-029's working name | The kit's |
|---|---|
| `InlineField` (text, number, date, select, autocomplete, multiline) | `InlineTextField`, `InlineNumberField`, `InlineDateField`, `InlineSelectField`, `InlinePickerField`, `InlineTextArea`; the assignee is `PersonField`. `optional` with an empty label is `emptyLabel`; `size: 'body' \| 'title'` (and `'table'`) is kept |
| `SectionHead`, `Section` | the same; `headingLevel` (2 or 3) |
| `CardHeaderBar` with `titleRow`, `actionRow`, `trailing` | `CardHeaderBar` with `identifier`, `title`, `meta`, `actions`; **no `trailing`**: the two dialog icons are `DialogShell`'s (`onExpand`, `onClose`), drawn first in the DOM at the top right |
| `PropertyBand` | the same (`blocks`, `meta`, `tabs`) |
| `BoxedTabs` | the same, with `BoxedTabPanel` and `idBase` |
| `DialogShell`, `PanelShell` | the same; `onClose(reason)`, `labelledBy`, `size="card"`, `fullScreenBelow`, `busy`, plus `guard` and `onExited`; **`label`** names the dialog while the card loads or fails; `PanelShell` takes the page's `trail` |
| `ColumnLayout` | the same (`main`, `side`, `extra`) |
| `SplitButton` | the same; `tone` defaults to `primary` |
| `ToolbarIconButton` | `IconAction size="bar"`; its glyphs: `Refresh` → `ThinRefreshIcon`, `Undo` → `ThinUndoIcon`, `SaveOutlined` (on the Save button) → `ThinSaveIcon`, `PersonOutline` → `ThinPersonIcon`, `OpenInFull` → `ThinExpandIcon`, `Close` → `ThinCloseIcon`, `MoreVert` → `ThinMoreVerticalIcon` (§ 5.1) |
| `MoreActionsMenu` | the same, on `MenuButton` |
| `ConfirmDialog` + `cancelLabel` | the same |
| `Notice` / `useNotice` | `NoticeProvider` in `AppLayout`; `useNotice().show(text, severity?)` — the card no longer hosts its own `Snackbar` |
| `StateFrame` NotFound compact | `NotFound({ level, compact, hint, action })`: `compact` is an `h2`, no tab title and no `registerNotFound` (§ 5.12) |
| `StateDot` | `CategoryMarker size="field"` (10 px) |
| `layout.workItemCard.dialogShare`, `typeBar`, `headerInset`, `rightInset` | `layout.card.*` (same names) |
| `…headerRows` `[4, 32, 8, 32, 8, 32, 8]` | `layout.card.headerPadTop`, `headerRowGap`, `headerPadBottom` and `density.controlHeight` |
| `…bandMinHeight`, `bandPaddingTop`, `bandRow`, `bandRowGap`, `bandLabelWidth`, `bandBlockWidth`, `bandBlockMin`, `bandBlockGap`, `tabStripHeight`, `tabIndicatorOffset`, `actionGap`, `dialogIconInset`, `dialogIconGap` | `layout.card.*` (same names, the card's values: `bandLabelWidth` is **82**) |
| `…bodyGutter`, `columnGap`, `sideColumn`, `threeColumnsMin`, `twoColumnsMin`, `bodyTop` | `layout.columns.gutter`, `gap`, `sideWidth`, `threeMin`, `twoMin`, `top` |
| `…sectionHeadGap`, `sectionGap`, `fieldInset` | `layout.section.headGap`, `gap`, `inset` |
| `…fieldBox`, `fieldGap`, `textMinRows` | `layout.field.box`, `gap`, `textMinRows` |
| `typeScale.cardTitle`, `palette.shell.fieldHover`, `palette.shell.cardLabel`, `.sectionRule`, `.splitDisabledFill`, `.splitDisabledText` | the same |

**Every rename.** Where the two designs named one thing differently, the kit's
family convention keeps **one** name and the card's owner follows it (the card's
`design.md` is on its own branch and is not edited here):

| feat-029 writes | The kit's (the only name) | Note |
|---|---|---|
| `adornmentPlacement="gutter"` | `adornmentPlacement="label"` | the kit's two values are `'value' \| 'label'`; `'label'` is **the default for `labelPlacement="start"`**, so the card passes nothing |
| `layout.card.bandAdornmentGap` (19) | `layout.field.adornmentGap` (**19**; the kit's draft said 16) | the adornment's gap belongs to the field family, not to the band |
| `layout.workItemCard.textPadX` (8) | `layout.field.textAreaInset` (8) | the same value; it was already the kit's |
| `layout.workItemCard.textTop` (15) | `layout.field.textAreaTop` (15) | new in the kit's `field` group, beside `textAreaInset` |
| `InlineTextArea padding="text"` | `InlineTextArea labelPlacement="hidden"` | no `padding` prop in the kit's vocabulary: a text area whose label is hidden **is** a text under a head, and carries `textAreaInset` and `textAreaTop` (§ 5.7) |
| `ThinSave`, `ThinChevronDown`, `ThinRefresh`, `ThinUndo`, `ThinPerson`, `ThinExpand`, `ThinClose`, `ThinMoreVertical` | the same with the **`Icon` suffix**: `ThinSaveIcon`, `ThinChevronDownIcon`, … | the exports keep the suffix the code already has (§ 5.1) |
| `ToolbarIconButton`, `InlineField`, `StateDot`, `Notice` | `IconAction size="bar"`, the seven field components, `CategoryMarker size="field"`, `useNotice` | the first rows of the map above |
| a `Section` rule in `divider` (the kit's draft) | `shell.sectionRule` | the kit took the card's grey (item 11) |

**Values that changed in the kit** (same name, the card's number now): `bandLabelWidth`
88 → **82** — the label column, the value's box starting where it ends and its
text `textInset` further in, so the text is still 88 from the label's edge;
`adornmentGap` 16 → **19**; the band's blocks `repeat(2, minmax(240px, 1fr))` →
two fixed tracks of `bandBlockWidth` 253 and a free one; `actions` 8 apart →
`actionGap` **11**; the dialog's icons «right 8, touching» → `dialogIconInset` **5**,
`dialogIconGap` **2**; the disabled `SplitButton` MUI's grey → `splitDisabled*`;
a field's label `text.secondary` → `shell.cardLabel`; the card's backdrop the
default dimming → `invisible`; the tab's indicator tab-wide at `bottom: 0` → label-wide,
2 thick, 4 up.

**Refinements the card inherits** (kit-level, so feat-029 follows them instead of
its draft): (1) a field's **hint** sits on its label's line, right-aligned, while
focused — not floating under the value over the next field's label (§ 5.7);
(2) a **busy** control is `aria-disabled` and keeps focus, not natively
`disabled` (§ 5.2) — «both parts disabled» in feat-029 § 4.2 reads «inert, focus
kept»; (3) the hover look also shows on `hover: none`; (4) `required` is an
asterisk and `aria-required`, never native; (5) a select's name is its label
only; (6) the card's notices go through `useNotice`; (7) **a confirmation is a
`dialog`, not an `alertdialog`**: `ConfirmDialog` keeps MUI's default role (K-14),
so feat-029 § 8's row «a confirmation | `alertdialog`» reads `dialog` and its
tests and E2E select by `dialog` and the name, as every existing one does;
(8) the placeholder is always the input's attribute, hidden at rest by CSS
(feat-029 § 4.7's «pass-through» is the whole of it); (9) in the band the state's
dot (and the Reason row's lock) stands in the label column — its centre 64 from
the label's edge, 19 before the text — so the values' text starts at 88 in every
row, and the field's frame takes the dot in: feat-029 § 4.3's `adornmentPlacement`
is the kit's `'label'`, the default for `start` (see the renames above); (10) a
text area's text is at `textAreaInset` (16 from the section's edge) and a text
under its head is `labelPlacement="hidden"`, `textAreaTop` below the rule; a
single-line title shows an ellipsis at rest; (11) the card Paper is `90 %`,
`margin: 0`, `maxHeight: none` and the `kit-card` container — all inside
`DialogShell` (feat-029 § 4.1 writes none of it); (12) a page's trail goes through
`usePageHead` to `PanelShell trail`, and `HeaderChrome` is deleted, not kept
(feat-029 § 5 «`HeaderChrome`, unchanged» is superseded); (13) the 32 px between
sections is the `ColumnLayout` area's, never a `Section` margin (feat-029 § 4.6
«the next section `sectionGap` 32 below»); (14) the card's glyphs are the thin
set (§ 5.1) for the toolbar, Save and person, and the split arrow is
`ThinChevronDownIcon`; (15) **the card's dialog draws no backdrop** — `invisible`,
the outside click still caught, the elevation-24 shadow kept — and a
`ConfirmDialog` over it dims; the card writes none of it (§ 5.10); (16) the clean
Save group is the reference's `#F0F0F0` / `#959595`, drawn by `SplitButton`, not
MUI's disabled grey (§ 5.2); (17) the header's icons stand at the reference's
measures — the toolbar `actionGap` 11 apart, the dialog's ⤢ and ✕ 2 apart and the
✕ 5 from the edge — and the tab strip's indicator is 2 thick, as wide as the
label and 4 above the strip's bottom: all of them the kit's, none of them the
card's to write; (18) a field's and the band's labels and the meta line are
`shell.cardLabel`, a section's rule `shell.sectionRule`.

**What feat-029 can start on:** plan part 6 (the card's kit) — tokens, icons,
actions, menus, marks, fields, the card parts, the shells, the notices and the
frames — **on the kit's branch**. **The guard (part 1) is armed before it
starts**, so feat-029 is checked from its first line: the workitems files it
rewrites (`WorkItemFormPage`, `FieldControl`, `AssigneePicker`, `AreaPicker`,
`StatePicker`, `ParentPicker`, `FormSection`, `RelatedWorkSection`,
`HeaderChrome`, `Marks`, `Notice`) are in the list with `until: 'feat-029'` and an
exact count, and each is lowered or removed in the commit that rewrites it — a new
hand-rolled field, menu or heading in them fails, and so does a new file that
does. The tables, the showcase and the coverage test (parts 7–10) do not block
its code.

**The merge order is `status.md` D-5's:** the kit's ten parts merge as **one
squash, before feat-029** — part 1 does not merge alone — and feat-029 is rebased
onto the main that holds it. The token lift renames reads in six boards files; its
isolated commit (§ 3.4) keeps a merge with feat-029's boards edits mechanical. If
another task merges to main before the kit's squash, the guard's baseline is
re-cut from the new main by § 9.3's procedure and the re-cut is logged in
`status.md`.

## 14. Hand-over notes for the frontend developer

- **The parts** (the `(Pn)` of § 5; the lines are in `status.md` ## Plan): 1 tokens,
  the lift **and the guards** (`kitGuard`, its baseline and list, the strengthened
  colour test); 2 icons, `IconAction`, buttons, menus; 3 marks, text helpers,
  tags; 4 fields; 5 `Surface`, `Section`, `ColumnLayout`, the card parts, the
  shells, `PageHeader` additions, `ConfirmDialog.cancelLabel`; 6 notices,
  `InlineNotice`, `StateFrame`, skeletons; 7 the table family; 8 the showcase,
  route, navigation, permission; 9 the coverage test, the guide's check, the docs
  sweep; 10 E2E 28. Each ends green with `bash scripts/verify.sh`.
  **Cut by D-6:** the showcase draws no table section (17 sections, E2E 28.1 lists 17,
  28.9 is not written) and part 9's `kitCoverage.test.ts` moves to tech-016.
- **No new dependency.** The guard uses the `typescript` package the colour test
  already imports; container queries are written in `sx` (emotion passes
  `@container` through; if a MUI/emotion version drops one the part's test and
  E2E 28.7 say so at once).
- **ESLint.** `react-refresh/only-export-components` warns on a file that exports
  a component and a hook: put `useNotice` and `usePageHead` in their own files.
- **Test infrastructure.** `src/test/renderWithProviders.tsx` mounts
  `NoticeProvider`; `src/test/kitGuard.test.ts`, `kitGuard.allow.ts`,
  `kitGuard.baseline.ts` (part 1) and `kitCoverage.test.ts` (part 9) are new, and
  `colourLiterals.test.ts` is extended (part 1); nothing else under `src/test/`
  changes. `KIT_GUARD_ROOT` and `KIT_GUARD_EMIT=baseline` are the guard test's two
  environment switches (§ 9.3) and are used once, in part 1.
- **Moving a file** is a rename with its imports and its test; keep the
  history (`git mv`). The Russian code comments of the files that move are
  translated in the same commit.
- **Visual check before the code review.** Lay the showcase's card section and
  the migrated backlog at 1536 × 790 CSS px (the reference's window; its pixel
  ratio 1.25) beside the reference screenshots and record each difference
  against § 3 and § 5 by token name — the screenshots stay outside the
  repository (feat-027 D-8's lesson). Include the new thin glyphs (refresh,
  undo, save, person) at their sizes — the person's ink against the reference's
  9 × 12 — and the split arrow's 12 × 6 chevron; the band's value x-alignment
  (every text 88 from its label's edge; the state dot's centre at 64, 19 before the
  text; block B's label 303 from the card's left edge, § 5.7, § 5.9); the clean Save
  group's greys (`#F0F0F0`, `#959595`); the toolbar's centres (110, 67, 24) and the
  dialog's (55, 21) from the right edge; the tab strip's indicator (2 thick, 4 above
  the strip's bottom); the text areas' insets and the text under its head (cap top
  30 below the rule, the text 16 in); the card window's lack of dimming. The
  Material `*Outlined` icons that remain on the card's tabs are the recorded
  difference of § 8.4, and the tab lines' `#DADADA` that of § 5.6, not findings.
- **Docs.** `docs/conventions/ui-kit.md` is already drafted with each component
  marked by the part that builds it; the last part removes the markers and
  checks the guide against the code. `frontend.md` and `src/frontend/AGENTS.md`
  carry the pointer (this pass).

## 15. Questions for the human — answered

The first draft put three questions to the human. All three are answered
(`status.md` D-4, D-5) and the answers are applied above; none of them is open.

**Q-1. Who sees the showcase — answered (a), the existing permission** (the
orchestrator, 2026-10-12, D-5). The page is for administrators (D-1) and the panel
decides by an instance permission code in the access token (ADR-0018). The
showcase **reuses `dictionaries.languages.write`**, held by the realm roles
`admin` and `dictionaries-admin`: no backend, realm or stand change, so **no stand
is reset**. The cost, accepted: a dictionaries administrator sees the page too, and
the code does not say what it grants; the page shows static demo data, so who sees
it is convenience, not security. It is one constant
(`uiKitPermissions.showcaseRead`, § 10.1): if the answer is ever a new code
`ui.showcase.read`, that line changes — together with `PermissionCatalog`, the
realm's client roles and the `admin` role, and **a stand reset for every existing
stand**, because Keycloak's drift check fails until `scripts/stand-reset.sh`, which
wipes that stand's database. *Applied in:* K-10, § 10.1.

**Q-2. Do the other pages move to the reference look, and at what pace — answered:
family by family, five tasks** (the human, 2026-10-12, through `AskUserQuestion`,
D-4). Moving the registries, forms, dialogs and the shell changes what people see
(§ 8.2); the human looks at each diff. Queued as **tech-016** `ui-kit-m1-boards`,
**tech-017** `ui-kit-m2-registries`, **tech-018** `ui-kit-m3-forms`,
**tech-019** `ui-kit-m4-shell` and **tech-020** `ui-kit-m5-legacy-out`, in that
order; **the guard's `until` names these ids** (§ 9.3, `UNTIL`), and tech-020
requires the allow-list empty. Rejected: one sweep task for the registries, forms
and shell (a large diff to review by eye, a regression hard to place) and stopping
after the backlog (two looks for good). *Applied in:* K-16, §§ 3.5, 5, 8, 9.3.

**Q-3. The look of a form field in a dialog or on a form page — answered: the
label above the frame** (the human, 2026-10-12, D-4: «Подпись над рамкой»). The
kit's framed field, `frame="always"` with the label above a 1 px frame, always
visible (§ 5.7), is **the one field anatomy**; the forms and dialogs take it in
**tech-018**. There is no second, outlined floating-label field in the kit; the
borderless look stays for the card and the other records edited in place. *Applied
in:* K-3, § 5.7, § 8.2.

