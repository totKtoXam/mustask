# US-1: A manager gives, changes and removes a role on a department, a project or a team

- story: US-1
- depends-on: -
- shippable: no — a grant changes nothing until US-3's policy reads it
- status: pending

As a **holder of «Управление» on a department, a project or a team, or an organization admin**, I want **to give an organization member a role of that object's scope, change it and remove it**, so that **each person can do what their work needs on the objects it concerns, and nothing more (BR-SA-1..-4)**.

## Acceptance criteria

Fixture, used by every story. Organization «Ромашка»: departments «Логистика» and «Финансы»; under «Логистика» the project nodes A and B; the project D at the top level; the sub-area «Платформа» under A's node and «Склад» under B's node; teams MARS and VENUS. Every project is «Организация». **Every user is an organization «Читатель» holding no grant unless a scenario says otherwise**, so the bundle of a grant is what a scenario shows: Olga, Pavel, Irina, Kirill, Vera, Denis. Mira is an organization «Участник» (she already changes the items of every project and reorders every backlog). Anna is an organization admin. Grant lists and the grantee picker follow mod-016 §§ 0.4-0.5: `UserRef`, display-name order, 2-character minimum, 20 results with `moreMatched`.

```gherkin
Scenario Outline: A manager gives a role of the object's scope
  Given M holds «Управление» on <object>, or is an organization admin holding no grant
  When M opens the object's grants and picks Olga
  Then exactly <roles> are offered
  And the grant is listed with Olga, the role, M as the giver, and the date and time
  And Olga's next request is decided with that grant

  Examples:
    | object      | roles                               |
    | «Логистика» | «Чтение», «Изменение», «Управление» |
    | A           | «Чтение», «Изменение», «Управление» |
    | MARS        | «Планирование», «Управление»        |

Scenario: Without «Управление» there is no grant to give
  Given Pavel holds «Изменение» on «Логистика» and on A
  When he opens their grants
  Then no control to give, change or remove a role is shown
  And his direct request to give Irina a role on either answers 403 «Недостаточно прав» and stores nothing

Scenario: Only an active member of this organization can be given a role (BR-SA-2)
  Given E is a member of another organization only, and Denis's membership here was removed
  When a manager looks for E and Denis in the grant picker, and a direct request names either
  Then neither is offered, and the request answers «Участник не найден.» and stores nothing

Scenario: A role is changed, then removed
  Given Olga holds «Изменение» on A, given by Anna, and her change to an item of A is kept
  When Anna changes it to «Чтение»
  Then Olga's next change to an item of A answers 403, and she still reads it
  When Anna changes it back to «Изменение» and then removes the grant
  Then Olga is no longer listed, and her next change answers 403 again
  And the list kept one row for Olga throughout (one grant per member and object)

Scenario: A grant of the wrong scope or on the wrong node type is refused (OQ-1 A)
  When a request gives Olga the team role «Планирование» on A, the project role «Управление» on MARS,
    or any department role on the node of A or on «Платформа»
  Then it is answered with the registry's validation or type refusal (the contract names the code, as mod-018's `type_not_allowed`) and stores nothing
  And the node of A and «Платформа» show no «Участники» section, and no grant is ever held on them

Scenario: An object's grants are read by those who read the object
  Given Vera holds no grant
  When she opens the grants of «Логистика», of A and of MARS
  Then she reads the lists and sees no control to change them
  And a grant held by an inactive user is listed with «неактивен», and an unknown one as «Неизвестный пользователь»

Scenario: Each «Управление» manages its own object (BR-SA-2)
  Given Kirill holds «Управление» on A and on MARS, and no other grant
  When he renames A, sets its default team, renames MARS and adds and removes its members
  Then each is saved
  And the same on B or on VENUS, deleting MARS, and creating a team answer 403 «Недостаточно прав»

Scenario: Removing the organization membership ends every grant and team membership
  Given Olga holds grants on «Логистика», A and MARS, and is a member of MARS
  When Anna removes her from the organization
  Then she is listed on none of those objects and in no team
  And adding her again gives back none of them

Scenario: A grant written while the membership is removed loses (BR-SA-3)
  When a manager gives Irina a role at the moment her membership is removed
  Then no grant of a removed member remains, and the grant answers «Участник не найден.» or is ended with the membership

Scenario: A grant ends with its object, and a deactivated project keeps it
  Given Olga holds «Управление» on MARS and on A
  When MARS is deleted
  Then her grant on MARS is gone
  When A is deactivated and then restored
  Then her grant on A is listed again after the restore

Scenario: Edge cases of the managers themselves (BR-SA-2, -3)
  Given Pavel and Kirill both hold «Управление» on MARS and each removes the other's grant at the same moment
  Then both removals may succeed and the team is left to the admin, who manages every object
  And a manager may remove their own grant
  And a grant given to an organization admin is stored and is kept when the admin is made a «Участник»

Scenario: A grantee is never the grant's giver (OQ-9 B, BR-SA-19)
  Given Pavel holds «Управление» on «Логистика», so he can give roles on A (OQ-8 A)
  When he gives himself the project role «Управление» on A, or changes the role of a grant he holds on any object, or Anna does the same for herself
  Then each is refused with the contract's validation code and stores nothing
  When Pavel gives Irina «Управление» on A, and she gives Pavel «Управление» on A
  Then both are kept, and each grant lists its giver; Pavel's own removal of a grant he holds is kept

Scenario: People move without moving work
  Given Olga is a member of MARS with «Планирование», and is the assignee of MARS items of A
  When she is removed from MARS and her grant is removed, and added to VENUS with «Планирование»
  Then those items keep their project, owning team, assignee and estimates
  And on her next request she reorders VENUS's backlog, and a reorder of MARS's answers 403

Scenario: An id from another organization names nothing
  When M gives a role on a department, project or team id of another organization through «Ромашка»'s address
  Then the answer is the object's not-found, as for an id that names nothing, and nothing is stored
```
