# Spec: Work item core, the MVP cut (mod-003)

## Context and goal

MUSTASK has organizations, keyed projects, teams and area trees (mod-002),
a seeded process with five work item types (mod-001) and a user directory
(mod-014), but nothing to track yet. This task makes the first tracker
screen: a project's list of work items, a dialog that creates one, and a
form that edits it. It is the short-route cut of module M3 (`status.md`
D-1, D-2): the field set, rules and copy come from the merged analyses —
doc-002 (rules BR-M3-n, W-n, stories US-1..US-4, US-11), doc-010 (D-7, D-8),
doc-001 (BR-n) — and from mod-002 and mod-015 for the screens. Rule ids
follow doc-010 PR-8: doc-002's BR-M3-n keep their numbers where restated,
new rules start at BR-M3-19, bare BR-n is doc-001's.

## Scope

### In scope (D-2)

- The M3 data model for the five work item types of the organization's
  process (mod-001).
- The identifier КЛЮЧПРОЕКТА-ТИП-№, resolved inside the organization
  (doc-010 D-8).
- The fields Заголовок, Описание (plain text), Статус among the type's
  states, Исполнитель over the user projection (mod-014), Область from the
  project's tree with its default (doc-010 D-7).
- The create dialog, the form (read and edit), state changes, the project's
  list surface.
- The work-item part of mod-002's area-deletion check (BR-M1-13), in its
  smallest form: an area that holds a work item is not deleted.

### Out of scope

| Item | Carried by |
|---|---|
| History and its ADR (doc-001 BR-4; doc-002 US-9) | follow-up line at gate-3 |
| Stored project tags (doc-002 BR-M3-11, US-8) | follow-up line at gate-3 |
| The «неактивен» and «не в организации» marks on an assignee (doc-001 BR-13) | follow-up line at gate-3; «не в организации» needs mod-016's membership |
| Rich text in Описание (doc-002 BR-M3-4) | follow-up line at gate-3; it converts the stored plain text |
| The client-visible concurrency token and its ADR (doc-001 BR-11) | the board cut's line, which needs it (D-2) |
| Filters and search beyond the list's own columns (doc-002 US-11, filter half) | feat-003-work-item-search |
| Deletion, «Удалена», restore, purge | mod-005-work-item-delete-restore |
| Comments («Обсуждение») | feat-002-work-item-comments |
| Attachments («Вложения») | mod-004-work-item-attachments |
| Parent/Child links («Связи») | feat-001-work-item-links |
| Критерии приёмки, Шаги воспроизведения | feat-007-work-item-type-fields |
| Приоритет (doc-002 § Fields; D-2 names it neither in nor out, OQ-2) | follow-up line at gate-3 |
| The preview side pane (mod-015 § 10.2) | follow-up line at gate-3 |
| Итерация (doc-004) | mod-007-iterations |
| Creating an item from a team's board or backlog, with that team's default area (doc-010 D-7) | mod-006-boards-backlogs |
| Membership, roles, «Недостаточно прав», a non-member's «not found» | mod-016-organization-access |
| CSV import and export (mod-015 § 10.1's secondary commands) | feat-005-csv-import-export (release 3) |
| A leave guard on unsaved input (doc-002 US-2) | none: mod-015 § 9.7 rules it out of MVP |

## Roles and access

D-3: until mod-016, access is mod-002 D-a unchanged (BR-M3-27).

| Who | What they may do |
|---|---|
| Any signed-in user | in every active organization and active project: list, open, create and edit work items, change their state |
| Anonymous caller | nothing: every page shows mod-015's auth card, every request answers 401 |

## Stories

| id | Title | depends-on | File |
|----|-------|------------|------|
| US-1 | A user creates a work item from the project's list | - | `stories/US-1-create-work-item.md` |
| US-2 | A user sees the project's work items in a list | US-1 | `stories/US-2-project-work-item-list.md` |
| US-3 | A user opens a work item by its address and edits it on the form | US-1 | `stories/US-3-open-and-edit-work-item.md` |
| US-4 | A user assigns a work item and places it in an area | US-3 | `stories/US-4-assignee-and-area.md` |
| US-5 | A user changes a work item's state | US-3 | `stories/US-5-change-state.md` |
| US-6 | An area that holds work items cannot be deleted | US-1, US-4 | `stories/US-6-area-with-work-items.md` |

All six are `shippable: no`: none is usable without the create dialog and the
form, and the task is not split (§ Decomposition).

## Data model

Logical level only. A work item attaches to mod-002's project and area,
mod-001's type and state (by identity, never by name: mod-001 BR-M2-9) and
mod-014's user (by its Keycloak user id).

| Field (UI label) | Kind | Required | Default at creation | Constraint | Edited on the form |
|---|---|---|---|---|---|
| ID | identifier | system | issued at creation | `<ключ проекта>-<ключ типа>-<номер>`; never changes (BR-M3-5) | no |
| Тип | one of the five types of the organization's process | yes | chosen in the dialog | never changes (BR-M3-1) | no |
| Заголовок | single-line text | yes | — | 1..255 characters after trimming, no line break (BR-M3-3) | yes |
| Описание | plain text | no | empty | 0..100 000 characters (BR-M3-19) | yes |
| Статус | one state of the item's type | yes | the type's initial state (BR-M3-20) | never «Удалена» in this task (BR-M3-21) | yes |
| Исполнитель | one user of the directory | no | none («Не назначен») | an active user when newly set (BR-M3-22) | yes |
| Область | one area of the item's project | yes | the project's root area (BR-M3-20) | an area of the same project (BR-M3-23) | yes |
| Создан (not shown in this task) | user and time | system | the creator, now | never changes (BR-M3-28) | no |
| Изменён | time | system | the creation time | the time of the latest accepted save (BR-M3-28) | no |

The item belongs to one project of one organization for good (BR-M3-2).

### The identifier and its number

- **Form.** `<ключ проекта>-<ключ типа>-<номер>`, e.g. `MSK-BUG-42`: the
  project's key (2..10 Latin capitals and digits, starting with a letter,
  unique in the organization, never changed: mod-002 BR-M1-1), the type's
  key (`EPIC`, `FEAT`, `US`, `TASK`, `BUG`: mod-001 BR-M2-9) and the number
  in decimal, without leading zeros.
