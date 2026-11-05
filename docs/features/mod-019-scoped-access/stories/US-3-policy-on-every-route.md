# US-3: One policy decides every route by the effective permissions on the addressed object

- story: US-3
- depends-on: US-1, US-2
- shippable: no — the policy is read by the panel (US-4) and held by the tests (US-6)
- status: pending

As an **organization member**, I want **every route, list and counter to answer from one computation of what I may do on the object it addresses**, so that **what a grant gives, and what a missing grant withholds, is the same on every surface (BR-SA-8..-10, doc-011 `scoped-permission-overrides`)**.

## Acceptance criteria

Fixture and users of US-1 (everyone a «Читатель» unless said; Mira a «Участник»). The matrix of codes and bundles is ADR-0029 § 4 as amended by OQ-3 B and OQ-4 B (spec BR-SA-16, -17); the scenarios pin examples, and US-6 generates the matrix.

```gherkin
Scenario Outline: A role of a scope gives its codes on the objects it reaches
  Given Olga is an organization «Читатель» and holds <grant>
  When she <action> on <object>
  Then the answer is <answer>

  Examples:
    | grant                      | action                         | object | answer                  |
    | «Чтение» on «Логистика»    | changes a work item            | A      | 403 «Недостаточно прав» |
    | «Изменение» on «Логистика» | changes a work item            | A      | kept                    |
    | «Изменение» on «Логистика» | reorders the project backlog   | A      | 403 «Недостаточно прав» |
    | «Изменение» on A           | reorders the project backlog   | A      | kept                    |
    | «Управление» on A          | renames the project            | A      | kept                    |
    | «Изменение» on A           | changes a work item            | B      | 403 «Недостаточно прав» |
    | «Планирование» on MARS     | reorders the team's backlog    | MARS   | kept                    |
    | «Планирование» on MARS     | changes a work item            | A      | 403 «Недостаточно прав» |

Scenario: Codes are only added
  Given Mira is a «Участник» and holds «Чтение» on A
  When she changes an item of A
  Then it is kept: no grant takes a code away

Scenario: A grant on a project nothing links works the same
  Given the project D sits at the top level and is linked to no team
  When Olga, holding «Изменение» on D, changes an item of D
  Then it is kept, and her change to an item of A answers 403

Scenario: The order of the grants does not change the result
  Given two copies of the organization, where Olga receives «Чтение» on «Логистика», «Изменение» on A and «Планирование» on MARS in one order in the first and in the reverse order in the second
  When her effective codes are read on every department, project, team and item of both
  Then they are identical, and stay identical after one grant is removed and given again

Scenario: Who reorders which backlog (BR-SA-10; doc-012 US-7)
  Given Mira is a «Участник», Vera and Olga «Читатель»; Olga holds «Планирование» on MARS and Kirill «Изменение» on A
  When each reorders the project backlog of A and then the backlog of MARS
  Then Mira keeps both; Olga keeps MARS's and gets 403 on A's; Kirill keeps A's and gets 403 on MARS's; Vera gets 403 on both
  And a «Участник» reorders every team's backlog, as today, and the project backlog of every project

Scenario: A shared board moves only the cards the user may change (doc-012 US-7)
  Given MARS owns items of A and of B, and Kirill holds «Изменение» on A
  When he opens MARS's board and moves a card of A and a card of B
  Then the board offers moves on the cards of A only, the move of A's card is kept, and a direct move of B's answers 403 «Недостаточно прав»

Scenario: Every organization route resolves its scope from the path
  When a route of the organization, of a department, of a project, of a team, or of a work item is called
  Then the check resolves each id in the path inside its container and runs once on the addressed object
  And a second code (a link's other end, a save's `workitems.link.write`) is decided against the resolved access, never the role alone

Scenario: The answer order stays today's (BR-SA-9)
  When a change to an item of A is requested with no sign-in, by a non-member, and by a «Читатель» with no grant
  Then the answers are 401, «Организация не найдена» and 403 «Недостаточно прав»
  And the same «Читатель» changing an item id that names nothing in A gets 403 (mod-016 § 0.1 row 3 before row 4), while Mira, a «Участник», gets «Рабочий элемент не найден.»
  And `RetrofitAccessTests` is unchanged by this task; `feat-041-project-visibility` restates it

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

Scenario: A single-item read costs a pinned number of statements (R-20)
  Given an integration test on a seeded organization of 10 and then 10 000 grants, at tree depth 1 and 10
  When a member reads one work item
  Then the check issues the same pinned number of statements in every case, and the read stays within 500 ms on the 10 000-grant, 1 000-area, 200-project seed
```
