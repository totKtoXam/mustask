# US-7: Every screen shows the same canon of states

- story: US-7
- depends-on: US-6
- shippable: no — the shared states are consumed by the restyled screens of US-11; alone they are components nothing renders
- status: pending

As **a signed-in user**, I want **loading, emptiness, failure and refusal to
look and read the same on every screen**, so that **I can tell at once
whether to wait, add something, loosen my filter, retry or ask for
access**.

Canon: BR-12; not found by level: BR-18. The shared frame is an icon, a heading, a hint and at most one
action, centred in the area it replaces. Its heading is the page's level-1
heading when it replaces the whole page (access denied, not found), and a
level-2 heading under the page header otherwise. It is not an alert.

## Acceptance criteria

```gherkin
Scenario: Loading is a skeleton of the content (BR-12)
  Given the languages request has not answered yet
  When a dictionaries reader opens «Языки»
  Then the page header and the filter bar are shown, and the table area
    shows placeholder rows under the real column headers
  And the placeholder is announced as a status named «Загрузка данных»
  And no spinner replaces the whole page

Scenario: Error with retry
  Given the languages request fails with a server error
  When «Языки» is shown
  Then the shared error component appears with the error text and
    «Повторить»
  When the user activates «Повторить» and the request now succeeds
  Then the rows are shown and the error is gone

Scenario: Empty, for an administrator
  Given a dictionary with no languages and a dictionaries administrator
  When they open «Языки» with no filter set
  Then the page header and the filter bar stay, and the table, its header
    row and paging give way to the shared frame
  And the frame reads «Справочник пуст» with the hint «Добавьте первый язык
    кнопкой «Добавить язык» выше.» and offers «Добавить язык»

Scenario: The empty state offers only what the user may do (BR-9)
  Given a dictionary with no languages and a reader without the write code
  When they open «Языки»
  Then the frame reads «Справочник пуст» with the hint «Языки ещё не
    добавлены.» and offers no button

Scenario: Empty after a filter
  Given a dictionary with languages and a filter that matches none of them
  When the registry is shown
  Then the page header and the filter bar with its values stay, and the
    table, its header row and paging give way to the shared frame
  And the frame reads «Ничего не найдено» with the hint «Ни один язык не
    подходит под заданные условия фильтра.» and offers «Сбросить фильтр»
  And «Сбросить фильтр» is the only control on the page with that name
  When the user activates it
  Then every filter is cleared and page 1 is shown

Scenario: Access denied, read-gated page
  Given a signed-in user without `dictionaries.languages.read`
  When they open `/dictionaries/languages`
  Then the shell stays in place and the frame replaces the page, reading
    «Доступ ограничен» with «У вас нет прав для просмотра этого раздела.
    Обратитесь к администратору, чтобы запросить доступ.»
  And no languages request is sent

Scenario: Access denied, write-gated page
  Given a dictionaries reader without `dictionaries.languages.write`
  When they open `/dictionaries/languages/new` or `/dictionaries/languages/ru`
  Then the same «Доступ ограничен» frame replaces the page
  And no heading «Добавить язык» is shown

Scenario: Not found (BR-13)
  Given a signed-in user
  When they open `/no-such-page`
  Then the address stays `/no-such-page`
  And the shell shows the frame «Страница не найдена» with «Проверьте адрес
    или вернитесь на главную.» and the action «На главную»
  When they activate «На главную»
  Then «Главная» opens

Scenario Outline: Not found names the missing context (BR-12, BR-18, D-15)
  Given, in a component test, the shared not-found frame used for <level>,
    as mod-002's pages will use it
  Then the frame reads «<heading>» with «Проверьте адрес или вернитесь на
    главную.» and the action «На главную»
  And the browser tab reads «<heading> — Mustask»

  Examples:
    | level           | heading                |
    | an organization | Организация не найдена |
    | a project       | Проект не найден       |
    | a team          | Команда не найдена     |

Scenario: One frame for the full-area states
  Given the empty, empty-after-filter, access-denied and not-found states
  Then each is laid out by the same shared component
  And `design.md` and the prototype show the same frame for the board,
    backlog and work item list empties of US-10
```
