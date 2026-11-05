# US-7: No team is the first, and a deleted team's items go to «Без команды»

- story: US-7
- depends-on: US-1, US-3, US-4, US-6
- shippable: no — one of the seven stories of `mod-017-work-item-owning-team`; it retires the first team's role and replaces "its areas pass to the first team" with an owner that is simply gone
- status: pending

As an **organization administrator**, I want **every team of a project to be an ordinary team, and deleting one to send its items to «Без команды»**, so that **no team is protected for owning the root, and removing a team never moves its work onto another team behind anyone's back** (BR-MX-12, single project; mod-002 BR-M1-9, BR-M1-18 replaced; doc-012 US-2).

## Acceptance criteria

Data: «Альфа» (A) was created with the team «Альфа», renamed MARS; later teams VENUS and ATLAS were added. MARS owns 5 stories of A, 2 of them «Закрыта»; VENUS owns ALP-US-20. Ann is an «Администратор», U an «Участник». Until `feat-028-org-level-teams` merges a team belongs to one project, so deleting it is BR-MX-12's unlink with «Без команды» as the destination, applied to that project.

```gherkin
Scenario: The teams are listed by name and none is first (BR-OT-11; mod-002 BR-M1-9, mod-016 BR-M1-27)
  When Ann opens A's «Команды»
  Then the rows read ATLAS, MARS, VENUS, in the alphabetical order of the names, ignoring letter case
  And no row carries «Владеет корневой областью», and the team created with the project is not above the others
  And the same order is the project's team drop-down's, the switcher's and the picker's of the card

Scenario: Any team may be renamed or deleted, the one created with the project too (BR-OT-11)
  When Ann renames MARS, and later deletes it while VENUS and ATLAS remain
  Then each is done, with no «Нельзя удалить команду, которой принадлежит корневая область проекта» (`organizations.team.first_team_cannot_be_deleted` is gone)

Scenario: The switcher's fallback team is the first in the list's order (BR-OT-11; mod-016 US-7 kept, "first team" read as the list's first)
  Given U belongs to no team of A
  When U chooses A in the switcher
  Then the team level shows ATLAS
  And with U a member of VENUS it shows VENUS, and a deleted remembered team falls back as before, with the fallback notice and no notice of the deletion's own (mod-002 DC-7)

Scenario: The confirmation counts what the team owns (BR-OT-12)
  When Ann chooses «Удалить команду» for MARS
  Then «Удаление команды» reads «Удалить команду «MARS»? Её рабочие элементы (5) станут «Без команды». Восстановить команду будет нельзя.», the 5 counting every state
  And for ATLAS, which owns nothing, it reads «Удалить команду «ATLAS»? Восстановить команду будет нельзя.»
  And «Отмена», Escape and «Закрыть» change nothing, and U is offered no deletion and is refused with 403 «Недостаточно прав»
  And the count is read when the dialog opens: an item given to MARS while it is open is not in the count, and the confirmed deletion reassigns it with the rest

Scenario: Confirming sends the items to «Без команды» in one operation (BR-OT-12; BR-MX-9, BR-MX-12)
  Given MARS has the members U and Ann, and A's settings name MARS the default team
  When Ann confirms
  Then MARS leaves the list, the pickers and the filters, and its address, its members' and its backlog's answer «Команда не найдена.»
  And its 5 items show «Без команды», keep their project, identifier, assignee, estimates, state, «Область» and project backlog place, and show the operation's time as «Изменён»
  And they are on A's views and on no team's, and their team places are gone
  And A's setting is empty, VENUS and ATLAS being two teams, so A has no default; MARS's memberships ended and U and Ann are still members of the organization
  And no area, project or other team's item was touched, and the name «MARS» is free for a new team

Scenario: The last team of a project cannot be deleted (BR-OT-11; interim guard, lifted by `feat-028-org-level-teams`)
  Given «Бета» has JUPITER only
  Then JUPITER's row and page offer no «Удалить команду»
  And a delete request sent anyway answers 409 «Нельзя удалить единственную команду проекта.» and changes nothing
  And the project's views, whose entry is the team drop-down, always have a team to be reached from

Scenario: Two deletions that would leave a project with no team end with one done (BR-OT-11, BR-OT-13)
  Given A has VENUS and ATLAS only
  When two administrators delete VENUS and ATLAS at once
  Then one is done and the other answers 409 «Нельзя удалить единственную команду проекта.»
  And A has one team, and no request answers with a server error

Scenario Outline: A write racing the deletion never leaves an item on a deleted team (BR-OT-13; NFR Concurrency)
  When <write> runs concurrently with Ann's deletion of MARS
  Then either the write is refused with «Команда не найдена.», or it lands first and the deletion takes it with the rest (an item is reassigned, a setting cleared)
  And no item of A is owned by MARS and no setting of A names it afterwards, and no request answers with a server error

  Examples:
    | write                                                  |
    | U saves ALP-US-20 with «Команда» «MARS»                |
    | U creates a story with «Команда» «MARS»                |
    | Ann names MARS the default team (US-3)                 |
    | U reorders MARS's backlog                              |

Scenario: A child added beneath a story that MARS owns is never refused for the team (BR-OT-3, BR-OT-13; the full case is US-2's)
  Given A's settings name MARS the default team
  When U adds a child beneath a story that MARS owns, concurrently with Ann's deletion of MARS
  Then the child is created in either order: reassigned with the rest if it committed first, else on the project default as it stands after the deletion
  And no request answers «Команда не найдена.» or with a server error, and no item of A is owned by MARS afterwards

Scenario: A deletion in a deactivated project is not reachable (BR-OT-12; ADR-0026 rule 3)
  Given Ann deactivated A
  Then deleting MARS answers «Проект не найден.», and after Ann restores A, MARS and its 5 items are as they were

Scenario: A big deletion is all or nothing, and quick (BR-OT-12; spec NFR: volumes)
  Given MARS owns 5 000 items of A
  When Ann confirms the deletion
  Then it completes within 5 s and every item is owned by no team
  And if it fails at any step, MARS still exists and still owns all 5 000, and their places, and A's setting, are as before
```

## Owed by later lines

```gherkin
# `feat-028-org-level-teams`: deleting a team reaches every project it is linked to, deactivated ones included, with «Без команды» as the destination;
#   the confirmation names each project with its count; unlinking a team from one project chooses another team as the destination (doc-012 US-2, US-3);
#   the last-team guard goes, since a project may have no team
# `mod-007-iterations`: the deleted team's items leave its sprints; its Closed sprints stay readable on the projects' «Спринты» (BR-MX-12, BR-MX-14)
# `mod-018-area-workspace`: a deleted team's area links end with it
# `mod-019-scoped-access`: the grants held on a deleted team end with it
```
