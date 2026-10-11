# US-3: A user expands or collapses the whole table one level

- story: US-3
- depends-on: US-2
- shippable: no — the header buttons work on US-2's tree
- status: pending

As a **signed-in user**, I want **to open or close one more level of the
whole backlog with one click**, so that **I switch between the epics alone,
the epics with their features, and the full tree without opening each row**.

## Acceptance criteria

Data: US-1's, on the «Эпики» backlog of «Платформа». Depth 0 is a top-level
row. Spec BR-T-7, BR-T-8; `design.md` § 4.5 (the hierarchy column's header).
The tree first opens expanded (BR-T-7, D-6 OQ-2 (d)); "every row collapsed"
below is reached with «Свернуть на один уровень» until it is unavailable.

```gherkin
Scenario: The two buttons look as the reference's
  Then the hierarchy header holds two 24 px square buttons with a thin dark
    outline, a plus («Развернуть на один уровень») and a minus («Свернуть на
    один уровень»), 4 px apart
  And a button with nothing to do is dimmed, still focusable, with
    `aria-disabled="true"`

Scenario: The first state leaves «Развернуть на один уровень» nothing to do
  When the user opens the «Эпики» backlog of «Платформа»
  Then every row with a child is expanded, so «Развернуть на один уровень»
    is marked unavailable and «Свернуть на один уровень» is not

Scenario: Each click opens one more level
  Given every row collapsed
  When the user activates «Развернуть на один уровень»
  Then MSK-EPIC-1 is expanded and its children MSK-FEAT-8, MSK-FEAT-6 and
    MSK-US-22 show, collapsed
  And the status region says «Показано строк: 5.»
  When the user activates it again
  Then MSK-FEAT-23, MSK-US-2 and MSK-BUG-4 show as well
  When the user activates it a third time
  Then MSK-BUG-5 and MSK-TASK-3 show, and «Развернуть на один уровень» is
    marked unavailable with `aria-disabled="true"`, still focusable

Scenario: Each click closes the deepest open level
  Given the whole tree expanded
  When the user activates «Свернуть на один уровень»
  Then MSK-BUG-5 and MSK-TASK-3 are hidden and MSK-US-2 is collapsed
  And MSK-EPIC-1, MSK-FEAT-8 and MSK-FEAT-6 are still expanded
  When the user activates it twice more
  Then only MSK-EPIC-1 and MSK-EPIC-20 show, and «Свернуть на один уровень»
    is marked unavailable

Scenario: Expanding completes a level the user opened in part
  Given MSK-EPIC-1 and, beneath it, only MSK-FEAT-6 expanded
  When the user activates «Свернуть на один уровень»
  Then MSK-FEAT-6 is collapsed and MSK-EPIC-1 stays expanded
  When the user expands MSK-FEAT-6 again and activates «Развернуть на один
    уровень»
  Then MSK-FEAT-8 is expanded too, and MSK-US-2 stays collapsed

Scenario: Nothing to expand or collapse
  Given the «Фичи» backlog of «Мастаск», whose one row MSK-FEAT-21 has no
    child
  Then both header buttons are marked unavailable
  And activating either changes nothing and announces nothing

Scenario: The buttons change only what is shown
  When the user expands and collapses by the header buttons
  Then no request is sent, and no order, link or «Изменён» changes
  And «Порядок» still numbers MSK-EPIC-1 «1» and MSK-EPIC-20 «2»

Scenario: The buttons work by keyboard
  Given every row collapsed, and focus on «Развернуть на один уровень»
  When the user presses Enter, then Space
  Then two levels open and focus stays on the button

Scenario: The state the buttons leave is kept like any expansion
  Given every row collapsed and two levels opened with «Развернуть на один
    уровень»
  When the tab loses and regains focus
  Then the same rows are expanded
  When the user opens «Фичи» and comes back
  Then the tree stands as at the first open
```