- **Sequence.** One sequence per project, shared by all five types. The
  first item of a project gets 1, and every creation takes a number greater
  than every number the project issued before. Two creations never get the
  same number, also when they run at the same moment. The number alone
  identifies the item inside its project.
- **Gaps.** A number may be consumed by a creation that is refused or fails.
  Gaps are legal and are never filled: whether a refused creation consumed a
  number is not promised either way.
- **Never reused, never changed.** A number is issued once. The identifier
  never changes, because the project key, the type and the number never do.
- **Resolution.** A work item's address carries its organization's internal
  id, its project's internal id and the identifier (doc-010 D-8; D-5). The
  identifier is resolved inside that organization only, under the addressed
  project: the project is the addressed project of that organization, its
  key must equal the identifier's first part, the item is that project's
  item with that number, and its type's key must equal the middle part. An
  identifier is well-formed only
  in the exact form above — a valid project key, one of the five type keys,
  a number of 1 or more without leading zeros — so `msk-bug-42`,
  `MSK-BUG-042` and `MSK-XYZ-1` are malformed and resolve to nothing.

## Business rules

| # | Rule | Source |
|---|------|--------|
| BR-M3-1 | A work item's type is chosen in the create dialog among the five types of its organization's process and never changes; no screen or request changes it. | doc-002 BR-M3-1; doc-001 BR-1 |
| BR-M3-2 | A work item belongs to exactly one project, of that project's organization, and never moves; its Область is an area of that same project. | doc-002 BR-M3-2; mod-002 BR-M1-22 |
| BR-M3-3 | Заголовок is trimmed before it is checked and stored trimmed. Empty or white-space-only is refused, more than 255 characters is refused, and a line break anywhere in it is refused. The server enforces all three, not only the browser. | doc-002 BR-M3-3, § Fields («single-line text») |
| BR-M3-5 | The identifier is `<ключ проекта>-<ключ типа>-<номер>` with one number sequence per project shared by all types, issued at creation, never reused, never changed; gaps are legal; it is resolved inside the organization the address carries, as § The identifier and its number states. | doc-002 BR-M3-5, W-6, W-10, W-11; doc-010 D-8 |
| BR-M3-19 | Описание is plain text of 0..100 000 characters, stored as entered and shown as entered, line breaks kept; nothing in it is interpreted as markup. | D-2; doc-002 NFR (size) |
| BR-M3-20 | A new item takes its type's initial state (the first in the seeded order, «Новая» on all five), no assignee, an empty Описание and the project's root area. The list is a create surface with no team context, so the root, which is the first team's default area, applies; a team's default area applies to items created from that team's board or backlog (mod-006). | mod-001 BR-M2-2; doc-010 D-7; mod-002 BR-M1-14; doc-002 § Fields |
| BR-M3-21 | Статус moves among its type's states other than «Удалена»: from any one directly to any other, forwards or backwards, with no reason and no rule check. A state of another type and «Удалена» are refused (only mod-005's Delete reaches it). Choosing the current state changes nothing. A state change changes no other field. | doc-001 BR-10, D-6; doc-002 US-3 |
| BR-M3-22 | Исполнитель is optional. Until mod-016, the candidates are every active user of mod-014's directory, found by display name or e-mail (at least 2 characters, at most 20 shown). An inactive user is never offered, and a save that newly sets one is refused. An assignee who becomes inactive stays assigned and is shown by name, with no mark (D-2); a save of any other field keeps them. «Не назначен» clears the field. | doc-001 BR-13 (members: mod-016); D-3; mod-014 A.3, A.8 |
| BR-M3-23 | Область is required and is any area of the item's project, the root included. An area of another project, or one that no longer exists, is refused. Renaming or moving the area keeps the item in it, and the item shows the area's current path; deleting the team that owns it changes nothing on the item. | doc-001 BR-3; mod-002 BR-M1-12, BR-M1-18, BR-M1-22 |
| BR-M3-24 | An area is deleted only when no work item has it as Область, whatever the item's state, so items in «Удалена» count once mod-005 ships; otherwise mod-002's refusal `organizations.area.not_empty` answers. When a deletion races a write that puts an item into that area — in this task only a save, since creation uses the root — one of the two is refused, and no item ever has a deleted area. | doc-006 BR-M1-13; doc-001 BR-14 (b); mod-002 BR-M1-13, NFR Concurrency |
| BR-M3-25 | Until the concurrency token arrives, a save changes exactly the fields the user changed on the form, all of them or none. A field the user did not change keeps the value stored at the time of the save, even if someone changed it meanwhile; two saves of the same field leave the later one. No conflict is reported. | this task's call under D-2 (doc-001 BR-11 deferred) |
| BR-M3-26 | One submission of the create dialog creates at most one work item: a double activation of «Создать», or the same submission sent again after a lost answer, leaves one item with one identifier. | doc-002 US-1 |
| BR-M3-27 | Every request needs a signed-in user and runs behind mod-002's organization-context filter: an organization or project that is unknown or deactivated answers its not-found code (mod-002 BR-M1-20). Nothing narrows by membership. This task declares the codes `workitems.workitem.read` and `workitems.workitem.write` as organization-scoped codes that nothing checks until mod-016, as mod-001 declared its read code. | D-3; mod-002 D-a, D-g; mod-001 BR-M2-10; doc-010 § Permission codes |
| BR-M3-28 | A work item records who created it and when, and the time of its latest accepted save, which the list orders by. It keeps no other change record (history is out of scope). | doc-002 § Fields (Дата изменения); this task's call |
| BR-M3-29 | The list holds every work item of its project in every state, most recently changed first, then by number descending, 50 rows a page. It has no filter, search or sort control. | doc-002 US-11 (list half), W-4; D-2 |

