# Spec: Boards and backlogs, the MVP cut (mod-006)

## Context and goal

A team needs two views of its own work: an ordered backlog per level, to
agree what comes next, and a board per level, to move work through its
states without opening every item. This is the short-route cut of module M4
(`status.md` D-1, D-2): rules from doc-003 (BR-M4-n, US-1, US-2, US-4),
doc-010 (D-4..D-7) and doc-001 (BR-3, BR-11, BR-12), screens from mod-015's
patterns (D-4), built on mod-001's process, mod-002's teams and areas and
mod-003's work items. Per doc-010 PR-8, doc-003's BR-M4-n keep their numbers
where restated, new rules start at BR-M4-23, bare BR-n is doc-001's.

## Scope

### In scope (D-2)

Per team and per level «Эпики», «Фичи», «Истории» (doc-010 D-4):

- which work items a team's backlog and board hold (§ Model, BR-M4-5);
- the ordered backlog: its stack rank, reordering by pointer and by
  keyboard, a place for the items that predate it (BR-M4-6, -8, -13,
  -23..-26);
- the board: columns per (type, state) (doc-010 D-4, D-5; BR-M4-2) and a
  card move that changes the item's state (BR-M4-3, -18);
- creating a work item from a team's backlog or board with the team's
  default area (doc-010 D-7; BR-M4-21);
- the «Бэклоги» and «Доски» entries of the shell's team-level navigation
  (mod-015 § 4.1; OQ-1);
- a deleted team's backlogs and boards answering «Команда не найдена», its
  items passing to the first team's (doc-010 D-6; BR-M4-20) — this follows
  from mod-002 BR-M1-18 and the area rule with no work of its own.

### Out of scope

| Item | Carried by |
|---|---|
| The client-visible concurrency token, its ADR and the stale-move notice «Порядок изменён другим участником — список обновлён» (doc-001 BR-11; mod-003 D-2 expected them here) | follow-up line at gate-3, `feat-NNN-work-item-concurrency-token` |
| The backlog hierarchy: expand/collapse, parent mapping (doc-003 US-3, BR-M4-9..-11) | feat-004-backlog-hierarchy |
| The filter bar on the board and the backlog (BR-M4-22) | feat-013-board-backlog-filter |
| Board settings: «Настройки доски» (mod-015 § 9.6), custom columns, column collapse (mod-015 § 10.4), card fields and styling, swimlanes, WIP limits, several boards per level | doc-011-release-2-scope |
| Deleted items: hidden, restored to their place, their moves refused (BR-M4-16, -17) | mod-005-work-item-delete-restore (D-5) |
| The «Итерация» column and the «Планирование» pane (mod-015 § 10.5) | mod-007-iterations, feat-009-iteration-planning-pane |
| The «Задачи» backlog (doc-003 BR-M4-1, US-2) | follow-up line at gate-3, `feat-NNN-tasks-backlog` (OQ-3) |
| Reordering inside a board column (doc-003 BR-M4-15, its drag half) | follow-up line at gate-3, `feat-NNN-board-in-column-reorder` (OQ-4) |
| Приоритет and Теги on cards and rows (BR-M4-14) | mod-003's follow-up lines `feat-NNN-work-item-priority`, `feat-NNN-work-item-tags`, each amended at gate-3 to show its field here |
| Live push of other users' moves; creating at a chosen position (doc-003 NFR, BR-M4-8) | doc-011-release-2-scope |
| Membership, roles, a reader's read-only board (doc-003 § Roles) | mod-016-organization-access |

## Access

D-3: until mod-016, mod-002 D-a unchanged (BR-M4-27).

| Who | What they may do |
|---|---|
| Any signed-in user | in every active organization and active project: open every active team's backlogs and boards, reorder a backlog, move a card, create a work item from either |
| Anonymous caller | nothing: every page shows mod-015's auth card, every request answers 401 |

## Stories

