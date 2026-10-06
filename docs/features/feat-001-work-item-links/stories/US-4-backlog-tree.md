# US-4: A user expands a backlog row to see its children

- story: US-4
- depends-on: US-1
- shippable: no — the tree displays the links of US-1 and US-5
- status: pending

As a **signed-in user**, I want **to open a row of my team's backlog and see
the work hung under it**, so that **I plan children under their parent
without leaving the list**.

## Acceptance criteria

Data: US-1's, plus MSK-EPIC-20 «Отчётность» and MSK-FEAT-23 «Вход по коду»
in «Платформа», MSK-FEAT-21 «Аудит входа» in «Мастаск» (team «Мастаск»),
MSK-US-22 «Запомнить устройство» in «Платформа». Links: MSK-EPIC-1 is the
parent of MSK-FEAT-6, MSK-FEAT-8 and MSK-FEAT-21; MSK-FEAT-6 of MSK-US-2 and
MSK-BUG-4; MSK-FEAT-8 of MSK-FEAT-23; MSK-EPIC-1 of MSK-US-22. The «Фичи»
backlog of «Платформа» reads MSK-FEAT-8, MSK-FEAT-6, MSK-FEAT-23. Spec
BR-H-9..-13, § Page «Бэклоги».

```gherkin
Scenario: Rows start collapsed, an expand control only where children show
  When the user opens the «Эпики» backlog of «Платформа»
  Then the rows are MSK-EPIC-1 and MSK-EPIC-20, both collapsed
  And MSK-EPIC-1 has «Развернуть MSK-EPIC-1» with `aria-expanded="false"`,
    MSK-EPIC-20 no expand control

Scenario: Expanding shows the children of the level below in their order
  When the user activates «Развернуть MSK-EPIC-1»
  Then MSK-FEAT-8 and MSK-FEAT-6 show beneath it, in that order, indented
    one tier, each with no «Порядок» and no handle
  And their «Родитель» cells read MSK-EPIC-1
  And the control reads «Свернуть MSK-EPIC-1», focus still on it

Scenario: A nested row expands one tier further
  Given MSK-EPIC-1 expanded
  When the user activates «Развернуть MSK-FEAT-6»
  Then MSK-US-2 and MSK-BUG-4 show beneath MSK-FEAT-6, indented two tiers

Scenario: Collapsing hides the children and changes nothing
  Given MSK-EPIC-1 and MSK-FEAT-6 expanded
  When the user activates «Свернуть MSK-EPIC-1»
  Then only MSK-EPIC-1 and MSK-EPIC-20 show
  And no order, link or «Изменён» time has changed

Scenario Outline: A link that does not nest leaves the child a top-level row elsewhere
  Then <child> is shown beneath no row of «Платформа»
  And it is a top-level row of <its backlog>, its «Родитель» reading <parent>

  Examples:
    | child       | why                       | its backlog                  | parent     |
    | MSK-FEAT-21 | another team's backlog    | «Фичи» of «Мастаск»          | MSK-EPIC-1 |
    | MSK-US-22   | parent two levels up      | «Истории» of «Платформа»     | MSK-EPIC-1 |
    | MSK-FEAT-23 | parent on its own level   | «Фичи» of «Платформа»        | MSK-FEAT-8 |

Scenario: Unparented rows are normal
  When the user opens the «Фичи» backlog of «Платформа» with nothing expanded
  Then MSK-FEAT-23's «Родитель» reads MSK-FEAT-8 and MSK-FEAT-6's MSK-EPIC-1
  And no row is marked as a warning or an error

Scenario: «Истории» rows do not expand in this cut
  Given MSK-TASK-3 has the parent MSK-US-2
  When the user opens the «Истории» backlog of «Платформа»
  Then no row has an expand control (OQ-5)

Scenario: What is expanded survives the page's re-reads, not a reload
  Given MSK-EPIC-1 expanded
  When the user reorders MSK-EPIC-20 to the top, and the tab loses and
    regains focus
  Then MSK-EPIC-1 is still expanded
  When the user reloads, or opens «Фичи» and comes back
  Then every row is collapsed

Scenario: A reorder moves a row with its shown children
  Given MSK-EPIC-1 expanded
  When the user drags MSK-EPIC-20 by its handle
  Then a drop line shows only between blocks — above MSK-EPIC-1, below
    MSK-FEAT-6, below MSK-EPIC-20 — never between nested rows
  When the user chooses «Выше» in MSK-EPIC-20's handle menu instead
  Then the rows read MSK-EPIC-20, MSK-EPIC-1, MSK-FEAT-8, MSK-FEAT-6
  And «MSK-EPIC-20 перемещён: позиция 1 из 2.» is announced

Scenario: A link changed elsewhere shows at the next read
  Given MSK-EPIC-1 expanded
  When MSK-FEAT-6's parent is cleared on its form in another tab
  And this tab regains focus
  Then MSK-FEAT-6 is no longer beneath MSK-EPIC-1, and MSK-FEAT-8 is

Scenario: The expand control works by keyboard
  Given focus on «Развернуть MSK-EPIC-1»
  When the user presses Enter, then Space
  Then the row expands, then collapses, focus staying on the control

Scenario: A large backlog stays fast
  Given 5 000 items on each of «Платформа»'s three levels, every feature
    under an epic and every story under a feature
  Then the «Эпики» backlog is served within 2 s
  And expanding any epic shows its features within 1 s
```
