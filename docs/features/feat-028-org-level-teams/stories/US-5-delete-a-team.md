# US-5: Deleting a team reaches every project it works on

- story: US-5
- depends-on: US-3, US-4
- shippable: no — one of the eight stories of `feat-028-org-level-teams`, an L and one MR (spec § Stories); doc-012 US-2's deletion scenarios, and the last-team guard of mod-017 lifted
- status: pending

As an **organization administrator**, I want **deleting a team to say what it will do in each project and to do all of it at once**, so that **no work item is left owned by a deleted team in any project, and removing a team never moves its work onto another team behind anyone's back**.

Cast (spec § Stories): MARS is linked to Alpha, Beta, Gamma and to Delta, which is deactivated. It owns 5 items of Alpha, 10 of Beta (2 of them «Удалена»), none of Gamma and 3 of Delta. Its members are Max and Vera; Max is also in VENUS. Alpha's and Delta's settings name MARS the default team and Beta's name VENUS, which is linked to Beta. ATLAS is linked to no project and owns nothing unless a scenario says otherwise; Epsilon is an active project with no team. Deleting is a command of the team's own page; a project's «Команды» offers «Отключить от проекта» only. Sprint, area and grant clauses are owed by later lines. New copy is proposed and fixed by the design.

## Acceptance criteria

```gherkin
Scenario: The confirmation counts, per linked project, the items that lose their team (BR-OL-9)
  When Ann chooses «Удалить команду» on MARS's page
  Then «Удаление команды» reads «Удалить команду «MARS»? Рабочие элементы станут «Без команды»: «Alpha» — 5, «Beta» — 10, «Gamma» — 0, «Delta» (деактивирован) — 3. Восстановить команду будет нельзя.»
  And the counts hold every state, «Удалена» included, as read when the dialog opens
  And «Отмена», Escape and «Закрыть» change nothing
  And for ATLAS, which is linked to no project, it reads «Удалить команду «ATLAS»? Восстановить команду будет нельзя.»

Scenario: Confirming deletes the team and sends its items to «Без команды» in one operation (BR-OL-9, BR-OL-2, BR-MX-9)
  When Ann confirms
  Then MARS is in no list, picker, multiselect or filter, and its address, its members' list and its backlog's address answer «Команда не найдена.»
  And its 18 items, 5 + 10 + 3, show «Без команды», keep project, identifier, assignee, estimates, state, «Область» and project backlog place, and show the operation's time as «Изменён»
  And they are on their projects' views and on no team's, and their team places are gone
  And no link names MARS: Alpha, Beta, Gamma and Delta list no MARS, and MARS lists no project
  And Alpha's and Delta's default settings are empty and BR-OL-7's inference applies again, while Beta's setting still names VENUS
  And Max's and Vera's memberships of MARS ended, Max is still in VENUS, and both are still members of «Ромашка»
  And no project, area or work item was deleted, and the name "MARS" is free for a new team

Scenario: The deletion reaches a deactivated project (BR-OL-9; ADR-0026 rule 2; mod-002 BR-M1-20)
  Given Ann deleted MARS
  When Ann restores Delta
  Then Delta is linked to no MARS, its 3 items show «Без команды», and its default setting, which named MARS, is empty (doc-012 US-2)

Scenario: A project's last team can be deleted, and the project works on (BR-OL-10; mod-017 BR-OT-11 lifted)
  Given project Zulu (key ZUL) is linked to JUPITER only, which owns 2 of its items
  When Ann deletes JUPITER
  Then the deletion is done, with no «Нельзя удалить единственную команду проекта.»: `organizations.team.last_team_cannot_be_deleted` is retired
  And the 2 items show «Без команды», and Zulu has no team and no default team, and works as US-2 says
  And every team's page offers «Удалить команду», and no team's row on a project's «Команды» does

Scenario: Two deletions that would leave a project with no team are both done (BR-OL-10)
  Given project Zulu is linked to JUPITER and ATLAS only
  When Ann deletes JUPITER and Ivan deletes ATLAS at the same moment
  Then both are done, Zulu has no linked team, and no request answers with a server error

Scenario Outline: A write that names the team while it is deleted is taken with the rest or refused (BR-OL-9; mod-017 BR-OT-13; spec § NFR Concurrency)
  When <write> overlaps Ann's deletion of MARS, in either order
  Then it either commits first and the deletion works on what it left, taking whatever it gave MARS with the rest, or it comes after and is refused <refusal>
  And no item is owned by MARS afterwards, no setting names it, no link names it, and no request answers with a server error
  Examples:
    | write                                                   | refusal                          |
    | Max saves ALP-US-20 with «Команда» MARS                 | with «Команда не найдена.»       |
    | Max creates a story of Alpha with «Команда» MARS        | with «Команда не найдена.»       |
    | Ann names MARS the default team of Gamma, a project that links it | with «Команда не найдена.» |
    | Ivan links MARS to Epsilon, an active project with no team        | with «Команда не найдена.» |
    | Max reorders MARS's backlog at Alpha's address          | with «Команда не найдена.»       |
    | Ivan unlinks MARS from Beta, naming VENUS               | with «Команда не найдена.»       |
    | Ivan unlinks VENUS from Beta, naming MARS as the destination | with «Команда не найдена.»  |

Scenario: A child created beneath a story MARS owns is never refused for the team (BR-OL-9; mod-017 BR-OT-13 (2))
  When Max adds a child beneath a story of Alpha that MARS owns, concurrently with the deletion
  Then the child is created in either order: reassigned with the rest if it committed first, else on Alpha's default as it stands after the deletion
  And no request answers «Команда не найдена.» or with a server error, and no item is owned by MARS afterwards

Scenario: Only an administrator deletes a team (BR-OL-5, spec § Roles)
  When Max or Vera asks, by any request, to delete MARS or to count what it owns
  Then each is refused with 403 «Недостаточно прав», MARS stays, and their pages offer no «Удалить команду»
  And a non-member of «Ромашка» is answered «Организация не найдена.»

Scenario: A big deletion is all or nothing, and quick (BR-OL-9; spec § NFR)
  Given MARS owns 5 000 items across Alpha, Beta and Delta
  When Ann confirms the deletion
  Then it completes within 5 s and every item is owned by no team
  And if it fails at any step, MARS still exists, still owns all 5 000, and its places, links and the settings are as before

Scenario: The dialog waits for the counts, which are read in a fixed number of statements (BR-OL-9; spec § NFR Query shape)
  When Ann opens «Удалить команду» for a team linked to 50 projects
  Then the dialog shows a skeleton and «Удалить команду» is unavailable until the counts are read
  And a failed read says so with «Повторить», and the deletion stays unavailable until it succeeds
  And the number of statements that read the counts is the same for 2 projects as for 50
```

## Owed by later lines

```gherkin
# `mod-007-iterations`: the deleted team's items leave its sprints; its Closed sprints and their outcomes stay readable, read-only, on the projects' «Спринты»
# `mod-018-area-workspace`: a deleted team's area links end with it
# `mod-019-scoped-access`: the grants held on a deleted team end with it
# `mod-009-dashboards-widgets`: no dashboard for a deleted team
```
