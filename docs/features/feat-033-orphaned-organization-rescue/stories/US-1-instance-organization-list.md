# US-1: A system administrator sees every organization and which have no active administrator

- story: US-1
- depends-on: -
- shippable: no — a list with nothing to act on; the appointment is US-2
- status: pending

As a **system administrator**, I want **to see every organization of the
instance and which of them have no active administrator**, so that **I find
the organizations nobody can administer any more**.

Copy: entry and page title «Все организации» (under «Настройки»); columns
«Организация», «Статус», «Администратор», «Последнее назначение»; filter
«Поиск по названию», switch «Только без активного администратора»; empty
states «Организаций пока нет» and, with the switch on, «Во всех организациях
есть активный администратор»; no match «Организации не найдены».

## Acceptance criteria

```gherkin
Scenario: The list shows every organization with its administrator state
  Given organizations «Альфа» (admin Анна, active), «Бета» (only admin Борис, disabled in Keycloak)
    and «Гамма» (deactivated, only admin Галина, deleted in Keycloak)
  And I hold the `system-admin` client role and am a member of none of them
  When I open «Все организации»
  Then I see the rows in name order:
    | Организация | Статус          | Администратор                | Последнее назначение |
    | Альфа       | Активна         | Есть активный администратор  | —                    |
    | Бета        | Активна         | Нет активного администратора | —                    |
    | Гамма       | Деактивирована  | Нет активного администратора | —                    |

Scenario: The list discloses nothing else
  Given «Бета» has 12 members, 3 projects and work items
  When I open «Все организации» and look at the row «Бета»
  Then the row shows no member, member count, administrator name, project, team, area or work item
  And the row is not a link into the organization

Scenario: The organization's content stays hidden
  Given I hold `system-admin` and no membership in «Бета»
  When I open «Бета»'s projects, members or settings by address
  Then I see «Организация не найдена», as for an organization that never existed

Scenario: Filter to the organizations that need rescue
  Given the three organizations above
  When I switch on «Только без активного администратора»
  Then I see «Бета» and «Гамма» only
  When I also type «гам» in «Поиск по названию»
  Then I see «Гамма» only

Scenario: An unknown administrator does not count
  Given «Дельта» was created a minute ago by a user the projection does not know yet
  When I open «Все организации»
  Then «Дельта» shows «Нет активного администратора»

Scenario: The state follows the projection
  Given «Бета» shows «Нет активного администратора»
  When Борис's account is enabled again in Keycloak and the projection receives it
  And I open «Все организации» again
  Then «Бета» shows «Есть активный администратор»

Scenario: Everyone with the role, nobody without it
  Given a user who holds `organization-creator` and `dictionaries-admin` but not `system-admin`
  When they look at «Настройки»
  Then there is no «Все организации» entry
  When they open the page by address
  Then they see «Доступ ограничен» and no organization is listed

Scenario: Losing the role takes effect with the next token
  Given my `system-admin` role is revoked in the company realm
  When my access token is renewed
  Then «Все организации» disappears from «Настройки» and the page answers «Доступ ограничен»

Scenario: Volumes
  Given 1 000 organizations of up to 500 memberships each
  When I open «Все организации» or switch the filter
  Then the list is shown within 2 s
```
