# US-4: Navigation collapses and adapts to narrow windows

- story: US-4
- depends-on: US-3
- shippable: no — collapses the navigation US-3 fills; with nothing in it there is nothing to collapse
- status: pending

As **a signed-in user on a laptop or a narrow window**, I want **to fold
the navigation down to icons, and have it get out of the way on its own
when space is short**, so that **wide lists and boards keep the room they
need**.

Rules: BR-10 (collapse, widths, focus), BR-19 (settings pages), D-8 (the
640 CSS px floor).

## Acceptance criteria

```gherkin
Scenario: Collapse to a rail and back (BR-10)
  Given a dictionaries reader on «Главная» in a 1440 px wide window with the
    navigation expanded
  When they activate «Свернуть меню» at the bottom of the navigation
  Then the navigation becomes a rail of icons without labels
  And each icon has the accessible name of its entry and shows it as a
    tooltip on hover and on keyboard focus
  And the toggle now reads «Развернуть меню» and has `aria-expanded="false"`
  When they activate «Развернуть меню»
  Then the labels are back and the toggle has `aria-expanded="true"`

Scenario: The choice is kept per browser
  Given the user collapsed the navigation
  When they reload the page, or open the panel in a new tab of the same
    browser
  Then the navigation is collapsed

Scenario: Defaults by width when no choice is kept
  Given a browser with no kept choice
  When the panel opens in a 1440 px wide window
  Then the navigation is expanded
  When the window is narrowed to 1200 px
  Then the navigation becomes a rail
  When it is widened to 1440 px again
  Then the navigation is expanded again

Scenario: A kept choice holds across widths at 1024 px and above
  Given the user expanded the navigation in a 1440 px window
  When the window is narrowed to 1200 px
  Then the navigation stays expanded

Scenario: Narrow window
  Given a window 900 px wide, with or without a kept choice
  Then the navigation is not shown and the top bar has a «Меню» button
  When the user activates «Меню»
  Then the navigation opens over the content
  When they press Escape or click outside it
  Then it closes and keyboard focus returns to «Меню»
  When they open it again and choose «Главная»
  Then it closes, «Главная» opens and keyboard focus is in its main region
  When the window is widened to 1440 px
  Then the kept choice, if any, applies again

Scenario: The overlay on a settings page (BR-19)
  Given a dictionaries reader on «Языки» in a 900 px window
  When they activate «Меню»
  Then the overlay shows the rail with «Настройки» marked current and
    «Меню настроек» beside it

Scenario: Settings pages keep the rail (BR-19)
  Given a dictionaries reader who expanded the navigation, in a 1440 px window
  When they open «Языки»
  Then «Основное меню» is a rail and no collapse toggle is shown
  When they return to «Главная»
  Then the navigation is expanded again, as kept

Scenario: No horizontal page scroll down to the floor (D-8)
  Given «Языки» in a window 1024 px wide with the navigation expanded, and
    again in a window 640 px wide
  Then the page itself has no horizontal scroll bar
  And if the table is wider than the content area, it scrolls inside its own
    container

Scenario: Reduced motion
  Given the browser emulates `prefers-reduced-motion: reduce`
  When the navigation collapses or expands
  Then it changes width without an animated transition
```
