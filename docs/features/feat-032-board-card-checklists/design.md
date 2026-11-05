# Design: Board card checklists (feat-032)

Stage 3 artifact. The rules (BR-C-n) and the scenarios (US-1..US-3) are in
`spec.md` and `stories/`; this file is the layout, the MUI components, every
state and every Russian string of the card's new part, and nowhere else.
Everything the spec already fixed (counter order, rows, names, announcements) is
restated only where a layout decision hangs on it; the spec's copy table is
kept verbatim in § 6.

- Base: mod-015's theme and notices; mod-006's «Канбан» and its card;
  feat-027's design for the inline add field (§ 4.8), the hairline icons
  (§ 4.4), the notices and the focus rules. A neighbour's pattern is reused, not
  redrawn: the field, the busy state, the refusal at the field and the
  announcements are feat-027's with this card's words.
- **Clickable prototype:** `prototype/card-checklists.html` (one file, no
  dependencies): counters, toggles, ticking, adding, the drag guard. Its look is
  approximate; this file decides the structure.
- The reference is `research/reference-card.png` (never named, D-1). Data of
  every example is US-1's: «Платформа», `MSK`, MSK-US-2 with its children.

## 1. Screen map

No new screen, no new route, no new theme change but one icon token (§ 2). One
screen changes: «Канбан» (`…/teams/:teamId/boards/:level`). Its head, columns,
column counts, «Новый элемент», the card menu and the card move are unchanged.

| Part | Stories |
|---|---|
| The card's **counters row** (§ 3) | US-1 |
| The **expanded lists** and their rows (§ 4) | US-1, US-2 |
| The **add field** in the task list (§ 5) | US-3 |
| States, keyboard, drag guard (§ 7–§ 9) | US-1..US-3 |

## 2. The one theme addition: the `testcase` token (BR-C-11)

| What | Value |
|---|---|
| `APPEARANCE_TOKENS` | gains `'testcase'` (`app/theme/tokens.ts`); feat-031 adopts the spelling |
| Icon (`workTracking.ts`, `TYPE_ICONS`) | `@mui/icons-material/Science` — the flask, as the reference's. One lookup, so every place that draws a type draws it |
| Colour (`typeColours.testcase`) | `#3B3A39`, a near-black neutral like the reference's flask: 11.4:1 on white, so it needs no entry in the contrast test's exemption list and is tested strictly |

Decision (not fixed by the spec): the colour. `workTracking.test.tsx` pins the
icon and the value; `theme.test.tsx` and the contrast test gain the token with
the rest.

## 3. The card and its counters row

### 3.1 Layout

The card keeps what it has (stripe, type icon, ID, title link, «Ещё действия»,
the state, the assignee with the priority caption) and gains, at its bottom, one
block, **`CardChecklists`**, drawn only when the card has a checklist
(BR-C-2). A card without one is as tall as before.

```
+-+-----------------------------------------+
|S| [story] MSK-US-2  Повторная отправка  (⋯)|   stripe S = 4 px type colour
|T| ● Активна                               |
|R| (АС) Анна Смирнова   Приоритет: 2       |   unchanged (BR-C-6, card side)
|I|                                         |
|P| [clip 1/3] [bug 1/2] [flask 2]          |   counters row  (§ 3.2)
|E| +--------------------------------------+|
| | | [+ Добавить задачу]                  ||   task list, open  (§ 4)
| | | [x] [clip] (АС) be                   ||
| | | [ ] [clip] (○)  fe                   ||
| | | [ ] [clip] (БП) qa                   ||
| | |                  ^                   ||   chevron closes the list
| | +--------------------------------------+|
+-+-----------------------------------------+
```

### 3.2 Components

