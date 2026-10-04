# US-12: The shell is usable by keyboard and screen reader

- story: US-12
- depends-on: US-3, US-4, US-5
- shippable: no — a constraint on the frame, navigation and switcher of US-2..US-5; there is nothing to constrain without them
- status: pending

As **a user who works by keyboard or with a screen reader**, I want **the
shell's every control reachable, named and announced**, so that **I use the
panel as fully as anyone with a mouse**.

## Acceptance criteria

```gherkin
Scenario: Skip to the content
  Given a signed-in user on any page, focus at the top of the document
  When they press Tab once
  Then a link «Перейти к содержимому» becomes visible and focused
  When they press Enter
  Then keyboard focus moves into the main region

Scenario: Landmarks outside settings
  Given a signed-in dictionaries reader on «Главная»
  Then a screen reader finds one banner, one main region and one navigation
    region, «Основное меню»

Scenario: Landmarks on a settings page (BR-19)
  Given a signed-in dictionaries reader on «Языки»
  Then a screen reader finds one banner, one main region and three
    navigation regions: «Путь к странице», «Основное меню» and
    «Меню настроек»

Scenario: Tab order follows the layout
  Given «Главная» for a dictionaries administrator in a 1440 px window
  When the user tabs from the skip link
  Then focus visits the product mark, the three switcher levels, the user
    menu, «Главная», «Настройки», the collapse toggle, then the page's own
    controls, in that order
  Given «Языки» for the same user
  When the user tabs from the skip link
  Then focus visits the product mark, the three switcher levels, the user
    menu, the rail's «Главная» and «Настройки», «Языки» in «Меню настроек»,
    then the page's own controls, in that order
  And every focused control shows a visible focus indicator (BR-5)

Scenario: Menus by keyboard
  Given focus on the switcher's organization level
  When the user presses Enter, Space or ArrowDown
  Then its menu opens with focus on the current entry
  And ArrowUp and ArrowDown move between entries, Enter chooses one
  And Escape closes the menu and returns focus to the level
  And the user menu behaves the same way

Scenario: Collapsed rail stays usable
  Given the navigation collapsed to a rail
  When the user tabs onto an icon
  Then its label is shown as a tooltip and announced as its accessible name
  And Enter opens its page

Scenario: Reflow at 200 % zoom (D-8)
  Given a 1280 px wide window zoomed to 200 %, a 640 CSS px viewport
  When «Языки» is shown
  Then the shell switches to the narrow-window layout of US-4
  And every shell control stays operable and fully named, and none
    overlaps another
  And a name truncated with an ellipsis keeps its full text in its
    accessible name and tooltip (BR-11)
  And the page has no horizontal scroll outside the table's own container
```
