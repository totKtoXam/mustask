# US-1: A system administrator sees every organization of the instance

- story: US-1
- depends-on: -
- shippable: no — a list with nothing to act on; the appointment is US-2
- status: pending

As a **system administrator**, I want **to see every organization of the
instance with its status**, so that **I can find the organization whose
members report that nobody can administer it any more**.

Copy: entry and page title «Все организации» (under «Настройки»); columns
«Организация», «Статус» («Активна» / «Деактивирована»), «Последнее
назначение»; filter «Поиск по названию»; empty state «Организаций пока нет»;
no match «Организации не найдены». Dates per BR-RS-12.

## Acceptance criteria

```gherkin
Scenario: The list shows every organization with its status
  Given organizations «Альфа» (active), «Бета» (active, appointed into on 03.11.2026) and «Гамма» (deactivated)
  And I hold the `system-admin` client role and am a member of none of them
  When I open «Все организации»
  Then I see the rows in name order:
    | Организация | Статус         | Последнее назначение |
    | Альфа       | Активна        | —                    |
    | Бета        | Активна        | 03.11.2026           |
    | Гамма       | Деактивирована | —                    |

Scenario: The list claims nothing about administrators
  When I open «Все организации»
  Then no column, badge or filter says whether an organization has an administrator

Scenario: The list discloses nothing else
  Given «Бета» has 12 members, 3 projects and work items
  When I look at the row «Бета»
  Then the row shows no member, member count, administrator name, project, team, area or work item
  And the row is not a link into the organization

Scenario: The organization's content stays hidden
  Given I hold `system-admin` and no membership in «Бета»
  When I open «Бета»'s projects, members or settings by address
  Then I see «Организация не найдена», as for an organization that never existed

Scenario Outline: The name filter
  Given the three organizations above
  When I type "<text>" in «Поиск по названию»
  Then I see <rows>

  Examples:
    | text     | rows                 |
    | гам      | «Гамма» only         |
    | ГАМ      | «Гамма» only         |
    |   бет    | «Бета» only          |
    | а        | all three            |
    | (spaces) | all three            |
    | омега    | «Организации не найдены» |

Scenario: ё matches е
  Given an organization «Ёлка»
  When I type «елк» in «Поиск по названию»
  Then I see «Ёлка»

Scenario: Paging
  Given 120 organizations
  When I open «Все организации»
  Then I see the first 50 in name order and the total 120
  When I filter by a text matched by 3 organizations on different pages
  Then I see those 3 on one page with the total 3

Scenario: Without the right
  Given a user who holds `organization-creator` and `dictionaries-admin` but not `system-admin`
  When they look at «Настройки»
  Then there is no «Все организации» entry
  When they open the page by address
  Then they see «Доступ ограничен» and no organization is listed
  When they request the list anyway, outside the panel
  Then it is refused with 403 «Недостаточно прав»

Scenario: Losing the role takes effect with the next token
  Given my `system-admin` role is revoked in the company realm
  When my access token is renewed
  Then «Все организации» disappears from «Настройки» and the page answers «Доступ ограничен»

Scenario: Volumes
  Given 1 000 organizations
  When I open «Все организации», turn a page or change the filter
  Then the page is shown within 2 s
```
