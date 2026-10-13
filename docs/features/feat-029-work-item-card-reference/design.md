# Design: The work item card as the reference (feat-029)

Stage 3 artifact of the short route (`status.md` D-2), written beside the spec
it serves: layout, MUI and kit components, every state, the measures and every
Russian string of this task are here, and nowhere else. The rules (BR-C-n) and
the scenarios (US-1..US-7) are in `spec.md` and `stories/`; they point here.

- **The reference screenshots** (the card, the human's own card, the reference
  backlog) live outside the repository and are never copied into it; the
  tracker they show is never named (mod-015 BR-16, tech-001 D-1). The numbers
  below were taken from them and are the **look to reach**, in CSS px and in
  the theme's tokens. Stage 3 fixes structure, components and states; this
  task's one acceptance test is a side-by-side look (BR-C-22), so the numbers
  are here for the developer and the QA reader to check against.
- **Built from tech-015's kit** (D-2), and named by it. Every component, prop
  and token below is the kit's: its design (`../tech-015-ui-kit/design.md`) holds
  the contract of each part (§ 5), the tokens (§ 3) and, in § 13, the map from
  this task's earlier working names and every rename. **Where this file and the
  kit's design differ, the kit wins** (tech-015 D-5). The kit took in the
  card's eleven asks (the transparent backdrop, the band's fixed blocks, the thin
  glyphs, …) on 2026-10-12, so the card asks it for nothing more and § 10 only
  lists what the card relies on. This file keeps what is the card's: the
  reference's measures (§ 2), which parts it passes where, its states and its
  texts. Where MUI has the standard component and the kit does not wrap it, it is
  named.
- mod-015's design (`../mod-015-app-shell-design-system/design.md`) and
  feat-027's (`../feat-027-backlog-tree-editing/design.md`) are the base: the
  theme, the font (Segoe UI, weights 400 and 600), the accent `#0078D4`, the
  type colours, the state dots, `ConfirmDialog`, `ErrorBanner`, `StateFrame`,
  the notices and the focus rules apply unless this file says otherwise. § 11
  lists what it supersedes.
- No prototype: not required on the short route (stage 03 makes it mandatory
  for `L` only), and the reference screenshot is the model.
- The data of every example is `stories/US-1`'s: project key `MSK`, team
  «Платформа», the story MSK-US-2.

## 0. How the reference was measured

The reference card is 1 919 × 988 device px. Its pixel ratio is **1.25**: the
human's own screenshot of the same panel, taken on the same machine, shows the
240 px navigation 299 px wide and the 4 px band stripe 5 px wide, and the
reference's sizes land on whole CSS steps at that ratio — its text sets in
Segoe UI at 12, 14, 16 and 17 px (the title 622 device px wide against 499 CSS
px for Segoe UI 17 px; its line "Updated by <name>: Aug 24" 257 against 258
at 12 px), and at ratio 1 they would be 15, 17.5, 20 and 21. So **CSS px =
device px ÷ 1.25**, and the window was 1 535 × 790 CSS px. The browser
paints saturated hues off (our story blue `#0098C7` reads `#5496C2` in both
screenshots), so hues come from the theme and only greys and sizes are taken
from the picture. A grey the theme's tokens do not already match has a token in
the kit (§ 3) or is listed as a difference (§ 2); it is never silently replaced
by the nearest token.

## 1. Screen map

| Surface | Address | Stories | Access |
|---|---|---|---|
| The card as a **dialog** over «Бэклоги» or «Канбан» | the page's own address + `?workitem=<идентификатор>` | US-1..US-6 | any signed-in user (BR-M3-27) |
| The card as a **page** | `/organizations/:o/projects/:p/workitems/:identifier` (mod-003 D-5) | US-2..US-5, US-7 | as above; anonymous: mod-015's auth card |
| Three confirmations above the dialog or the page | — | US-5, US-6 | — |

```
▌ [icon] ПОЛЬЗОВАТЕЛЬСКАЯ ИСТОРИЯ MSK-US-2                                      [⤢] [✕]     <- 32 px
▌
▌ MSK-US-2   Письмо со ссылкой________________________________                              <- 32 px
▌
▌ (person) Анна Смирнова   ·· places kept ··        [▣ Сохранить|▾] [⟳] [↶] [⋮]            <- 32 px
+----------------------------------------------------------------------------------------+
| Статус    ● Новая                Область     Платформа                Изменён: 05.10…   |  band
| ·· Причина ··                    ·· Итерация ··                       +--------+        |  76 px
|                                                                       | Детали |        |
+----------------------------------------------------------------------------------------+
| Описание                      | Планирование              | Связанная работа           |  body
| ----------------------------- | ------------------------- | -------------------------- |
|   Пользователь получает …     |   Оценка в пунктах        |   Родитель                 |  scrolls
| Критерии приёмки              |   Приоритет  2            |   MSK-FEAT-6 …             |
| ----------------------------- |   Риск                    |   Дочерние элементы (3)    |
|   Ссылка действует 15 минут.  | Классификация             |                            |
| ·· Обсуждение ··              |   Вид ценности  Бизнес    | ·· Развёртывание, Разработка ··
+----------------------------------------------------------------------------------------+
```

The three top rows are the header, the band is the grey strip with the tab,
the body is the tab's panel. `·· … ··` marks a place kept (§ 4.8): drawn as
nothing. The card has **no `<form>`**: Enter in the title saves through the field's
key handler, «Сохранить» is a plain button, and Enter in any other single-line field
does nothing (in a text it adds a line); the kit's shells give a form no element to
wrap (§ 12, «The form»).

## 2. Measures from the reference

CSS px. "Device" is the screenshot; the right column is what the card uses.

