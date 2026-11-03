# US-2: The hierarchy diagram: departments, projects, sub-areas

- story: US-2
- depends-on: US-1
- shippable: no — shown only through the switch of US-1 and completed by US-3's relations
- status: pending

As a **member of an organization**, I want **a diagram of departments, projects and sub-areas as they nest**, so that **I see the whole structure and where each project stands without expanding rows**.

## Acceptance criteria

```gherkin
Scenario: Departments and projects by nesting
  Given department «Логистика» holds project A, and project B stands at the top level
  When I open the diagram
  Then «Логистика» is drawn with A beneath it, joined to it
  And B is drawn at the top level, joined to no department
  And the organization itself is not drawn as a node (as in the tree)

Scenario: Sub-areas under their project
  Given project A has sub-area «Склад» with sub-area «Приёмка» beneath it
  When I open the diagram
  Then «Склад» is drawn under A and «Приёмка» under «Склад», every node joined to its parent

Scenario: Each node says what it is
  When I read any node
  Then it shows its name and its type («Департамент», «Проект», «Область»), by text and not by colour alone
  And an active project also shows its key (a deactivated project shows none: neither read carries it, BR-SD-3, BR-SD-4)

Scenario: Order as in the tree
  Given the area tree is read in display order
  When I open the diagram
  Then siblings are drawn in that same order

Scenario: Nodes open their pages
  Given project A is active
  When I click the name of A, of a department, or of a sub-area, or focus it and press Enter
  Then the object's page opens as the tree's link would (department page, project «Общие», sub-area under its project's menu)
  And clicking the node's body outside the name opens nothing (it chooses, US-3, BR-SD-10)

Scenario: A deactivated project
  Given project C is deactivated
  When I open the diagram
  Then C and its sub-areas are drawn tagged «деактивирован»
  And activating them opens nothing

Scenario: A large structure
  Given the organization has 1 000 areas over 10 levels
  When I open the diagram
  Then it is drawn within 2 s of the data arriving, measured on the stand with a seeded organization of that size in a named browser at stage 6 (not in jsdom)
  And it scrolls inside its own frame, the page itself does not scroll sideways
  And a branch can be collapsed so that its sub-areas are hidden, and expanded again

Scenario: The initial expansion
  Given the tree's rule D-3: all nodes expanded up to `TREE_EXPAND_ALL_LIMIT`, collapsed above it
  When I open the diagram of an organization at or below the limit
  Then every branch is expanded
  And above the limit the same branches are collapsed as the tree would collapse them

Scenario: A relation line into a collapsed branch
  Given project A is inside a collapsed department and has the team MARS
  When I look at the diagram
  Then the line from MARS is drawn to the nearest visible ancestor of A, marked as standing for hidden nodes
  And expanding the branch moves the line to A

Scenario: Layout details are left to design
  Then where the teams stand (a band, beside their projects, in groups) and their order are decided at the design stage
  And the criteria above and in US-3 hold whatever the layout is

Scenario: Keyboard and screen reader
  When I press Tab
  Then focus enters the diagram once, on one node, and the next Tab leaves the diagram (one Tab stop, as the tree has; BR-SD-10)
  And the arrow keys move focus between nodes, Left and Right collapse and expand a branch, with a visible focus
  And each node's accessible name states its type, its name, its parent and, for a project or a team, the number of its relations
  And the diagram announces itself as a structure with levels and positions (a tree role with level on each node), so a screen reader user can walk the hierarchy

Scenario: Narrow window
  Given the window is 640 CSS px wide
  When I open the diagram
  Then every node is reachable by scrolling its frame and no text is cut off without a full name available

Scenario: No department at all
  Given the organization has only top-level projects
  When I open the diagram
  Then the projects are drawn at the top level and no empty department frame appears
```
