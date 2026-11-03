# US-2: The hierarchy diagram: sections, levels, layouts, search, expand all, counters

- story: US-2
- depends-on: US-1
- shippable: no — shown only through the switch of US-1 and completed by US-3's relations
- status: pending

As a **member of an organization**, I want **a diagram of departments, projects and sub-areas as they nest, in two layouts, with the nesting levels I choose**, so that **I see the whole structure or only the levels I care about, at which level each node stands and where each project is, and I can find a node and open the whole picture in one step**.

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
  Then siblings are drawn in that same order, in both layouts

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
  When I open the diagram in either layout
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

Scenario: Two sections
  Given team ATLAS is linked to no project and no department, and MARS works on a project
  When I open the diagram
  Then the section «Без проектов и департаментов» is drawn first, holding ATLAS and not MARS, in the order of the selector offers (by name), with no lines
  And the section «Департаменты, проекты и области» follows, holding the hierarchy and the teams that have relations
  And each header shows its title and a count (teams, nodes)
  And when no team qualifies for the first section it is not drawn, with no empty caption

Scenario: Sections fold and unfold
  When I click a section header, or focus it and press Enter or Space, or press Left / Right
  Then the section's content hides or shows, the header and its count stay, and the header announces «<раздел>: свёрнуто» / «<раздел>: развёрнуто»
  And folding «Департаменты, проекты и области» hides the hierarchy and the teams drawn beside it
  And with both folded the frame shows the two headers only
  And the folds are not stored and survive a view switch for the visit (US-1)

Scenario: Choosing unfolds what it needs
  Given the section «Департаменты, проекты и области» is folded and team MARS works on project A
  When I choose MARS in the other section, or find A with «Найти узел» and press Enter
  Then the section unfolds, with the branches hiding A (BR-SD-11), and A is emphasised or focused

Scenario: Level chips are a clickable ruler
  Given the hierarchy is three levels deep: department, project, sub-area
  When I open the diagram
  Then a chip «1», «2», «3» stands for each level of the whole tree and for no absent level, in a group named «Уровни вложенности»
  And in the horizontal layout each chip stands above its column, in the vertical layout at the start of its row
  And every hierarchy node shows its true level number, department 1, its project 2, the sub-area 3 (a top-level project is 1); teams carry none
  And with no chip pressed all levels are shown

Scenario: Only the chosen levels
  Given the mode «Режим уровней» is «Только выбранные уровни» (the default)
  When I press the chips «1» and «3»
  Then exactly the nodes of levels 1 and 3 are shown, the chips «1» and «3» are marked pressed
  And a second press of a chip unpresses it
  And the announcement «Показаны уровни: 1, 3» is made and focus stays on the chip

Scenario: A level and its nested ones
  Given the mode «Режим уровней» is «Уровень и вложенные»
  When I press the chip «2», or «2» and then «3»
  Then the nodes of level 2 and of every deeper level are shown, the topmost pressed chip decides and a deeper pressed chip changes nothing
  And the deeper chips that are included are marked by a lighter tint besides the text
  And the announcement reads «Показаны уровни: 2 и более глубокие»

Scenario: All levels
  Given some chips are pressed
  When I press «Все уровни»
  Then no chip is pressed, every level is shown, the mode is unchanged and «Показаны все уровни» is announced
  And while no chip is pressed «Все уровни» is disabled

Scenario: Hidden levels drop their rows
  Given levels «1» and «3» are shown and level «2» is hidden
  When I look at the picture
  Then no node of level 2 is drawn
  And a level-3 node is joined by a dotted nesting join to its nearest visible ancestor (a solid join means a direct parent)
  And a node with no visible ancestor is drawn at its own level with no join
  And indent and rows keep their true level, so gaps show missing levels and the chips stay aligned

Scenario: Hidden levels and teams
  Given the level of project A is hidden and A has the team MARS
  When I look at the diagram
  Then MARS is still drawn (teams are never hidden by levels)
  And its line goes dotted to the nearest visible ancestor of A
  And with no visible ancestor the line is not drawn and MARS stands at the end of the teams with no line
  And the counts and the summaries (US-3) still speak of all data

Scenario: Levels with expansion and «Только связанные»
  Given a branch is collapsed, or the mode «Только связанные» shows a chosen node
  When I choose levels
  Then the level choice applies after expansion and after «Только связанные», so the intersection is shown (BR-SD-11)
  And a chosen node on a hidden level stays chosen and its emphasis, lines and summary remain

Scenario: Levels that show nothing
  When the chosen levels hold no node
  Then the hierarchy section says «Нет узлов на выбранных уровнях», the teams stay

