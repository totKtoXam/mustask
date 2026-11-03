# US-2: The diagram tree: sections, team cards, levels, layouts, search, expand all, counters

- story: US-2
- depends-on: US-1
- shippable: no — shown only through the switch of US-1 and completed by US-3's relations
- status: pending

As a **member of an organization**, I want **a diagram in which departments, projects, sub-areas and teams stand as one tree, in two layouts, with the nesting levels I choose**, so that **I see the whole structure or only the levels I care about, at which level each card stands, under which department each team works, and I can find a card and open the whole picture in one step**.

## Acceptance criteria

```gherkin
Scenario: Departments and projects by nesting
  Given department «Логистика» holds project A, and project B stands at the top level
  When I open the diagram
  Then «Логистика» is drawn with A beneath it, joined to it
  And B is drawn in the section «Без департамента», joined to no department
  And the organization itself is not drawn as a node (as in the tree)

Scenario: Sub-areas under their project
  Given project A has sub-area «Склад» with sub-area «Приёмка» beneath it
  When I open the diagram
  Then «Склад» is drawn under A and «Приёмка» under «Склад», every card joined to its parent

Scenario: Each card says what it is
  When I read any card
  Then it shows its name and its type («Департамент», «Проект», «Область», «Команда»), by text and not by colour alone
  And an active project also shows its key (a deactivated project shows none: neither read carries it, BR-SD-3, BR-SD-4)

Scenario: Order as in the tree
  Given the area tree is read in display order
  When I open the diagram
  Then siblings are drawn in that same order, in both layouts
  And inside a department its projects (with sub-areas) come first, then its teams in the order of the selector offers

Scenario: Cards open their pages
  Given project A is active
  When I click the name of A, of a department, of a sub-area or of a team, or focus it and press Enter
  Then the object's page opens as the tree's link would (department page, project «Общие», sub-area under its project's menu, team page)
  And clicking the card's body outside the name opens nothing (it chooses, US-3, BR-SD-10)

Scenario: A deactivated project
  Given project C is deactivated
  When I open the diagram
  Then C and its sub-areas are drawn tagged «деактивирован»
  And activating them opens nothing

Scenario: A team stands under its department
  Given team MARS is linked to department «Логистика» and works on project A of «Логистика»
  When I open the diagram
  Then MARS is drawn as a card inside «Логистика», after the department's projects, joined to the department as a child (BR-SD-16)
  And no separate lane of teams exists

Scenario: A team in several departments
  Given team JUPITER is linked to departments «Логистика» and «Финансы»
  When I open the diagram
  Then JUPITER is drawn under «Логистика» and again under «Финансы», each copy after that department's projects
  And the copy under «Логистика» is marked «ещё в: Финансы» and the copy under «Финансы» «ещё в: Логистика»
  And consecutive copies are joined by a dash-dot line, the legend line «та же команда в другом департаменте» describing it
  And each copy opens the same team page

Scenario: A team with no department
  Given team ATLAS is linked to no department, whether or not it works on projects
  When I open the diagram
  Then ATLAS is drawn in the section «Без департамента», after the top-level projects, as a level-1 card
  And a department is not required of any team

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
  Then the line from MARS is drawn to the nearest visible ancestor of A, as a dotted line standing for hidden cards
  And expanding the branch moves the line to A

Scenario: Two sections
  Given top-level project B, team ATLAS with no department, and department «Логистика» with its projects and teams
  When I open the diagram
  Then the section «Без департамента» is drawn first, holding B with its sub-areas and then ATLAS
  And the section «Департаменты, проекты и области» follows, holding the departments with their projects and teams
  And each header shows its title and a count of cards
  And when nothing qualifies for «Без департамента» it is not drawn, with no empty caption
  And the old title «Без проектов и департаментов» appears nowhere

Scenario: Sections fold and unfold
  When I click a section header, or focus it and press Enter or Space, or press Left / Right
  Then the section's content hides or shows, the header and its count stay, and the header announces «<раздел>: свёрнуто» / «<раздел>: развёрнуто»
  And with both folded the frame shows the two headers only
  And the folds are not stored and survive a view switch for the visit (US-1)

Scenario: Choosing unfolds what it needs
  Given the section «Департаменты, проекты и области» is folded
  And team MARS, linked to no department, stands in «Без департамента» and works on project A, which is in the folded section
  When I choose MARS, or find A with «Найти узел» and press Enter
  Then the section unfolds, with the branches hiding the emphasised or found cards, the departments of a team's copies included (BR-SD-11), and the card is emphasised or focused

Scenario: Level chips are a clickable ruler
  Given the tree is three levels deep: department, project, sub-area
  When I open the diagram
  Then a chip «1», «2», «3» stands for each level of the whole tree and for no absent level, in a group named «Уровни вложенности»
  And in the horizontal layout the chips stand above the first row, in the vertical layout at the start of the row of their level in each section band
  And every card shows its true level number: department 1, its project and its team 2, the sub-area 3; a top-level project and a team in «Без департамента» are 1
  And with no chip pressed all levels are shown
  And nothing else stands beside the chips: no empty or faint circle, no control other than the chips (the text button «Все уровни» is in the toolbar, BR-SD-19)

Scenario: Each level chip explains itself
  When I hover a chip, or focus it with the keyboard
  Then a tooltip says what that level holds and what a click does:
    | chip | the tooltip starts |
    | 1 | «Уровень 1 — департаменты, проекты верхнего уровня и команды без департамента.» |
    | 2 | «Уровень 2 — проекты и команды под департаментом, области под проектом верхнего уровня.» |
    | N from 3 | «Уровень N — области N-го уровня вложенности.» |
  And it ends with the action: «Нажмите, чтобы показать только этот уровень» in «Только выбранные уровни»; «Нажмите, чтобы показать этот и более глубокие уровни» in «Уровень и вложенные»; «Нажмите, чтобы убрать уровень из выбранных» on a pressed chip
  And the chip stays a button that a screen reader reads with the same text as its description

Scenario: Only the chosen levels
  Given «Режим уровней» is «Только выбранные уровни» (the default)
  When I press the chips «1» and «3»
  Then exactly the cards of levels 1 and 3 are shown, the chips «1» and «3» are marked pressed
  And a second press of a chip unpresses it
  And the announcement «Показаны уровни: 1, 3» is made and focus stays on the chip

Scenario: A level and its nested ones
  Given «Режим уровней» is «Уровень и вложенные»
  When I press the chip «2», or «2» and then «3»
  Then the cards of level 2 and of every deeper level are shown, the topmost pressed chip decides and a deeper pressed chip changes nothing
  And the deeper chips that are included are marked by a lighter tint besides the text
  And the announcement reads «Показаны уровни: 2 и более глубокие»

Scenario: All levels
  Given some chips are pressed
  When I press «Все уровни»
  Then no chip is pressed, every level is shown, the mode is unchanged and «Показаны все уровни» is announced
  And while no chip is pressed «Все уровни» is disabled

Scenario: Each level has its colour on the card itself
  When I look at cards of levels 1, 2, 3, 4 and 5 or deeper, of every kind (department, project, sub-area, team)
  Then each card wears its level's colour as a left edge, an outline and a light tint of its fill, the same colour as its level chip and its number badge, one colour for level 5 and deeper
  And the number stays on every card and on every chip, so the level is never told by colour alone
  And every level outline and edge colour has a contrast of at least 3:1 against the diagram background
  And the text of every level badge and chip, level 3 included, has a contrast of at least 4.5:1 against its fill
  And the text of a card has a contrast of at least 4.5:1 against its tint at rest, on hover, when related and when chosen (BR-SD-12)
  And a deactivated card keeps its outline on a neutral fill
  And a chosen card keeps its level colour and adds a ring (US-3)

Scenario: Hidden levels drop their cards
  Given levels «1» and «3» are shown and level «2» is hidden
  When I look at the picture
  Then no card of level 2 is drawn, a team under a department included
  And a level-3 card is joined by a dotted nesting join to its nearest visible ancestor (a solid join means a direct parent)
  And a card with no visible ancestor is drawn at its own level with no join
  And indent and rows keep their true level, so gaps show missing levels and the chips stay aligned

Scenario: Hidden levels and relation lines
  Given the level of project A is hidden and A has the team MARS, whose level is shown
  When I look at the diagram
  Then the line from MARS goes dotted to the nearest visible ancestor of A
  And with no visible ancestor the line is not drawn
  And the counts and the summaries (US-3) still speak of all data

Scenario: Levels with expansion and «Только связанные»
  Given a branch is collapsed, or the mode «Только связанные» shows a chosen branch
  When I choose levels
  Then the level choice applies after expansion and after «Только связанные», so the intersection is shown (BR-SD-11)
  And a chosen node on a hidden level stays chosen and its emphasis, lines and summary remain

Scenario: Levels that show nothing
  When the chosen levels hold no card
  Then the diagram says «Нет узлов на выбранных уровнях»

Scenario: The level choice is not stored
  Given I chose levels
  When I switch the view and back, the level choice is as I left it
  When I reload the panel, leave the page, or change the organization
  Then every level is shown and nothing about levels is in browser storage (BR-SD-9)

Scenario: Horizontal layout
  Given the layout is «Горизонтально» (the default)
  When I open the diagram
  Then there is one lane: one row per visible card in tree order, a full-width card indented by true level, all ending at one right edge
  And the level chips stand above the first row
  And relation lines run inside the lane from a team card to its projects' cards

Scenario: Vertical layout
  Given I chose «Раскладка: Вертикально»
  When I open the diagram
  Then each level is a row, departments and top-level projects on top, projects and teams of a department below it, sub-areas lower, and a parent stands above its visible children, joined to them
  And the section «Без департамента» is a band on top across the frame width, holding top-level projects and loose teams on its first row
  And the level chips stand at the start of their rows in each band
  And the order of the cards, the keyboard order, the links, the levels and the relation lines are as in the horizontal layout

Scenario: The layout is switched
  Given the diagram is open with a node chosen, levels chosen and a section folded
  When I choose the other layout in «Раскладка»
  Then the picture is redrawn in the other layout within 200 ms at 200 projects and 200 teams, measured on the stand at stage 6 (not in jsdom)
  And the chosen node, the level choice, the folds, the search, the zoom and the expansion are kept
  And the choice is stored under its own key (US-1, BR-SD-9)

Scenario: A wide vertical picture
  Given the layout is «Вертикально» and more than 120 cards are visible
  When I look at the diagram
  Then above the frame a note «Вертикальная раскладка широкая: показано узлов — N. Сверните ветви, выберите уровни или переключитесь на горизонтальную.» is shown with N the visible cards
  And nothing is blocked, and the note is gone when 120 or fewer cards are visible, or in the horizontal layout

Scenario: Display mode, layout and level mode are segmented icon toggles
  When I look at the toolbar
  Then «Показ при выборе» (US-3), «Раскладка» and «Режим уровней» are three segmented icon toggles: each value is its own icon button, exactly one is pressed, there is no menu
  And «Раскладка» offers «Горизонтально» and «Вертикально»; «Режим уровней» offers «Только выбранные уровни» and «Уровень и вложенные»
  And each group is named after its control, and the accessible name of each button is its value (BR-SD-19)
  And the tooltip of each button reads «<управление>: <значение>. <подсказка>», for example «Раскладка: Вертикально. Департаменты сверху»
  And «Режим уровней» stands beside «Раскладка»
  And «Все уровни» is a text button right after «Режим уровней», disabled while no chip is pressed

Scenario: The focus ring stays on the button
  When I focus a button of a toggle with the keyboard, and when I click it
  Then the focus indicator is drawn around that button and nowhere else, and it does not drift off the icon

Scenario: A toggle by keyboard
  Given focus is on a button of a toggle
  When I move to the button of another value and press Enter or Space
  Then that value is applied and shown pressed, and focus stays on its button

Scenario: Line styles and their legend
  When I open the diagram of an organization with a multi-department team
  Then the legend lists four lines: «вложенность», «команда работает над проектом», «та же команда в другом департаменте», «связь через скрытый узел»
  And each is drawn in a style of its own: nesting solid (dotted across hidden levels), team on a project dashed, copy join dash-dot, through a hidden card dotted (BR-SD-17)
  And the strokes are heavy and dark enough to tell apart at a glance, each at least 3:1 against the diagram background
  And the line on the canvas is drawn in the same stroke as its legend sample

Scenario: Pan with the middle mouse button
  Given the diagram is wider or taller than its frame
  When I press the middle mouse button on the diagram and drag
  Then the picture follows the pointer, and the cursor changes for as long as I drag and returns when I let go
  And no card is chosen or opened by that gesture, and the browser's own autoscroll does not start
  And the frame's scrollbars still work

Scenario: Zoom with Ctrl and the wheel
  Given the zoom is 100 %
  When I turn the wheel over the diagram with Ctrl held
  Then the picture zooms around the pointer, in the range from 50 % to 200 %, and does not go beyond either end
  And the browser page itself is not zoomed
  And the wheel without Ctrl still scrolls the frame
  And no card, line, level choice or emphasis changes, only the size of the drawing (BR-SD-20)

Scenario: The zoom control by keyboard
  Given the toolbar shows «− 100% +» in a group named «Масштаб»
  When I press «+» or «−» with the mouse or the keyboard
  Then the zoom steps up or down within 50 % to 200 %, and the value shows it («120%»)
  And «+» is inactive at 200 % and «−» at 50 %
  And the tooltips of «−» and «+» are «Уменьшить масштаб (Ctrl + колесо мыши)», «Увеличить масштаб (Ctrl + колесо мыши)»
  And «Масштаб: <значение>» is announced to a screen reader after each step
  When I press the value «120%», or Enter on it
  Then the zoom returns to 100 %, the value button is inactive at 100 %, and its name and tooltip read «Масштаб: 120%. Вернуть 100%» before it, «Масштаб: 100%» at 100 %

Scenario: Zoom is not stored
  Given I zoomed to 150 %
  When I switch the layout, or switch to «Дерево» and back
  Then the zoom is still 150 %
  When I reload the panel, or change the organization
  Then the zoom is 100 % and nothing about zoom is in browser storage (BR-SD-9, BR-SD-20)

Scenario: Zoom and pan at volume
  Given 200 projects and 200 teams
  When I zoom with Ctrl + wheel or the buttons and pan with the middle button
  Then each step changes the picture within 200 ms, measured on the stand with a seeded organization of that size in a named browser at stage 6 (not in jsdom)
  And no read is sent and the layout is not rebuilt (checked by a test)

Scenario: The hint under the frame
  When I look under the diagram's frame
  Then it tells the keys: Tab enters once, arrows move, Space chooses, Enter opens, Esc clears, «Ctrl + колесо мыши — масштаб, средняя кнопка мыши (зажать и двигать) — перемещение»

Scenario: Find a node
  Given the diagram holds a collapsed branch with the sub-area «Приёмка»
  When I type «приемка» in «Найти узел»
  Then every card whose name matches is marked, case and «ё/е» ignored, and «Найдено: N. Enter — к следующему» is shown
  And no card is removed or hidden by the search (BR-SD-8)
  When I press Enter in the field
  Then the section and branches hiding the first match are expanded, it gets focus and is scrolled into view
  And each next Enter goes to the next match, and after the last one to the first
  And a team matched by name jumps to its first copy

Scenario: Find a node on a hidden level
  Given the level of «Приёмка» is hidden by the level choice
  When I find it and press Enter
  Then the toast «Узел скрыт выбором уровней» is shown, focus does not move to it and the choice is not changed

Scenario: Nothing found
  When I type text that matches no card
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
  Given the organization has 2 departments, 5 projects, 12 sub-areas and 5 active teams, one of them linked to both departments
  When I open the diagram
  Then the toolbar shows «Департаментов: 2 · Проектов: 5 · Областей: 12 · Команд: 5»
  And each team is counted once, whatever number of copies it has
  And the figures count all nodes, collapsed or hidden ones included
  And while the offers are not loaded the line has no teams part

Scenario: Keyboard and screen reader
  When I press Tab
  Then focus enters the tree of the diagram once, on one item, and the next Tab leaves it (one Tab stop; BR-SD-10)
  And the toolbar controls are separate Tab stops before it: «Найти узел», «Развернуть всё», each of the toggles «Показ при выборе», «Раскладка» and «Режим уровней» as one stop (arrow keys move between its buttons), «Все уровни» while enabled, the zoom buttons «−», «100%», «+», and the level chips
  And the arrow order is the same in both layouts: the section «Без департамента» header, its projects with sub-areas, its teams, then the second section's header and each department with its projects, sub-areas and teams, a team's copies being separate items
  And the arrow keys move focus between items, Left and Right fold and unfold a section or a branch, Left on a card goes to its nearest visible ancestor, with a visible focus
  And each card's accessible name states its type, its name, its parent department or «без департамента», its «ещё в: …» marks and, for a project or a team, the number of its relations
  And the diagram announces itself as a tree: section headers are items of level 1, every card its true level plus 1 (BR-SD-10)
  And the level chips and the level numbers drawn on cards are not read out a second time

Scenario: Narrow window
  Given the window is 640 CSS px wide
  When I open the diagram in either layout
  Then every card is reachable by scrolling its frame, the toolbar wraps, and no text is cut off without a full name available

Scenario: No department at all
  Given the organization has only top-level projects
  When I open the diagram
  Then the projects are drawn in «Без департамента» and no empty department frame appears
```
