# Design: «Все организации», the appointment dialog and «История назначений» (feat-033)

Inputs: `spec.md` (BR-RS-1..12), US-1..US-3, the human's gate-1 answers in
`status.md`. Relies on ADR-0029 § 9, ADR-0033, ADR-0034 and the proposed
ADR-0035. Prototype: `prototype/index.html` (clickable, static).

## 0. Scope and design calls

Three surfaces, one slice (`features/organizations`, which already owns the
members page, `UserLine`, `UserSearchField`, `RowActionsMenu` and `ListFilter`;
a second slice would have to import their internals):

1. page «Все организации» for the holder of the new instance code (§ 3);
2. dialog «Назначение администратора» from a row (§ 4);
3. dialog «История назначений» from a row, and from «Участники» for the
   organization's administrators (§ 5).

| # | Call | Why |
|---|---|---|
| DC-1 | The page is **instance-scoped**: no organization in the address, no organization switcher effect, no work context. Route `/administration/organizations`. | The holder has no membership; nothing of an organization is opened (BR-RS-3). |
| DC-2 | Its settings entry sits in a **new section «Администрирование»** between «Справочники» and «Интерфейс», so «Настройки» still lands on the first visible entry as before. Entry «Все организации», `scope: 'instance'`, code `organizations.organization.rescue` (ADR-0035). | A section of its own: this is not a dictionary and not the showcase. Visibility is by the code in the token, as «Языки» (ADR-0033). |
| DC-3 | The organization name in a row is **plain text, not a link**. | A link would suggest the content opens (US-1 "The list discloses nothing else"). |
| DC-4 | Row actions in a `RowActionsMenu` («Действия: <название>»): «Назначить администратора», «История назначений». Both on every row. | Two actions per row; 25 rows; the menu is the members table's pattern (mod-016 § 3.2). |
| DC-5 | The filter and the page are **component state, not the address**; a change of text shows page 1. | The page has no deep link worth sharing; one less thing to test. |
| DC-6 | «Причина» is **not** limited by `maxLength` (it counts UTF-16 units and would cut emoji); the field counts code points of the trimmed text (BR-RS-5) and reports on «Назначить». | Dialog and server must count alike (R-9). |
| DC-7 | «История назначений» is a **read-only dialog** (`DialogShell size="card"`), not a page. | One reader surface for two hosts (the list and «Участники»); no new route, no navigation entry for non-holders. |
| DC-8 | The panel hides the page by the code but **never trusts that**: a 403 on the list or on a dialog's call turns the page into «Доступ ограничен». | Token lifetime (BR-RS-1, ADR-0018). |

## 1. Screen map and story coverage

```
Настройки (rail)
 └─ Администрирование ─ Все организации   /administration/organizations   US-1
        ├─ row «Действия» → «Назначить администратора»  → dialog (§ 4)   US-2
        └─ row «Действия» → «История назначений»        → dialog (§ 5)   US-3
Организация ─ Участники  /organizations/:o/settings/members
        └─ header command «История назначений» (organizations.membership.write) → dialog (§ 5)   US-3
```

| Story | Closed by |
|---|---|
| US-1 | § 3 (list, filter, paging, states, no link, no administrator column), § 2 (visibility) |
| US-2 | § 4 (dialog, validation, refusals, success), § 6 (order) |
| US-3 | § 5 (both hosts, newest first, only «Закрыть»), § 6 |

## 2. Access and navigation