| Part | Device | CSS | The card |
|---|---|---|---|
| Dialog | 1 728 × 891, 96 px from the left, 51 from the top | 1 382 × 713, margins 77 and 41 | **90 % × 90 %** of the window, centred; at 1 536 × 790 that is 1 382 × 711 |
| Backdrop | none: the shell's top bar stays at 254–255 and the navigation away from the dialog at `#EAEAEA`; only the dialog's large shadow (70–90 device px) separates it from the page | no dimming | **transparent**, and still catching the outside click: the page behind keeps its full brightness, the dialog keeps the elevation-24 shadow (§ 4.1) |
| Colour bar | 12 wide, the header's height | 9.6 | **10** wide (`layout.card.typeBar`), the header's height only (not the band) |
| Header | 154 high | 123 | **124**: 4 + 32 + 8 + 32 + 8 + 32 + 8 (§ 4.2; `layout.card.headerPadTop`, `headerRowGap`, `headerPadBottom` and `density.controlHeight`) |
| Content's left edge (header, band) | 22 from the dialog's edge | 17.6 | **18** (`layout.card.headerInset`): the bar and 8 |
| Type line | icon ink 18 × 15; text 12 px, regular, `#335A99`, 101 wide | 16 px icon; 12 / 16 | `TypeLabel eyebrow`: `caption` capitals, `primary.dark` (`#0C5598`, close to the measured blue) |
| Title row | id and title 17 px; the id column 46 wide | 17 / 24; the id then a gap | `typeScale.cardTitle` 17 / 24 / 400; the id, a 16 px gap, the title |
| Assignee | the person's outline 11 × 15 ink, centre 142 (46 from the dialog's edge); the text from 164 (68) | ink 9 × 12; centre 37; text at 54 | `PersonField`'s slot is 24 wide, `layout.field.textInset` 6 in from the content's edge: **centre 36, name at 54** (§ 4.2) |
| Action row | buttons 40 high | 32 | `density.controlHeight` 32 |
| Save group | 140 × 40; the label part 101, the arrow 39; clean: fill `#F0F0F0`, label and glyph about `#959595` | 112 × 32; 81 and 31 | `SplitButton`, 32 high; clean = the reference's `#F0F0F0` / `#959595` (`shell.splitDisabledFill` / `splitDisabledText`), not MUI's disabled `#E0E0E0` fill with a `#A6A6A6` label (1.84 : 1), a darker block with fainter text |
| Icon buttons, row 3 | glyphs 16 hairlines, pitch 54: centres 138, 83 and 29 from the dialog's right edge | glyph 13, pitch 43: centres 110, 66 and 23 | 32 × 32 boxes, glyph 18, **11 apart** (`layout.card.actionGap`; pitch 43) between the Save group and each icon and between icons, the last box 8 (`rightInset`) from the edge: centres 24, 67 and 110 |
| Dialog icons, row 1 | ⤢ and ✕ hairlines 16, pitch 42; the ✕ centre 26.5 from the dialog's right edge | glyph 13; pitch 34; ✕ centre 21 | 32 × 32 boxes, **adjacent** (`layout.card.dialogIconGap` 2: pitch 34), the ✕ box **5** from the right edge (`dialogIconInset`: centre 21), on row 1's centre line |
| Band | 96 high, `#F8F8F8` | 77 | **76** minimum, `background.default` (`#F8F8F8`) |
| Band rows | label centres 27.5 and 71 below the band's top | 22 and 57 | 6 above, rows of 32, 4 between |
| Band label / value | 12 px `#707070` / 14 px; the value's text starts **110 from the label's left edge** in both blocks («New» +110, «MT» +112) | 12 / 14; **+88** («New» +88, «MT» +89.6) | `caption` `shell.cardLabel` / `body1`; label column `layout.card.bandLabelWidth` **82** and the value's text `layout.field.textInset` 6 inside its box: **every band value's text starts at label + 88** |
| Band blocks | label A at 117 (17 from the dialog's edge), label B at 473 (**302**); pitch 356 | 17 and 302; pitch **285** | fixed blocks `layout.card.bandBlockWidth` **253** and `bandBlockGap` 32: label A at 18, **label B at 303 from the dialog's left edge** (§ 4.3) |
| State dot | 13 across, +74…+86 from the label's left edge | 10 across: **+59…+69, centre +64**; 19 before the name | 10 px, **in the label column** (the kit's `adornmentPlacement="label"`, the default of a `start` field): centre at label + 64, **19** (`layout.field.adornmentGap`) before the name; never between the label column and the text (tables keep 8) |
| «Updated by …» | 12 px `#707070`, centre 16.5 below the band's top | 12; centre 13 | `caption`, `shell.cardLabel`, top of the band, 6 down |
| Tab strip | top line 51 below the band's top; 276 wide for four tabs, 11 from the right; `#DADADA` lines; label 14 px semibold; underline under the label only, 2 device px thick, its foot 5 device px above the band's lower edge | 41 below; 221 wide, 9 from the right; 1 px lines; 14 / 20; underline **2 thick, 4 above the band's lower edge** | `BoxedTabs`: **36** high (`layout.card.tabStripHeight`) against the band's lower edge, 8 from the right, lines `divider` (a known difference), a 2 px `primary.main` indicator under the label, **4 px above the strip's bottom** (`tabIndicatorOffset`; MUI's own `bottom: 0` would set it on the band's edge) |
| Body | gutter 15 left and right; scrollbar 15 | 12; 12 | **12** each side (`layout.columns.gutter`); classic scrollbar kept (`scrollbar-gutter: stable`) |
| Columns | 840, 399, 400 with 20 between | 672, 319, 320; 16 | **first column the rest, 320, 320, gap 16** (`layout.columns.sideWidth`, `gap`): 671 / 320 / 320 at 1 343 |
| Section head | 16 px semibold `#000`; cap top 27 below the body's top; `#EAEAEA` rule 62 below the body's top | 16 / 24; rule 50 below the top | `Section`'s `h2` 16 / 24 / 600; 16 above (`layout.columns.top`), 12 below (`layout.section.headGap`), a 1 px `shell.sectionRule` (`#EAEAEA`) rule, 4 under it (`bodyTop`) |
| Field | label 12 px `#737373` at +10, value 14 px `#191919` at +17; label to label 59 when the value is empty, 71 when it holds a text | label +8, value +14; 47 empty, 57 filled | label 16, 2, value box 28 (4 + 20 + 4), 2 between fields = **48 whether empty or filled** (`layout.field.*`; a known difference: the reference's empty fields are shorter, and typing into one must move nothing); label at +8, text at +14 |
| Text under its head | no label under «Description» or «Acceptance Criteria»: the head names the text; the first line's cap top 38 below the rule, the text 20 from the column's edge; for a two-line text the next head's cap top 145 below the rule | cap top **30** below the rule; text at **16**; next head's cap top 116 below the rule | the text has no label of its own (`InlineTextArea labelPlacement="hidden"`, § 4.6): its box starts `layout.field.textAreaTop` 15 below the 4 px that follow the rule, so the first line's **cap top is 30 below the rule** (1 + 4 + 15 + the box's 4 + 6 down to the cap); the text at **16** = the section's inset 8 + `layout.field.textAreaInset` 8; the card passes `minRows={2}` (D-4; the kit's default `layout.field.textMinRows` is 3), so the next head's cap top is **107** below the rule for a text of up to two lines (1 + 4 + 15 + 48 + 2 + 32 to the head's top, 5 down to its cap) — 9 px above the reference's 116 (KD-12) |
| Section gap | the next heading 270 below the previous, three fields between | 216 | **32** (`layout.section.gap`, the `ColumnLayout` area's) below the last field (the head's 41 and three fields of 48 make up 185) |
| Greys | label and meta line `#707070`–`#737373`; section rules `#EAEAEA`; tab lines `#DADADA`; clean Save group `#F0F0F0` with `#959595` | — | tokens (§ 3): `shell.cardLabel` **`#707070`** (4.95 : 1 on white, 4.66 : 1 on `#F8F8F8`; `#737373` is 4.47 : 1 on the band, under 4.5, and is not used), `shell.sectionRule` `#EAEAEA`, `shell.splitDisabledFill` / `splitDisabledText`; the tab lines stay `divider` `#E0E0E0` (a known difference) |

At 1 536 × 790 the dialog is 1 382 × 711; header 124 + band 76 leave the body
about 511 px, the reference's 513.

**Known differences from the reference.** BR-C-22 holds every measure above
within 2 px **except these, and only these**: each is a choice with its reason,
so the side-by-side neither fails it nor «fixes» it. A difference not in the
list is a defect; an item of the list is not. The numbers are stable: KD-7 (the
texts 11 px low at three rows) was withdrawn by D-4 when the texts took two rows,
and its number is not reused; what two rows leave is KD-12. KD-13 was added after the first review round (R1-4).

| # | The reference | Here | Reason |
|---|---|---|---|
| KD-1 | A filled field is 57 high, an empty one 47 | 48 either way: «Риск» stands about 8 px higher than the reference's «Risk» after one filled field, and each filled field above moves the next up by 9 | the no-move rule: typing into an empty field moves nothing (§ 4.7) |
| KD-2 | The Save group stands left of the «Follow» group and its settings button | no such group: the Save group's right edge is 137 px from the dialog's right edge, the reference's 274, so the group stands about 137 px further right | D-1, BR-C-18: «Следить» is a place kept (§ 4.8) |
| KD-3 | The third column opens with «Deployment» and «Development» | «Связанная работа» heads the third column, about 390 px higher than the reference's «Related Work» | D-1, BR-C-18: both are places kept |
| KD-4 | The tab strip holds four tabs | one tab, «Детали», 8 px from the right edge: about 160 px further right than the reference's «Details» | D-1, BR-C-13: the other tabs are places kept |
| KD-5 | The band's second rows hold «Reason» and «Iteration»; the meta line reads «Updated by <name>: <date>» | those rows are empty; the line reads «Изменён: <дата>» (the Russian line is wider; its right edge is the reference's) | D-1, BR-C-18; D-4 (the item stores no «changed by»; `updatedBy` is a slot for feat-015) |
| KD-6 | A parent's group opens with «Add link ⌄», then a «Parent» heading of 14 px near-black over the linked item | no «Add link»; «Родитель» is a field: a 12 px label over a picker whose value reads as the linked item (§ 4.6) | D-3 (the human): the parent picker now, the reference's block later — it is **feat-030-work-item-related-work**; BR-C-20 keeps the name «Родитель» until then |
| KD-8 | A resize grip at the dialog's bottom-right corner; a single letter underlined in «State», «Area» and «Iteration» (keyboard access keys) | neither is drawn | this task has no resizing and no access keys; drawing either without the feature would break D-1 |
| KD-9 | Tab lines `#DADADA` | `divider` `#E0E0E0` | decorative, 6 steps of grey apart; a token of its own is not worth it |
| KD-10 | English labels and values | Russian ones: a text's width differs, so compare left edges, not right edges (the meta line's right edge is a fixed one) | the product's language |
| KD-11 | Parts of the reference that D-1 keeps as places (comments count, tags, «Discussion», the history, links and attachments tabs, «Follow») | not drawn (§ 4.8) | D-1, BR-C-18 |
| KD-12 | The texts are a rich editor (bullets, links); a two-line text leaves the next head's cap top 116 below the rule | plain texts; the first line and the left edge hold (§ 2); at two rows the next head's cap top is 107, 9 px higher, and a longer text grows the box and moves the head down with it | feat-018 (rich text) is out; the reference leaves about 9 px under its text that a plain text area does not have, and the kit gives `InlineTextArea` no space below its text to take it in (tech-015 D-5: the kit wins). `minRows={2}` is D-4's; three rows (the kit's default) would put the head 20 px lower, at 127 |
| KD-13 | An empty optional list («Risk», «Value area» with nothing chosen) shows its label and nothing under it at rest; the placeholder and the caret come with hover or focus | «Риск» and «Вид работ» with nothing chosen show «Не указан» as their value at rest. An empty text, number or date stays blank at rest, its placeholder on hover and focus only | tech-015 D-5: the kit wins. `InlineSelectField` draws its `emptyLabel` as the selected value while nothing is chosen (`displayEmpty`; pinned by `InlineSelectField.test.tsx`), and hiding it would be the card restyling a kit field (E2E 25 and 27.3 assert «Не указан»). Found by the code review (R1-4), not by the side-by-side |

## 3. Tokens

The card adds none. Appearance has one place, the theme (frontend.md "Theme"),
and every token the card reads is **tech-015's**: the kit's part 1 adds them
(tech-015 § 3.2 and § 3.3, the values taken from the measures of § 2 above) and
pins them in `theme.test.tsx` and `contrast.test.ts` (tech-015 § 3.6). The
`layout.workItemCard` group that this file's first draft proposed does not exist:
tech-015 § 13 maps each of its keys to the kit's groups. The card reads the
tokens through the theme object (`useTheme()`, `sx` callbacks) and writes no
literal for what one names. The values are the kit's; this table only says what
the card reads.

