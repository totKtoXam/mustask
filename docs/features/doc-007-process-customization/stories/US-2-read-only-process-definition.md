# US-2: Any organization member reads the process definition, read-only

- story: US-2
- depends-on: US-1
- shippable: no — delivered by mod-001-process-seed
- status: pending

As an **organization member (any role)**, I want **to see the process
definition without being able to change it**, so that **I understand how
work items are typed and stated in my organization**.

## Acceptance criteria

```gherkin
Scenario: Every role can read the same process definition
  Given a user whose role in an organization is admin, member or viewer
  When they open the process-definition screen from one of its projects
  Then they see all five types and, for each: its key, its full state list
    in order, with the initial state marked, the Russian label of each
    state's category, its fixed icon and colour, and its backlog level
  And the information shown is identical regardless of their role

Scenario: A user with no membership in the organization reads nothing
  Given a user who holds no ORG_MEMBERSHIP in that organization
  When they request that organization's process definition
  Then the answer is «Организация не найдена», the same as for an
    organization that does not exist (doc-001 BR-5, doc-006 BR-M1-8)
  And no type, state or backlog level of it is disclosed

Scenario: No role can add, rename or remove anything
  Given a user whose role in the organization is admin, member or viewer
  When they attempt to create, rename or delete a type, a key, a state or a
    backlog level
  Then no MVP surface exposes such an operation, and any request that would
    change the process definition is refused (MVP has no process editing at
    all, D-6)

Scenario: Categories are shown by their Russian labels
  Given the organization's built-in process
  When a member inspects the states of «Задача» on the process-definition
    screen
  Then «Новая» shows the category «Новые», «Активна» «В работе», «Закрыта»
    «Завершённые» and «Удалена» «Удалённые» (spec.md § "MVP: the seeded
    process")
  And no category is shown by its English name

Scenario: One state label may belong to two categories
  Given the organization's built-in process
  When a member compares «Пользовательская история» and «Баг» on the
    process-definition screen
  Then «Разрешена» of «Пользовательская история» shows the category
    «В работе»
  And «Разрешена» of «Баг» shows the category «Решённые» (doc-010 D-4)
```
