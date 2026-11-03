# US-1: Switch between the tree and the diagram, the choice remembered

- story: US-1
- depends-on: -
- shippable: no — the diagram it switches to arrives with US-2 and US-3
- status: pending

As a **member of an organization**, I want **to switch «Структура» between «Дерево» and «Диаграмма»**, so that **I can read the same structure as a table or as a picture, and find the page the way I left it**.

## Acceptance criteria

```gherkin
Scenario: The tree is the default
  Given I have never chosen a view for this organization
  When I open «Структура»
  Then the tree is shown, as before this task
  And the switch shows «Дерево» as chosen

Scenario: Switch to the diagram
  Given «Структура» shows the tree
  When I choose «Диаграмма»
  Then the diagram is shown in place of the tree
  And the address stays the page's address
  And the page title «Структура» stays
  And the tree's filter «Найти область» and its «Развернуть всё» / «Свернуть всё» are not shown beside the diagram

Scenario: The choice is remembered
  Given I chose «Диаграмма» in organization A
  When I leave «Структура» and open it again, or reload the panel
  Then the diagram is shown

Scenario: The memory is per organization and per user
  Given I chose «Диаграмма» in organization A
  When I open «Структура» of organization B, or another user signs in on the same browser
  Then the tree is shown

Scenario: A damaged memory falls back
  Given the stored value is not one of the two views
  When I open «Структура»
  Then the tree is shown and no error appears

Scenario: Switching by keyboard
  Given focus is on the switch
  When I move between «Дерево» and «Диаграмма» with the arrow keys and confirm
  Then the view changes and focus stays on the switch

Scenario: Loading and failure follow the page
  Given the area tree or the selector offers are loading, or both reads failed
  When I switch the view
  Then the loading state or the error with «Повторить» is shown in either view, never a blank page
  And «Повторить» retries every read that failed

Scenario: Only the offers are not ready
  Given the area tree arrived and the selector offers are still loading or failed
  When I open the diagram
  Then the hierarchy is drawn without teams, project keys and relation lines
  And an inline notice «Команды и ключи проектов не загружены» is shown, with «Повторить» for the offers only while they failed
  And no error replaces the hierarchy

Scenario: Only the tree is not ready
  Given the selector offers arrived and the area tree is loading or failed
  When I open the diagram
  Then the loading state or the error with «Повторить» is shown, as in the tree view, and no teams are drawn alone

Scenario: The organization is not found
  Given either read answers 404
  When I open the diagram
  Then the page shows the same «not found» state as the tree view

Scenario: The two reads disagree
  Given a project node of the tree has no matching offer, or an offer has no node in the tree
  When I open the diagram
  Then the project node is drawn without its key and without team lines, and no error appears
  And an offer with no node is ignored

Scenario: Only the two reads are sent
  Given the page is open on the tree
  When I switch to «Диаграмма» and back, and choose nodes in the diagram
  Then no request is sent beyond the area tree and the selector offers (BR-SD-3)

Scenario: What survives a switch
  Given I typed a filter in the tree, expanded some rows, and chose «Диаграмма» and a node there
  When I switch back to «Дерево»
  Then the filter text and the expansion are as I left them
  And the diagram's emphasis is dropped: when I choose «Диаграмма» again nothing is emphasised
  And while the diagram was shown the filter had no effect on it (BR-SD-8)

Scenario: The empty organization
  Given the organization has no department and no project
  When I choose «Диаграмма»
  Then the same empty state as the tree's is shown, with the create action only for the holder of `area.create`

Scenario: Edits survive a switch
  Given I created a department in the tree
  When I switch to «Диаграмма»
  Then the new department is in the diagram without reloading the page
```
