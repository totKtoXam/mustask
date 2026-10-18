# US-5: Creating from a team view, the project chosen first

- story: US-5
- depends-on: US-1, US-2
- shippable: no — a team view with no project in its address cannot create without it, in the same MR
- status: pending

As a **member who plans a team's work**, I want **to start a new item on the team's backlog or board and choose which of the team's projects it belongs to**, so that **the team's new work lands in the right project, owned by the team, without leaving the team's view**.

## Acceptance criteria

The common cast of spec § Stories. Rules: BR-TS-9, BR-TS-13; mod-017 BR-OT-3 (the team) kept, and the dialog's other fields as today. Proposed copy: the field «Проект», its refusal «Выберите проект».

```gherkin
Scenario: The dialog asks for the project first, among the team's projects (BR-TS-9; doc-012 US-7)
  When Max starts a new item with «Новый рабочий элемент» on MARS's backlog with no project filter
  Then the dialog's first field is «Проект», offering «Alpha», «Beta» and «Gamma» by name, with none chosen
  And «Команда» is «MARS»
  When Max presses «Создать» with a title and no project
  Then nothing is sent, and «Проект» reads «Выберите проект» and takes the focus

Scenario: The project starts chosen when there is one to choose (BR-TS-9; doc-012 US-7)
  When Max starts a new item on MARS's backlog filtered to «Beta»
  Then «Проект» starts as «Beta»
  When Max starts one on MARS's board filtered to «Alpha» and «Beta»
  Then «Проект» starts with none chosen
  When Max starts one on VENUS's board, VENUS being linked to Alpha only
  Then «Проект» starts as «Alpha», and still lists «Alpha» as its one option

Scenario: The item is created in the chosen project, owned by the team (BR-TS-9; mod-017 BR-OT-6)
  Given Beta's last item number is 41
  When Max chooses «Beta», types a title and presses «Создать» on MARS's backlog
  Then the item BET-US-42 is created in Beta, owned by MARS
  And it stands unplaced on MARS's backlog and on Beta's backlog, below every placed item
  And it shows on MARS's backlog and board with «Проект» «Beta»

Scenario: «Команда» offers the chosen project's teams and «Без команды» (BR-TS-9; mod-017 BR-OT-3)
  When Max chooses «Alpha» in MARS's dialog
  Then «Команда» offers «MARS», «VENUS» and «Без команды», with «MARS» chosen
  When Max chooses «VENUS» and then changes «Проект» to «Beta», to which VENUS is not linked
  Then «Команда» is set back to «MARS» and offers «MARS» and «Без команды»
  When Max chooses «Без команды» with «Beta» and presses «Создать»
  Then the item is created in Beta with no owning team: it is on Beta's views and on no team's

Scenario: The team is unlinked, or the project deactivated, before the save (BR-TS-9; BR-MX-9, BR-MX-12)
  Given Max's dialog on MARS's backlog has «Beta» and «MARS» chosen
  When Ann unlinks Beta from MARS, and Max then presses «Создать»
  Then the save is refused with «Команда не участвует в проекте.» under «Команда», no item is created, and the dialog keeps its values
  When instead Ann deactivates Beta first
  Then the save is refused with «Проект не найден.» under «Проект», no item is created, the dialog stays open with its values, and «Проект» read again no longer offers «Beta»
  When instead Ann deletes MARS first
  Then the save is refused with «Команда не найдена.» under «Команда», and no item is created

Scenario: A save that commits before the unlink is reassigned with the rest (BR-TS-9; feat-028 BR-OL-8)
  Given Max's dialog on MARS's backlog has «Beta» and «MARS» chosen
  When Max's save and Ann's unlink of Beta from MARS, choosing «Без команды», run at the same moment
  Then either the item is created owned by MARS and the unlink then makes it «Без команды», or the save is refused with «Команда не участвует в проекте.»
  And no request ends in a server error, and no item is owned by a team not linked to its project

Scenario: The command is offered only where a project can be chosen and the role may write (BR-TS-9, -13)
  When Max opens JUPITER's backlog and board, JUPITER being linked to no project
  Then neither offers «Новый рабочий элемент» nor «Новый элемент»
  When Vera opens MARS's backlog and board
  Then neither offers the command, and her direct creation in Beta naming MARS is refused with 403 «Недостаточно прав»
  When Ann unlinks Gamma from MARS while Max's dialog is open
  Then the next time Max opens the dialog, «Проект» offers «Alpha» and «Beta» only

Scenario: The dialog works by keyboard and on a narrow screen (spec NFR accessibility)
  When Max opens the dialog, chooses «Проект» and «Команда» and creates by keyboard alone
  Then the item is created as above, and the focus returns where it was, as today
  And at 640 CSS px the dialog fits with no horizontal page scroll
```