| What the card places | The kit's tokens |
|---|---|
| the title row's text | `typeScale.cardTitle` (→ `typography.cardTitle`) |
| the dialog, the colour bar, the header's rows | `layout.card.dialogShare`, `typeBar`, `headerInset`, `rightInset`, `headerPadTop`, `headerRowGap`, `headerPadBottom` (with `density.controlHeight`) |
| the action row and the dialog's icons | `layout.card.actionGap`, `dialogIconInset`, `dialogIconGap` |
| the band and the tab strip | `layout.card.bandMinHeight`, `bandPaddingTop`, `bandRow`, `bandRowGap`, `bandLabelWidth`, `bandBlockWidth`, `bandBlockMin`, `bandBlockGap`, `stackBelow`, `tabStripHeight`, `tabIndicatorOffset` |
| the body's columns | `layout.columns.gutter`, `top`, `bottom`, `gap`, `sideWidth`, `threeMin`, `twoMin` |
| the sections | `layout.section.headGap`, `bodyTop`, `gap`, `inset` |
| the fields and the texts | `layout.field.box`, `gap`, `textInset`, `textAreaInset`, `textAreaTop`, `adornmentGap`, `textMinRows` |
| the greys | `palette.shell.fieldHover`, `cardLabel`, `sectionRule`, `splitDisabledFill`, `splitDisabledText` |