| id | Title | depends-on | File |
|----|-------|------------|------|
| US-1 | A user sees a team's backlog per level | - | `stories/US-1-team-backlog-per-level.md` |
| US-2 | A user reorders a team's backlog | US-1 | `stories/US-2-reorder-backlog.md` |
| US-3 | A user creates a work item from a team's backlog | US-1 | `stories/US-3-create-from-backlog.md` |
| US-4 | A user sees a team's board per level | US-1 | `stories/US-4-team-board-per-level.md` |
| US-5 | A user moves a card between columns | US-4 | `stories/US-5-move-card-between-columns.md` |
| US-6 | A user creates a work item from a team's board | US-3, US-4 | `stories/US-6-create-from-board.md` |
| US-7 | Changes made elsewhere move items on and off a team's surfaces | US-1, US-4 | `stories/US-7-changes-made-elsewhere.md` |

All seven are `shippable: no`; the task is not split (§ Decomposition).

## Model

### What a team's backlog and board hold

For team T of project P and level L, the backlog and the board hold exactly
the work items I for which all of these hold, at the moment of the read:

1. I belongs to P (mod-003 BR-M3-2), and T is an active team of P;
2. I's type belongs to L, per the table below (mod-001 BR-M2-4);
3. I's Область resolves to T: its explicit owner if it has one, else the
   explicit owner of its nearest ancestor that has one, the root being the
   first team's for good — mod-002's `resolvedOwner` (BR-M1-11; doc-001
   BR-3). An item is therefore on exactly one team's backlog at its level;
4. I's state is not in the Removed category (BR-M4-16). No item can be in
   «Удалена» before mod-005 (D-5), so in this cut every item that meets 1–3
   is shown, «Закрыта» included (OQ-2).

| Level | Address segment | Types, in the process's display order |
|---|---|---|
| «Эпики» | `epics` | «Эпик» |
| «Фичи» | `features` | «Фича» |
| «Истории» | `stories` | «Пользовательская история», «Баг» |

The board shows the same items in the same order as the backlog, placed in
columns by (type, state); it holds nothing the backlog does not.

### The stack rank

- **What it orders.** Each (team, level) has one order over the items its
  backlog holds (doc-001 BR-12). The backlog lists them in it; each board
  column lists its cards in it (BR-M4-15). It is not a field of the work
  item: reordering changes no field and not the item's «Изменён» time.
- **Placed and unplaced.** An item on a backlog is either placed there or
  not. The order is: the placed items by their places, then the unplaced
  ones by identifier number, ascending (one sequence per project, so this
  is creation order: mod-003 BR-M3-5).
- **Where items land.** An item is unplaced on a backlog when it predates
  the backlog's first reorder (every item when this task ships), and when it
  comes onto the backlog: created (from any surface), or moved in because
  its Область changed or its area's owning team changed (an owner set or
  cleared, an area moved, a team deleted). So a new item is always the last
  row (it has the highest number), and an item moved in lands below every
  placed item (OQ-5).
- **Places are given by a reorder only.** A reorder of a backlog places
  every item on it where it currently stands, drops the places held there by
  items that have left it, then moves its one item. An item holds at most
  one place; given one on another backlog, it loses the old one. So an item
  that leaves a backlog and comes back is unplaced there — unless, while it
  was away, that backlog was not reordered and the item was placed nowhere
  else: then it is back where it was (OQ-5).
- **What a reorder says.** The moved item goes to the top, to the bottom,
  or directly after one named item of the same backlog (BR-M4-25).
- **Two users at once, no token (D-2).** Reorders of one backlog apply one
  at a time, in arrival order, each to the order as it stands then, and
  each moves only its own item: two reorders of different items both take
  effect; of two of the same item, the later decides its place. That is
  "last write wins" here: no move is rejected for an old view, nobody is
  told, each user sees the combined order at the next read. A reorder whose
  moved or named item is no longer on that backlog is refused (BR-M4-26).

### The board's columns and what a card move writes

