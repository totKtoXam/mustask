# US-3: Each selector offers what lies under the chosen parents

- story: US-3
- depends-on: US-1, US-6
- shippable: no — it changes the offers that US-4's choices act on; both ship in `feat-NNN-cascading-context-selectors` (one organization) and `feat-NNN-cross-organization-selectors` (organization «все»)
- status: pending

As an **organization member**, I want **each header selector to offer the objects under what I chose above it, and with «все» everything those parents hold**, so that **the list I pick from is always the relevant one**.

Cast: spec § Stories; Ann sees everything in «Ромашка». OQ-2 A and OQ-3 A assumed: under OQ-3 B the narrowed lists are followed by «Остальные» as today.

## Acceptance criteria

```gherkin
Scenario: An organization chosen, everything else «все» (BR-TD-8)
  Given Ann's selection is «Ромашка» / «все» / «все» / «все»
  Then the department selector offers «все», «Логистика», «Финансы»
  And the team selector offers «все», JUPITER, MARS, VENUS
  And the project selector offers «все», A, B, C, D

Scenario: A department narrows teams and projects
  Given Ann's selection is «Ромашка» / «Логистика» / «все» / «все»
  Then the team selector offers «все», MARS, VENUS
  And the project selector offers «все», A, B (standing in «Логистика») and C (linked to MARS), and not D
  And no group «Остальные» appears in any selector

Scenario: A team narrows projects to its links
  Given Ann's selection is «Ромашка» / «Логистика» / MARS / «все»
  Then the project selector offers «все», A, B, C
  And with VENUS chosen instead it offers «все», A

Scenario: A top-level project under a department's team
  Given JUPITER stands in «Финансы» and is linked to D at the top level
  When Ann chooses «Финансы»
  Then the project selector offers «все», C, D

Scenario: A top-level project linked to no team is offered with the organization
  Given the project E stands at the top level of «Ромашка» and no team is linked to it
  When Ann's selection is «Ромашка» / «все» / «все» / «все»
  Then the project selector offers E among A, B, C, D
  And with «Логистика» or «Финансы» chosen it does not offer E

Scenario: The department selector is not narrowed by lower levels
  Given Ann's selection is «Ромашка» / «Логистика» / MARS / A
  Then the department selector still offers «все», «Логистика», «Финансы»

Scenario: Organization «все» offers every organization's objects by heading
  Given Olga is a member of «Ромашка» and «Другая» and her selection is «Все организации»
  Then the department selector lists, under the heading «Ромашка», the departments she sees there, and under «Другая», «Склад»
  And the team and project selectors list her visible teams and projects the same way, an organization with nothing to offer having no heading
  And a value never appears under another organization's heading

Scenario: Only visible objects are offered (BR-TD-12..-15)
  Given Olga sees, in «Ромашка», only what US-6 gives her (VENUS, «Логистика», A, MARS)
  When she opens the team selector with «Ромашка» chosen
  Then it offers «все», VENUS (under «Мои команды»), MARS, and not JUPITER
  And the department selector offers «все», «Логистика», and not «Финансы»

Scenario: No project node, sub-area, deactivated or deleted object is offered
  Given A has the sub-area «Склад-1», B is deactivated and SATURN is deleted
  Then no selector offers «Склад-1», A's node, B or SATURN

Scenario: Empty offers (BR-TD-11)
  Given Pavel is organization «Читатель» of «Ромашка» with no grant and no team, and no project has «Видим всей организации» on
  When Pavel opens the department selector
  Then it shows «все» and «Нет доступных департаментов»
  And the team selector shows «все» and «Нет доступных команд», the project selector «все» and «Нет доступных проектов»
  And a user with no organization sees «Нет доступных организаций» in the organization selector
  And a search with no match shows «Ничего не найдено»

Scenario: The lists follow the selection
  Given Ann's selection is «Ромашка» / «Логистика» / «все» / «все»
  When she opens «Проекты» and «Команды»
  Then «Проекты» lists A, B and C and «Команды» lists MARS and VENUS
  And with a new department «Маркетинг» chosen, which holds nothing, «Проекты» reads «Здесь нет проектов, доступных вам.» and «Команды» «Здесь нет команд, доступных вам.»

Scenario: Offers at volume (NFR)
  Given Olga is a member of 10 organizations, each with 1 000 areas, 200 projects and 200 teams she sees
  When she opens any selector with organization «все», and then types part of a name
  Then the offer is shown within 2 s, and narrowed within 2 s
```