The tokens that already existed stay as they are and are read inside the kit's
parts (the band `background.default`, the tab lines `divider`, the type line
`primary.dark`, the focus ring `focusRing`, the page panel's `cardShadow`); the
dialog's elevation-24 shadow is MUI's own and has no token. The relations that
keep the measures honest — the header adding up to 124, the block pitch 285,
block B's label at 303, a band value's text at label + 88, the toolbar's centres
at 24, 67 and 110 and the ✕'s at 21, `cardLabel` at 4.5 : 1 or more on white and
on the band — are pinned by the kit (tech-015 § 3.6); the card's tests read the
tokens through the theme and do not pin them again.

## 4. The card

One component, `WorkItemCard`, of `features/workitems`, rendered by two hosts:
`WorkItemCardDialog` (US-1) and the page `WorkItemFormPage` (US-7). It takes the
item, the process, a `mode` (`'dialog' | 'page'`), `readOnly`, `onOpenItem`,
`onClose`, `onExpand` and `onSaved`, and the slots of § 4.8.

### 4.1 The dialog shell

The kit's `DialogShell size="card"` (tech-015 § 5.10). The card passes `open`,
`onClose(reason)`, `header` (the `CardHeaderBar`), `band` (the `PropertyBand`),
`alert` (the card's `InlineNotice`, when there is one), `children` (the tab's
panel), `labelledBy` (the header's `headingId`) or — while the card loads or
fails and has no heading — `label`, `busy`, `onExpand`, `guard` and `onExited`.
The shell owns the rest, and the card writes none of it.

| Part | The kit's shell | The card's side |
|---|---|---|
| Container | the Paper 90 % × 90 % of the window, centred (`layout.card.dialogShare`), `margin: 0`, `maxHeight: none`, corners `shape.borderRadius`; **below `md` (1 024 px) full screen**, square (`fullScreenBelow`, the card's default); MUI's elevation-24 shadow; the `kit-card` container that the header and the band read | — |
| Backdrop | **transparent**: `size="card"` sets MUI `Dialog`'s backdrop to `invisible` — still rendered, so a click outside the dialog still reaches `onClose('backdrop')` and the guard, but it paints nothing. **Not** `hideBackdrop` (that removes the outside click with it) and not the theme's default `rgba(0, 0, 0, .5)`, which would grey the backlog or board and the shell to half brightness. A `ConfirmDialog` over the card dims the whole window: a question dims | the reference draws no dimming (§ 2): the shadow alone separates the card from the page, which keeps its full brightness. E2E 27.2 asserts the backdrop's computed `background-color` is transparent and the page behind is as bright as before the dialog opened; the kit's `DialogShell.test.tsx` pins `invisible` and the closing click |
| Inside | fixed areas in this order: `header`, `band`, `alert`; then the panel (`children`), `flex: 1` and scrolling: **only the body scrolls** | `header` is the bar of § 4.2, `band` the band with the tab strip (§ 4.3, § 4.4), `alert` the card's `InlineNotice` (§ 6), the panel the tab's panel (§ 4.5) |
| Icons | «Развернуть на страницу» (`ThinExpandIcon`, drawn when `onExpand` is given) and «Закрыть» (`ThinCloseIcon`): `IconAction size="bar"` (32 × 32, glyph 18), first in the DOM, in row 1 at the right, **adjacent** (`layout.card.dialogIconGap` 2: pitch 34), the ✕ box `dialogIconInset` 5 from the edge (centre 21; § 2); tooltips, 500 ms; inert while `busy` | the dialog's only close controls: the card passes `onExpand` and draws none of its own |
| Name | `role="dialog"`, modal; `aria-labelledby` the header's `headingId` or, with no heading, `aria-label` = `label` (exactly one of them) | `headingId` is the type line's; while loading or failing `label` is «Рабочий элемент <ID>» |
| Focus | opens with focus on the dialog's own frame, in no field; a trap; on closing back to the opener | `onExited` puts it, when the opener is gone, on the row or card with the same `data-identifier` (the host keeps the element), else `main` |
| Closing | «Закрыть», Esc and the backdrop call `onClose` with their reason (`'close'`, `'escape'`, `'backdrop'`) (US-6); with `guard.dirty` they first open the confirmation and call `onClose` only on a discard; `busy`: none of them closes (mod-015 § 9.1) | `guard`: `dirty` the draft's `isDirty`, `title` «Закрыть без сохранения?», `text` «Несохранённые изменения рабочего элемента <ID> будут потеряны.», `discardLabel` «Закрыть без сохранения», `cancelLabel` the kit's default «Продолжить редактирование» (§ 9; D-4) |
| Address | — | `?workitem=<идентификатор>` of the host's address. Opening pushes an entry marked `{ cardDepth: n }` (n counts the entries the card pushed: a switch to a related item adds one); closing goes back `n` entries; a dialog opened by the address has no mark and closes by replacing the address. A switch made from such a dialog marks the entries it pushes `{ cardDepth: n, cardBase: 'address' }`: their close goes back `n` entries onto the address-opened entry, which still holds `?workitem=<first>`, and replaces it without the parameter, so one close ends it and Back still leaves the page (BR-C-2, BR-C-4; review R1-1) |
| Expand | `onExpand` is a plain callback, never guarded | pushes the page's address with the unsaved edits in the router state (`{ edits }`); the page reads them once and clears the state with a replace (BR-C-3) |
| Over | a `ConfirmDialog` or a menu opens above the card; a notice is above all of them | the confirmations of § 6; notices through `useNotice().show` (6 s, bottom left, the kit's one host) — the card hosts no `Snackbar` of its own |

The page behind stays mounted and is inert while the dialog is open (MUI's
modal). The browser tab keeps the host page's title. There is no in-place
maximize: «Развернуть на страницу» is the dialog's maximize (D-1) and opens the
page (§ 5); the dialog's own size is the 90 % × 90 % above, or the whole window
below 1 024 px.

### 4.2 The header bar (`CardHeaderBar`)

The kit's `CardHeaderBar` (tech-015 § 5.9) draws the colour bar, the three rows
and their insets: padding left `layout.card.headerInset` 18, right `rightInset` 8;
rows top to bottom 4 · **32** · 8 · **32** · 8 · **32** · 8 = 124. The card passes
`appearance`, `typeLabel`, `identifier`, `headingLevel` (`1` on the page, `2` in
the dialog), `headingId`, `title` (row 2), `meta` (row 3, left) and `actions`
(row 3, right); the two dialog icons are the shell's (§ 4.1), not a slot of the
bar.

**Glyphs.** Every glyph of this header is a hairline in the reference (1 device
px), so the card draws exactly eight glyphs of the kit's thin set (tech-015
§ 5.1; the exports carry the `Icon` suffix) and no Material glyph:
`ThinExpandIcon`, `ThinCloseIcon` (the shell's), `ThinSaveIcon` (the Save
button's), `ThinChevronDownIcon` (the Save arrow), `ThinRefreshIcon`,
`ThinUndoIcon`, `ThinMoreVerticalIcon` and, for an unassigned item,
`ThinPersonIcon`. Material's `SaveOutlined`, `Refresh`, `Undo` and
`PersonOutline` draw strokes of about 1.5 px at 24, and a 24 px person has about
16 px of ink against the reference's 9 × 12. Material `*Outlined` stays outside
the header: the menu entry's `ContentCopyOutlined` and the body's link
`OpenInNewOutlined`, where the kit thins nothing.

| Row | Content | Components and notes |
|---|---|---|
| bar | at the left edge, `layout.card.typeBar` 10 wide, the header's full height, in the type's colour | drawn by `CardHeaderBar` (`TypeStripe`); the band under it is not coloured |
| 1: type line | `TypeLabel eyebrow` (the 16 px icon in the type's colour, `aria-hidden`; «<Тип> <ID>» as one text in capitals by CSS: `caption`, `primary.dark`, no letter-spacing); at the right the shell's «Развернуть на страницу» and «Закрыть» (§ 4.1; dialog only) | the text is the heading: `h1` on the page, the dialog's name in the dialog (`h2`); the DOM text is not changed by the capitals |
| 2: title row | the identifier (`typeScale.cardTitle`, `text.primary`, no wrap, not a link), a 16 gap, the title: `InlineTextField size="title"`, `label` «Заголовок» with `labelPlacement="hidden"`, `required` (`aria-required`; never the native attribute: the card has no form to validate), `placeholder` «Введите заголовок», one line, an ellipsis at rest | Enter saves (the field's `onKeyDown` through `inputProps`, not while an IME composes); the error line shows under the row |
| 3: action row | `meta`, at the left: the assignee, a `PersonField`, `label` «Исполнитель» with `labelPlacement="hidden"`, in a container of `minmax(160px, 260px)` (a kit field fills its container; the card sizes the container), `emptyText` «Не назначен» in `text.secondary`. **Its slot is 24 wide: while nobody is assigned `ThinPersonIcon` (a 16 px glyph in the slot, `text.secondary`), once someone is `UserAvatar` 24 with the initials** — `PersonField`'s own; the glyph's centre is 36 and the name at 54 from the dialog's left edge (§ 2). Then the places kept (§ 4.8). `actions`, at the right, **11 apart** (`layout.card.actionGap`): the Save group, «Обновить» (`IconAction size="bar"`, `ThinRefreshIcon`), «Отменить все изменения» (`IconAction size="bar"`, `ThinUndoIcon`), «Ещё действия» (`MoreActionsMenu`) | the actions wrap under the assignee below a card width of 720 (`layout.card.stackBelow`, § 7) |

**The Save group** (`SplitButton`; `tone` is `primary`, its default): `groupLabel`
«Сохранение»; the main button with `icon` `ThinSaveIcon` and `label` «Сохранить»
(a plain button: its `onClick` saves); the arrow, the kit's own
`ThinChevronDownIcon` (the reference's 12 × 6 chevron; never Material's filled
`ArrowDropDown`), named by `menuLabel` «Ещё варианты сохранения» (`aria-haspopup`
and `aria-expanded` are the kit's) and opening a menu of one entry in `items`,
«Сохранить и закрыть». Enabled while the draft has a change and no save runs.
**Clean it is `disabled`, and the kit draws it in the reference's clean look: fill
`shell.splitDisabledFill` `#F0F0F0`, label and glyph `shell.splitDisabledText`
`#959595`** — MUI's own disabled button is a `#E0E0E0` fill with a `#A6A6A6`
label, a visibly darker block with fainter text. While a save runs (`busy`) the
main button shows the kit's spinner in place of its glyph and **both parts are
inert with focus kept** (`aria-disabled`, a click does nothing; not natively
`disabled`, so a user who pressed Enter on «Сохранить» keeps their place while the
save runs; the tone's enabled fill stays: the work is running, the button is not
unavailable). Once the save is accepted the clean button is natively `disabled` and a
disabled button gives its focus up, so **when the button held focus at the click the
card takes focus to the title** (R1-6): the next Tab starts from the header, and Esc
closes a saved dialog at once.
It is the card's one filled button (frontend.md "One accent"). The name of the
arrow does not contain «Сохранить», so a query by that name finds the main button
alone (the kit pins it).

**The three icon buttons** (`IconAction`, whose `label` is the name and the
tooltip): «Обновить» is `busy` (a spinner, `aria-busy`, inert) while a read runs
and `inactive` while a save runs; «Отменить все изменения» is `disabled` without
edits and `inactive` while a save runs; «Ещё действия» is a `MoreActionsMenu`
(its `label` defaults to «Ещё действия») whose one entry in `items` is «Копировать
ссылку» (`ContentCopyOutlined`, a neutral entry icon).

**While «Обновить» reads** (R1-7), one piece of work runs at a time: the Save group
over unsaved edits is `busy` too (the kit has no quieter inert state), so a click or
Enter in the title that the card would absorb is visibly waiting, never silent; a
clean Save group stays in its clean look. «Отменить все изменения» is **not** held:
it only empties the draft, which the read then rebases like any other, so it works
during a read. The refresh's notice is worded from the draft as it stands when the
read ends: text typed during the read gets «…остались на месте.».

### 4.3 The grey band (`PropertyBand`)

The kit's `PropertyBand` (tech-015 § 5.9) draws the strip: `background.default`,
at least `layout.card.bandMinHeight` 76 high, padding `bandPaddingTop` 6 ·
`rightInset` 8 · 0 · `headerInset` 18, four tracks — the two **fixed** blocks
(`bandBlockWidth` 253, `bandBlockGap` 32 apart, a pitch of 285), a free track that
takes the rest, and the aside (`auto`, right-aligned: `meta` on top, `tabs` below,
against the lower edge). The card passes `blocks` (two stacks of fields with
`labelPlacement="start"`), `meta` and `tabs`:

| Part | Content | Notes |
|---|---|---|
| Block A, row 1 | «Статус»: `InlineSelectField`, `labelPlacement="start"`, label column `layout.card.bandLabelWidth` **82**, the value text **88** from the label's edge; the value is the state's name, each option (and the chosen one) with `CategoryMarker size="field"` (10) as its `startAdornment`. The dot stands **in the label column** — the kit's `adornmentPlacement="label"`, the default of a `start` field, so the card passes nothing: its centre at label + 64 and its right edge `layout.field.adornmentGap` **19** before the name's text. The hover and focus frame of this field takes in the dot and the name (the box starts 6 before the dot) | the type's states without «Удалена» (mod-003 § Pickers); the label is `shell.cardLabel`, «Статус» ends near +38, well before the dot at +59 |
| Block A, row 2 | the place of «Причина» (§ 4.8) | |
| Block B, row 1 | «Область»: `InlinePickerField`, `labelPlacement="start"`, label column 82, the value text at label + 88, the value the area's path (an ellipsis when it is wider than the block; no `title` holds the whole — the kit's `FieldInputProps` has none — and the open list shows every path in full) | as mod-003 § Pickers; its label stands **303** from the dialog's left edge |
| Block B, row 2 | the place of «Итерация» | |
| `meta` | «Изменён: <дата>» (`caption`, `shell.cardLabel`), 6 from the top, at the right | `formatDateTime(item.changedAt)`; «—» when unreadable; the place of «Обновил <имя>» (`updatedBy`, § 4.8; D-4) |
| `tabs` | the tab strip (§ 4.4) against the band's lower edge | |

**Every band field's value text starts at its label + 88, whatever sits before
it**: the label column is 82 wide and the text sits `layout.field.textInset` 6
inside the box, so «Статус» and «Область» line up (the reference's «New» at +88
and «MT» at +89.6). The state's dot is not part of that offset: it stands inside
the label column, 19 before the text, so «Новая» is at +88 and not at +110.

The blocks are **fixed** (253 + 32 = 285), so block A's label is 18 and block B's
303 from the dialog's left edge (the reference's 17 and 302). The room left is the
empty third track before the aside — it is not shared out to the blocks; a `1fr`
block would put «Область» about 335 px right of the reference. The blocks shrink
toward `bandBlockMin` 240 only where the card is too narrow for 253 + 253 and the
aside (between a card width of 720 and about 790); then the meta line ends in an
ellipsis and the tab strip never shrinks. Rows are `bandRow` 32 high with
`bandRowGap` 4 between. **The band keeps its 76 px with the two places empty**, so
«Причина» and «Итерация» arrive without moving anything below (BR-C-18). Below a
card width of 720 (`stackBelow`) the blocks stack in one column of `bandBlockMin`
at least, the aside under them (§ 7), and the minimum height is dropped.

### 4.4 The tab strip

`BoxedTabs` (tech-015 § 5.6) with one tab, «Детали», selected, passed to the
band's `tabs`. The kit draws the strip: `layout.card.tabStripHeight` 36 against
the band's lower edge, `rightInset` 8 from the right, 1 px `divider` lines on the
top and the sides (the reference's `#DADADA` is KD-9) and none at the foot, the
selected label 600 and the others 400; and the selected tab's indicator — a 2 px
(`layout.frame.thick`) `primary.main` line **as wide as the label**, not the tab,
**`tabIndicatorOffset` 4 px above the strip's bottom**: the reference's underline
sits 4 px above the band's lower edge, directly under the label, where MUI's own
indicator (`bottom: 0`, tab-wide) would stand on the edge itself. The kit's
`BoxedTabs.test.tsx` pins the computed height (2) and `bottom` (4); E2E 27.2
measures it in the browser (the indicator's foot 4 px above the band's lower
edge).

The card passes `idBase` (from `useId()`), `aria-label` «Разделы рабочего
элемента», `value`, `onChange` and `tabs`; the panel is a `BoxedTabPanel`
(`role="tabpanel"`, `aria-labelledby` the tab). More tabs join to the left of the
strip's right end and take their own `icon` and `count` (the reference draws
«История» and «Вложения» as an icon, «Связи» and «Вложения» with a count): their
owners' call, and until an owner adds its hairline glyph to the kit's icons the
tab passes a Material `*Outlined` icon, a difference tech-015 records (§ 8.4 there).

### 4.5 The body grid (`ColumnLayout`)

The panel is the kit's `ColumnLayout` (tech-015 § 5.8): `main` the texts, `side`
the sections, `extra` «Связанная работа». It is the **container**, padded
`layout.columns.top` 16 above, `gutter` 12 at the sides and `bottom` 24 below.
Its **content width** — the card's own, not the window's — picks the layout by
CSS container queries:

| Content width | Columns | Tracks |
|---|---|---|
| ≥ `layout.columns.threeMin` 1 040 | **three** | `minmax(0, 1fr) 320px 320px`, gap 16: the texts · the sections · «Связанная работа» |
| ≥ `twoMin` 680 | **two** | `minmax(0, 1fr) 320px`: the texts; the sections with «Связанная работа» under them (`main side` / `main extra`), 32 between those two |
| below | **one** | the texts, the sections, «Связанная работа», 32 between; a field takes at most `layout.formColumnWidth` (480) as feat-022 has it, a text the column |

At the reference window the content is 1 343 (671 / 320 / 320); the page at
1 440 with the navigation expanded has 1 128 (456 / 320 / 320). The window's
width does not decide: the user's collapsing the navigation changes the card's
columns, as it should. No `useMediaQuery` is used for the columns (the kit's
guard, KIT-17). The 32 px between sections is the layout area's, never a
section's margin (§ 4.6).

### 4.6 Sections and fields per type

The columns hold feat-022's sections unchanged (`lib/fields.ts`, `layoutOf`
by `type.appearance`): the type's `texts` stand in the first column, its
`sections` in the second, «Связанная работа» (feat-001) alone in the third. A
section a type lacks is absent. **Section** (the kit's `Section`, tech-015 § 5.8):
a `<section aria-labelledby>` (a `region`) whose head is an `h2`
(`typography.h2`, 16 / 24 / 600), `layout.section.headGap` 12 below it a 1 px rule
in `shell.sectionRule` (`#EAEAEA`, the reference's; not `divider`), then `bodyTop`
4, the content inset `layout.section.inset` 8 at both sides. A section has no
margin of its own: the `layout.section.gap` 32 between two sections is the layout
area's (§ 4.5), so the card places its sections in `ColumnLayout`'s areas and
spaces them with nothing.

**A text is named by its head.** The reference draws no field label under
«Описание» or «Критерии приёмки»: the section's head names the text. So each
text of the first column (`Описание`, `Критерии приёмки`, `Шаги воспроизведения`,
`Сведения о системе`) is an `InlineTextArea` with `labelPlacement="hidden"` — for
a text area the kit means exactly «a text under a head of its own name» (tech-015
§ 5.7): the box begins `layout.field.textAreaTop` 15 below the 4 px that follow
the rule and the text stands at `textAreaInset` 8 — and its `label` the head's own
text, so the textbox's accessible name stays «Описание» (BR-C-20) and no second,
12 px «Описание» is drawn under the head (with `labelPlacement="top"` it would be,
and the next head would stand about 16 px lower than the reference's). The first
line's cap top is 30 below the rule and the text 16 from the column's edge (§ 2).
The card passes `minRows={2}` to every text, on the dialog and the page alike (D-4;
the kit's default is 3), and the box grows with its text.

| Type | First column | Second column | Third |
|---|---|---|---|
| Эпик, Фича | «Описание» | «Планирование»: Приоритет, Риск, Трудоёмкость, Бизнес-ценность, Критичность по времени, Дата начала, Целевая дата · «Классификация»: Вид ценности | «Связанная работа» |
| Пользовательская история | «Описание», «Критерии приёмки» | «Планирование»: Оценка в пунктах, Приоритет, Риск · «Классификация»: Вид ценности | «Связанная работа» |
| Задача | «Описание» | «Планирование»: Приоритет, Вид работ · «Трудозатраты (часы)»: Исходная оценка, Оставшаяся работа, Выполненная работа | «Связанная работа» |
| Баг | «Описание», «Шаги воспроизведения», «Сведения о системе» | «Планирование»: Оценка в пунктах, Приоритет, Серьёзность, Вид работ · «Трудозатраты (часы)»: Исходная оценка, Оставшаяся работа, Выполненная работа | «Связанная работа» |

The control of each field (the kit's field by kind; labels, lists, limits, hints
and placeholders are `lib/fields.ts`'s and feat-022 § Controls and copy's,
unchanged):

| Field | Kit field | Notes |
|---|---|---|
| Приоритет, Серьёзность, Вид ценности | `InlineSelectField`, no `emptyLabel` | always set: no empty option; «Приоритет» hint «1 — самый высокий» |
| Риск, Вид работ | `InlineSelectField`, `emptyLabel` «Не указан» | the `emptyLabel` is what makes the field optional, and the kit draws it as the value while nothing is chosen: **«Не указан» at rest** (KD-13) |
| Оценка в пунктах, Трудоёмкость, Исходная оценка, Оставшаяся работа, Выполненная работа, Критичность по времени | `InlineNumberField`, `inputMode="decimal"` | hint «От 0 до 9 999»; placeholders «Не оценено», «Не задано» |
| Бизнес-ценность | `InlineNumberField`, `inputMode="numeric"` | hint «Целое число от 0 до 9 999» |
| Дата начала, Целевая дата | `InlineDateField`, the browser's own `type="date"`, `min` 2000-01-01, `max` 2099-12-31, `onIncomplete` | the empty mask and the calendar button show on hover and focus only; hint of «Целевая дата» «Не раньше даты начала» |
| Описание and the type texts | `InlineTextArea`, `labelPlacement="hidden"`, `minRows={2}`, grows with its text | hint «До 100 000 символов» (under the text on an opaque background: a hidden label has no line to carry it); placeholders as feat-022 |
| Родитель | `InlinePickerField`, the composition tech-015 § 5.7 names, in the card's `ParentField`: a `TypeMark compact` start adornment (not `TypeLabel`, whose name does not fit the field's width), `renderOption` of `TypeMark` + «<ID> <Заголовок>» + `StateLabel`, `endAction` the stored parent's `StateLabel` and «Открыть <ID>» | feat-001's `ParentPicker`'s behaviour: «Нет родителя» first; `ParentPicker` itself stays for the board's «Изменить родителя» until tech-018 |
| «Дочерние элементы (n)» | a `List` of feat-001's `ChildrenList` under its head (below) | read-only; «Дочерних элементов нет.» (`InlineEmpty`) |

«Связанная работа»: a `Section`; «Родитель» as the picker above, with the parent's
state and the link «Открыть <ID>» (`OpenInNewOutlined`, the picker's `endAction`)
to its right while a parent is stored; under it «Дочерние элементы (n)» — a
`SectionHead` (`headingLevel={3}`, `count`, `divider={false}`) so that the card
writes no heading element of its own (tech-015 KIT-21) — and the list, named by
that head. In the dialog both links (`Открыть` and the children's identifiers) are
`Link`s with the full page's address and call `onOpenItem(identifier)` on a plain
click (BR-C-5); on the page they navigate.

**What the reference draws here and this card does not** (D-3, KD-6). The
reference opens «Related Work» with «Add link ⌄» — a menu of «Изменить родителя…»
and «Добавить дочерний элемент…» — then the groups «Родитель» and «Дочерние
элементы (n)», each a head of 14 px near-black over link rows. The human chose
(D-3): **the parent picker now, the reference's block later.** This card keeps
feat-001's picker: «Родитель» is a field — a 12 px `cardLabel` label over a picker
whose value reads as the linked item (type, identifier, title, state) — and
«Дочерние элементы (n)» a head over a list; there is **no «Add link»**, and no
place is kept for it. The reference's block is
**feat-030-work-item-related-work** (queued, deps feat-029): it rebuilds this
section's content, so the names and tests BR-C-20 keeps here for «Родитель» are
its to move. The reasons the card does not draw it now:

- «Add link» opens a menu. The product has one operation the menu could hold —
  change the parent — and the picker already is that operation; the children are
  read from the other side. A menu of one entry that duplicates the picker, or of
  entries the card cannot do, is the stand-in BR-C-18 forbids. The other link
  types are release 2 (doc-002 § Out of scope), with the «Связи» tab (spec, Out).
- The parent is an editable field with a name: «Родитель» is the combobox that
  feat-001's tests and E2E 24 find (BR-C-20), and its error and conflict line
  (`workitems.link.parent_changed`) belongs under the control. A group head over a
  picker would be a second name for the same thing.
- Building the reference's block here — «Добавить связь» as a menu of «Изменить
  родителя…» and «Добавить дочерний элемент…» with group heads and link rows — is
  a new dialog or two for operations the card does not have today; it is
  feat-030's task, not part of this look.

### 4.7 The inline fields (the kit's family)

The kit's borderless fields, for the card's every value: `InlineTextField`,
`InlineNumberField`, `InlineDateField`, `InlineSelectField`, `InlinePickerField`,
`PersonField` and the multiline `InlineTextArea` (tech-015 § 5.7). One look and
one anatomy: their states, names and keys are the kit's and are tested there (the
shared field suite, tech-015 § 12). What the card relies on and chooses:

| Part | The card relies on |
|---|---|
| Label | a real `<label for>`, `caption` `shell.cardLabel`, and the control's accessible name is the label only: **above** the value (`labelPlacement="top"`, the body); at the **left** in a column of `layout.card.bandLabelWidth` 82 (`start`, the band: the value's text is 88 from the label's edge); or visually hidden (`hidden`): the title, the assignee, **and every text, whose head names it** (§ 4.6) — the name is still the label |
| Value box | `layout.field.box` 28 high (4 + 20 + 4), `body1` 14 / 20, `text.primary`; the label at the section's inset 8, the value's text at 14 (`textInset` 6 in the box). **A text under its head** (`InlineTextArea labelPlacement="hidden"`) has its text at `textAreaInset` 8 (16 from the section's edge) and stands `textAreaTop` 15 below the 4 px that follow the section's rule: the first line's cap top 30 below the rule (§ 2) |
| Rest, hover, focus | no frame, no fill, no caret at rest: the value is text, an empty text, number or date field blank, an empty optional list «Не указан» (KD-13), a select's caret and a date's calendar button hidden; hover a 1 px `shell.fieldHover` frame with the caret and the placeholder; focus a 2 px `primary.main` frame (the focus ring) and the hint. **No state changes the box's size**; where the pointer cannot hover (`hover: none`) the hover look is the rest look |
| Placeholder | always the input's own `placeholder` attribute (E2E 25 reads it), its text visible on hover and focus only; never the label |
| Hint | in the field's accessible description always (`aria-describedby`); visible only while focused, **on the label's own line, right-aligned** (`top`), covering nothing and moving nothing; for `start` and `hidden` fields (the band, the texts), which have no label line to share, under the value on an opaque background above its neighbours |
| Error | a 1 px `error.main` frame at rest and hover, 2 px on focus; the error line (`caption`, `error.main`) under the field, which does move what is below, with `aria-invalid="true"` and the line as the description (replacing the hint); the card clears it when the field is edited (§ 6) |
| Read-only, busy, disabled | read-only: the value as text, reachable by Tab, `aria-readonly`, no hover frame; `busy` (a save runs): the read-only look with the text colour unchanged, `aria-busy`, edits refused; `disabled` (`text.disabled`) only where the field can never be edited |
| Select, picker | a select opens on Enter, Space or ArrowDown, and Esc closes the list and not the dialog; a picker's input is always there, shows the chosen option's text at rest and selects it on focus; both `role="combobox"` named by the label; the options, `loadingText`, `noOptionsText` and the errors are mod-003's (§ 9) |
| Number | decimal separator «,» or «.»; shown with «,» and no trailing zeros; the draft holds the text (feat-022 BR-M3-32) |
| Pass-through | `inputProps` (`inputMode`, `aria-*`, `data-*`, `min`, `max`) and a ref that focuses the input (the first error) |

### 4.8 The places kept

BR-C-18: the layout keeps a named place for each of the reference's parts that
the product does not have yet. A place is a slot of the card (`slots.<name>`, a
`ReactNode`, absent by default): the card draws nothing there, no disabled
stand-in, no empty box; its owner's task passes a component and nothing else
moves. `WorkItemCard.slots.test.tsx` renders a card with none and finds none of
their words, and a card with a probe in each slot finds it in its row.

| Slot | Where it stands | What the layout does without it | Owner |
|---|---|---|---|
| `commentsCount` | row 3, after the assignee | the row's middle is empty and flexible | feat-002 |
| `tags` | row 3, after `commentsCount` | as above | feat-016 |
| `follow` | row 3, left of «Обновить» | the Save group is the first of the actions | none queued (D-4; release 2, doc-011) |
| `updatedBy` | the aside's top: «Обновил <имя>: <дата>» replaces «Изменён: <дата>» | «Изменён: <дата>» | feat-015 (D-4) |
| `reason` | band, block A, row 2 | the band keeps its 76 px | none queued (D-4; release 3, doc-002 D-6) |
| `iteration` | band, block B, row 2 | as above | mod-007 |
| `tabs` | the strip, left of «Детали»: «История», «Вложения» | one tab | feat-015, mod-004 |
| `discussion` | first column, under the type's texts: «Обсуждение» | the first column ends with its texts | feat-002 |
| `deployment`, `development` | third column, above «Связанная работа»: «Развёртывание», «Разработка» | «Связанная работа» stands at the column's top | none queued (D-4; not in the product plan, feat-022 D-2) |

The reference's «Links» tab is not a place kept: «Связанная работа» holds the
parent and the children, and the other link types are release 2. Nor is its «Add
link» (§ 4.6, KD-6): feat-030 rebuilds the section's content (D-3). **Not drawn
and not kept either**: the resize grip at the dialog's bottom-right corner (this
task has no resizing) and the single letters underlined in «State», «Area» and
«Iteration» (it has no access keys) — both are KD-8 of § 2, the one list that
BR-C-22's side-by-side exempts.

## 5. The page

`WorkItemFormPage` renders the card in `mode="page"` in the kit's `PanelShell`
(tech-015 § 5.10): the shell's trail above — `usePageHead(...).trail` handed to
the shell's `trail`, `HeaderChrome` being deleted (tech-015 § 13) — and one white
`Surface` (`theme.cardShadow`, `shape.borderRadius`) on the application
background, holding the same header, band, tab strip and body in the same slots
(`header`, `band`, `alert`, `children`). Differences from the dialog:

- the type line is the page's `h1`; **no** «Развернуть на страницу» and **no**
  «Закрыть» (the trail leads back); «Сохранить и закрыть» goes back in
  history, or to «Рабочие элементы» when the page was the first the user
  opened (`location.key === 'default'`);
- the page scrolls, not the card: the header, the band and the tab strip
  scroll with it, nothing is fixed; the body has no inner scrollbar, so its
  width is the card's less 24;
- the tab title is «<Тип> <ID> — Mustask», set through `usePageHead` (its
  `title`; `useDocumentTitle` outside the kit fails the guard, KIT-22); the
  address, the not-found frames (mod-003 § Not-found answers) and the key
  (`organizationId/projectId/identifier`: another item starts afresh) are
  unchanged;
- a carried `{ edits }` in the router state starts the draft (§ 4.1).

## 6. States

The card keeps its frame in every state; the dialog keeps its two icons. In the dialog that is **one `DialogShell` element, mounted once** by the dialog's host (`DialogFrame`) and never replaced between the loading, the failed read, the not-found frame and the card: until the card has published its frame the shell is named by `label` and holds the state's own frame, and when it has, the shell takes the card's header, band, notice and body and is named by the type line (`labelledBy`). So the dialog neither fades out and in nor hands focus back to the page between the skeleton and the card, and a screen reader meets one dialog (review R1-3; Vitest holds the same element across the read and across the not-found frame a save finds).

| State | What is shown |
|---|---|
| **Loading** | the frame at once and the kit's `CardSkeleton` in the card's own shape, `aria-hidden` but for one `role="status"` «Загрузка данных» — the dialog is named by `DialogShell`'s `label` «Рабочий элемент <ID>» and passes no `heading`, the page passes `heading: { text: 'Рабочий элемент <ID>', level: 1 }` so it has its `h1` while loading: the header (a 220 × 16 line, a 60 % × 24 title, a circle and a 160 × 16 line, a 112 × 32 block), the band (two label and value pairs, a 160 × 16 meta line), the body in three columns (a 24 high head and six lines; a head and three 46 high fields; a head and two fields) — the type is not known yet, so the columns are generic |
| **Error** (the read failed) | `ErrorBanner` with the server's text and «Повторить» under the title «Рабочий элемент», in the card's body area |
| **Not found** (the item, the project or the organization) | the page: mod-003's frame of that level, unchanged. The dialog: the kit's `NotFound({ level, compact, hint, action })` in the card's body, `hint` «Проверьте адрес или закройте окно.», `action` «Закрыть»; `compact` is an `h2` with no tab title and no registering with the shell, so the host page's tab and navigation stay (tech-015 § 5.12) |
| **Clean** | «Сохранить» and «Отменить все изменения» disabled (the arrow too); the Save group wears the clean look of § 4.2 (`#F0F0F0`, `#959595`) |
| **Dirty** | both enabled; nothing else changes; the card keeps no «unsaved» mark |
| **Saving** | § 4.2: the main button's progress in place of its glyph, both parts of the Save group **inert with focus kept** (`aria-disabled`, not natively `disabled`), «Обновить» and «Отменить все изменения» `inactive`, every field `busy` (read-only look), `aria-busy`, the dialog not dismissible (its two icons inert) |
| **Saved** | the answer's values; «Изменён» from the answer; the notice «Изменения сохранены.»; the dialog stays |
| **Refused at a field** | the field's error line and frame (`aria-invalid`); focus and a scroll into view to the **first** such field in the order of § 4.6, the title first; editing a field clears its error (feat-022's «Дата начала» also clears «Целевая дата»'s) |
| **Refused at the form** | an `InlineNotice severity="error"` (`role="alert"`) in the shell's fixed `alert` area between the band and the body, so scrolling does not hide it; the input kept; the next save, «Обновить» or undo clears it |
| **Conflict** (`workitems.link.parent_changed`) | the item is read again; «Родитель» shows the stored parent with the code's text under it, the touched fields keep the user's values, the rest show the stored ones (feat-001 BR-H-4) |
| **Refreshing** | «Обновить» is `busy` (a spinner, `aria-busy`, inert) while the read runs; the notice (`useNotice`) «Данные обновлены.» (with «Несохранённые изменения остались на месте.» when edits stand); a failed read: the card's `InlineNotice severity="error"` with `apiErrorText` |
| **Read-only** (`readOnly`, BR-C-19) | every kit field read-only; the Save group and «Отменить все изменения» not drawn; «Обновить» and «Ещё действия» stay; an `InlineNotice severity="info"` with the host's text (default «Рабочий элемент доступен только для чтения.») in the same fixed area. Nothing sets it today; a component test renders it, so mod-016 and mod-005 find it ready |
| **Closing with edits** (D-4) | the shell's `guard`: a `ConfirmDialog destructive` «Закрыть без сохранения?», over the card, for «Закрыть», Esc and a click outside; Esc or «Продолжить редактирование» keeps editing. Browser Back and navigation are not guarded (mod-015 § 9.7) |
| **Switching item with edits** | the host's own `ConfirmDialog destructive` with «Перейти без сохранения?» (a switch is not a close, so not the shell's `guard`) |
| **Undo all** | `ConfirmDialog destructive` «Отменить все изменения?» |
| **Expanding with edits** | no question: the edits go with the page (§ 4.1) |
| **Copying** | the notice «Ссылка скопирована.», or «Не удалось скопировать ссылку.» when `navigator.clipboard` is missing or refuses |
| **Who sees what** | everyone the same until mod-016 (BR-M3-27); a non-member's answer is the API's not-found frame |

The confirmations use `ConfirmDialog` with `destructive`, the cancel focused
(`cancelAutoFocus`) so Enter never discards, and a `cancelLabel` of
«Продолжить редактирование» (the kit's, tech-015 § 5.10; the shell's `guard`
defaults to the same). A confirmation is a `dialog`, not an `alertdialog`
(tech-015 K-14); it dims the whole window, the card included, and Esc is its
cancel.

## 7. Narrow widths

The card's own width decides inside it; the window's decides the dialog.

| Width | What changes |
|---|---|
| dialog, window ≥ 1 024 | 90 % × 90 %, centred |
| dialog, window < 1 024 | full screen (`fullScreenBelow="md"`), no margin, square corners; the sizes below follow the card's width |
| card 720 to about 790 | the band's fixed blocks shrink toward `layout.card.bandBlockMin` 240 and the meta line ends in an ellipsis; the tab strip does not shrink (§ 4.3) |
| card < 720 (`layout.card.stackBelow`) | header row 3 wraps: the assignee takes its own line, the actions below it at the right; the band's blocks stack in one column with their labels at the left, the aside under them, and `bandMinHeight` is dropped |
| body ≥ 1 040 / ≥ 680 / below | three / two / one column (§ 4.5) |
| 640 (the supported floor) | one column; nothing cut; the page does not scroll sideways (E2E 27.7) |

Reflow below 640 is not claimed (mod-015 D-8).

## 8. Accessibility

**Roles and names** (BR-C-20: a control the form already has keeps both):

| Element | Role | Name |
|---|---|---|
| the dialog | `dialog`, modal | «<Тип> <ID>»; «Рабочий элемент <ID>» while loading or failing |
| the type line | `h1` on the page, the dialog's label in the dialog | «<Тип> <ID>» |
| the title | `textbox` | «Заголовок» |
| the assignee | `combobox` | «Исполнитель» |
| the Save group | `group` | «Сохранение»: the button «Сохранить», the menu button «Ещё варианты сохранения», the item «Сохранить и закрыть» |
| the three icon buttons | `button` | «Обновить», «Отменить все изменения», «Ещё действия» (its menu: «Ещё действия», the item «Копировать ссылку») |
| the dialog's icons | `button` | «Развернуть на страницу», «Закрыть» |
| the band's fields | `combobox` | «Статус», «Область» |
| the tab strip | `tablist`, `tab`, `tabpanel` | «Разделы рабочего элемента», «Детали», «Детали» |
| a section | `region` | its head: «Описание», «Критерии приёмки», «Шаги воспроизведения», «Сведения о системе», «Планирование», «Трудозатраты (часы)», «Классификация», «Связанная работа» |
| a field | `textbox` or `combobox` (a date by its label) | its label: «Приоритет», «Риск», «Оценка в пунктах», … feat-022's, «Родитель» |
| the links | `link` | «Открыть <ID>»; a child's identifier with its title |
| the children | `list`, named by its `h3` head | «Дочерние элементы (n)» |
| a confirmation | `dialog`, modal (tech-015 K-14: not `alertdialog`) | «Закрыть без сохранения?», «Перейти без сохранения?», «Отменить все изменения?» |
| the card's `InlineNotice` | `alert` (error), `status` (info) | its text |
| the loading frame | `status` | «Загрузка данных» |

**Keyboard.** The Tab order is the DOM's: the dialog's two icons, the title, the
assignee, the places kept, «Сохранить», its arrow, «Обновить», «Отменить все
изменения», «Ещё действия», «Статус», «Область», the tab, then the body's
fields in column order. A disabled button is skipped; a busy or inactive one
(a save runs) is not — focus stays where it was. Enter in the title saves.
Esc closes what is open on top — a list, a menu, a confirmation — and only then
the dialog. The menus' keys are mod-015 § 12.4's.

**Focus.** On opening, the dialog's frame; on a refusal, the first field with
an error (scrolled into view); after a confirmation closes, where it was; after
the dialog closes, the title that opened it. The 2 px accent frame is the focus
indicator of every control (≥ 3 : 1 on every surface, mod-015 BR-5); the hover
frame is decorative.

**Other.** Colour is never the only carrier: the state's dot has its name, the
type's icon its name in the type line. The capitals are CSS, the DOM text and the
accessible name are in the case written here. The notice (`useNotice`) is
`role="alert"`. `prefers-reduced-motion` removes the dialog's fade (mod-015
§ 12.7). The transparent backdrop (§ 4.1) changes nothing for the keyboard or a
screen reader: the page behind is still inert and the dialog is still modal.

## 9. Interface texts

Final here. Strings the card already has (the field labels, lists, hints,
placeholders and checks of feat-022 and mod-003, «Родитель» and «Связанная
работа» of feat-001, the assignee picker's messages, `apiErrorText`) are cited,
not copied: `lib/fields.ts`, `lib/rules.ts`, `lib/fieldValues.ts`,
`lib/cardTexts.ts` (the card's own texts) stay their one place; `AssigneeField`,
`ParentField` and `ChildItems` of the card read them there.

| Where | Text |
|---|---|
| Type line | «<Тип> <ID>», capitals by CSS; while loading or failing, the dialog's name «Рабочий элемент <ID>» |
| Dialog icons | «Развернуть на страницу», «Закрыть» (also the tooltips) |
| Title | label «Заголовок» (not drawn); placeholder «Введите заголовок» |
| Assignee | label «Исполнитель» (not drawn); empty «Не назначен»; unknown «Неизвестный пользователь» |
| Save group | «Сохранение» (group); «Сохранить»; «Ещё варианты сохранения»; «Сохранить и закрыть» |
| Icon buttons | «Обновить»; «Отменить все изменения»; «Ещё действия»; item «Копировать ссылку» (the names are also the tooltips) |
| Band | «Статус»; «Область»; «Изменён: <ДД.ММ.ГГГГ ЧЧ:ММ>», «Изменён: —» |
| Tabs | list «Разделы рабочего элемента»; tab «Детали» |
| Notices (6 s, bottom left) | «Изменения сохранены.» · «Данные обновлены.» · «Данные обновлены. Несохранённые изменения остались на месте.» · «Изменения отменены.» · «Ссылка скопирована.» · «Не удалось скопировать ссылку.» |
| Close with edits | title «Закрыть без сохранения?» · «Несохранённые изменения рабочего элемента <ID> будут потеряны.» · «Продолжить редактирование» · «Закрыть без сохранения» |
| Switch with edits | title «Перейти без сохранения?» · the same text · «Продолжить редактирование» · «Перейти без сохранения» |
| Undo all | title «Отменить все изменения?» · «Поля вернутся к сохранённым значениям. Несохранённые изменения рабочего элемента <ID> будут потеряны.» · «Продолжить редактирование» · «Отменить изменения» |
| Loading | «Загрузка данных» |
| Error | title «Рабочий элемент»; the server's text; «Повторить» |
| Not found, the page | mod-003's: «Рабочий элемент не найден» / «Проект не найден» / «Организация не найдена», «Проверьте адрес или вернитесь на главную.», «На главную» |
| Not found, the dialog | the same titles; «Проверьте адрес или закройте окно.»; «Закрыть» |
| Read-only | «Рабочий элемент доступен только для чтения.» (the host may pass its own: mod-016's, mod-015 § 10.3's for a deleted item) |
| Refusal at the form | the server's `detail` |
| Parent changed | feat-001's «Родитель уже изменён другим участником. Данные обновлены — проверьте и повторите.» on «Родитель» |

## 10. What the card relies on from the kit

The kit is tech-015's (`../tech-015-ui-kit/design.md`, with the guide
`docs/conventions/ui-kit.md`). This section lists what the card relies on and
points at the part's contract; it repeats no part's spec. **Where it and the kit
differ, the kit wins** (tech-015 D-5). The map of this task's earlier working
names (`InlineField`, `ToolbarIconButton`, `Notice`, `StateDot`, `CardHeaderBar`'s
`trailing`, …) and every rename are in tech-015 § 13.

The eleven changes this section once asked of the kit (the transparent backdrop,
the fixed band blocks, the thin glyphs, the label-column adornment, the text under
its head, the tab indicator, …) were taken in on 2026-10-12 (tech-015 K-15), and
none is open. The card asks for nothing more: the 9 px of KD-12 would need space
below the text in `InlineTextArea`, which the kit does not have, and the card
records the difference instead (§ 2).

The rule of the kit (tech-015 § 9, `ui-kit.md`): a screen is built from the kit
only. The card writes no MUI field, button, menu, dialog, `Alert`, `Snackbar`,
`Tabs`, `Skeleton`, `CircularProgress`, `Paper` or `Table`, no heading element of
its own and no `useMediaQuery` for the columns. The guard lists the `workitems`
files this task rewrites with `until: 'feat-029'`; each entry is lowered or removed
in the commit that rewrites its file.

| Kit part | What the card relies on | Kit § |
|---|---|---|
| `InlineTextField`, `InlineNumberField`, `InlineDateField`, `InlineSelectField`, `InlinePickerField`, `PersonField`, `InlineTextArea` | `label` always (drawn or not), `labelPlacement` `top` / `start` / `hidden`, `size` `body` / `title`, `value` and `onChange(value)`, `placeholder`, `hint`, `error`, `required`, `readOnly`, `busy`, `emptyText`, `startAdornment`, `inputProps`, a ref that focuses; select `options`, `emptyLabel`; picker `options`, `loading`, `endAction`, `renderOption`; number `inputMode`; date `min`, `max`, `onIncomplete`; text area `minRows`. Behaviour: a name that is the label only, no size change between states, the hint on the label's line, the start adornment in the label column by default, a text area with `labelPlacement="hidden"` is a text under its head, the placeholder always the attribute | 5.7 |
| `Section`, `SectionHead` | `title`, `headingLevel`, `count`, `divider`; a `region` named by its head, the `sectionRule` rule, no margin of its own | 5.8 |
| `ColumnLayout` | `main`, `side`, `extra`; container queries on its own width (`layout.columns`); the 32 px between sections | 5.8 |
| `CardHeaderBar`, `TypeStripe`, `TypeLabel eyebrow` | `appearance`, `typeLabel`, `identifier`, `headingLevel`, `headingId`, `title`, `meta`, `actions`; the bar, the three rows, the capitals by CSS; no `trailing` slot | 5.9, 5.4 |
| `PropertyBand` | `blocks`, `meta`, `tabs`; the fixed blocks, 76 px with empty rows, stacking below `stackBelow` | 5.9 |
| `BoxedTabs`, `BoxedTabPanel` | `idBase`, `aria-label`, `value`, `onChange`, `tabs`; the 2 px indicator, label-wide, 4 above the strip's bottom; a single tab is a valid strip | 5.6 |
| `DialogShell size="card"`, `PanelShell` | `open`, `onClose(reason)`, `labelledBy` or `label`, `busy`, `onExpand`, `guard`, `fullScreenBelow`, `onExited`, `header`, `band`, `alert`, `children`; the `invisible` backdrop that still catches the outside click, the two icons, the 90 % Paper and the `kit-card` container; `PanelShell`'s `trail` | 5.10 |
| `SplitButton`, `IconAction size="bar"`, `MoreActionsMenu` | `label`, `icon`, `tone`, `type`, `busy`, `disabled`, `groupLabel`, `menuLabel`, `items`; `inactive`; the clean disabled look, the inert busy state with focus kept, the arrow's name free of the main label's word, the menus' keys | 5.2, 5.3 |
| `ConfirmDialog` | `cancelLabel`, `destructive`, `cancelAutoFocus`; role `dialog` | 5.10 |
| `useNotice`, `InlineNotice`, `ErrorBanner`, `CardSkeleton`, `NotFound`, `InlineEmpty` | `show(text)`; `severity`, `action`; `heading`; `compact`, `hint`, `action`; one `status` «Загрузка данных» | 5.12, 5.5 |
| `usePageHead` | `{ title, ancestors }` → `{ trail }`, handed to `PanelShell`'s `trail` | 5.6 |
| `CategoryMarker size="field"`, `StateLabel`, `UserAvatar`, `PersonLabel` | the dot beside its name, never alone; the 24 px avatar | 5.4 |
| The thin icons | `ThinExpandIcon`, `ThinCloseIcon`, `ThinSaveIcon`, `ThinChevronDownIcon`, `ThinRefreshIcon`, `ThinUndoIcon`, `ThinMoreVerticalIcon`, `ThinPersonIcon` | 5.1 |
| Tokens | the table of § 3 | 3 |

## 11. What this supersedes

- mod-003 § Page: a work item's form, and mod-015 § 10.3 — the band with a 4 px
  stripe and a `subtitle2` first line, the strip of three outlined pickers,
  the `InputBase` 20 px title, «Сохранить» at the band's right end — for the
  header, band and tab above; and the outlined `TextField` controls of
  feat-022 § Controls and copy for the kit's inline fields (the labels, lists,
  limits, hints and checks stay).
- feat-022 § The form's sections — the 8/4 grid at ≥ 1 280 px and one column
  below, by the window — for the three/two/one columns by the card's width
  (BR-C-14); «Связанная работа» is the third column, not the last of the
  second. The sections and their fields per type stay.
- mod-015 § 10.3's «Ещё действия → Удалить»: «Удалить» waits for mod-005.
- mod-015 § 9.7's no leave guard, **for this dialog only** (D-4, Q-1 (a)); pages
  and navigation stay unguarded.
- tech-010's m1 (the draft copies the item on the first edit) for the card:
  the draft holds the touched fields only (BR-C-16).
- mod-003's `bandSx`, `gridSx` and `FormLoading` in `WorkItemFormPage.tsx`;
  `FormSection.tsx` becomes the kit's `Section`, and the slice's `Notice`, `Marks`
  and `HeaderChrome` go to the kit's `useNotice`, marks and `usePageHead`
  (tech-015 § 8.1).
Nothing changes in any `api-contract.md`, in the backlog's or the board's
own layout, or in `lib/fields.ts`.

## 12. Hand-over notes for the frontend developer

- **Files** (`features/workitems`): `components/WorkItemCard.tsx` (the card),
  `components/WorkItemCardDialog.tsx`, `hooks/useWorkItemEdits.ts` (the draft
  of touched fields: `edits`, `valueOf`, `changes`, `isDirty`, `rebase`,
  `undo`, built on `lib/draft.ts`'s comparisons), `hooks/useWorkItemDialog.ts`
  (the `?workitem` parameter, `cardDepth`, `open`, `close`, `switchTo`),
  `pages/WorkItemFormPage.tsx` thinned to read, key and host. The barrel exports
  `WorkItemCardDialog` and `useWorkItemDialog`; `features/boards` imports them
  and sets its title links' `onClick` (a plain click only) in `BacklogRow` and
  `BoardColumns`, keeping `href` (feat-027's E2E reads it).
- **Order of work.** The kit first: tech-015's parts 1–6 (tokens and the guard,
  icons and actions, marks, fields, the card's parts and shells, feedback) are
  what the card waits for, on the kit's branch; the kit merges as **one squash
  before this task** (tech-015 D-5) and this branch is rebased onto the main that
  holds it. Then the card as the page (US-2..US-5, US-7); then the dialog and the
  hosts (US-1, US-6); the E2E last. Each pass ends green (`scripts/verify.sh`).
- **The form.** There is none (settled in pass A; review R1-5). The kit's shells draw
  `header`, `band`, `alert` and the panel as separate areas of one Paper and give
  no form element to wrap them (tech-015 § 5.10), and a dialog is portaled, so a
  `<form>` outside the shell would not own the controls in the DOM. So «Сохранить»
  is a plain button whose `onClick` saves, and Enter in the title saves through the
  title field's `onKeyDown` (`inputProps`; not while an IME composes, BR-C-8, US-4).
  Enter in the other single-line fields — «Оценка в пунктах» and the like — does
  nothing, where a form would have submitted. Names, states and tests are as written.
- **Tests.** Vitest per component of the card: its rows and their measures read
  through the theme tokens, the slots test of § 4.8, the touched-only draft (a
  changed-back value, a rebase, a carried draft against a newer read), the
  read-only look, the three confirmations, `useWorkItemDialog`'s history rules
  (push depth, close, replace for an address), the host refresh, a text's
  `labelPlacement="hidden"` with its name from the head and no second «Описание»
  label in the DOM, `minRows` 2 on every text, the assignee's `ThinPersonIcon`
  while empty and `UserAvatar` once assigned. MSW per api-contract. **Pinned by
  the kit, not again here** (tech-015 § 12): the field states and names, the tab
  indicator's 2 px and 4 px (`BoxedTabs.test.tsx`), the card backdrop `invisible`
  and still closing on a click (`DialogShell.test.tsx`), `cardLabel`'s contrast
  (`contrast.test.ts`), the tokens' relations (`theme.test.tsx`).
- **E2E.** File 27 is the card's, 28 the kit showcase's (tech-015 § 11): the kit
  merges first, so neither renumbers. **27.2** measures, in the browser at
  1 536 × 790, ratio 1.25 (± 2 px): the backdrop's computed `background-color` is
  transparent (and the page behind is as bright as before the dialog opened);
  «Область»'s label is 303 from the dialog's left edge and both band values 88
  from their labels; «Новая»'s dot is 19 px before its name; the refresh button's
  centre is 110 and the ✕'s 21 from the dialog's right edge; the tab indicator is
  2 px thick with its foot 4 px above the band's lower edge; «Описание»'s text box
  starts 20 below the section's rule (the first line's cap top about 30) and its
  text 16 from the column's edge, and with a text of up to two lines the next
  section's head starts 102 below that rule (its cap top about 107, KD-12). The
  other differences are KD-1..KD-6 and KD-8..KD-13 of § 2 and are not asserted.
- **Visual check.** Before the code review, the developer lays the card at
  1 536 × 790 (ratio 1.25) beside the reference and records the differences
  against § 2 **and its list of known differences** (anything else is a defect,
  feat-027 D-8's lesson); the screenshots stay outside the repository.
- **feat-028** moves teams across projects: the dialog reads the item with the
  page's project today; the owner of feat-028 re-checks that.
- **feat-030** (queued, deps feat-029) rebuilds «Связанная работа» as the
  reference's block (§ 4.6, D-3): it finds the section as `Section` + `ParentField`
  + the children's `SectionHead` and `ChildItems`, with BR-C-20's names and E2E 24 as the
  tests it moves.