## Operation completeness

| Entity | Create | Read | Update | Delete/deactivate | Cancel/reverse |
|--------|--------|------|--------|-------------------|----------------|
| WorkItem | any signed-in user, from the project's list (US-1) | any signed-in user: the list (US-2) and the form (US-3) | any signed-in user: Заголовок, Описание (US-3), Исполнитель, Область (US-4), Статус (US-5); Тип never (BR-M3-1) | no: mod-005 | no undo: a change is reversed by another save; a state moves back freely (BR-M3-21) |
| Number sequence (per project) | system, at each creation (BR-M3-5) | as the identifier | no: never changes | no: never reused | no: a gap stays (BR-M3-5) |
| Area (mod-002), its deletion only | — | — | — | refused while a work item has it (US-6) | — |

## Screens and copy

Built only from mod-015's patterns (its `design.md`, cited "mod-015 § n")
and in the manner of mod-002's pages; nothing here edits the shell or the
theme. Type icons and category dots follow mod-015 § 2.3. Dates read
«05.10.2026 14:30». Loading is mod-015's skeleton inside `role="status"`
«Загрузка данных»; an error is `ErrorBanner` with the server's text and
«Повторить»; not found is the named frame (§ Not-found answers).

### Navigation

The main navigation's «Рабочие элементы» entry exactly as mod-015 § 4.1
defines it (icon `ListAltOutlined`, code `workitems.workitem.read`, project
scope, landing `/organizations/:o/projects/:p/workitems`), registered through
the navigation registry, in the work group after «Главная» (OQ-5). It shows
when a project context is real; until mod-016 the code is satisfied by any
signed-in user (mod-015 BR-8).

