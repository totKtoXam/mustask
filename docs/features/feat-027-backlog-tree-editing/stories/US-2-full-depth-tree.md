# US-2: A user sees the whole tree under a row

- story: US-2
- depends-on: US-1
- shippable: no — the tree is what US-3 expands and US-4..US-6 edit
- status: pending

As a **signed-in user**, I want **to open a row down to its tasks and
bugs**, so that **I see everything an epic, a feature or a story is made of
on one page**.

## Acceptance criteria

Data: US-1's. Spec BR-T-4..-7, BR-T-15, BR-T-17; `design.md` § 4.4
(chevron), § 4.12 (first state). The tree first opens expanded as deep as the
2 000 drawn rows allow (BR-T-7, D-6 OQ-2 (d)); a scenario that starts
"collapsed" reaches it with «Свернуть на один уровень» until that is
unavailable.

```gherkin
Scenario: The first state is expanded as far as the budget allows
  When the user opens the «Эпики» backlog of «Платформа»
  Then every row with a child is expanded: the ten rows of the table below
    show, MSK-FEAT-21 and MSK-TASK-7 not among them
  Given 5 000 items on each of four levels, each under one of the level
    above (5 000 top-level rows)
  Then the page opens with no row expanded, as one level more would draw
    more than 2 000 rows

Scenario: The «Эпики» tree reaches the tasks
  Given the «Эпики» backlog of «Платформа», every row collapsed
  When the user expands MSK-EPIC-1, MSK-FEAT-8, MSK-FEAT-6 and MSK-US-2
  Then the rows read, indented by depth:
    | depth | row         |
    | 0     | MSK-EPIC-1  |
    | 1     | MSK-FEAT-8  |
    | 2     | MSK-FEAT-23 |
    | 1     | MSK-FEAT-6  |
    | 2     | MSK-US-2    |
    | 3     | MSK-BUG-5   |
    | 3     | MSK-TASK-3  |
    | 2     | MSK-BUG-4   |
    | 1     | MSK-US-22   |
    | 0     | MSK-EPIC-20 |

Scenario: Children stand by level, then in their backlog's order
  Then beneath MSK-EPIC-1 the features come first, in the «Фичи» order,
    then MSK-US-22
  And beneath MSK-US-2 its bug MSK-BUG-5 comes before its task MSK-TASK-3

Scenario: Another team's children stand beneath no row
  When MSK-EPIC-1 and MSK-US-2 are expanded
  Then MSK-FEAT-21 and MSK-TASK-7, both in «Мастаск»'s area, are not shown
  And MSK-FEAT-21 is a top-level row of «Мастаск»'s «Фичи» backlog

Scenario: A same-level child stands beneath its parent only
  When the user opens the «Фичи» backlog of «Платформа»
  Then the top-level rows are MSK-FEAT-8 «1» and MSK-FEAT-6 «2»
  And MSK-FEAT-23 shows only beneath an expanded MSK-FEAT-8, with no
    «Порядок»

Scenario: «Истории» rows open to their bugs and tasks
  When the user opens the «Истории» backlog of «Платформа»
  Then the top-level rows are MSK-US-2, MSK-BUG-4, MSK-US-22, numbered 1..3
  And MSK-US-2 has «Развернуть MSK-US-2»; expanded, it shows MSK-BUG-5 and
    MSK-TASK-3
  And MSK-BUG-4 and MSK-US-22 have no chevron

Scenario: The chevron stands left of the icon and works by keyboard
  Then MSK-EPIC-1's chevron stands between the indent and the type icon,
    and a row with no child keeps an empty box of the same width, so the
    icons of one depth line up
  And one depth is indented 16 px
  When focus is on «Развернуть MSK-EPIC-1» and the user presses Enter
  Then the row expands, the control reads «Свернуть MSK-EPIC-1» with
    `aria-expanded="true"`, and focus stays on it

Scenario: A child in «Удалена» frees its parent's place
  Given MSK-BUG-5's state is placed in the Removed category directly
  When the «Истории» backlog is read
  Then MSK-BUG-5 shows nowhere, and MSK-US-2 expands to MSK-TASK-3 only

Scenario: Expanded rows survive the page's re-reads
  Given MSK-EPIC-1 and MSK-FEAT-6 expanded
  When the tab loses and regains focus
  Then both are still expanded
  When the user reloads, or opens «Фичи» and comes back
  Then the tree stands as at the first open (BR-T-7)

Scenario: A link made elsewhere shows at the next read
  Given MSK-EPIC-1 expanded
  When MSK-US-22's parent is set to MSK-FEAT-8 on its form in another tab
  And this tab regains focus
  Then MSK-US-22 is no longer a child of MSK-EPIC-1 and shows beneath
    MSK-FEAT-8

Scenario: Expanding asks the server nothing
  When the user expands and collapses any row
  Then no request is sent and no order, link or «Изменён» changes

Scenario: A large backlog stays fast
  Given 5 000 items on each of «Платформа»'s four levels, each one under an
    item of the level above
  Then the «Эпики» and the «Истории» backlogs are each served within 2 s
  And expanding a row shows its children within 1 s while the table draws
    at most 2 000 rows
```
