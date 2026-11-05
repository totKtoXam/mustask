# US-1: A manager gives, changes and removes a role on a department, a project or a team

- story: US-1
- depends-on: -
- shippable: no — a grant changes nothing until US-3's policy reads it
- status: pending

As a **holder of «Управление» on a department, a project or a team, or an organization admin**, I want **to give an organization member a role of that object's scope, change it and remove it**, so that **each person can do what their work needs on the objects it concerns, and nothing more (BR-SA-1..-4)**.

## Acceptance criteria

Fixture, used by every story: organization «Ромашка»; department «Логистика»; project nodes A and B under it; D at the top level; teams MARS and VENUS. Olga, Pavel, Irina, Kirill are active members with the organization role «Участник» unless said; Vera is «Читатель». Every project is «Организация».

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
  Given E is a member of another organization only, and D's membership here was removed
  When a manager looks for E and D in the grant picker, and a direct request names either
  Then neither is offered, and the request answers «Участник не найден.» and stores nothing

Scenario: A role is changed, then removed
  Given Olga holds «Изменение» on A, given by M, and her change to an item of A is kept
  When M changes it to «Чтение»
  Then Olga's next change to an item of A answers 403, and she still reads it
  When M changes it back to «Изменение» and then removes the grant
  Then Olga is no longer listed, and her next change answers 403 again
  And the list kept one row for Olga throughout (one grant per member and object)

Scenario: A grant of the wrong scope is refused
  When a request gives Olga the team role «Планирование» on A, or the project role «Управление» on MARS
  Then it is answered 400 with the registry's validation error and stores nothing

Scenario: An object's grants are read by those who read the object
  Given Vera holds no grant
  When she opens the grants of «Логистика», of A and of MARS
  Then she reads the list and sees no control to change it

Scenario: Each «Управление» manages its own object and nothing else
  Given Kirill holds «Управление» on A and on MARS, and no other grant
  When he renames A, sets its default team, renames MARS and adds and removes its members
  Then each is saved
  And the same on B or on VENUS, deleting MARS, and creating a project or a team answer 403 «Недостаточно прав»

Scenario: Removing the organization membership ends every grant and team membership
  Given Olga holds grants on «Логистика», A and MARS, and is a member of MARS
  When an organization admin removes her from the organization
  Then she is listed on none of those objects and in no team
  And adding her again gives back none of them

Scenario: A grant written while the membership is removed loses
  When a manager gives Irina a role at the moment her membership is removed
  Then either the membership is removed first and the grant answers «Участник не найден.», or the grant is ended with the membership; no grant of a removed member remains

Scenario: A grant ends with its object, and a deactivated project keeps it
  Given Olga holds «Управление» on MARS and on A
  When MARS is deleted
  Then her grant on MARS is gone
  When A is deactivated and then restored
  Then her grant on A is listed again after the restore

Scenario: People move without moving work
  Given Olga is a member of MARS with «Планирование», and is the assignee of MARS items of A
  When she is removed from MARS and her grant is removed, and added to VENUS with «Планирование»
  Then those items keep their project, owning team, assignee and estimates
  And on her next request she reorders VENUS's backlog, and a reorder of MARS's answers 403

Scenario: An id from another organization names nothing
  When M gives a role on a department, project or team id of another organization through «Ромашка»'s address
  Then the answer is the object's not-found, as for an id that names nothing, and nothing is stored
```