| Who | Sees |
|---|---|
| `system-admin` | the section «Администрирование» → «Все организации»; the page; both row actions |
| everyone else (`organization-creator`, `dictionaries-admin`, `user`) | no section, no entry; by address: `AccessDenied` «Доступ ограничен» (full page, the kit's canon text) |
| organization administrator | on «Участники»: header command «История назначений»; no list, no appointment |
| organization member or reader | on «Участники»: nothing new |

Frontend mirror: `INSTANCE_ROLE_CODES['system-admin']` in
`shared/api/tokenClaims.ts` gains `organizations.organization.rescue`, and no
other role does (ADR-0033 § 2, the table is mirrored by hand). The entry is a
`scope: 'instance'` entry of `app/navigation/model.ts`; its `pages` is
`[ADMIN_ORGANIZATIONS_PATH]`. A system-admin who is also a member of an
organization sees that organization's pages as before; this feature changes
none of them.

The page does not use `OrganizationGate`/`RequiresOrganizationCode`: its gate
is `useAccessCheck(code)` (instance scope) as the languages page does.

## 3. «Все организации» (US-1)

### 3.1 Layout

```
Настройки / Администрирование
Все организации
+------------------------------------------------------------------+
| ⏷ [ Поиск по названию                 ]      [Очистить фильтры]  |  ListFilter
+------------------------------------------------------------------+
| Организация            | Статус          | Последнее назначение |    |
| Альфа                  | Активна         | —                    | ⋯  |
| Бета                   | Активна         | 03.11.2026           | ⋯  |
| Гамма                  | Деактивирована  | —                    | ⋯  |
+------------------------------------------------------------------+
| 1–25 из 120                                           ‹   ›      |  TablePager
```

### 3.2 Components (all existing)

| Block | Component | Notes |
|---|---|---|
| Header | `PageHeader` «Все организации», trail «Настройки / Администрирование» from the shell | no commands, no views |
| Filter | `ListFilter` (`FilterBar` + `InlineTextField`), label «Поиск по названию» | text is sent trimmed after the debounce of `useDebouncedValue`; ё = е and case are the server's (BR-RS-2); «Очистить фильтры» only while text is set |
| Table | kit table family: `TableCard aria-label="Все организации"`, `HeadCell`, `BodyRow`, `Cell`; `TablePager` (25 a page, total always shown) | server order by name; the page never sorts; no column sorting |
| «Организация» | `TruncatedName` (full name in a `Tooltip`) | plain text (DC-3) |
| «Статус» | «Активна» as text; «Деактивирована» as `Chip size="small" variant="outlined"` | mark is text, not colour alone |
| «Последнее назначение» | `formatDate` → «ДД.ММ.ГГГГ» in the viewer's zone (BR-RS-12); «—» with visually hidden «Назначений не было» (`VisuallyHidden`) | `formatDate` is a one-function addition to `shared/lib/format` next to `formatDateTime`, not a kit part |
| Row actions | `RowActionsMenu name="<название>"`: «Назначить администратора» (`PersonAddAltOutlined`), «История назначений» (`HistoryOutlined`) | neither item is destructive |
| Notices | `useNotify` (`Snackbar`) | § 7 |

Nothing else is on the row: no member count, no administrator, no link, no
badge about administrators (US-1 "The list claims nothing about administrators").

### 3.3 States

| State | Shown |
|---|---|
| Codes loading | the shell shows no entry yet; the page answers once the token is read (no flash of «Доступ ограничен») |
| Loading (first) | `TableSkeleton` with the three columns and an empty action column, 8 rows, `role="status"` «Загрузка данных»; header and filter stand |
| Paging / filtering (data on screen) | the old rows stay, `aria-busy`, table at reduced opacity; no skeleton flash; the pager is disabled meanwhile |
| Data | the table and the pager |
| Empty (no organizations, no filter) | `StateFrame compact` icon `CorporateFareOutlined`, heading «Организаций пока нет», no action |
| No match (filter set) | `StateFrame compact` «Организации не найдены», action «Сбросить фильтр» (clears the text) |
| Error | `ErrorBanner` with the server's text and «Повторить» in place of the table; header and filter stay |
| 403 (no right, or revoked) | `AccessDenied` full page «Доступ ограничен»; nothing listed; no retry |
| Beyond the last page (rows deleted meanwhile) | the request is repeated for the last page that exists |

Focus: opening the page moves focus to the `h1` (shell rule). After a page
change focus stays on the pager button; after a filter change it stays in the
field. A live region (`LiveRegion`) announces «Найдено: <N>» after a filter
settles.

### 3.4 Narrow widths

The filter band wraps; the table sits in `TableCard`'s horizontal scroll
(the action column stays reachable); the pager keeps the arrows.

## 4. Dialog «Назначение администратора» (US-2)

Opened from the row's menu with focus in «Пользователь». `FormDialog`
(`size` sm), mounted per opening, title «Назначение администратора», footer
«Отмена» left of the filled «Назначить».

```
Назначение администратора                                     ✕
Выбранный пользователь станет администратором организации
«Бета». Никто не будет удалён, ничья роль не будет понижена.
Вы не получите доступа к содержимому организации.
┌ ⚠ MUSTASK не видит, отключена ли учётная запись в Keycloak:
│   убедитесь, что пользователь может войти.
Пользователь *   [ поиск по имени или почте                ]
Причина *        [                                          ]
                 [                                          ]
                 Не более 500 символов
                                         [Отмена] [Назначить]
```

### 4.1 Parts

| Part | Component | Copy and behaviour |
|---|---|---|
| Lead | `Typography variant="body2"` | «Выбранный пользователь станет администратором организации «<название>». Никто не будет удалён, ничья роль не будет понижена. Вы не получите доступа к содержимому организации.» The name is the row's, without truncation |
| Warning | `InlineNotice severity="warning"` (`role="status"`) | «MUSTASK не видит, отключена ли учётная запись в Keycloak: убедитесь, что пользователь может войти.» Always shown, from the first frame |
| «Пользователь» | `UserSearchField` (the `Autocomplete` of mod-016 § 3.5), required | a candidate line: name, e-mail below; **no mark for membership, no «неактивен»** (BR-RS-6); starts at 2 trimmed characters; `maxLength` 100 (mod-014 BR-UP-14); the field shows the chosen name. New `scope` variant `{ kind: 'appointee', organizationId }` in `useUserSearch` — the same projection search, the organization id only for the server's order of refusals |
| «Причина» | `InputField multiline`, `minRows` 3, required (`inputLabel.required`) | no `maxLength` (DC-6); helper «Не более 500 символов» while there is no error; value trimmed for the check and for sending |
| «Назначить» | `FormDialog` submit | disabled until a user is chosen (mod-015 BR-9); **enabled with an empty «Причина»**: the error appears on the press (US-2 "The reason is required") |

The search states are mod-016 § 3.5's, unchanged: «Введите не менее 2
символов имени или почты» / «Поиск…» / «Пользователи не найдены» /
«Показаны первые 20. Уточните запрос.» / «Не удалось выполнить поиск.
Попробуйте ещё раз.» A user who never signed in is not found (BR-RS-6); no hint
line says why, because the dialog must not reveal who exists beyond the
projection. The operator's procedure (ask them to sign in once) is in the
runbook, not in the dialog.

### 4.2 Validation and refusals

The dialog checks «Причина» first (code points of the trimmed text, via
`[...text.trim()].length`), then sends; the server judges in BR-RS-6a's order.
Nothing is sent while the field is invalid.

| Condition | Where | Text |
|---|---|---|
| «Причина» empty or only spaces | helper of «Причина», error colour; focus moves to it | «Укажите причину» |
| «Причина» over 500 code points | same | «Не более 500 символов» |
| 403 | the page becomes `AccessDenied` (DC-8); the dialog closes | «Недостаточно прав» is the server's text, kept as the fallback `InlineNotice` if the page cannot switch |
| Organization missing | `InlineNotice severity="error"` in the dialog; «Назначить» disabled; the list is read again in the background | «Организация не найдена» |
| User not appointable (never signed in, pre-tech-025 inactive row, not in the projection) | helper of «Пользователь», error colour; the chosen user stays so another can be picked | «Этого пользователя нельзя назначить администратором» |
| Self | same | «Нельзя назначить администратором себя» |
| Already an administrator | same | «Пользователь уже администратор этой организации» |
| Server's «Причина» errors (a client/server miscount) | helper of «Причина» | «Укажите причину» / «Не более 500 символов» |
| Network, 5xx | `InlineNotice severity="error"` above the fields with the server's text; fields keep their values | the catalog text |

The three user refusals replace the helper of «Пользователь» and clear when
the user changes. Focus goes to the field that carries the error. A refusal
never leaves a success notice.

### 4.3 Success and cancel

Success: the dialog closes; the list is read again in the background (the
row's «Последнее назначение» becomes today's date); notice «Администратор
назначен: <имя>» (the display name, as in mod-016 § 3.8); focus returns to the
row's «Действия: <название>». No highlight of the row.

«Отмена», Esc and a click outside close the dialog with nothing sent (busy:
ignored while the request runs). No unsaved-changes guard: two small fields,
as «Добавление участника».

## 5. Dialog «История назначений» (US-3)

One component, `AppointmentHistoryDialog`, used by two hosts. It takes the
organization's id and name and the host's read (the list's route for a
holder of the instance code; the organization's route for an
administrator): the table is identical, the data are the same rows (BR-RS-10).

`DialogShell size="card"`, title «История назначений — «<название>»», footer
only «Закрыть» (`ActionButton`); nothing edits or deletes (US-3 "Nobody edits
or deletes a record"). Opens with focus on «Закрыть».

| Column | Cell |
|---|---|
| «Когда» | `formatDateTime` «ДД.ММ.ГГГГ ЧЧ:мм», one line (BR-RS-12) |
| «Кто назначил» | `UserLine` (mod-016 § 3.3: name; «неактивен» and «Неизвестный пользователь» can show only for pre-tech-025 rows) |
| «Кого назначили» | `UserLine` |
| «Причина» | plain text, wraps, `overflowWrap: anywhere`; the full reason, never truncated (up to 500 code points) |

Rows newest first, as served. The table is `TableCard aria-label="История
назначений"`; no paging (up to 100 rows, scrolled inside the dialog).

| State | Shown |
|---|---|
| Loading | `TableSkeleton`, 4 columns, 4 rows, `role="status"` «Загрузка данных» |
| Data | the table |
| Empty | `StateFrame compact` «Назначений не было» |
| Error | `ErrorBanner` with the server's text and «Повторить» inside the dialog |
| 403 (a host that lost the right) | `InlineNotice severity="error"` «Недостаточно прав»; the host page re-reads its access (mod-016 § 2.2) |
| 404 (the organization is gone for the reader) | `InlineNotice severity="error"` «Организация не найдена» |

Hosts:

- **«Все организации»:** the row's menu item (§ 3.2). After «Закрыть» focus returns to that row's «Действия».
- **«Участники»:** a header command «История назначений» (`HistoryOutlined`, `tone="neutral"`, so «Добавить участника» stays the one primary), rendered with `organizations.membership.write` only, like «Добавить участника»; it is shown to the administrator even when the organization has no appointments (the dialog then reads «Назначений не было»). The members table itself is unchanged: the appointee's «Добавлен» / «Роль изменена» already name the system administrator (BR-RS-8).

## 6. Order in which the server's answers are shown (BR-RS-6a)

The dialog's field check runs first and sends nothing when it fails. After
that the order is the server's: 403 → «Организация не найдена» → «Этого
пользователя нельзя назначить администратором» → «Нельзя назначить
администратором себя» → «Пользователь уже администратор этой организации» →
«Причина» errors. The dialog shows only the one answer it received; it does
not pre-check self or administrator state (the list has none, BR-RS-2), so the
self refusal comes from the server.

## 7. Notices

| Event | Notice |
|---|---|
| Appointed | «Администратор назначен: <имя>» |
| Others | none: refusals are in the dialog; the lists change silently |

## 8. Keyboard and screen reader

- The row menu: button `aria-label` «Действия: <название>», `aria-haspopup="menu"`; opens with Enter/Space/Down; Esc returns focus to the button.
- Dialogs trap focus (MUI); Esc closes unless busy. A refusal sets `aria-invalid` and `aria-describedby` on its field.
- «Статус» and «—» are text (hidden text for the dash). The pager, the filter and the live region have names.
- Colour is never the only carrier: the deactivated mark is a word.

## 9. Reuse and new parts

| Reused as is | From |
|---|---|
| `PageHeader`, `FilterBar` (via `ListFilter`), `TableCard`/`HeadCell`/`BodyRow`/`Cell`, `TablePager`, `TableSkeleton`, `StateFrame`, `AccessDenied`, `ErrorBanner`, `FormDialog`, `DialogShell`, `InlineNotice`, `InputField`, `ActionButton`, `LiveRegion`, `VisuallyHidden`, `Chip` via the kit's marks | `shared/ui` |
| `RowActionsMenu`, `UserSearchField`, `UserLine`, `TruncatedName`, `ListFilter`, `useNotify`, `useDebouncedValue`, `useUserSearch` | `features/organizations` |
| the members-page header pattern (command with a code) | `MembersPage` |

| New | Where | Is it a kit part? |
|---|---|---|
| `AllOrganizationsPage` | `organizations/pages` | no: a page |
| `AppointAdminDialog` | `organizations/components` | no: a composition of `FormDialog` + `UserSearchField` + `InputField` |
| `AppointmentHistoryDialog` | `organizations/components` | no: `DialogShell` + `TableCard` |
| `rescueApi.ts`, hooks, texts (`rescueTexts.ts`) | `organizations/api`, `hooks`, `lib` | no |
| `formatDate` | `shared/lib/format` | no: a function |
| a `scope` variant `appointee` in `useUserSearch` | `organizations/hooks` | no |
| nav section «Администрирование», role-table row | `app/navigation/model.ts`, `shared/api/tokenClaims.ts` | no |

No new kit component is needed. Nothing here changes the theme.

## 10. Strings added by this design

Every string below is quoted by the spec; the ones marked *(added)* are this
design's.

| Where | Text |
|---|---|
| Section, page, trail | «Администрирование» *(added)*; «Все организации» |
| Filter | «Поиск по названию»; «Очистить фильтры»; «Сбросить фильтр» (kit) |
| Columns | «Организация», «Статус», «Последнее назначение» |
| Status | «Активна», «Деактивирована» |
| Empty | «Организаций пока нет»; no match «Организации не найдены» |
| Hidden text | «Назначений не было» (reused from US-3's empty) |
| Row | «Действия: <название>» (kit pattern); «Назначить администратора»; «История назначений» |
| Dialog | «Назначение администратора»; lead and warning as § 4.1; «Пользователь»; «Причина»; «Не более 500 символов» *(helper, the error text reused)*; «Назначить»; «Отмена» |
| Errors | «Укажите причину»; «Не более 500 символов»; «Организация не найдена»; «Этого пользователя нельзя назначить администратором»; «Нельзя назначить администратором себя»; «Пользователь уже администратор этой организации»; «Недостаточно прав»; «Доступ ограничен» |
| Success | «Администратор назначен: <имя>» |
| History | «История назначений — «<название>»»; «Когда», «Кто назначил», «Кого назначили», «Причина»; «Назначений не было»; «Закрыть» |
| Live region | «Найдено: <N>» *(added)* |

Nothing names the reference tracker.

## 11. Hand-over notes for the frontend developer

- Pass the organization's name from the row to both dialogs; do not re-read it.
- Count «Причина» with `Array.from(text.trim()).length`; send the trimmed text.
- Do not render any administrator-related column or filter, ever (BR-RS-2); a reviewer will grep for it.
- The list read must not be cached across a role change: on a 403 drop the page's queries.
- The history dialog's host passes the read; the component knows no route.
- MSW fixtures: 120 organizations (3 pages of 25, the last holds 20), one deactivated, one with a last appointment, a filter returning 3 across pages.

## 12. Open points for gate-2

1. **Section name «Администрирование»** and route `/administration/organizations` are this design's choice; the spec fixes only the title «Все организации» under «Настройки».
2. **«История назначений» dialog is a 90 %-wide card** so that 500-character reasons read well; an alternative is a narrower dialog with the reason under each row. The card was chosen for the table the spec asks for.
3. **The filter and page are not in the address** (DC-5).
4. **No hint explains why a user is not found** (§ 4.1) — a deliberate non-disclosure; the runbook carries the procedure.
