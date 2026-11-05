# US-2: A new item starts with the right team, on every surface that creates one

- story: US-2
- depends-on: US-1, US-3
- shippable: no — one of the seven stories of `mod-017-work-item-owning-team`; it reads the field of US-1 and the default of US-3
- status: pending

As a **user who may change a project's work items**, I want **a new item to start with the team it most likely belongs to, and to change that before saving**, so that **I do not set the team by hand on every item, and no item lands on a team by an accident of its area** (BR-MX-10, BR-MX-11; doc-012 US-4, US-7).

## Acceptance criteria

Data as US-1: «Альфа» (ALP, A) has MARS and VENUS, «Бета» (BET, B) has JUPITER; U is an «Участник», V a «Читатель». The states «Новая» and the root area «Альфа» are the seeded ones. A project's default team is its settings' named team, else its only team, else none (BR-OT-4, US-3). "A project view" is the project's backlog or board of feat-039.

```gherkin
Scenario Outline: Outside a team's surface a new item starts with the project default team (BR-OT-3, BR-OT-4)
  Given project <project> with <teams>
  When U opens «Новый рабочий элемент» on <project>'s «Рабочие элементы» and picks a type
  Then the dialog's «Команда» reads <team>, and «Создать» creates the item with it

  Examples:
    | project | teams                                    | team          |
    | ALP     | MARS and VENUS, VENUS named in settings  | «VENUS»       |
    | BET     | JUPITER only, none named                 | «JUPITER»     |
    | ALP     | MARS and VENUS, none named               | «Без команды» |
    | ALP     | MARS and VENUS, MARS named, then deleted | «VENUS»       |

Scenario: The dialog says where the item goes and offers the project's teams (BR-OT-3, BR-OT-10)
  When U opens «Новый рабочий элемент» on any surface
  Then it reads «Рабочий элемент будет создан в статусе «Новая» в области «Альфа». Остальные поля заполняются в его форме.», the root area, never a team's default area
  And «Команда» stands between «Тип» and «Заголовок» and offers «Без команды», then the project's teams by name
  And under it «Встанет в конец бэклога команды «VENUS».» reads for a team and «Попадёт только в бэклог проекта.» for «Без команды», following the choice, for a type of a backlog level; for a type of none (a «Задача» before `feat-023-tasks-backlog`) neither line reads

Scenario: The switcher's team is not a team surface (BR-OT-3, BR-OT-4)
  Given the switcher names MARS at its team level, and A's settings name VENUS
  When U opens «Новый рабочий элемент» on A's «Рабочие элементы»
  Then «Команда» reads «VENUS», not «MARS»

Scenario: The dialog waits for the project's teams (BR-OT-3)
  When U opens «Новый рабочий элемент» while A's teams are being read
  Then «Команда» shows a skeleton and «Создать» is unavailable until they are read
  And if the read fails, the field shows the error with «Повторить», and after it succeeds «Команда» reads the default of the moment

Scenario: The dialog creates with the team it shows (BR-OT-3)
  Given U's dialog on A's «Рабочие элементы» reads «Команда» «VENUS», A's named default
  When Ann names MARS the default before U presses «Создать»
  Then the item U creates is VENUS's
  And a creation sent directly with no «Команда» field, after Ann's change, is MARS's: the default as stored when it is saved

Scenario: Created from a team's backlog or board, the item starts with that team (BR-OT-3)
  Given A's settings name VENUS the default team
  When U opens «Новый рабочий элемент» on MARS's «Истории» backlog, or «Новый элемент» in the first column of MARS's board
  Then «Команда» reads «MARS» and offers «Без команды», «MARS» and «VENUS» only
  And the item created is MARS's and is the last row of MARS's «Истории» backlog and the last card of its first column
  And with «VENUS» picked before «Создать» it is VENUS's and on MARS's views no more; with «Без команды» it is on no team's views
  And in each case it is the last row of A's project backlog, unplaced, and the creation wrote no place on any order

Scenario: A project view offers the creation, starting with the project default (BR-OT-3; feat-039 BR-PB-6 lifted)
  Given U is on A's «Истории» backlog, and A's settings name VENUS
  Then the head has «Новый рабочий элемент», and A's board has «Новый элемент» in its first column
  When U opens it
  Then «Команда» reads «VENUS», U may change it to «MARS» or «Без команды» before «Создать», and the item is on A's views and on its team's
  And with no default named it starts with «Без команды», A having two teams
  And V sees neither command, on the backlog or on the board

Scenario: An empty project view reads as an empty team view does (BR-OT-3; feat-039 OQ-2 lifted)
  Given project D has no story and U is an «Участник»
  When U opens D's «Истории» backlog and board
  Then the backlog reads «Бэклог пуст» with «Добавьте рабочий элемент — он встанет в конец бэклога.» and the action «Новый рабочий элемент»
  And the board reads «Канбан пуст» with its hint and the same action
  And for V the backlog's hint reads «Рабочие элементы создают участники организации с ролью «Участник» или «Администратор».» and offers no action

Scenario Outline: A team named in a direct creation is checked as in a save (BR-OT-2)
  When a creation of a story in A that names <team> is sent
  Then it is refused with <text>, nothing is created and no number is consumed

  Examples:
    | team                               | text                              |
    | JUPITER, a team of B               | «Команда не участвует в проекте.» |
    | a deleted team, an id of no team   | «Команда не найдена.»             |
    | SATURN, a team of «Другая»         | «Команда не найдена.»             |

Scenario: A team gone while the dialog was open refuses the creation (BR-OT-2, BR-OT-13)
  Given the dialog reads «Команда» «MARS», and MARS is deleted before «Создать»
  When U creates the item
  Then «Команда не найдена.» shows under «Команда», the title is kept, nothing is created and no number is consumed
  When U changes «Команда» to «VENUS» and submits again
  Then one item is created with VENUS
  And a repeated submission of an item already created, sent after MARS was deleted, answers 200 with that item as it is now, owned by no team

Scenario: A child starts with its parent's team, else the project default (BR-OT-3)
  Given A's settings name VENUS the default team; ALP-FEAT-6 is MARS's and ALP-FEAT-7 has no owning team
  When a story is added to ALP-FEAT-6 from its row's «+», and another to ALP-FEAT-7
  Then the first is MARS's and the second VENUS's, both in project «Альфа»
  And on a project view the first is MARS's too: the parent's team decides, whatever the surface
  And neither a row's add nor «Добавить задачу» on a card has a «Команда» field: it is changed afterwards on the child's card
  And the same holds on a card opened over another team's page

Scenario: The parent's team is read when the child is saved (BR-OT-3, BR-OT-13)
  Given U adds a story to ALP-FEAT-6 while Ann sets ALP-FEAT-6's «Команда» to «VENUS»
  Then the story takes the team ALP-FEAT-6 had when the story was saved, MARS or VENUS, and neither request fails
  And when MARS is deleted while the add row is open, the story takes the project default team, the parent being without one

Scenario: A child is never refused for a team, whichever way the deletion of its parent's team falls (BR-OT-3, BR-OT-13)
  Given A's settings name VENUS the default team, and ALP-FEAT-6 is MARS's
  When U adds a story to ALP-FEAT-6 while Ann deletes MARS
  Then the story is created in either order, and no request answers «Команда не найдена.» or with a server error
  And if the add commits first, the story is MARS's until the deletion, which reassigns it with ALP-FEAT-6: both read «Без команды»
  And if the deletion commits first, ALP-FEAT-6 is without a team and the story takes the project default as it stands then, «VENUS»
  And a creation sent directly that names ALP-FEAT-6 as the parent and also carries a deleted team, or JUPITER of B, is not refused for the team: it takes the parent's rule

Scenario: Who creates (BR-OT-15)
  Given V is an organization «Читатель»
  Then V is offered no «Новый рабочий элемент» on the list, on a team view or on a project view
  And a creation sent directly answers 403 «Недостаточно прав» and creates nothing
```

## Owed by later lines

```gherkin
# `feat-028-org-level-teams`: a team being unlinked while the dialog is open → «Команда не участвует в проекте.», nothing created
#   (doc-012 US-4); and a child added while its parent's team is being unlinked takes the parent's team as stored and is moved with it
# `feat-040-team-surfaces-across-projects`: creation from a team view across projects, with «Проект» chosen first (doc-012 US-7)
# `feat-043-area-project-team-navigation`: the «Рабочие элементы» list across projects, and so its creation with a project choice
```
