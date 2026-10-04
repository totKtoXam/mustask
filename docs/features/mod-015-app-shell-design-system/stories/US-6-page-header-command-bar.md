# US-6: Pages share one header with a command bar

- story: US-6
- depends-on: US-2
- shippable: no — its only consumers are the restyled screens of US-11
- status: pending

As **a signed-in user**, I want **every page to open with the same header —
title on the left, the page's actions on the right, its views as tabs
beneath**, so that **I find the main action of any page in the same place**.

## Acceptance criteria

```gherkin
Scenario: Title and command bar
  Given a dictionaries administrator on «Языки»
  Then the page header shows the title «Языки» at the left as the page's
    level-1 heading
  And the command bar at the right holds «Добавить язык» as a filled button
    in the theme's accent colour, with a leading plus icon

Scenario: At most one primary action (BR-2)
  Given any page header in the panel or the prototype
  Then at most one of its commands is filled in the accent colour
  And every other command is a text button with a leading icon, its label
    in the theme's neutral text colour and its icon in the accent colour

Scenario: A reader sees no write command (BR-9)
  Given a dictionaries reader without `dictionaries.languages.write`
  When they open «Языки»
  Then the command bar holds no «Добавить язык»
  And the header keeps its height, so the content does not shift

Scenario: Pivot tabs and overflow in the pattern
  Given `design.md` and the prototype
  Then they show a page header with views as tabs under the title, the
    selected tab underlined in the accent colour
  And switching a tab changes the address, so each view can be linked to
  And they show that commands which do not fit the width move into a menu
    button named «Ещё действия», in their original order

Scenario: The header scrolls with the page, the frame does not
  Given «Языки» with more rows than fit the window
  When the user scrolls the content
  Then the page header scrolls away with the content
  And the top bar and the navigation stay in place (US-2)
```