### Page «Рабочие элементы» (US-1, US-2)

Address `/organizations/:organizationId/projects/:projectId/workitems`, a
project-level page (mod-015 BR-18); `h1` and tab «Рабочие элементы», no
trail; mod-015 § 10.1 without its filter bar, secondary commands and pane.

| Block | Content |
|---|---|
| Primary | «Новый рабочий элемент» (`Add`, `ExpandMore`), rendered once the organization's process is loaded; opens the type picker |
| Type picker | `Menu`: the five types in the order the process read returns them, each with its icon: «Эпик», «Фича», «Пользовательская история», «Баг», «Задача»; a choice opens the dialog preset to it |
| Table | `Table size="small" aria-label="Рабочие элементы"`, columns «ID», «Тип» (icon + type name), «Заголовок» (link to the form), «Исполнитель» (initials avatar + display name, or «Не назначен»), «Статус» (category dot + state name), «Область» (path as mod-002 DC-9: «Платформа / API», the root by its own name), «Изменён» |
| Paging | mod-015 § 8.2's `TablePagination`, 50 rows, «1–50 из 120» |

| State | Shown |
|---|---|
| Loading | `RegistrySkeleton` with the seven headers |
| Error | `ErrorBanner` + «Повторить» |
| Empty | frame «Рабочих элементов пока нет», hint «Создайте первый рабочий элемент кнопкой «Новый рабочий элемент» выше.», action «Новый рабочий элемент» (opens the type picker) |
| Not found | «Организация не найдена» or «Проект не найден» |

An assignee the directory does not know reads «Неизвестный пользователь».

### Dialog «Новый рабочий элемент» (US-1)

mod-015 § 9.5, `maxWidth="sm"`.

| Part | Content |
|---|---|
| Text | `body2`, `text.secondary`: «Рабочий элемент будет создан в статусе «<начальный статус типа>» в области «<путь корневой области>». Остальные поля заполняются в его форме.» |
| «Тип» | `TextField select`, preset from the picker, options with their icons |
| «Заголовок» | `TextField`, focused, required, helper «До 255 символов» |
| Footer | «Отмена», «Создать» |

Browser checks on submit, cleared as the field is edited: «Введите заголовок.»,
«Не более 255 символов.», «Заголовок должен быть в одну строку.». Server
refusals go where § Error codes says; the dialog stays open with the input
kept. Busy: mod-015 § 9.1. On success the dialog closes, the list returns to
its first page with the new row on top, and the notice reads «Рабочий
элемент <ID> создан.» (OQ-4).

### Page: a work item's form (US-3, US-4, US-5)

Address `/organizations/:organizationId/projects/:projectId/workitems/:identifier`,
a project-level page (mod-015 BR-18; D-5): the shell marks «Рабочие
элементы» and the switcher follows the address. mod-015 § 10.3 without its
tabs, «Ещё действия», Итерация, Теги and «Обсуждение».

| Block | Content |
|---|---|
| Band | stripe in the type's colour; line 1, the `h1`: type icon + «<Тип> <ID>» («Баг MSK-BUG-42»); line 2: the editable title, `InputBase`, `aria-label="Заголовок"` |
| «Сохранить» | contained, at the band's right end; disabled until a value differs from the loaded one |
| Header strip | «Исполнитель», «Статус», «Область» (§ Pickers) |
| «Описание» | `h2` section; `TextField multiline minRows={6}`, label «Описание», placeholder «Добавьте описание», helper «До 100 000 символов» |
| Trail | «Рабочие элементы», a link to the item's project's list |
| Tab title | «<Тип> <ID> — Mustask» |