Scenario: The level choice is not stored
  Given I chose levels
  When I switch the view and back, the level choice is as I left it
  When I reload the panel, leave the page, or change the organization
  Then every level is shown and nothing about levels is in browser storage (BR-SD-9)

Scenario: Horizontal layout
  Given the layout is «Горизонтально» (the default)
  When I open the diagram
  Then the hierarchy stands on the left, one row per visible node in tree order, indented by true level
  And the teams stand on the right, each beside the mean height of its anchors in the hierarchy, in that order, without overlap

Scenario: Vertical layout
  Given I chose «Раскладка: Вертикально»
  When I open the diagram
  Then each level is a row, departments and top-level projects on top, deeper levels below, and a parent stands above its visible children, joined to them
  And the section «Без проектов и департаментов» is above, across the frame width
  And the teams stand in one row below the hierarchy with the caption «Команды», each under the mean position of its anchors, without overlap
  And the level chips stand at the start of their rows
  And the order of the nodes, the keyboard order, the links, the levels and the relation lines are as in the horizontal layout

Scenario: The layout is switched
  Given the diagram is open with a node chosen, levels chosen and a section folded
  When I choose the other layout in «Раскладка»
  Then the picture is redrawn in the other layout within 200 ms at 200 projects and 200 teams, measured on the stand at stage 6 (not in jsdom)
  And the chosen node, the level choice, the folds, the search and the expansion are kept
  And the choice is stored under its own key (US-1, BR-SD-9)

Scenario: A wide vertical picture
  Given the layout is «Вертикально» and more than 120 nodes are visible
  When I look at the diagram
  Then above the frame a note «Вертикальная раскладка широкая: показано узлов — N. Сверните ветви, выберите уровни или переключитесь на горизонтальную.» is shown with N the visible nodes
  And nothing is blocked, and the note is gone when 120 or fewer nodes are visible, or in the horizontal layout

Scenario: Find a node
  Given the diagram holds a collapsed branch with the sub-area «Приёмка»
  When I type «приемка» in «Найти узел»
  Then every node whose name matches is marked, case and «ё/е» ignored, and «Найдено: N. Enter — к следующему» is shown
  And no node is removed or hidden by the search (BR-SD-8)
  When I press Enter in the field
  Then the section and branches hiding the first match are expanded, it gets focus and is scrolled into view
  And each next Enter goes to the next match, and after the last one to the first

Scenario: Find a node on a hidden level
  Given the level of «Приёмка» is hidden by the level choice
  When I find it and press Enter
  Then the toast «Узел скрыт выбором уровней» is shown, focus does not move to it and the choice is not changed

Scenario: Nothing found
  When I type text that matches no node
  Then «Ничего не найдено» is shown, nothing is marked and Enter does nothing
  And clearing the field removes every mark

Scenario: Expand and collapse all
  Given the diagram has at least one branch with children
  When I press «Развернуть всё»
  Then every branch is expanded and the button reads «Свернуть всё»
  When I press «Свернуть всё»
  Then every branch is collapsed
  And the tree's own expansion and the section folds are untouched
  And without any branch the button is disabled

Scenario: Counters line
  Given the organization has 2 departments, 5 projects, 12 sub-areas and 5 active teams
  When I open the diagram
  Then the toolbar shows «Департаментов: 2 · Проектов: 5 · Областей: 12 · Команд: 5»
  And the figures count all nodes, collapsed or hidden ones included
  And while the offers are not loaded the line has no teams part

Scenario: Keyboard and screen reader
  When I press Tab
  Then focus enters the tree of the diagram once, on one item, and the next Tab leaves it (one Tab stop; BR-SD-10)
  And the toolbar controls and the level group (chips, «Режим уровней», «Все уровни») are separate Tab stops before it
  And the arrow order is the same in both layouts: the first section's header and teams, the hierarchy section's header, the hierarchy top to bottom in tree order, then the other teams
  And the arrow keys move focus between items, Left and Right fold and unfold a section or a branch, Left on a node goes to its nearest visible ancestor, with a visible focus
  And each node's accessible name states its type, its name, its parent and, for a project or a team, the number of its relations
  And the diagram announces itself as a tree: section headers are items of level 1, teams of level 2, hierarchy nodes of their true level plus 1 (BR-SD-10)
  And the level chips and the level numbers drawn on nodes are not read out a second time

Scenario: Narrow window
  Given the window is 640 CSS px wide
  When I open the diagram in either layout
  Then every node is reachable by scrolling its frame, the toolbar wraps, and no text is cut off without a full name available

Scenario: No department at all
  Given the organization has only top-level projects
  When I open the diagram
  Then the projects are drawn at the top level and no empty department frame appears
```
