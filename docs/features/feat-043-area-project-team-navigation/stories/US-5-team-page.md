# US-5: A team chosen: its menu and settings

- story: US-5
- depends-on: US-1, US-2
- shippable: no — the page that «Команды» opens and that takes over the team pages in the organization's settings (US-9)
- status: pending

As an **organization member**, I want **choosing a team to put its sections in the left menu, spanning its projects, with its members and its settings**, so that **I find the team's work, its projects and its people in one place, whichever project or department I came from**.

## Acceptance criteria

Cast as US-2: MARS is linked to projects A, B and C, VENUS to A, JUPITER to none; «Логистика» is linked to MARS and VENUS. Olga is a member of VENUS. Ann «Администратор», Vera «Читатель». The sprints of `mod-007-iterations` and the grants of `mod-019-scoped-access` are absent here.

```gherkin
Scenario: A team's sections (BR-NV-3; US-10 of doc-012)
  When Olga chooses MARS in the team selector
  Then the left menu holds «Обзор», «Рабочие элементы», «Канбан», «Бэклог», «Проекты», «Участники и доступ» and, last, the derived «Настройки», in this order, and MARS's «Обзор» opens
  And there is no capacity, analytics or «Спринты» section

Scenario: «Обзор» shows the team and its departments (BR-NV-4)
  When Olga opens MARS's «Обзор»
  Then she sees its name, description, the number of its members and projects, and the department «Логистика» it is linked to, opening the department's page
  And MARS is one page, the same whether reached from A, B, C, «Логистика» or «Команды»

Scenario: «Проекты» lists the team's projects across the organization (BR-MX-17; BR-NV-4)
  Given MARS is also linked to the deactivated project E
  When Olga opens MARS's «Проекты»
  Then she sees A, B and C once each, not E and not counted with them (BR-NV-13; the link returns when E is restored), with their places as paths, each opening the project's page
  And the page does not nest a project in a department or a department in a project

Scenario: «Канбан» and «Бэклог» span the team's projects (BR-MX-17; feat-040)
  When Olga opens MARS's «Бэклог» and «Канбан»
  Then they show the items MARS owns in all its projects, with each row's project and owning team, and a card opens in its own project
  And the project filter and the order are those of feat-040, unchanged

Scenario: «Участники и доступ» holds the team's members only (BR-NE-3; BR-MX-22)
  When Olga opens VENUS's «Участники и доступ»
  Then it lists the members of VENUS, Olga among them, who gets no role from it, and Ann sees «Добавить участника» and a remove command per row, which Olga and Vera do not
  And there is no list of grants until `mod-019-scoped-access`

Scenario: A team's «Параметры» are in the settings navigation (BR-NV-14, BR-NV-18; feat-028)
  When Ann has MARS chosen and opens «Настройки», the group «Команда» and «Параметры»
  Then she renames it, changes its description and deletes it
  And the deletion's confirmation names what it ends (the team's links, memberships and area links) and where its items go («Без команды»), and nothing is deleted before she confirms
  And a name taken by another team answers the message the interim page gave, and nothing changes

Scenario: A team is created from «Команды» (BR-NV-14)
  When Ann creates the team SATURN from «Команды»
  Then SATURN is listed once, its page opens at «Обзор», and no project, department or membership is created by it (BR-MX-2)

Scenario: A team opened from a row shows its own menu (BR-NV-19; design D-3 A)
  Given the selectors read «Логистика» / «все» / A
  When Olga opens MARS from A's «Команды»
  Then the selectors read «Логистика» / MARS / «все», A resets to «все» silently as it is more specific than a team, and the menu is MARS's
  And opening A from MARS's «Проекты» gives MARS / A, and A's menu opens at «Общие»
  And opening MARS from «Команды» with nothing else chosen reads «все» / MARS / «все»

Scenario: A team with no project and no department (BR-MX-2; SC-07)
  When Olga opens JUPITER
  Then every section is shown; «Проекты» reads «Команда не привязана ни к одному проекту», and «Обзор» reads «Команда не привязана ни к одному департаменту», each offering the link command only to Ann

Scenario: A deleted team answers not found (BR-NV-7)
  When Olga opens the address of a team Ann deleted, and one of «Другая»
  Then she sees «Команда не найдена» for each, the selectors do not change, and the team is in no list

Scenario: A reader sees the team and no command (BR-NV-3)
  When Vera chooses MARS
  Then her menu holds «Обзор», «Рабочие элементы», «Канбан», «Бэклог», «Проекты» and «Участники и доступ» with no command, her «Настройки» holds no «Команда» group, and the address of MARS's «Параметры» shows «Доступ ограничен»
```