Columns follow doc-003 BR-M4-2: category order «Новые» → «В работе» →
«Решённые» → «Завершённые»; within a category the level has as many
columns as the most states any of its types has there, and the k-th maps
each type to its k-th state of that category in seed order, or to none.
The first column of a category is headed by the category's label, a further
one by the label of the state it maps (if its types map differently
labelled states, the first type's in display order — never so in the
seeded process; this task's call). Removed has no column. The columns are
derived from the organization's process on every read, never stored. For
the seeded process (mod-001 contract § 1.4; doc-010 D-23 (c)):

| Level | Columns: header → the state a drop writes, per type ("—": no drop target) |
|---|---|
| «Эпики», «Фичи» | «Новые» → «Новая» · «В работе» → «Активна» · «Разрешена» → «Разрешена» · «Завершённые» → «Закрыта» |
| «Истории» | «Новые» → both «Новая» · «В работе» → both «Активна» · «Разрешена» → История «Разрешена», Баг — · «Решённые» → История —, Баг «Разрешена» · «Завершённые» → both «Закрыта» |

> **Superseded in part by bug-004 D-3 (2026-10-12), one name, one meaning:**
> «Разрешена» is in the Resolved category on «Эпик», «Фича», «Пользовательская
> история» and «Баг», and a state name has one category on every type of the
> process. Columns are still derived per category and k-th state, but a column
> is **headed by the name of the state it maps** — the types' shared name, and
> where they differ the first type's in display order (the tie rule above,
> now for every column, not only the further ones) — and the category's label
> is no header. The seeded result on all three levels is «Новая», «Активна»,
> «Разрешена», «Закрыта», each mapping every type of its level; the story's
> and the bug's «Разрешена» stand in one column, each type's own state under
> it. So the paragraph's header rule and the table above, with its «Новые»,
> «В работе», «Решённые» and «Завершённые» headers, the «Разрешена»/«Решённые»
> pair of «Истории» and the "—" cells, are left as written but no longer hold.
> A column that maps no state for a type still exists for a process shaped
> differently (BR-M4-18) — the seed has none. The column keys lose
> `InProgress-2`: the seeded keys are `Proposed-1`, `InProgress-1`,
> `Resolved-1`, `Completed-1`. The empty board's hint (§ Page «Доски») names
> the first column by its own header, «Новая». The other places that name a
> column by its old header read the same way: the «Новый элемент» row of § Page
> «Доски»'s block table ("the first column, «Новые»" is «Новая»), the dialog
> section's "the last card of «Новые»" («Новая») and OQ-2's "in «Завершённые»"
> («Закрыта»).

A card sits in the column its type's current state maps to. Moving it to
another column writes one thing: the item's Статус becomes the state that
column maps for its type — the same change as choosing it on the form
(mod-003 BR-M3-21), moving the item's «Изменён» time (BR-M3-28). Nothing
else changes, its backlog place included. The move is last write wins with
no check (D-2): it writes its state whatever the item's state or team is
meanwhile. A form opened earlier and saved afterwards changes only the
fields its user changed (mod-003 BR-M3-25): unless Статус is one of them,
the moved state stays.

## Business rules

