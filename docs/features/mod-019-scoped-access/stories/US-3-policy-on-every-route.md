# US-3: One policy decides every route by the effective permissions on the addressed object

- story: US-3
- depends-on: US-1, US-2
- shippable: no — the policy is read by the panel (US-4) and held by the tests (US-6)
- status: pending

As an **organization member**, I want **every route, list and counter to answer from one computation of what I may do on the object it addresses**, so that **what a grant gives, and what a missing grant withholds, is the same on every surface (BR-SA-8..-10, doc-011 `scoped-permission-overrides`)**.

## Acceptance criteria

Fixture of US-1. The matrix of codes and bundles is ADR-0029 § 4; scenarios here pin behaviour and examples, and US-6 generates the full matrix.

```gherkin
Scenario Outline: A role of a scope gives its codes on the objects it reaches
  Given Olga is an organization «Читатель» and holds <grant>
  When she <action> on <object>
  Then the answer is <answer>

  Examples:
    | grant                    | action                          | object        | answer                |
    | «Чтение» on «Логистика»  | changes a work item             | A             | 403 «Недостаточно прав» |
    | «Изменение» on «Логистика» | changes a work item           | A             | kept                  |
    | «Изменение» on «Логистика» | reorders the project backlog  | A             | 403 «Недостаточно прав» |
    | «Изменение» on A         | reorders the project backlog    | A             | kept                  |
    | «Управление» on A        | renames the project             | A             | kept                  |
    | «Изменение» on A         | changes a work item             | B             | 403 «Недостаточно прав» |
    | «Планирование» on MARS   | reorders the team's backlog     | MARS          | kept                  |
    | «Планирование» on MARS   | changes a work item             | A             | 403 «Недостаточно прав» |

Scenario: Codes are only added
  Given Pavel is a «Участник» and holds «Чтение» on A
  When he changes an item of A
  Then it is kept: no grant takes a code away

Scenario: A grant on a project nothing links works the same
  Given the project Z is linked to no team and sits at the top level
  When Olga, holding «Изменение» on Z, changes an item of Z
  Then it is kept

Scenario: The order of the grants does not change the result
  Given two copies of the organization, where Olga receives «Чтение» on «Логистика», «Изменение» on A and «Планирование» on MARS in one order in the first and in the reverse order in the second
  When her effective codes are read on every department, project, team and item of both
  Then they are identical, and stay identical after one grant is removed and given again

Scenario: The project backlog's order has its own code (BR-SA-10)
  Given Vera is a «Читатель», Pavel a «Участник», Olga a «Участник» holding «Чтение» on A
  When each reorders the project backlog of A
  Then Vera's answers 403, Pavel's and Olga's are kept
  And a «Участник» still reorders every team's backlog, and Vera still cannot

Scenario: Every organization route resolves its scope from the path
  When a route of the organization, of a department, of a project, of a team, of a work item of a project, or of a sprint's team is called
  Then the check resolves each id in the path inside its container and runs once on the addressed object
  And a second code (a link's other end, a save's `workitems.link.write`) is decided against the resolved access, never the role alone

Scenario: The answer order stays today's (BR-SA-9)
  When a change to an item of A is requested with no sign-in, by a non-member, and by a «Читатель» with no grant
  Then the answers are 401, «Организация не найдена» and 403 «Недостаточно прав»
  And the same «Читатель» changing an item id that names nothing in A gets «Рабочий элемент не найден.», not 403

Scenario: Split codes (takes the codes split from mod-018)
  Given Pavel holds «Управление» on «Логистика» and no organization right beyond «Участник»
  When he creates a child area under «Логистика», renames it and deletes it
  Then each is kept
  When he creates a department, creates a project, or deletes a team
  Then each answers 403 «Недостаточно прав»
  And creating a project with a place under «Логистика» is decided by `organizations.project.create` first, so it answers 403 and stores nothing

Scenario: A list is filtered by what the caller reads
  Given Olga holds «Чтение» on A only and is a «Читатель»
  When she opens the organization's project list
  Then every project is listed (every project is «Организация» until feat-041), each once

Scenario: A grant removal applies from the next check
  Given Olga holds «Изменение» on A only
  When the grant is removed while her save of an item of A is in flight
  Then a save checked before the removal committed is kept, one checked after it answers 403 and leaves the item unchanged, and no save is half-applied

Scenario: Refusals are logged without content
  When the policy refuses a request
  Then one log line carries the route, the code, the resource scope and the user id, and no project or item name

Scenario: The instance administrator holds no organization code
  Given a user with the instance role and no membership
  When he calls any organization route
  Then he gets «Организация не найдена»

Scenario: A single-item read stays fast
  Given an organization with 10 000 grants, 1 000 areas and 200 projects
  When a member reads one work item
  Then the answer arrives within 500 ms, access check included, in a number of statements that does not grow with the grants
```
