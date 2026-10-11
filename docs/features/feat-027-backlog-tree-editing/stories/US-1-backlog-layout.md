# US-1: A user reads the backlog laid out as the reference screen

- story: US-1
- depends-on: -
- shippable: no — the columns frame the tree of US-2 and the editing of US-4..US-6
- status: pending

As a **signed-in user**, I want **the team backlog to look as the reference
screen — its head, the hierarchy column, the order, the type, the state and
the planning values**, so that **I plan a team's work in the layout I know,
without opening each item**.

D-7 (2026-10-09) redrew this story after the human compared the first build
with the reference screenshot: the columns, the title cell, the head and the
font changed.

## Acceptance criteria

Data for every story of this task, as feat-001's: organization «Пример
организации»; project «Мастаск», key `MSK`, first team «Мастаск» (root area
«Мастаск»); team «Платформа», default area «Платформа», «Платформа / API»
below it. In «Платформа»: MSK-EPIC-1 «Вход и регистрация» (Трудоёмкость 20,
Бизнес-ценность 300, «Бизнес», Приоритет 1), MSK-EPIC-20 «Отчётность»
(«Архитектура», Приоритет 2, nothing else); MSK-FEAT-8 «Вход через SSO»
(Трудоёмкость 2,5), MSK-FEAT-6 «Вход по ссылке», MSK-FEAT-23 «Вход по коду»;
MSK-US-2 «Письмо со ссылкой», MSK-BUG-4 «Ссылка истекает раньше срока»,
MSK-BUG-5 «Письмо не доходит», MSK-US-22 «Запомнить устройство»; MSK-TASK-3
«Шаблон письма» (Приоритет 3). In «Мастаск»: MSK-FEAT-21 «Аудит входа»,
MSK-TASK-7 «Настроить SMTP». Links: MSK-EPIC-1 is the parent of MSK-FEAT-8,
MSK-FEAT-6, MSK-FEAT-21 and MSK-US-22; MSK-FEAT-8 of MSK-FEAT-23; MSK-FEAT-6
of MSK-US-2 and MSK-BUG-4; MSK-US-2 of MSK-BUG-5, MSK-TASK-3 and MSK-TASK-7.
The «Фичи» backlog of «Платформа» was reordered to
MSK-FEAT-8, MSK-FEAT-6, MSK-FEAT-23; every other backlog is in number order.
Spec BR-T-1..-3, BR-T-16; `design.md` § 2, § 3, § 4.2..§ 4.6. Rows are found
by `data-identifier`, controls by `design.md` § 5's names.