| # | Rule | Source |
|---|------|--------|
| BR-M4-1 | «Эпики», «Фичи» and «Истории» each have one backlog and one board per active team, every team included, whenever it was created; no «Задачи» board, and in this cut no «Задачи» backlog either (OQ-3). | doc-003 BR-M4-1, BR-M4-13; D-2 |
| BR-M4-2 | The columns per (type, state) and their headers, as § Model states, derived from the process. *Superseded in part by bug-004 D-3: a column is headed by the name of the state it maps, not by the category's label (§ Model's note); «Разрешена» is one column for the story and the bug.* | doc-003 BR-M4-2; doc-010 D-4, D-5, D-23 (c) |
| BR-M4-3 | A card has no board position of its own; moving it writes only its state, as § Model states, last write wins (no token, D-2). | doc-003 BR-M4-3; doc-001 BR-11; D-2 |
| BR-M4-5 | A backlog and its board hold the items § Model's conditions 1–4 select, recomputed at every read. | doc-003 BR-M4-5; doc-001 BR-3; mod-002 BR-M1-11 |
| BR-M4-6 | An item that comes onto a backlog is unplaced there and lands below every placed item; its former place elsewhere is not carried over. | doc-003 BR-M4-6; this cut's precision BR-M4-24 |
| BR-M4-8 | Reordering is the only way to change the order, by pointer drag or by its keyboard equivalent, which offers «В начало», «Выше», «Ниже», «В конец» — the positions a drag can reach (mod-015 § 10.5; WCAG 2.1.1). There is no numeric rank field and no create-at-position. *Superseded in part by feat-027 BR-T-13 (D-7, D-11) on the backlog: the keyboard equivalent is the four entries in the row's «Ещё действия», not the handle's menu «Переместить <ID>», and a drag may start from any point of the row.* | doc-003 BR-M4-8, amended by mod-015 § 10.5 |
| BR-M4-13 | Items that predate a backlog's first reorder are listed by identifier number; nothing is backfilled by hand. | doc-003 BR-M4-13 |
| BR-M4-14 | A backlog row shows «Порядок», ID, «Тип», «Заголовок», «Исполнитель», «Статус»; a card shows type, ID, «Заголовок», «Статус», «Исполнитель». Приоритет and Теги join them with their own lines (§ Out of scope). *Superseded in part by feat-027 BR-T-1 (D-7) for the backlog row: its columns are the hierarchy column, «Порядок», «Тип», «Заголовок», «Статус», «Трудоёмкость», «Бизнес-ценность», «Вид ценности» — no ID, «Исполнитель» or «Приоритет» (the card keeps its fields).* | doc-003 BR-M4-14; mod-003 § Out of scope |
| BR-M4-15 | A column lists its cards in the backlog's order; a card move keeps the item's place. Dragging within a column is not offered in this cut (OQ-4). | doc-003 BR-M4-15 |
| BR-M4-18 | A column that maps no state of a card's type offers that card no drop target and is not offered in its keyboard menu; a direct request for a state of another type is refused with mod-003's `workitems.workitem.state_not_allowed`. | doc-003 BR-M4-18; mod-003 BR-M3-21 |
| BR-M4-20 | A deleted team's backlog and board addresses answer «Команда не найдена»; its areas pass to the first team (mod-002), so its items come onto the first team's backlogs unplaced. | doc-003 BR-M4-20; doc-010 D-6; mod-002 BR-M1-18 |
| BR-M4-21 | Creating from a team's backlog or board uses mod-003's dialog with the type choice limited to the level's types; the item takes the team's default area as it stands when the item is created (the first team's is the root), mod-003's initial state, and lands last on that team's backlog. The dialog has no Область field (mod-003's dialog), so a different area is set afterwards on the form, which moves the item (US-7). *Changed by feat-027: an item created from a backlog row, with that row as its parent (feat-027 BR-T-10), takes the parent's area as stored instead of the team's default.* | doc-003 BR-M4-21; doc-010 D-7; mod-002 BR-M1-14 |
| BR-M4-23 | The level → types mapping and the shown states are § Model's: every state outside the Removed category. | mod-001 BR-M2-4; doc-003 BR-M4-16; OQ-2 |
| BR-M4-24 | Placed and unplaced items, where items land and how places are given, as § Model states. | this task's call under D-2 (OQ-5) |
| BR-M4-25 | A reorder names its backlog (team, level), the moved item and one position: the top, the bottom, or directly after a named item. Reorders of one backlog apply one at a time in arrival order; last write wins with no notice, as § Model defines. | doc-001 BR-11, BR-12; D-2 |
| BR-M4-26 | A reorder whose moved item or named item is no longer on that backlog (its Область or its area's owner changed meanwhile) is refused with `boards.backlog.reorder_stale`, and nothing changes. | this task's call |
| BR-M4-27 | Every request needs a signed-in user and runs behind mod-002's organization-context filter. Reads declare `workitems.workitem.read`, a card move and a creation `workitems.workitem.write` (mod-003), a reorder `boards.backlog.reorder`, which this task registers as an organization-scoped code outside the realm import; nothing checks them until mod-016. | D-3; doc-010 § Permission codes; mod-003 BR-M3-27 |
| BR-M4-28 | No live push: a page shows what its last read returned. It is read again when it opens, after each write of its user, and when the browser tab regains focus. A change made elsewhere — a state or Область set on the form, an area re-owned or moved, a team deleted — shows at that next read, with no notice. | doc-003 NFR (refresh model); D-2 |

## Operation completeness

| Entity | Create | Read | Update | Delete/deactivate | Cancel/reverse |
|--------|--------|------|--------|-------------------|----------------|
| Backlog / board (team, level) | no: exists for every active team and level (BR-M4-1) | any signed-in user (US-1, US-4) | no: settings are doc-011's | no direct action: gone with its team (BR-M4-20) | no: a deleted team is never restored (mod-002) |
| Board column | no: derived from the process (BR-M4-2) | as the board | no: doc-011 | no: doc-011 | n/a |
| Place in a backlog (stack rank) | system, by a reorder (BR-M4-24) | as the backlog | any signed-in user: reorder (US-2) | system: dropped by a reorder of that backlog after the item left it, or when the item is placed on another one (BR-M4-24) | another reorder; no undo |
| Work item: state by card move | — | — | any signed-in user (US-5) | — | another move or the form |
| Work item: create from a surface | any signed-in user (US-3, US-6) | — | — | no: mod-005 | no: mod-005 |

## Screens and copy

Built only from mod-015's patterns ("mod-015 § n"), in the manner of
mod-003's pages; the shell and the theme are not edited. Type icons and
category dots are mod-003's `TypeMark` and category marker (mod-015 § 2.3).
Loading is a skeleton in `role="status"` «Загрузка данных»; a load error is
`ErrorBanner` with the server's text and «Повторить», header and tabs kept.
Both pages are team-level (mod-015 BR-18): the switcher follows the address,
and choosing another team opens the same entry's landing page there.

### Navigation

Two entries of mod-015 § 4.1, registered through the navigation registry in
the work group after «Рабочие элементы», shown when the team context is
real; code `workitems.workitem.read`, satisfied by any signed-in user until
mod-016 (mod-015 BR-8). Pass A registers «Бэклоги», pass B «Доски».

| Entry | Icon | Landing |
|---|---|---|
| «Доски» | `ViewWeekOutlined` | `/organizations/:o/projects/:p/teams/:t/boards/stories` |
| «Бэклоги» | `FormatListNumbered` | `/organizations/:o/projects/:p/teams/:t/backlogs/stories` |

### Page «Бэклоги» (US-1, US-2, US-3)

> **Superseded in part** by feat-001 BR-H-15 and BR-H-16 (its spec § Screens
> and copy), built by feat-026: the tabs «Эпики», «Фичи», «Истории» are one
> drop-down «Уровень: <уровень>» under the `h1`, and «Показать доску» reads
> «Показать канбан». The text and tables below are left as written.

> **Superseded in part** by feat-027 (its spec BR-T-1, BR-T-13, BR-T-14,
> BR-T-16 and `design.md`): the page's head is the team's
> name as the `h1` with one tab «Бэклог»; the table's columns are the
> hierarchy column, «Порядок», «Тип», «Заголовок» (the tree, with «⋮»),
> «Статус», «Трудоёмкость», «Бизнес-ценность», «Вид ценности» — no «ID»,
> «Исполнитель», «Приоритет» or handle column; and the handle with its menu
> «Переместить <ID>» is gone: the reorder is dragged from any point of a
> top-level row (the dotted grip at its edge is a hint), and its keyboard
> path, «В начало», «Выше», «Ниже», «В конец», is in the row's «Ещё действия».
> The text and tables below are left as written.

Address `/organizations/:organizationId/projects/:projectId/teams/:teamId/backlogs/:level`,
`:level` one of `epics`, `features`, `stories`; any other is an unknown
address, «Страница не найдена» (mod-015 BR-13). mod-015 § 10.5 without the
«Итерация» column and the planning pane.

| Block | Content |
|---|---|
| Header | `h1` «Бэклоги», tab title «Бэклоги — Mustask», no trail; tabs «Эпики», «Фичи», «Истории», each an address |
| Primary | «Новый рабочий элемент» (`Add`). On «Истории» it carries `ExpandMore` and opens the type picker «Пользовательская история», «Баг» with their icons; on «Эпики» and «Фичи» it opens the dialog preset to the one type |
| Secondary | «Показать доску» (`ViewWeekOutlined`): the board of the same team and level |
| Table | `Table size="small" aria-label="Бэклог"`; columns «Порядок» (1..n), the handle column (visually hidden header «Перемещение»), «ID», «Тип» (icon + name), «Заголовок» (link to the item's form, `…/projects/:p/workitems/<ID>`), «Исполнитель» (initials avatar + display name, «Не назначен», «Неизвестный пользователь»), «Статус» (category dot + state name). No paging: the whole backlog |
| Handle | `IconButton` `DragIndicator`, `aria-label` «Переместить <ID>», `aria-haspopup="menu"` |

By pointer: press the handle and drag; a 2 px accent line marks the drop
position between rows; release drops, Escape cancels. Above the first row
asks "the top", below the last "the bottom", elsewhere "directly after the
row above the line". By keyboard: Enter, Space or a click on the handle
opens a `Menu` «В начало», «Выше», «Ниже», «В конец» — the top; directly
after the row two above (the top for the second row); directly after the
row below; the bottom. On the first row «В начало» and «Выше» are disabled,
on the last «Ниже» and «В конец» (mod-015 BR-9). After either, the row shows
at its new place at once, «Порядок» is renumbered, focus stays on its
handle, and a visually hidden `role="status"` region announces «<ID>
перемещён: позиция <n> из <m>.». While the move is saved, handles and
dragging are off; then the backlog is read again. A refusal: § Notices.

| State | Shown |
|---|---|
| Loading | header and tabs; the table skeleton with the real column headers |
| Error | `ErrorBanner` + «Повторить» in place of the table |
| Empty | frame «Бэклог пуст», hint «Добавьте рабочий элемент — он встанет в конец бэклога.», action «Новый рабочий элемент» (as the primary) |
| Not found | § Not-found answers |

### Page «Доски» (US-4, US-5, US-6)

> **Superseded in part** by feat-001 BR-H-15 and BR-H-16 (its spec § Screens
> and copy), built by feat-026: the page and its navigation entry
> (§ Navigation) are «Канбан» — `h1` «Канбан», tab title «Канбан — Mustask»,
> region «Канбан», empty frame «Канбан пуст» — and the tabs are the drop-down
> «Уровень: <уровень>», as on «Бэклоги». The address segment `boards` is
> unchanged. The text and tables below are left as written.

Address `/organizations/:organizationId/projects/:projectId/teams/:teamId/boards/:level`,
`:level` as above. mod-015 § 10.4 without «Настройки доски» and column
collapse.

| Block | Content |
|---|---|
| Header | `h1` «Доски», tab title «Доски — Mustask», no trail; tabs «Эпики», «Фичи», «Истории»; no primary; secondary «Показать бэклог» (`FormatListBulleted`): the backlog of the same team and level |
| Board | `Box component="section" aria-label="Доска"`; columns side by side, `layout.boardColumnWidth` each, scrolling sideways inside their container |
| Column | header `Typography` «<колонка>» + count (`caption`); cards in a `role="list"` named «<колонка>, рабочих элементов: <n>»; an empty column shows its header with «0» |
| «Новый элемент» | `Button variant="text" startIcon={<Add />}` at the top of the first column, «Новые»; opens the picker or the dialog as the backlog's primary does |
| Card | `Card` (elevation 1), 4 px stripe in the type's colour; line 1: type icon + ID + «Заголовок» as a link to the form; line 2: category dot + state name; line 3: «Исполнитель» as in the backlog |
| Card menu | `IconButton` `MoreHoriz`, `aria-label` «Ещё действия: <ID>» → `Menu` with the subheader «Переместить в колонку» and the other columns that map a state of the card's type, in board order |

By pointer: drag a card; columns that map a state of its type show a drop
area, the others and its own column none (BR-M4-18); Escape cancels. By
keyboard: the card menu. After either, the card shows in the new column at
its rank at once, focus goes to its «Ещё действия» there, and the status
region announces «<ID> перемещён в колонку «<колонка>».». While the move is
saved, card menus and dragging are off; then the board is read again. A
refusal: § Notices.

| State | Shown |
|---|---|
| Loading | header and tabs; column skeletons |
| Error | `ErrorBanner` + «Повторить» in place of the columns |
| Empty (no item at the level) | frame in place of all columns (mod-015 § 7.3; doc-003 US-1's empty columns yield to the canon, D-4): «На доске нет рабочих элементов», hint «Новые рабочие элементы этого уровня появятся в колонке «Новые».», action «Новый рабочий элемент» |
| Not found | § Not-found answers |

### Dialog «Новый рабочий элемент» from a team surface (US-3, US-6)

mod-003's dialog (mod-015 § 9.5), unchanged but for two things: «Тип»
offers only the level's types, and its text reads «Рабочий элемент будет
создан в статусе «<начальный статус типа>» в области «<путь области по
умолчанию команды>» и встанет в конец бэклога команды «<команда>».
Остальные поля заполняются в его форме.» — the path as mod-002 DC-9
(«Платформа / API», the root by its own name). Validation, busy state,
idempotent submission and refusals are mod-003's. On success the dialog
closes, the notice reads «Рабочий элемент <ID> создан.», and the page is read
again: the item is the last backlog row, or the last card of «Новые».

### Notices

`Snackbar` with `Alert severity="error"`, 6 s, bottom left; the moved row or
card returns to where it was.

| Case | Text |
|---|---|
| `boards.backlog.reorder_stale` | the code's text; the backlog is read again first |
| any other refusal with a code | the server's `detail` |
| no answer or an answer without a code | «Не удалось сохранить перемещение. Попробуйте ещё раз.» |
| a not-found of the address (org, project, team) | no notice: the page is replaced by its frame |

### Not-found answers

mod-015 § 7.2's frames, outermost level first, on both pages: «Организация
не найдена» (malformed, unknown or deactivated organization), «Проект не
найден» (the same for the project, or one of another organization),
«Команда не найдена» (the team id malformed, unknown, deleted, or of another
project). In an open dialog the answer is its `Alert`, and closing the dialog
shows the frame (mod-003).

### Error codes

New, module `boards`, `contracts/errors/boards.json`; Kazakh texts are
written at stage 4 with `kkNeedsReview`. Codes that only answer a panel
defect (a malformed position, an unknown level) are the backend stage's, as
in mod-003.

| Code | Status | Params | ru | Shown at |
|---|---|---|---|---|
| `boards.backlog.reorder_stale` | 409 | — | «Не удалось переместить: бэклог изменился. Список обновлён — повторите перемещение.» | § Notices |

Reused, unchanged: mod-002's three not-found codes (frames; the dialog's
`Alert`); `workitems.workitem.state_not_allowed` (a direct move to a column
that maps no state of the type; § Notices); mod-003's create codes in the
dialog.

## Non-functional requirements

- **Volumes and speed** (doc-001 NFR): a backlog or board of one team and
  level with up to 5 000 items is served within 2 s; a reorder, a card move
  or a creation within 1 s.
- **Concurrency:** reorders of one backlog apply one at a time in arrival
  order (BR-M4-25); a card move is last write wins (BR-M4-3); a creation is
  mod-003's, one item per submission.
- **Accessibility:** every move has a keyboard path and is announced
  (BR-M4-8); at 640 CSS px the board and the table scroll inside their
  containers (mod-015 § 12.6).
- **Audit:** none; a reorder is never a history event (doc-003 NFR).
- **Language:** copy Russian; codes and address segments Latin; the
  reference tracker is never named.

## Decomposition for the backlog

| id | Task | size | deps |
|----|------|------|------|
| mod-006-boards-backlogs (this task) | Pass A, the backlog: the membership rule, the stack rank with reorder, creation with the team's default area, `boards.backlog.reorder`, the «Бэклоги» page and entry — US-1, US-2, US-3, US-7 except its board scenario. Pass B, the board: columns, card move, creation, the «Доски» page and entry, the two cross-links — US-4, US-5, US-6, US-7's board scenario. Backend A, B, then frontend A, B (D-1); each pass ends green and leaves nothing half-wired | M | mod-003-work-item-core, doc-003-backlogs-boards |
| feat-NNN-work-item-concurrency-token | The client-visible concurrency token and its ADR (doc-001 BR-11): the form's revision check, the card move advancing the revision, the stale-move notice on the board and the backlog (doc-003 NFR) | L | mod-006-boards-backlogs |
| feat-NNN-tasks-backlog | The «Задачи» backlog, a fourth tab on «Бэклоги» only (doc-003 BR-M4-1) — if OQ-3 is (a) | S | mod-006-boards-backlogs |
| feat-NNN-board-in-column-reorder | Dragging a card within its column reorders the backlog, with «Выше»/«Ниже» in the card menu (doc-003 BR-M4-15) — if OQ-4 is (a) | S | mod-006-boards-backlogs |

Amended at gate-3, not new: mod-005's line gains the M4 part of deletion
(BR-M4-16, -17; D-5); mod-003's priority and tags follow-ups gain their
field on the card and the row (BR-M4-14). Not split further: pass A is the
board's order and its creation path, so pass B cannot ship alone.

## Open questions

- [ ] **OQ-1. Names of the two entries and pages.** (a) Recommended: «Доски»
      and «Бэклоги», as mod-015 § 4.1, § 10.4, § 10.5 and its prototype
      fix them — each page holds three levels. (b) «Доска» and «Бэклог», as
      D-2 words them.
- [ ] **OQ-2. Closed items on the backlog.** (a) Recommended: every state
      but Removed, «Закрыта» included, as doc-003 BR-M4-5/-16 are written;
      the board shows them in «Завершённые» anyway, and hiding them comes
      with feat-013's state filter. (b) Leave out the Completed category.
- [ ] **OQ-3. The «Задачи» backlog.** (a) Recommended: a follow-up line,
      keeping D-2's three levels and mod-015's three tabs. (b) A fourth tab
      «Задачи» on «Бэклоги» here (doc-003 BR-M4-1).
- [ ] **OQ-4. Reordering inside a board column.** (a) Recommended: not in
      this cut — the board shows the backlog's order and «Показать бэклог»
      is one step away; a follow-up line. (b) Here, as doc-003 BR-M4-15
      says, with «Выше»/«Ниже» in the card menu.
- [ ] **OQ-5. The order among newcomers.** (a) Recommended: below every
      placed item, among themselves by identifier number (§ Model): exact
      for a new item, but an item moved in may sort above a newer one that
      came first, and a deleted team's items keep creation order, not their
      old relative order. M4 reads at read time, as ADR-0026 rule 7 wants;
      no hook in mod-002 or mod-003. (b) Exact arrival order, as doc-003
      BR-M4-6/-20 read: mod-002 and mod-003 tell M4 of every area, owner
      and Область change.
