# US-4: A member creates a work item from a team's board or backlog

- story: US-4
- depends-on: US-1, US-2
- shippable: no — delivered by `mod-006-boards-backlogs`
- status: pending

As an **organization member**, I want **to create a work item right on my team's
board or backlog**, so that **it lands on that team's board and backlog
without me picking one of the team's areas by hand (BR-M4-21)**.

## Acceptance criteria

The dialog is M3's «Создать рабочий элемент» (`doc-002` US-1), with the
defaults below; everything else about creation — the required title, the
first state, the identifier — is `doc-002`'s.

```gherkin
Scenario: An item created on a team's backlog takes the team's default area
  Given a project whose first team is A, and team B whose default area is
    «Платформа/API» (doc-006 BR-M1-14)
  When a member opens «Создать рабочий элемент» on team B's Истории backlog
    and saves a «Пользовательская история» with only a title
  Then its Область is «Платформа/API»
  And it is the last entry of team B's Истории backlog (BR-M4-6)
  And it is not on team A's backlog

Scenario: The first team's default area is the project's root
  Given team A, the project's first team
  When a member creates a «Фича» with only a title from team A's Фичи board
  Then its Область is the project's root area
  And its card appears in «Новые», the column of its first state, below
    every other card of that column (BR-M4-2, BR-M4-15)

Scenario: The type picker offers only the level's types
  Given a team's boards and backlogs
  When a member opens «Создать рабочий элемент» on the Истории backlog, on
    the Эпики board and on the Задачи backlog
  Then the picker offers «Пользовательская история» and «Баг», then only
    «Эпик», then only «Задача» (BR-M4-21)

Scenario: A changed Область routes the item to its owning team
  Given the create dialog opened on team B's Истории backlog
  When the member picks an area team A owns and saves
  Then the item is the last entry of team A's Истории backlog, not of team
    B's (BR-M4-5)

Scenario: A later change of the team's default area affects new items only
  Given team B's items created while its default area was «Платформа/API»
  When an administrator makes «Платформа/Web» team B's default area and a
    member then creates an item on team B's backlog
  Then the new item's Область is «Платформа/Web», and the earlier items keep
    «Платформа/API»

Scenario: Creation rules stay M3's
  Given the create dialog opened on a team's board
  When the member saves with an empty title
  Then the save is refused with «Заголовок обязателен» and nothing is
    created (`doc-002` US-1)

Scenario: A reader cannot create from a board or backlog
  Given a user whose role in the organization is viewer
  When they open any team's board or backlog
  Then no «Создать рабочий элемент» control is offered
  And a create request sent directly is refused (`doc-001` BR-5)
```