```gherkin
Scenario: The columns stand in the reference screen's order
  When the user opens the «Эпики» backlog of «Платформа»
  Then the headers read, in order: «Иерархия» (the first header cell's
    `aria-label`; it holds the two level buttons, whose names are not part of
    it), «Порядок», «Тип», «Заголовок», «Статус», «Трудоёмкость»,
    «Бизнес-ценность», «Вид ценности»
  And «Трудоёмкость» and «Бизнес-ценность» are drawn cut with an ellipsis, as
    the reference cuts its own, and are still named in full
  And there is no «ID», «Родитель», «Исполнитель», «Приоритет» or «Действия»
    column and no handle column
  And the «Фичи» and «Истории» backlogs have the same headers

Scenario: The hierarchy column's header holds the two level buttons
  Then the first header holds two small square outlined buttons,
    «Развернуть на один уровень» (a plus) and «Свернуть на один уровень» (a
    minus), each with its name as tooltip

Scenario: The title cell carries the chevron, the icon and the plain title
  Then MSK-EPIC-1's «Заголовок» shows its chevron, the epic's crown icon and
    «Вход и регистрация» as plain text — not blue, not underlined at rest —
    that opens its form
  And no identifier is drawn on the row; the title's tooltip reads
    «MSK-EPIC-1: Вход и регистрация»
  And its «Тип» reads «Эпик» as plain text with no icon, and its «Статус»
    shows the category dot and «Новая»

Scenario Outline: Each type has its icon in its colour
  Then the row of <type> shows <icon> before its title

  Examples:
    | type                     | icon                         |
    | Эпик                     | a crown, orange              |
    | Фича                     | a trophy, purple             |
    | Пользовательская история | an open book, blue           |
    | Баг                      | a bug, red                   |
    | Задача                   | a clipboard with a check, gold |

Scenario: The state is a dot by its category and the name
  Then a row in «Новые» shows a grey dot and the state's name, one in
    «В работе» a blue dot, one in «Завершённые» a green dot

Scenario: Only top-level rows are numbered
  Given MSK-EPIC-1 expanded
  Then MSK-EPIC-1 reads «1» and MSK-EPIC-20 «2» in «Порядок»
  And MSK-FEAT-8, MSK-FEAT-6 and MSK-US-22 beneath it show no «Порядок»

Scenario Outline: The planning values, and blanks where a type has no field
  Given the «Эпики» backlog with MSK-EPIC-1, MSK-FEAT-6 and MSK-US-2 shown
  Then <row> reads «Трудоёмкость» <effort>, «Бизнес-ценность» <value>,
    «Вид ценности» <area>, left-aligned for the two numbers, as the reference draws them (D-10)

  Examples:
    | row         | effort | value | area          |
    | MSK-EPIC-1  | «20»   | «300» | «Бизнес»      |
    | MSK-EPIC-20 | blank  | blank | «Архитектура» |
    | MSK-FEAT-8  | «2,5»  | blank | «Бизнес»      |
    | MSK-US-2    | blank  | blank | «Бизнес»      |
    | MSK-TASK-3  | blank  | blank | blank         |

Scenario: «Приоритет» is not on the backlog row
  Then no row of the backlog shows a «Приоритет»
  And the «Канбан» card of MSK-EPIC-1 still shows «Приоритет: 1»

Scenario: A value changed on the form shows at the next read
  When MSK-EPIC-1's «Трудоёмкость» is saved as «13» on its form
  And the user opens the «Эпики» backlog again
  Then MSK-EPIC-1 reads «13» in «Трудоёмкость»

Scenario: Rows are compact, white and quiet
  Then every row is 36 px high, white, with no line between rows
  And the header row is small grey labels with one line under it, a thin
    vertical separator after «Тип», «Заголовок», «Статус», «Трудоёмкость» and
    «Бизнес-ценность» (none after the hierarchy column, «Порядок» or the last
    header), and it stays in view while the rows scroll inside the card
  When the pointer is over a row
  Then the row turns light grey and shows the dotted grip at its left edge,
    the pale grey «+» when it has one (D-10), and «⋮» at its title cell's right edge
  When the user clicks a row
  Then it is light blue with a blue frame — 2 px at its left edge, 1 px
    elsewhere — its type icon turns dark blue, and it shows the same three
    controls
  When the user tabs into a row
  Then its controls show and the focused one has a visible focus ring

Scenario: The page head
  Then the `h1` reads «Платформа», with «Сменить команду» beside it, and the
    browser's tab reads «Бэклоги — Mustask»
  And one tab «Бэклог» stands under it, selected and underlined
  And «Эпики» with a chevron stands at the right end of the tab row, its
    accessible name «Уровень: Эпики»
  And «Новый рабочий элемент» is the one blue primary button — a leading
    plus, no trailing chevron — and «Показать канбан» a grey button with an
    icon
  And the page shows no planning pane, column options, tags, filter, full
    screen, «Аналитика» tab or favourite star

Scenario: The team drop-down moves to another team of the project
  Given «Мастаск» and «Платформа» are the project's teams
  When the user chooses «Мастаск» in «Команды проекта»
  Then the «Эпики» backlog of «Мастаск» opens, its `h1` reads «Мастаск», and
    the shell's switcher reads «Мастаск» too

Scenario: A project with one team still has the drop-down
  Given a project with one team, «Мастаск»
  Then the `h1` reads «Мастаск», with «Сменить команду» beside it, as the
    reference always draws the chevron
  When the user opens it
  Then «Команды проекта» lists «Мастаск» alone, selected, with
    `aria-current="page"`

Scenario: «Канбан» has the same head
  When the user chooses «Показать канбан»
  Then the `h1` reads «Платформа», the tab «Канбан» is selected, «Эпики»
    stands at the right end of the tab row, «Показать бэклог» is a grey
    button, and there is no primary button
  And the browser's tab reads «Канбан — Mustask»

Scenario: The panel's font is the system stack
  Then the text of the backlog, the board and the dialogs is set in Segoe UI
    where it exists, then the system sans-serif, and no Roboto file is loaded
  And the heading, the selected tab and the buttons are Semibold (600), the
    unselected tab and the body text Regular (400) — no 500, which Segoe UI
    does not have

Scenario: The skeleton names the columns
  When the backlog is loading
  Then the head stands complete and the skeleton shows «Порядок», «Тип»,
    «Заголовок», «Статус», «Трудоёмкость», «Бизнес-ценность», «Вид ценности»,
    the hierarchy header blank

Scenario: A narrow window scrolls the table, not the page
  Given a window 1 280 px wide
  Then the table scrolls sideways inside its card and the page does not
  And at 1 440 px with the navigation expanded it does not scroll sideways
```