| Block | MUI | Notes |
|---|---|---|
| Row | `Box role="group"` `aria-label="Вложенные элементы: <ID>"`, `display: flex`, `flexWrap: wrap`, `gap: 0.5`, `mt: 0.5` | one row, wraps at the column's 280 px if the three counters do not fit |
| One counter | `Button variant="text" color="inherit" size="small"`, `startIcon={<TypeMark appearance compact />}` (16 px, the type's colour), label «1/3» in `typography.body2`, `aria-expanded`, `aria-controls` the list's id, `aria-label` by § 6 | a toggle (BR-C-4). **Not `ToggleButton`**: that announces `aria-pressed`, the spec asks `aria-expanded`. Neutral, not the accent (frontend.md "One accent"); `minWidth: 0`, `px: 1`; `aria-expanded="true"` draws `action.selected` as the background (the reference's grey on the open counter) — a second carrier beside the announced state |
| Counter values | «<done>/<total>» for task and bug, «<total>» for test case | `<done>` and `<total>` are what the card shows now, so an optimistic tick (§ 7.2) moves the counter at once |
| Order | task, bug, test case; only the kinds present, the task counter always on a story (BR-C-2) | the card's own type decides the task counter's presence by feat-027's `CHILD_APPEARANCES` (`story → task`), recognised by appearance, never by name |

The `<тип>` of a counter is the process's own name of that kind. On a counter
with children it is read from the children (`type.name`). **On a story's «0/0»
no child exists to read it from**: it comes from the process definition, the
way feat-027's `addEntriesOf` finds «Задача» for the add field; with the process
not read yet the counter is not drawn yet (as feat-027's «+» is not) rather than
drawn with a guessed word.

The counter shows no colour of its own for «all done»: the number is the carrier.

## 4. The expanded lists

### 4.1 Layout and components

An open counter's list stands under the row, **in kind order, not in opening
order** (BR-C-4, US-1). Each list is its own block; several may be open.

| Block | MUI | Notes |
|---|---|---|
| List frame | `Box id={listId}` `bgcolor: background.default`, `borderRadius: shape.borderRadius`, `p: 0.5`, `mt: 0.5` | the reference's bordered region, as a quiet panel inside the white card. No new token |
| The list | `List dense disablePadding` (a `ul`) `aria-label="Список: <тип> (<ID>)"` | one list per kind |
| Add line (task list of a story only) | `ListItem disableGutters` holding the button of § 5 | first line of the list, above every row (BR-C-9) |
| A row | `ListItem disableGutters` (`li`), `display: flex`, `alignItems: center`, `gap: 0.5`, `minHeight: 32 px` via the theme's spacing (`spacing(4)`), hover `action.hover` | `data-child="<ID>"` |
| Checkbox | `Checkbox size="small"`, `sx={{ p: 0.5 }}` (the default 9 px padding would make a 38 px box; 28 px stays a target above 24) | tasks and bugs only; **absent on a test case's row** (BR-C-5), where the row simply starts with the icon — a list holds one kind, so the icons still line up |
| Type icon | `TypeMark appearance compact` (16 px), `aria-hidden` | the child's own appearance |
| Assignee avatar | `AssigneeAvatar` (§ 4.2), 24 px | the reference's avatar between the icon and the title |
| Title | `Link component={RouterLink} to={workItemPath(…)}` `variant="body2"`, `flex: 1 1 auto`, `minWidth: 0`, `overflowWrap: 'anywhere'`, `title="<ID>: <заголовок>"` | opens the item as the card's own title does (BR-C-5); like the card's title link, so the page has one look for a link |
| List chevron | `IconButton size="small"` with a new `ThinChevronUpIcon`, centred under the rows, `aria-label` «Свернуть список: <тип> (<ID>)» | sized as feat-027 § 4.4 (`width/height: 24, p: 0`, glyph 20 px, `text.primary`); `ThinIcons.tsx` gains `ThinChevronUpIcon`, path `M4.5 15.5 12 8l7.5 7.5`, same stroke |

A done row is not struck through and not greyed: the checkbox is the carrier,
and the title keeps its link look. No row shows the state's name or a «…» menu
(BR-C-5, D-4 OQ-5).

### 4.2 `AssigneeAvatar`

The card's existing `UserAvatar` is `aria-hidden` (the name stands beside it).
A row has no name beside the avatar, so the avatar carries it: a new exported
component `AssigneeAvatar({ assignee })` next to `AssigneeName` in
`features/workitems/components/Marks.tsx`, exported from the slice's
`index.ts` (the slice's public entry; boards may not reach into internals) —
`Avatar` 24 px with the initials of `shared/lib/initials`, or
`PersonOutlineOutlined` when nobody is assigned or the directory does not know
the name; `role="img"`; `aria-label` and a `Tooltip` with the same text:

| Assignee | Avatar | Name and tooltip |
|---|---|---|
| a known person | initials «АС» | «Анна Смирнова» |
| nobody | neutral person icon | «Не назначен» |
| not in the directory | neutral person icon | «Неизвестный пользователь» |

The text comes from `assigneeLabel` (`workitems/lib/assignee.ts`, exported with
it). It is not a tab stop: the tooltip is for the pointer, the name for assistive
technology (BR-C-6).

### 4.3 What a counter and a list do together

- Press on an open counter closes its list; focus stays on the counter.
- The chevron closes the list and **moves focus to its counter** (US-1).
- Opening or closing moves nothing else: the card stays in its column and
  keeps its place (BR-C-4).
- The set of open lists is **held by `BoardColumns`**, keyed by card id and kind
  (not by the card's component, which is redrawn in a new column after a move
  and by every re-read), so it survives the read and the move, and ends with the
  visit: `BoardColumns` is already keyed by level, `BoardPage` by team, so a
  reload, another level, another team or coming back from a form starts
  collapsed (BR-C-4; no storage, no query cache).

## 5. The add field («Добавить задачу»)

Only on a card whose type offers a task (a story, BR-C-9), as the first line of
the task list.

| Element | MUI | Notes |
|---|---|---|
| Button | `Button variant="text" color="inherit" size="small"`, `startIcon={<AddIcon />}`, visible «Добавить задачу», `aria-label` «Добавить задачу (<ID>)» | neutral text button with a leading icon (the reference's grey «+ Add Task»); the accessible name contains the visible text. The text is fixed (BR-C-9), not the process's type name |
| Field line | below the button, in the same list: `TypeMark appearance="task" compact` and `TextField variant="outlined" size="small" hiddenLabel fullWidth` | feat-027 § 4.8's field as it is: 28 px high, `typography.tableCell` text, 1 px `primary.main` border, `placeholder` «Введите заголовок», `aria-label` «Заголовок: <Тип> (<ID>)» (the card's ID), single line. The icon is offset by one checkbox slot (28 px) so it lines up with the rows' icons |
| Busy | `readOnly`, `aria-busy="true"`, `CircularProgress size={16}` as the end adornment | from Enter until the board has been read again |
| Refusal | `error`, `aria-invalid`, text in the `FormHelperText` (`role="alert"`) | at the field, the title kept, focus in the field (below) |

Behaviour is feat-027 § 4.8's, with the card's differences:

- **Opening:** the button opens the field under it, focused; the button stays
  where it is. Pressing it while the field is open returns focus to the field.
  One field open per board, none on another card at the same time: opening it
  with an empty field elsewhere closes that one first; with text typed elsewhere,
  focus goes back to that field and nothing opens (feat-027 § 4.8).
- **Enter** creates one task: parent the card's item, one `requestId` made at
  the first Enter and kept for every retry of this opening, a double Enter makes
  one task. A blank title is no refusal: the field closes, nothing is sent, no
  text. A title over 255 characters: «Не более 255 символов.» at the field
  (mod-003's `titleError`), nothing sent.
- **Escape** (and leaving the field empty) closes it, nothing sent; focus goes to
  «Добавить задачу». Escape is stopped there, so it cancels nothing else.
  Leaving the field with text typed keeps it open.
- **While another write is saved** (a tick, a move, another creation) Enter does
  nothing and the text is kept (BR-C-8); it works again after the read.
- **Success:** the field closes; the board is read again; the task is the
  list's last row, not ticked; the task counter's total grows by one; the
  notice «Рабочий элемент <ID> создан.»; **focus on «Добавить задачу»**.
- **A list closed over an open field:** the field is not drawn but not lost — the
  card's state holds its title and `requestId` (feat-027 F-7), and opening the
  list draws it again with both, **without taking focus**. A refusal that
  arrives while it is folded opens the list and shows the refusal at the field.

## 6. Interface texts (final; the developer copies them)

`<ID>` is the card's identifier unless the row's child is named, `<тип>` the
process's own name of the type. New with this design are marked ◆; the rest is
the spec's table and feat-027's.

| Where | Text |
|---|---|
| ◆ Counters group's name | «Вложенные элементы: <ID>» |
| Counter, visible | «1/3» (task, bug), «2» (test case) |
| Counter, accessible name (D-5: starts with the visible text) | «<n>/<m> — <ID>, <тип>: выполнено <n> из <m>» (task, bug), e.g. «1/3 — MSK-US-2, Задача: выполнено 1 из 3»; «<m> — <ID>, <тип>: <m>» (test case), e.g. «2 — MSK-US-2, Тест-кейс: 2»; `aria-expanded` |
| ◆ List's name | «Список: <тип> (<ID>)» |
| List chevron | «Свернуть список: <тип> (<ID>)» |
| Checkbox | «<ID ребёнка>: выполнено», checked by the state |
| Avatar on a row | the person's name, «Не назначен», «Неизвестный пользователь» (§ 4.2) |
| Row's link tooltip | «<ID ребёнка>: <заголовок>» |
| Add button | visible «Добавить задачу»; name «Добавить задачу (<ID>)» |
| Field | «Заголовок: <Тип> (<ID>)» (`aria-label`), placeholder «Введите заголовок» |
| Field refusals | «Не более 255 символов.»; for a refused create, the code's own text as feat-027 § 4.8 (`workitems.link.parent_not_found` «Родитель не найден в этом проекте.», `workitems.link.item_deleted` «Удалённый рабочий элемент нельзя связывать.»); no field named or no answer: «Проверьте правильность заполнения полей.» |
| Announcement (`role="status"`, at the tick) | «<ID>: статус «<состояние>».» — «MSK-TASK-4: статус «Закрыта».» |
| Notice, a refused tick (`Snackbar`, `Alert` error, 6 s, bottom left) | the server's `detail` when it answers with a code; otherwise «Не удалось сохранить статус. Попробуйте ещё раз.» |
| Notice, a created task | «Рабочий элемент <ID> создан.» (mod-003's plain notice) |

No empty-list text, no per-card loading text, no new error code. An empty counter
(«0/0») is its own explanation, and a task list with no rows is the button alone.

## 7. States

### 7.1 Screen-level (the board; unchanged)

| State | What the user sees |
|---|---|
| Loading | the board's existing column skeletons. **The checklists come with the board's answer** (BR-C-10): there is no per-card or per-list loading, and no request when a list opens |
| Empty board | the existing frame «На доске нет рабочих элементов» |
| Board read error | the existing `ErrorBanner` with «Повторить»; a failed re-read keeps the board and its open lists, with the banner above |
| Not found / auth | as today |
| Permissions | none until mod-016 (spec § Roles): every signed-in user sees every control; no hidden or permission-disabled state is designed. mod-016 decides what a missing code does |

### 7.2 The checklist

| State | Counter | List and rows |
|---|---|---|
| No children, a story | «0/0», `text.secondary` number | the list has the button alone; no text |
| No children, other card | no row at all | — |
| Collapsed (first state) | as above, `aria-expanded="false"` | not drawn |
| Open | `aria-expanded="true"`, `action.selected` | rows in order of the child's number |
| Removed child | in no counter | in no list (BR-C-1, -3) |
| Resolved bug | counted in the total, not in the done count | its checkbox is empty |
| Type without a Completed state | — | that row's checkbox is `disabled` (static; it never has focus to lose) |
| The process read has not answered, or has failed | a story's «0/0» is not drawn (§ 3.2) and «Добавить задачу» is absent | every row's checkbox is `disabled`, out of the Tab order, and stays so if the read failed, with no notice: a tick needs the process to know the target state (BR-C-7), so none can be computed. Accepted (code review round 1, m-1); the rows, links and counters work |
| **Tick or untick in flight** | the new value at once, the name follows | the row shows the new checked state at once (optimistic, as the card's move is), the announcement is made at once |
| **Write in flight** (a tick, a move, a creation; BR-C-8) | counters still open and close | every checkbox, every «Добавить задачу» and every card menu is `aria-disabled="true"`, `text.disabled`, **still focusable** (like the menu buttons today, so a focused checkbox keeps focus through the read); the checkbox's change is ignored; Enter of an open field does nothing; no card is draggable |
| **Refused or unanswered tick** | the counter returns | the row returns, the board is read again, then the error notice; the notice corrects the announcement made at the tick |
| **Refused create** | — | refusal at the field (§ 5) |
| A re-read while open | unchanged | the open lists stay open; the rows are replaced in place by the child's id, so focus on a checkbox stays |
| The focused row left a re-read (a child removed or moved elsewhere) | — | focus goes to its list's counter |
| A tick on a child removed since the read | not designed: BR-C-7 leaves it to mod-005; this task adds no check and no special refusal state. Whatever the server answers goes through the two refusal rows above |

A ticked child that is also a card (a bug under a story) moves column **at the
read, not at once** (BR-C-8, US-2): only the checklist row changes at the tick.

`aria-disabled` rather than `disabled` is the point: MUI's `disabled` checkbox
leaves the Tab order and drops focus to the page, which would break «focus stays
on it after the read» (US-2). The change handler returns without writing while a
write is saved; the checkbox is controlled, so the browser's own toggle is put
back.

### 7.3 Narrow

The window is supported down to 640 CSS px (frontend.md); the board scrolls
sideways inside its container and the column stays 280 px, so a card never
narrows. The counters row wraps; a long title wraps (`overflowWrap: anywhere`). A
card with up to 200 children has no inner scrolling and no virtualization: the
card grows with its list, and the 100 ms budget is met by drawing rows once
(`memo` per row).

## 8. Keyboard and focus

Tab order inside a card, in document order: title link, «Ещё действия: <ID>»,
the counters, then for each **open** list: «Добавить задачу» (a story's task
list), the field when open, each row's checkbox and title link, the chevron.
The avatars and the stripe are not stops; a collapsed list has no stops.

| Control | Keys |
|---|---|
| Counter | Enter, Space toggle (`Button`) |
| Checkbox | Space ticks (native); focus stays on it after the read |
| Title link | Enter opens the item |
| «Добавить задачу» | Enter, Space open the field |
| Field | Enter creates; Escape cancels |
| Chevron | Enter, Space close the list |

Focus after each action: a counter toggled → on it; the chevron → on the counter
(US-1); a tick → stays on the checkbox; a cancelled or created field → on
«Добавить задачу»; a refusal → in the field. The focus ring is mod-015 § 2.6's,
unchanged. Every change of a tick is announced through the page's existing
`role="status"` region (mod-015 § 12). State is never colour alone: the
checkbox, the number and `aria-expanded` carry it.

## 9. The drag guard (BR-C-12)

The card is `draggable` today, and a `draggable` ancestor takes a drag that
starts on its controls. A press or a drag that starts inside the checklist
block must not start one. The mechanism, to keep the contended card file's diff
small (feat-029 rewrites it too, D-2):

- `CardChecklists` is its own component and file, mounted at the card's bottom;
  its root carries `data-no-drag`. The only edits to `BoardCard` are one
  `onMouseDownCapture` that records whether the pressed element is inside a
  `[data-no-drag]`, and a line in `onDragStart` that `preventDefault()`s and
  returns when it was (the existing `saving` guard is the model). The flag is
  cleared at `dragend` and at the next press.
- Not `draggable={false}` on the controls: that does not stop an ancestor's drag.
- The add field's text can be selected and typed (spec BR-C-12). If a
  Firefox build refuses text selection in an input under a `draggable` ancestor,
  the fallback is `draggable={false}` on that card **while its add field is
  open**; the guard above stays. The E2E runs in Chromium only.
- The rest of the card — stripe, title area, state, assignee, padding — drags as
  in mod-006 US-5, dropping onto a column with the existing rules; while a write
  is saved no card drags, as now.

## 10. For the developer

- **New files:** `features/boards/components/CardChecklists.tsx` (counters row,
  lists, rows, the add field), a slice-local `useChecklistWrite` hook if the
  state change and the create need one; `ThinChevronUpIcon` in `ThinIcons.tsx`.
- **Touched:** `BoardColumns.tsx` (the open-lists state, the two drag-guard
  lines, the mount; whichever of this and feat-029 merges second rebases),
  `workitems` (`AssigneeAvatar`, exports of `assigneeLabel`, `UNKNOWN_USER_LABEL`
  and the avatar from `index.ts`), `app/theme` (§ 2), the board's API types and
  fixtures (each card's children, additively, BR-C-10).
- **Write path:** a tick or untick is the existing `useSetWorkItemState` with the
  child's Completed or initial state (BR-C-7); the creation is feat-027's with
  `parentId` the card's item. Both run through the board's single in-flight guard
  and read the board again, as a move does; nothing new in the API layer but the
  read's field.
- **Test hooks:** the group `data-checklist="<ID>"`, each counter
  `data-counter="task|bug|testcase"`, each row `data-child="<ID>"`; otherwise by
  the names of § 6.
- **No new dependency**, no `api-contract.md` change beyond the additive read.

## 11. Decisions the spec did not fix

| # | Decision | Where |
|---|---|---|
| 1 | The `testcase` colour is `#3B3A39` | § 2 |
| 2 | The counters are `Button`s with `aria-expanded`, not `ToggleButton` | § 3.2 |
| 3 | The lists sit in a `background.default` panel, no new token | § 4.1 |
| 4 | New names: the counters group «Вложенные элементы: <ID>» and the list «Список: <тип> (<ID>)» | § 6 |
| 5 | `aria-disabled` (focusable) for the write-in-flight state of checkboxes, not `disabled` | § 7.2 |
| 6 | No empty-list text; the story's «0/0» list is the button alone | § 7.2 |
| 7 | No inner scroll or virtualization up to 200 children | § 7.3 |
| 8 | The drag guard is a mouse-down flag plus `dragstart` refusal, the whole card staying the drag source | § 9 |
| 9 | `AssigneeAvatar` is added to the `workitems` slice's public entry | § 4.2 |

## 12. Questions for the human (gate-2)

No blocking question. The earlier note on WCAG 2.5.3 (label in name) is
resolved by D-5: a counter's accessible name starts with its visible text (§ 6),
so voice control by «1/3» hits it.