| State | Shown |
|---|---|
| Loading | band and strip skeletons, `FormSkeleton` for «Описание» |
| Error | `ErrorBanner` + «Повторить» under the title «Рабочий элемент» |
| Not found | § Not-found answers |

Field checks: «Заголовок» as in the dialog; «Описание» «Не более 100 000
символов.». On success the form shows the stored values, «Сохранить» is
disabled again, and the notice reads «Изменения сохранены.». A refusal about
a field is that field's error text; any other is an error `Alert` above the
band, the input kept. A not-found answer to a save replaces the page with
its frame. Leaving with unsaved input is not guarded (mod-015 § 9.7).

### Pickers (US-4, US-5)

| Control | Content and copy |
|---|---|
| «Статус» | `TextField select`: the type's states in seeded order without «Удалена», each with its category dot; the current one selected |
| «Исполнитель» | `Autocomplete` searching the directory as typed. First option always «Не назначен»; then up to 20 users, initials avatar + display name, e-mail as secondary text. Below 2 characters: «Введите не менее 2 символов имени или почты». While searching: «Поиск…». No match: «Никого не найдено». More than 20 matched: a last, non-selectable line «Показаны первые 20. Уточните запрос.». Failed search: «Не удалось выполнить поиск. Попробуйте ещё раз.» |
| «Область» | `Autocomplete` over every area of the project by path, in tree order, the root first. No match: «Ничего не найдено». While the tree loads: «Загрузка…». Failed read: «Не удалось загрузить области. Попробуйте ещё раз.»; opening the list again retries |

### Area deletion (US-6)

mod-002's «Удаление области» dialog, unchanged: a refused deletion shows its
existing `Alert` «Нельзя удалить область, в которой есть рабочие элементы
(включая удалённые) или вложенные области». No new copy.

### Not-found answers

mod-015 § 7.2 frames, outermost level first. The new variant
«Рабочий элемент не найден» has icon `SearchOff`, hint «Проверьте адрес или
вернитесь на главную.», action «На главную».

| Address | Condition | Frame |
|---|---|---|
| any page of this task | the organization id is malformed, unknown or deactivated | «Организация не найдена» |
| the list | the project id is malformed, unknown, deactivated or of another organization | «Проект не найден» |
| the form | the project id is malformed, unknown, deactivated or of another organization | «Проект не найден» |
| the form | the identifier is malformed, or its project key is not the addressed project's key, or the project has no item with that number, or that item's type key differs | «Рабочий элемент не найден» |

### Error codes

New codes go to `contracts/errors/workitems.json` (module `workitems`); the
Kazakh texts are written at stage 4 with `kkNeedsReview`. The panel places a
refusal by its code.

| Code | Status | Params | ru | Shown at |
|---|---|---|---|---|
| `workitems.workitem.not_found` | 404 | — | «Рабочий элемент не найден.» | the frame |
| `workitems.validation_failed` | 400 | — | «Проверьте правильность заполнения полей.» | carries the field reasons below |
| `workitems.workitem.title_required` | 400 (field reason) | — | «Введите заголовок.» | «Заголовок» |
| `workitems.workitem.title_too_long` | 400 (field reason) | — | «Не более 255 символов.» | «Заголовок» |
| `workitems.workitem.title_multiline` | 400 (field reason) | — | «Заголовок должен быть в одну строку.» | «Заголовок» |
| `workitems.workitem.description_too_long` | 400 (field reason) | — | «Не более 100 000 символов.» | «Описание» |
| `workitems.workitem.type_not_found` | 404 | — | «Тип рабочего элемента не найден.» | dialog `Alert` |
| `workitems.workitem.state_not_allowed` | 409 | — | «Этот статус недоступен для рабочего элемента этого типа.» | «Статус» |
| `workitems.workitem.assignee_unavailable` | 409 | — | «Этого пользователя нельзя назначить исполнителем.» | «Исполнитель» |

