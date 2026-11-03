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
  And the page title and the tree's filter and expansion controls are not shown beside the diagram

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
  Given the data is loading, or a read failed
  When I switch the view
  Then the loading state or the error with «Повторить» is shown in either view, never a blank page

Scenario: The empty organization
  Given the organization has no department and no project
  When I choose «Диаграмма»
  Then the same empty state as the tree's is shown, with the create action only for the holder of `area.create`

Scenario: Edits survive a switch
  Given I created a department in the tree
  When I switch to «Диаграмма»
  Then the new department is in the diagram without reloading the page
```
