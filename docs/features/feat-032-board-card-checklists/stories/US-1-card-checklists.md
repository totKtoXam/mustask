# US-1: A user sees a card's tasks, bugs and test cases as collapsible checklists, with who is assigned

- story: US-1
- depends-on: -
- shippable: yes — read-only, and everything it draws is used; not split off only because the route is one M task (spec § Stories)
- status: pending

As a **signed-in user**, I want **each card to show the tasks, bugs and test
cases under its item as counters that open their own checklists, and to see who
each is assigned to**, so that **I read the progress of a story and who does
what without opening its form**.

## Acceptance criteria

Data: the «Истории» board of «Платформа». MSK-US-2 «Повторная отправка письма»
in «Активна», assigned to «Анна Смирнова», with the children below; MSK-US-9 in
«Новая», nobody assigned, no children; MSK-FEAT-6 as a feature on the «Фичи»
board with children MSK-US-2 and MSK-BUG-4 (a bug in «Активна»). MSK-TASK-7 is
seeded directly into the Removed category, as no item can be there before
mod-005. The test cases exist only in a fixture process with a type of
appearance `testcase` until feat-031 is in `main`. Spec BR-C-1..-6, -10..-12.

| Child | Type | State | Assignee |
|---|---|---|---|
| MSK-TASK-3 «be» | Задача | «Закрыта» | Анна Смирнова |
| MSK-TASK-4 «fe» | Задача | «Новая» | Не назначен |
| MSK-TASK-6 «qa» | Задача | «Активна» | Борис Петров |
| MSK-TASK-7 «release» | Задача | Removed category | Борис Петров |
| MSK-BUG-5 «Неверный счётчик» | Баг | «Закрыта» | Борис Петров |
| MSK-BUG-8 «Дубль» | Баг | «Разрешена» | Анна Смирнова |
| MSK-TC-10 «Письмо приходит» (fixture) | Тест-кейс | «Готов» | Анна Смирнова |
| MSK-TC-12 «Письмо не дублируется» (fixture) | Тест-кейс | «Закрыта» | Анна Смирнова |

```gherkin
Scenario: A card shows its counters under its assignee, lists collapsed
  When the user opens the «Истории» board
  Then the card of MSK-US-2 shows, under «Анна Смирнова», a row of counters:
    the task icon with «1/3», the bug icon with «1/2», and no list
  And the counter «1/3» has the accessible name «1/3 — MSK-US-2, Задача: выполнено 1 из 3»
  And MSK-TASK-7 is in no counter and no list

Scenario: A «Разрешена» bug is not done
  Then MSK-BUG-8 is counted in the bug total and not in its done count

Scenario: Test cases are a count only, whatever their state
  Given the fixture process with the type of appearance `testcase`
  Then the card of MSK-US-2 also shows the flask icon with «2», named
    «2 — MSK-US-2, Тест-кейс: 2», after the bug counter
  And MSK-TC-12 in «Закрыта» is counted

Scenario: Without a test-case type there is no flask
  Given the process has no type of appearance `testcase`
  Then no card shows a flask counter

Scenario: A story with no children still has its task counter
  Then the card of MSK-US-9 shows the task counter «0/0» and no other
  And a card of a type that offers no task, with no children, shows no row of counters

Scenario: A counter opens its own list and a second press closes it
  Given every list is collapsed
  When the user presses the task counter of MSK-US-2
  Then the list shows MSK-TASK-3, MSK-TASK-4, MSK-TASK-6 in that order
  And the counter has `aria-expanded="true"` and the card is still in its column
  When the user presses it again
  Then the list is gone and `aria-expanded` is "false"

Scenario: A counter works by keyboard
  When the user tabs to the task counter of MSK-US-2 and presses Enter
  Then its list opens
  When the user presses Space
  Then it closes

Scenario: The lists are independent
  When the user opens the task counter and then the bug counter of MSK-US-2
  Then both lists are shown, the task list above the bug list

Scenario: Rows are in the order of the child's number, for every kind
  Given the bug list of MSK-US-2 is open
  Then MSK-BUG-5 stands before MSK-BUG-8, whatever their order on the bug backlog

Scenario: A row shows what the reference shows
  Given the task list of MSK-US-2 is open
  Then each row shows a checkbox, the task icon, an avatar and the title as a link
  And MSK-TASK-3's checkbox is checked, MSK-TASK-4's and MSK-TASK-6's are not
  And MSK-TASK-3's avatar shows the initials «АС» and its accessible name is
    «Анна Смирнова», MSK-TASK-4's is the neutral person avatar named «Не назначен»

Scenario: An assignee the directory does not know
  Given MSK-TASK-6's assignee is not in the directory
  Then its row's avatar is the neutral person avatar named «Неизвестный пользователь»

Scenario: A test case's row has no checkbox
  Given the fixture process and the flask list of MSK-US-2 is open
  Then MSK-TC-10's row shows the flask icon, an avatar and the title, and no checkbox

Scenario: The chevron closes a list
  Given the task list of MSK-US-2 is open
  When the user activates «Свернуть список: Задача (MSK-US-2)»
  Then the list is closed and focus is on the task counter

Scenario: A row's title opens the child
  When the user activates «be» in the list
  Then MSK-TASK-3 opens the way a card's own title opens an item

Scenario: The assignee is on the card
  Then the card of MSK-US-2 shows the avatar «АС» with «Анна Смирнова»
  And the card of MSK-US-9 shows «Не назначен»

Scenario: What was opened stays opened across reads, not across visits
  Given the task list of MSK-US-2 is open
  When the board is read again after a card move
  Then the list is still open
  When the user reloads the page, opens another level and comes back, or comes
    back from an item's form
  Then every list is collapsed

Scenario: Controls inside the card do not start a card drag
  When the user presses and drags from a counter, a checkbox, a row, a title
    link, «Добавить задачу» or the open add field
  Then no card drag starts and the card stays in its column
  And text in the add field can be selected with the pointer
  When the user drags the card from the rest of its surface
  Then the card is dragged as in mod-006 US-5

Scenario: Only task, bug and test-case children are listed
  When the user opens the «Фичи» board
  Then the card of MSK-FEAT-6 has no counter for MSK-US-2, and none for its story children
  And the bug MSK-BUG-4, a child of MSK-FEAT-6, makes a bug counter «0/1» there

Scenario: Children of another team count
  Given MSK-TASK-4's Область resolves to «Мастаск»
  Then MSK-TASK-4 is in MSK-US-2's list and in its task total
  And the backlog's tree, unlike the card, shows no row for it beneath MSK-US-2

Scenario: A child bug is still its own card
  Given MSK-BUG-5 is a child of MSK-US-2
  Then the «Истории» board also shows a card of MSK-BUG-5 in its column

Scenario: The checklists come with the board's answer
  When the user opens a counter
  Then no request is sent
  And the board of 5 000 cards with five children each, all lists collapsed, is read within 2 s
  And a card with 200 children opens its list within 100 ms

Scenario: The board's other behaviour is unchanged
  Then columns, counts, the card menu «Ещё действия: <ID>» and the drag of
    a card behave as in mod-006 US-4 and US-5
```
