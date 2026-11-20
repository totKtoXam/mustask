# US-1: The context path button and four cascading columns

- story: US-1
- depends-on: -
- shippable: no — the columns need US-2's choice rules to be usable; the three stories ship in this task
- status: pending

As an **organization member**, I want **one button in the header that shows my context path and opens a panel of four columns, each offering only what lies under what I chose above**, so that **the list I pick from is always the relevant one**.

Cast: Ann sees everything in «Ромашка» (departments «Логистика», «Финансы»; teams MARS, VENUS in the first, JUPITER in the second; projects A, B in «Логистика», C, D in «Финансы»; MARS is linked to A and B, VENUS to A). Rules: doc-016 BR-TD-8, -11, -22(a); design § 3.0, 3.2, 3.4. «Создать …» lines and the organization-«все» columns are out (feat-052, feat-064).

## Acceptance criteria

```gherkin
Scenario: The button replaces the four selectors
  Given Ann's selection is «Ромашка» / «Логистика» / MARS / «все»
  Then the header shows one borderless button reading «Ромашка › Логистика › MARS › все» with a type icon on each segment and the colour dot of «Логистика» and MARS
  And the four feat-043 selector triggers are gone
  And the button's accessible name is «Контекст: Ромашка, Логистика, MARS, все»

Scenario: A level with nothing chosen reads «все»
  Given Ann's selection is «Ромашка» / «все» / «все» / «все»
  Then the button reads «Ромашка › все › все › все»

Scenario: The panel shows four columns
  When Ann opens the button
  Then the panel shows the columns «Организация», «Департамент», «Команда», «Проект» in that order
  And a department, team and project row has a colour stripe at its left edge and no type icon
  And the chosen value of each column is highlighted

Scenario: Esc and a click outside close the panel
  Given the panel is open
  When Ann presses Esc, or clicks outside it
  Then the panel closes and the selection is unchanged

Scenario: An organization chosen, everything else «все» (BR-TD-8)
  Given Ann's selection is «Ромашка» / «все» / «все» / «все»
  Then the department column offers «все», «Логистика», «Финансы»
  And the team column offers «все», JUPITER, MARS, VENUS
  And the project column offers «все», A, B, C, D

Scenario: A department narrows teams and projects
  Given Ann's selection is «Ромашка» / «Логистика» / «все» / «все»
  Then the team column offers «все», MARS, VENUS
  And the project column offers «все», A, B, and not C or D
  And no group «Остальные» appears in any column

Scenario: A team narrows projects to its links; a department's unlinked project stays until then
  Given the project G stands in «Логистика» and no team is linked to it
  When Ann's selection is «Ромашка» / «Логистика» / «все» / «все»
  Then the project column offers «все», A, B, G
  And with MARS chosen it offers «все», A, B, and not G
  And with VENUS chosen it offers «все», A

Scenario: The department column is not narrowed by lower levels
  Given Ann's selection is «Ромашка» / «Логистика» / MARS / A
  Then the department column still offers «все», «Логистика», «Финансы»

Scenario: The team column keeps feat-043's own-team grouping
  Given Ann is a member of VENUS and her selection is «Ромашка» / «Логистика» / «все» / «все»
  Then the team column lists VENUS under «Мои команды» and MARS under «Другие команды», and not JUPITER

Scenario: Only active objects are offered
  Given a sub-area «Склад-1», a deactivated project E and a deleted team PHOBOS exist in «Логистика»
  Then Ann's columns never offer «Склад-1», the project node of A, E or PHOBOS

Scenario: Only visible objects are offered
  Given Olga sees in «Ромашка» only VENUS, MARS, A and «Логистика»; B is active and hidden from her
  Then her department column offers «все», «Логистика», not «Финансы»
  And her project column offers «все», A, not B

Scenario: Empty offers (BR-TD-11)
  Given Pavel has no grant, no team, and no project is visible to him
  When he opens the panel
  Then the department column shows «все» and «Нет доступных департаментов»
  And the team column «все» and «Нет доступных команд», the project column «все» and «Нет доступных проектов»

Scenario: A chosen department holding nothing visible
  Given Ann's selection is «Ромашка» / «Маркетинг» / «все» / «все» and «Маркетинг» holds no team or project
  Then the team column shows «все» and «Нет доступных команд», the project column «все» and «Нет доступных проектов»

Scenario: A user with no organization
  Given Nina has no active organization
  Then the header keeps today's link «Нет организаций» and no panel opens

Scenario: Organization «все» is inert here (BR-10; feat-064 lifts it)
  Given Olga's selection is «Все организации»
  Then the button reads «Все организации › все › все › все»
  And the department, team and project columns show only «все» and the muted line «Выберите организацию»
  And the search field is disabled with the helper «Выберите организацию»
  And choosing an organization in the first column follows US-2

Scenario: The lists follow the selection
  Given Ann's selection is «Ромашка» / «Логистика» / «все» / «все»
  Then «Проекты» lists A and B and «Команды» lists MARS and VENUS
  And with «Маркетинг», which holds nothing, chosen: «Проекты» reads «Здесь нет проектов, доступных вам.» and «Команды» «Здесь нет команд, доступных вам.»

Scenario: Loading and failure
  When the offers are loading, the panel shows four skeleton rows under the search field
  And when the read fails it shows «Не удалось загрузить список. Попробуйте ещё раз.» with «Повторить»

Scenario: Keyboard
  Given the panel is open
  When Ann presses ↓ from the search field, then ↑ ↓ inside a column, ← → between columns and Enter
  Then focus moves as pressed and Enter chooses the focused row
```
