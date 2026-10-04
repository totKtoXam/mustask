# US-11: The existing screens are restyled without losing behaviour

- story: US-11
- depends-on: US-1, US-3, US-8, US-9
- shippable: no — the restyle is the consumer of US-2..US-9; it cannot land without them
- status: pending

As **a user of today's panel**, I want **«Главная», «Языки», the sign-in
screens and the crash screen to move onto the new theme and patterns and
keep working exactly as before**, so that **the new look costs me nothing I
already rely on**.

The closed list of intended changes (BR-15). The last column names every
existing test that asserts the old behaviour and is rewritten to assert the
new one; a test not named here passes without an edit. A test may move with
its component into `shared/ui`, its assertions unchanged.

| # | Change | Story | Tests rewritten |
|---|---|---|---|
| C-1 | The display name leaves the top bar: the user menu button shows initials, is named «Профиль: <имя>», and the open menu shows the name | US-2 | `AppLayout.test.tsx` «показывает имя из токена в шапке»; e2e `14-header-name` |
| C-2 | «Выйти» moves from the top bar into the user menu, as a menu item | US-2 | `AppLayout.test.tsx` «"Выйти" вызывает signOut»; e2e `04-sign-out`, `05-logout-keycloak-down` (open the menu first) |
| C-3 | «Mustask» in the top bar is a link to «Главная», not a heading; each page title is the level-1 heading (was level 2) | US-2 | `AppLayout.test.tsx` «показывает название приложения в шапке» |
| C-4 | «Языки» moves under «Настройки» → «Справочники», address kept (D-14); the main navigation shows «Настройки» | US-3 | `AppLayout.test.tsx` «с languagesRead…» and «без languagesRead…» (assert «Настройки») |
| C-5 | An unknown address shows «Страница не найдена», address kept, instead of redirecting to «Главная» (D-13) | US-7 | none |
| C-6 | A page trail of the page's ancestors appears in the top bar | US-2 | none |
| C-7 | The browser tab reads «<title> — Mustask» instead of «Mustask» | US-2 | none |
| C-8 | Both empty states leave the table row for the shared frame; the table's header row and paging are not shown then | US-7 | none |
| C-9 | A reader's empty state hints «Языки ещё не добавлены.» | US-7 | none |
| C-10 | The filter bar offers «Очистить фильтры» while a filter is set | US-8 | none |
| C-11 | Sortable headers carry `aria-sort` | US-8 | none |
| C-12 | The form page loses «К списку»; the trail's «Языки» and «Отмена» lead back | US-9 | none |
| C-13 | Dialogs gain «Закрыть» in their header | US-9 | none |
| C-14 | «Доступ ограничен» comes from the shared frame instead of the dictionaries slice; its texts are unchanged | US-7 | `AccessDenied.test.tsx` moves with it, assertions unchanged |
| C-15 | The auth screen states and the crash screen sit on the theme, outside the shell; texts, actions and effects unchanged | US-11 | none |

## Acceptance criteria

```gherkin
Scenario: Home in the shell
  Given a signed-in user
  When they open `/` or `/home`
  Then «Главная» opens in the shell with the page header «Главная»
  And the content keeps «Добро пожаловать в Mustask» and «Mustask — трекер
    задач. Разделы появятся в меню слева по мере их готовности.»

Scenario: Dictionaries on the patterns
  Given a dictionaries administrator
  Then «Языки» uses the page header of US-6, the filter bar and table of
    US-8 and the states of US-7
  And the language form and «Назначить языком по умолчанию» use the form
    and dialog patterns of US-9
  And create, edit, activation, the default assignment with its notice, and
    the version-conflict panel behave as before

Scenario: The panel's auth screens on the theme
  Given an anonymous visitor
  When each state `AnonymousScreen` renders is shown — «Выполняется вход…»,
    «Вы вышли из системы.», «Вход не выполнен.» with each of its detail
    lines, «Сессия истекла, войдите заново.», «Сервис аутентификации
    недоступен, повторите позже.», «Выход не завершён: сервис
    аутентификации недоступен.»
  Then it is shown outside the shell, on the application background, in a
    centred card headed by the product mark «Mustask»
  And its texts, its action if any, and that action's effect are unchanged

Scenario: The crash screen on the theme
  Given a page that throws while rendering
  Then «Что-то пошло не так» and «Обновить страницу» are shown outside the
    shell on the application background, announced as an alert, as today

Scenario: In a browser (BR-2, BR-3, BR-17)
  Given a dictionaries administrator on «Языки» in a 1440×900 window
  When QA reads the computed styles
  Then body text is 14 px, the top bar is 48 px high and every row is at
    most 40 px high
  And the application background, navigation surface, content surface, top
    bar and «Добавить язык» equal the theme's matching role tokens
  And the network log shows requests only to the panel's, Mustask.Api's and
    Keycloak's origins

Scenario: Existing tests hold
  Given the frontend unit suite and the e2e specs at the start of this task
  When they run after the restyle — the e2e specs at stage 7 against the
    stand, with the human's consent given 2026-10-01 (D-22)
  Then every test passes, and the only edited ones are those the change
    table names
  And `bash scripts/verify.sh` is green

Scenario: Naming and dependencies (BR-16, BR-17)
  Given the diff of this task and its commit messages
  When they are searched case-insensitively for the reference tracker's
    product and vendor names and their abbreviations
  Then nothing is found
  And `package.json` gains no dependency that renders UI

Scenario: Parallel modules (D-4, D-12, D-16, D-18)
  Given the diff of this task
  Then it does not change any file under mod-001's slice
  And if mod-001 merged first, its entry «Процесс» is registered in the
    navigation model by this task, after its rebase
  And that entry stays hidden while the switcher reads placeholder data
    (BR-8), so it is checked in the navigation model, not on screen
  And wiring mod-001's screen to the theme lookup and the canon frames is
    left to mod-001 or, if mod-001 merged first, to the follow-up adoption
    line (BR-15)

Scenario: Keycloak's own login page is not part of this task (D-2)
  Given the diff of this task
  Then nothing under `deploy/keycloak/` is changed
```