Reused, unchanged: `organizations.organization.not_found` and
`organizations.project.not_found` (the frames; in an open dialog, its
`Alert`, and closing it shows the frame), `organizations.area.not_found`
(«Область не найдена.», under «Область»), `organizations.area.not_empty`
(mod-002's dialog, now also for work items).

## Non-functional requirements

- **Volumes and speed** (doc-002 NFR; mod-002 NFR Volumes): with 5 000 items
  in a project, a list page answers within 2 s; opening and saving a form
  within 1 s each; the area picker with a 500-node tree within 2 s; an
  assignee search per mod-014 A.7 (500 ms at 10 000 users).
- **Concurrency:** numbers are unique under concurrent creations (BR-M3-5);
  an area deletion and an item entering that area never both succeed
  (BR-M3-24); saves follow BR-M3-25.
- **Language:** interface copy Russian; keys and codes Latin. The reference
  tracker is never named.

## Decomposition for the backlog

| id | Task | size | deps |
|----|------|------|------|
| mod-003-work-item-core (this task) | US-1..US-6: the data model, the identifier, the list, the create dialog, the form with its pickers, state changes, the area-deletion check | M | mod-002, mod-014, doc-002 |
| feat-NNN-work-item-history | History of every create and save; owes the history/audit ADR (doc-010 § ADR debt); decides what items created before it show | M | mod-003-work-item-core |
| feat-NNN-work-item-tags | Stored project tags on the form and in the list (doc-002 BR-M3-11, US-8) | M | mod-003-work-item-core |
| feat-NNN-assignee-marks | «неактивен» and «не в организации» on the form and in the list (doc-001 BR-13) | S | mod-003-work-item-core, mod-016 |
| feat-NNN-work-item-rich-text | Описание as rich text under doc-002 BR-M3-4, converting stored plain text | M | mod-003-work-item-core |
| feat-NNN-work-item-priority | Приоритет 1..4, default 2 (doc-002 § Fields), if OQ-2 keeps it out | S | mod-003-work-item-core |
| feat-NNN-work-item-preview-pane | mod-015 § 10.2's pane beside the list | S | mod-003-work-item-core |

Not split: each story needs the create dialog or the form of another. The
concurrency token and its ADR are added to the board cut's line, not a line
of their own (D-2).

## Open questions

All five are decided by `status.md` D-5: OQ-1 → (b), OQ-2 → (a), OQ-3 →
(a), OQ-4 → (a), OQ-5 → (a).

- [x] **OQ-1. The form's address.** Decided by D-5: (b). (a) Recommended:
      `/organizations/:o/workitems/MSK-BUG-42`, doc-010 D-8 literally — the
      organization's id and the identifier, whose key names the project; a
      pasted link never carries a second id that could disagree. The cost:
      an organization-level page, so the switcher keeps its project and the
      «Рабочие элементы» entry is not marked current there. (b)
      `/organizations/:o/projects/:p/workitems/MSK-BUG-42`, mod-015's
      prototype: the shell marks the entry and the switcher shows the
      project, but a project id that disagrees with the key needs its own
      not-found case.
- [x] **OQ-2. Приоритет.** Decided by D-5: (a). In doc-002's MVP field set, named by D-2 neither
      in nor out. (a) Recommended: a follow-up line (above), keeping D-2's
      cut exact. (b) Add it here: one integer field, 1..4, default 2.
- [x] **OQ-3. Assignee candidates until mod-016.** Decided by D-5: (a). (a) Recommended: every
      active user of the directory, the consequence of D-3's "every
      signed-in user may do everything"; mod-016 narrows it to the
      organization's members (doc-001 BR-13). (b) No Исполнитель until
      mod-016, which contradicts D-2's scope.
- [x] **OQ-4. After «Создать».** Decided by D-5: (a). (a) Recommended: stay on the list with the
      new row on top, so several items are captured in a row (doc-002 US-1's
      "one sentence"; mod-002 DC-6). (b) Open the new item's form.
- [x] **OQ-5. The navigation label.** Decided by D-5: (a). (a) Recommended: «Рабочие элементы»,
      as mod-015 § 4.1 and § 10.10 fix it: «Задача» names only the type and
      «Задачи» is a backlog level (mod-001). (b) «Задачи», as the task brief
      calls the screen.
