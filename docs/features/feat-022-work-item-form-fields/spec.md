# Spec: Work item form — fields per type and sections (feat-022)

## Context and goal

mod-003 gave the work item a form with Заголовок, Описание, Статус,
Исполнитель and Область. The human asked for more detail, taking the
reference tracker's work item form as the base (`status.md` D-1): the
planning, classification and estimation fields each type carries, the
type's own text field, and the form laid out in sections. This task fixes
the exact field set per type (D-2) from the merged analyses — doc-002
(§ Fields, § Release 2, W-5, US-2, US-12, BR-M3-n), doc-010 (D-15), the
field list of doc-001's research note (cited "research § Fields") — and
re-lays mod-003's form with mod-015's patterns (D-4). Rule ids follow
doc-010 PR-8: mod-003 ended at BR-M3-29, so new rules start at BR-M3-30.

## Scope

### In scope (D-2)

- Приоритет on all five types.
- The planning and classification fields of Эпик, Фича and Пользовательская
  история; the estimation fields of Задача and Баг and the bug's Серьёзность
  (D-2 overrides doc-010 D-15's release-2 placing).
- «Критерии приёмки», «Шаги воспроизведения» and «Сведения о системе» as
  plain text (the last per D-6, OQ-4 (b)).
- The form laid out in sections, folding at narrow widths.
- Приоритет on the backlog row and the board card (doc-003 BR-M4-14), if
  OQ-1 keeps the recommended option.

### Out of scope

| Item | Carried by |
|---|---|
| Теги in the header | feat-016-work-item-tags |
| Rich text in the three text fields | feat-018-work-item-rich-text (amended, § Decomposition) |
| «Обсуждение» and the comment count | feat-002-work-item-comments |
| «История» tab | feat-015-work-item-history (amended) |
| «Связи» tab, «Связанная работа», the parent | feat-001-work-item-links |
| «Вложения» tab | mod-004-work-item-attachments |
| Итерация in the strip | mod-007-iterations |
| Reason (state reasons, the bug's resolved reason) — release 3 in doc-002 § Out of scope and doc-007 D-6 | doc-007's release-3 line `feat-NNN-process-reason-transitions` (not queued) |
| The deployment and development sections (releases, builds, commits) | none: not in the product plan (D-2) |
| The revision check of the new fields | feat-021-work-item-concurrency-token (amended) |
| A column in the project's list (BR-M3-40) | none: doc-002 US-11 puts none there |

## Roles and access

D-3: access is mod-002 D-a unchanged until mod-016 — mod-003 BR-M3-27 holds
as written. Any signed-in user reads and edits every field below on every
work item of every active organization and project; an anonymous caller gets
mod-015's auth card and 401. No new permission code: the fields are read
under `workitems.workitem.read` and saved under `workitems.workitem.write`.

## Stories

| id | Title | depends-on | File |
|----|-------|------------|------|
| US-1 | A user works on a form laid out in sections that fold at narrow widths | - | `stories/US-1-sectioned-form.md` |
| US-2 | A user plans and classifies epics, features and stories | US-1 | `stories/US-2-planning-and-classification.md` |
| US-3 | A user estimates tasks and bugs and grades a bug's severity | US-1 | `stories/US-3-estimation-and-severity.md` |
| US-4 | A story carries acceptance criteria, a bug repro steps and system info | US-1 | `stories/US-4-type-text-fields.md` |
| US-5 | Every item has a priority and the defaults, new and existing alike | US-1 | `stories/US-5-priority-and-defaults.md` |
| US-6 | A user sees the priority on the backlog and the board | US-5 | `stories/US-6-priority-on-backlog-and-board.md` |

All six are `shippable: no`: each needs the sectioned form of US-1 or the
Приоритет of US-5. US-6 rests on OQ-1.

## The field table

«История» below is the type «Пользовательская история». Every field is new
in this task; mod-003's fields do not change. A fixed list's key is what is
stored, its label is what is shown.

| Поле | Types | Kind | Values or range | Default at creation | Required | Section | Source |
|---|---|---|---|---|---|---|---|
| «Приоритет» | Эпик, Фича, История, Задача, Баг | fixed list | `1` «1», `2` «2», `3` «3», `4` «4»; 1 is the highest | `2` | always set | Планирование | doc-002 § Fields, US-2; research § Fields |
| «Риск» | Эпик, Фича, История | fixed list | `1` «1 — Высокий», `2` «2 — Средний», `3` «3 — Низкий» | empty | no | Планирование | research § Fields; D-1 |
| «Оценка в пунктах» | История, Баг | decimal | 0..9 999, ≤ 2 digits after the separator | empty | no | Планирование | doc-002 § Release 2; doc-010 D-15 |
| «Трудоёмкость» | Эпик, Фича | decimal | 0..9 999, ≤ 2 digits | empty | no | Планирование | research § Fields |
| «Бизнес-ценность» | Эпик, Фича | integer | 0..9 999 | empty | no | Планирование | research § Fields (integer) |
| «Критичность по времени» | Эпик, Фича | decimal | 0..9 999, ≤ 2 digits | empty | no | Планирование | research § Fields |
| «Дата начала» | Эпик, Фича | date | 01.01.2000..31.12.2099 | empty | no | Планирование | research § Fields |
| «Целевая дата» | Эпик, Фича | date | as «Дата начала», not earlier than it | empty | no | Планирование | research § Fields; BR-M3-35 |
| «Серьёзность» | Баг | fixed list | `1` «1 — Критическая», `2` «2 — Высокая», `3` «3 — Средняя», `4` «4 — Низкая» | `3` | always set | Планирование | research § Fields (default 3) |
| «Вид работ» | Задача, Баг | fixed list | `Deployment` «Развёртывание», `Design` «Проектирование», `Development` «Разработка», `Documentation` «Документирование», `Requirements` «Требования», `Testing` «Тестирование» | empty | no | Планирование | doc-002 § Release 2 |
| «Исходная оценка» | Задача, Баг | decimal, hours | 0..9 999, ≤ 2 digits | empty | no | Трудозатраты (часы) | doc-002 § Release 2; doc-010 D-15 |
| «Оставшаяся работа» | Задача, Баг | decimal, hours | 0..9 999, ≤ 2 digits | empty | no | Трудозатраты (часы) | doc-002 § Release 2; doc-010 D-15 |
| «Выполненная работа» | Задача, Баг | decimal, hours | 0..9 999, ≤ 2 digits | empty | no | Трудозатраты (часы) | doc-002 § Release 2; doc-010 D-15 |
| «Вид ценности» | Эпик, Фича, История | fixed list | `Business` «Бизнес», `Architectural` «Архитектура» | `Business` | always set | Классификация | research § Fields (required); D-1 |
| «Критерии приёмки» | История | plain text | 0..100 000 characters | empty | no | its own, left column | doc-002 W-5, US-12; D-2 |
| «Шаги воспроизведения» | Баг | plain text | 0..100 000 characters | empty | no | its own, left column | doc-002 W-5, US-12; D-2 |
| «Сведения о системе» | Баг | plain text | 0..100 000 characters | empty | no | its own, left column, under «Шаги воспроизведения» | research § Fields; D-6 (OQ-4 (b)) |

Per type: Эпик and Фича gain 8 fields, История 5, Задача 5, Баг 9.

### Left out of the reference form

| Field or block | Reason |
|---|---|
| Header: comment count, Теги | feat-002, feat-016 |
| Strip: Reason, Итерация | doc-007 D-6 (release 3); mod-007 |
| Tabs «История», «Связи», «Вложения» (and so the «Детали» tab: one tab is no tab bar) | feat-015, feat-001, mod-004 |
| «Обсуждение» | feat-002 |
| The deployment and development sections | not in the product plan (D-2) |
| «Связанная работа» (add a link, add a parent) | feat-001 |
| Баг: system information as an html field | kept as plain text, «Сведения о системе» (D-6, OQ-4 (b)); rich text is feat-018's |
| Баг: found-in and integration build | not in the product plan: build integration, like the development section |
| Баг: resolved reason | release 3 (doc-002 § Out of scope; doc-007 D-6) |
| История, Задача: start and finish date | not on the story's form (D-1); D-2 names dates for Эпик and Фича only |

## Business rules

| # | Rule | Source |
|---|------|--------|
| BR-M3-30 | Each field of § The field table exists only on the types its row names. The form shows it only there, and a save carrying a value for a field the item's type lacks is refused (`field_not_on_type`) and stores nothing; the create takes none of these fields (BR-M3-40), and a member naming one is ignored (status.md D-7). | doc-002 BR-M3-18, US-12 |
| BR-M3-31 | A fixed list takes only its listed keys; anything else is refused (`value_not_in_list`). Приоритет, Серьёзность and Вид ценности are always set: the form offers no empty choice, and a request that empties one stores the list's default. Риск and Вид работ may be empty, offered as «Не указан». Приоритет is a plain value, 1 the highest and 4 the lowest. | doc-002 § Fields, US-2 (emptied Приоритет returns to 2); research § Fields; this task's call for Серьёзность and Вид ценности |
| BR-M3-32 | A number field is empty or a number from 0 to 9 999; a decimal field holds at most two digits after the separator, Бизнес-ценность an integer. Anything else is refused (`number_out_of_range`, `number_too_precise`, `integer_required`), never rounded or clamped (up to the 28 significant digits a stored number holds). The form accepts «,» or «.» as the separator and shows a stored value with «,» and without trailing zeros («2,5»). Empty means "not set" — on an estimate "not estimated" — and is never 0; 0 is a value of its own. | doc-002 § Release 2 (≥ 0, empty is never 0); research § Fields; range and precision this task's call |
| BR-M3-33 | Units: Исходная оценка, Оставшаяся работа and Выполненная работа are hours; Оценка в пунктах is points; Трудоёмкость, Бизнес-ценность and Критичность по времени are relative numbers with no unit. Nothing converts or sums them. | doc-002 § Release 2; research § Fields |
| BR-M3-34 | No field is derived or read-only: each holds what a user last saved. A state change changes none of them — closing a Задача leaves Оставшаяся работа as entered — and no field changes another (Выполненная работа does not reduce Оставшаяся работа). A type never changes (BR-M3-1), so no value ever moves between types. | doc-002 BR-M3-18; doc-001 D-6; mod-003 BR-M3-1, BR-M3-21 |
| BR-M3-35 | A date is a calendar date with no time or time zone, from 01.01.2000 to 31.12.2099 (`date_out_of_range`). Дата начала and Целевая дата may each be set alone; when both are set after a save, Целевая дата is not earlier than Дата начала (`target_before_start`, reported on «Целевая дата» also when the save changed only Дата начала). | research § Fields; range and order this task's call (OQ-3) |
| BR-M3-36 | Критерии приёмки, Шаги воспроизведения and Сведения о системе are plain text of 0..100 000 characters, stored and shown as entered with line breaks, nothing interpreted as markup (`text_too_long`), as BR-M3-19 has it for Описание. | doc-002 W-5, US-12; D-2; D-6 (OQ-4); mod-003 BR-M3-19 |
| BR-M3-37 | A new item takes Приоритет 2, on a Баг Серьёзность 3, on an Эпик, Фича or История Вид ценности «Бизнес», and every other new field empty. An item created before this task ships reads the same values from the first moment, and receiving them moves neither its «Изменён» nor its place in the list. | doc-002 § Fields; research § Fields; this task's call for existing items |
| BR-M3-38 | The new fields join the form's one save under BR-M3-25: a save changes exactly the fields the user changed, all of them or none, so one refused value stores nothing. Emptying an optional field is a change. «Изменён» moves when at least one value changes (BR-M3-28). | mod-003 BR-M3-25, BR-M3-28 |
| BR-M3-39 | The backlog row and the board card show Приоритет (OQ-1). | doc-003 BR-M4-14; mod-006 BR-M4-14 |
| BR-M3-40 | The project's list gains no column and the create dialog no field: a new item takes BR-M3-37's values, and the dialog's text «Остальные поля заполняются в его форме.» stays true. | doc-002 US-11; mod-015 § 10.1; D-2 |

## Operation completeness

| Entity | Create | Read | Update | Delete/deactivate | Cancel/reverse |
|--------|--------|------|--------|-------------------|----------------|
| The new field values of a WorkItem | system: BR-M3-37's defaults at creation and for existing items | any signed-in user, on the form; Приоритет also on the backlog and the board (US-6) | any signed-in user, on the form (US-2..US-5) | an optional value is emptied by a save; an always-set one cannot be (BR-M3-31) | no undo: another save (BR-M3-38) |
| The five fixed lists | no: fixed by this spec; lists become editable only with doc-007's release-3 custom fields | everyone, as the form's options | no: a code change | no: a key stays valid while any item holds it | n/a |

## Screens and copy

Built only from mod-015's patterns (its `design.md`, "mod-015 § n") and
mod-003's form as it stands (D-4). Unchanged from mod-003: the address, the
band with the `h1` «<Тип> <ID>» and the editable title, «Сохранить» at the
band's right end, the strip «Исполнитель», «Статус», «Область», the trail,
the tab title, the not-found frames, and no tab bar.

### The form's sections

Below the strip the body is mod-015 § 10.3's «Детали» grid: two columns at
≥ 1280 px (8/4), one column below. Each section has an `h2` heading with a
`divider` rule. A section a type lacks is absent, not empty. feat-001's
«Связанная работа» (the parent and the children, in main before this task)
stands last in the right column on every type.

| Type | Left column | Right column, top to bottom |
|---|---|---|
| Эпик, Фича | «Описание» | «Планирование»: Приоритет, Риск, Трудоёмкость, Бизнес-ценность, Критичность по времени, Дата начала, Целевая дата · «Классификация»: Вид ценности · «Связанная работа» |
| Пользовательская история | «Описание», «Критерии приёмки» | «Планирование»: Оценка в пунктах, Приоритет, Риск · «Классификация»: Вид ценности · «Связанная работа» |
| Задача | «Описание» | «Планирование»: Приоритет, Вид работ · «Трудозатраты (часы)»: Исходная оценка, Оставшаяся работа, Выполненная работа · «Связанная работа» |
| Баг | «Описание», «Шаги воспроизведения», «Сведения о системе» | «Планирование»: Оценка в пунктах, Приоритет, Серьёзность, Вид работ · «Трудозатраты (часы)»: Исходная оценка, Оставшаяся работа, Выполненная работа · «Связанная работа» |

```
▌(icon) Пользовательская история MSK-US-12                          [Сохранить]
▌[ Вход по ссылке из письма______________________________ ]
 Исполнитель (АС) Анна Смирнова   Статус ● Активна   Область Платформа / API
 +---------------------------------------------+  +----------------------+
 | Описание                                    |  | Планирование         |
 | Критерии приёмки                            |  |  Оценка в пунктах    |
 +---------------------------------------------+  |  Приоритет  2        |
                                                  |  Риск                |
                                                  | Классификация        |
                                                  |  Вид ценности Бизнес |
                                                  +----------------------+
```

**Folding.** In the right column every control takes the column's width,
one under another. Below 1280 px the sections stand in one column in the
order left column first, then right — «Описание», the type's text sections
(a bug's «Шаги воспроизведения», then «Сведения о системе»), «Планирование»,
«Трудозатраты (часы)», «Классификация», «Связанная работа» — and the right
column's controls keep at most `layout.formColumnWidth` (480 px). The same
holds at 640–1023 px with the navigation as an overlay (mod-015 § 3.6);
the page never scrolls sideways; below 640 px nothing is designed
(mod-015 D-8, § 12.6). The reading and Tab order is the order above.

### Controls and copy

Controls are mod-015 § 2.6's small outlined `TextField`s with label and
helper; an error replaces the helper in the error colour with
`aria-invalid="true"` (mod-015 § 9.3).

| Field | Control | Placeholder | Helper |
|---|---|---|---|
| «Приоритет» | `TextField select`: «1», «2», «3», «4» | — | «1 — самый высокий» |
| «Риск», «Вид работ» | `TextField select`: «Не указан», then the list in § The field table's order | — | — |
| «Серьёзность», «Вид ценности» | `TextField select`: the list only | — | — |
| «Оценка в пунктах», «Трудоёмкость», «Исходная оценка», «Оставшаяся работа», «Выполненная работа» | `TextField`, `inputMode="decimal"` | «Не оценено» | «От 0 до 9 999» |
| «Критичность по времени» | `TextField`, `inputMode="decimal"` | «Не задано» | «От 0 до 9 999» |
| «Бизнес-ценность» | `TextField`, `inputMode="numeric"` | «Не задано» | «Целое число от 0 до 9 999» |
| «Дата начала» | `TextField type="date"`, label always raised, min 2000-01-01, max 2099-12-31 (OQ-2) | — | — |
| «Целевая дата» | as «Дата начала» | — | «Не раньше даты начала» |
| «Критерии приёмки» | as mod-003's «Описание»: `TextField multiline minRows={6}` under its `h2` | «Опишите, по каким признакам история считается готовой» | «До 100 000 символов» |
| «Шаги воспроизведения» | as «Критерии приёмки» | «Опишите шаги, ожидаемый и фактический результат» | «До 100 000 символов» |
| «Сведения о системе» | as «Критерии приёмки», its `h2` section right under «Шаги воспроизведения» | «Опишите окружение: ОС, браузер, версию приложения» | «До 100 000 символов» |

A number field accepts digits with at most one «,» or «.», spaces at
either end ignored. Browser checks run on «Сохранить», each error clears
as its field is edited, and nothing is sent while one stands:

| Check | Text |
|---|---|
| a number field's text is not a number | «Введите число.» |
| a number below 0 or above 9 999 | «Допустимо значение от 0 до 9 999.» |
| more than two digits after the separator | «Не более двух знаков после запятой.» |
| «Бизнес-ценность» with a fraction | «Введите целое число.» |
| a date typed only in part | «Введите дату полностью.» |
| a date outside 2000–2099 | «Укажите дату с 2000 по 2099 год.» |
| «Целевая дата» earlier than «Дата начала» | «Целевая дата не может быть раньше даты начала.» (under «Целевая дата») |
| a text field over 100 000 characters | «Не более 100 000 символов.» |

### Saving

As mod-003: «Сохранить» is enabled while any value differs from the loaded
one and sends only the changed fields (BR-M3-25, BR-M3-38). A number field
compares the number its text denotes — «2,50» against a stored 2,5 is no
change — and a text that is not a number counts as a change, so its check
can speak. While the save runs, every field, the strip and «Сохранить» are
disabled (mod-015 § 9.3). On success the form shows the stored values
(numbers re-shown as BR-M3-32 says), «Сохранить» is disabled again, and the
notice reads «Изменения сохранены.». A refusal about a field is that field's
error text; any other is an error `Alert` above the band, the input kept.

### States

| State | Shown |
|---|---|
| Loading | band and strip skeletons; a `FormSkeleton` in each column |
| Error | `ErrorBanner` + «Повторить» under the title «Рабочий элемент» (mod-003) |
| Not found | mod-003 § Not-found answers, unchanged |
| An empty optional field | an empty control with its placeholder; a list shows «Не указан» |
| Field error | the field's error text; «Сохранить» stays enabled |
| Saving, saved, refused | § Saving |

### Backlog and board (US-6, OQ-1)

mod-006's backlog table gains the column «Приоритет» after «Статус», the
value as a number; its skeleton shows the header too. A board card's third
line reads, after «Исполнитель», «Приоритет: <n>» (`caption`). Nothing else
there changes.

### Error codes

New field reasons under mod-003's `workitems.validation_failed` (400),
module `workitems`, `contracts/errors/workitems.json`; the Kazakh texts are
written at stage 4 with `kkNeedsReview`.

| Code | Status | Params | ru | Shown at |
|---|---|---|---|---|
| `workitems.workitem.number_out_of_range` | 400 (field reason) | — | «Допустимо значение от 0 до 9 999.» | the number field |
| `workitems.workitem.number_too_precise` | 400 (field reason) | — | «Не более двух знаков после запятой.» | the number field |
| `workitems.workitem.integer_required` | 400 (field reason) | — | «Введите целое число.» | «Бизнес-ценность» |
| `workitems.workitem.value_not_in_list` | 400 (field reason) | — | «Выберите значение из списка.» | the list field |
| `workitems.workitem.date_out_of_range` | 400 (field reason) | — | «Укажите дату с 2000 по 2099 год.» | the date field |
| `workitems.workitem.target_before_start` | 400 (field reason) | — | «Целевая дата не может быть раньше даты начала.» | «Целевая дата» |
| `workitems.workitem.text_too_long` | 400 (field reason) | — | «Не более 100 000 символов.» | «Критерии приёмки», «Шаги воспроизведения», «Сведения о системе» |
| `workitems.workitem.field_not_on_type` | 400 (field reason) | — | «Это поле недоступно для рабочего элемента этого типа.» | error `Alert` above the band (the panel never sends such a field) |

mod-003's codes are reused unchanged. Stage 4 note: mod-003's save reads an
absent or `null` field as "unchanged", so the contract must give the new
optional fields a way to say "empty" (as `assignee` does).

## Non-functional requirements

- **Speed:** mod-003's NFR holds with the new fields — opening and saving
  the form within 1 s each; the backlog and the board within mod-006's
  bounds with the extra value.
- **Language:** interface copy Russian; list keys Latin. The reference
  tracker is never named.

## Decomposition for the backlog

| id | Task | size | deps |
|----|------|------|------|
| feat-022-work-item-form-fields (this task) | US-1..US-6: the field set per type, the defaults and the backfill, the sectioned form, the error reasons, Приоритет on the backlog and the board | M | mod-003, mod-006 (D-5) |
| feat-NNN-backlog-card-priority | US-6 alone, only if OQ-1 is (b) | S | feat-022-work-item-form-fields |

Amendments to queued lines, for the orchestrator in `main`:
- feat-018-work-item-rich-text: converts «Критерии приёмки», «Шаги
  воспроизведения» and «Сведения о системе» too (doc-002 BR-M3-4; D-6).
- feat-015-work-item-history: records the new fields.
- feat-021-work-item-concurrency-token: the new fields join the revision
  check (doc-002 BR-M3-18).
- mod-011-sprint-views: its dep on the withdrawn feat-014 becomes feat-022.
- doc-011-release-2-scope: the per-type fields doc-002 § Out of scope left
  to it are now this task's.

## Open questions

All four are decided by the orchestrator in status.md D-6 (2026-10-05).

- [x] **OQ-1. Приоритет on the backlog row and the card.** (a) Recommended:
      here (US-6, BR-M3-39) — the line this task absorbed (feat-019)
      carried it in mod-006's § Out of scope, and D-5 already starts this
      task after mod-006. (b) A separate S line after this task.
      **Decided: (a)** — US-6 is built here; the backlog read and the board
      read carry Приоритет.
- [x] **OQ-2. The date control.** mod-015 has no date pattern. (a)
      Recommended: the browser's own date field (`TextField type="date"`) —
      no new library; the calendar looks as each browser draws it. (b) A
      date-picker library — one look everywhere, a new dependency and a new
      shared pattern. **Decided: (a)** — the browser's own date field; a
      date is a plain calendar date with no time (BR-M3-35).
- [x] **OQ-3. Целевая дата before Дата начала.** (a) Recommended: refuse it
      (BR-M3-35) — a plan that ends before it starts is a typo. (b) Accept
      it, as the reference tracker does. **Decided: (a)** — refused with
      `target_before_start` (BR-M3-35).
- [x] **OQ-4. The bug's system information.** (a) Recommended: leave it out
      — doc-002 W-5 fixed the two type text fields, and «Описание» holds it.
      (b) Add «Сведения о системе», a third plain-text field on Баг.
      **Decided: (b)** — «Сведения о системе», plain text of 0..100 000
      characters, in the bug's left column under «Шаги воспроизведения»
      (§ The field table, BR-M3-36, § Screens and copy, US-4).
