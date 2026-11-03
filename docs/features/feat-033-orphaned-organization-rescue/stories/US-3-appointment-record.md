# US-3: Every appointment is recorded and readable

- story: US-3
- depends-on: US-2
- shippable: no — the record is written by US-2's appointment and has nothing to show without it
- status: pending

As a **system administrator**, I want **every appointment to leave a record
of who appointed whom and when, which nothing later erases**, so that **the
use of this cross-organization power can always be checked**.

Copy: row action «История назначений»; dialog «История назначений —
«<название>»» with columns «Когда» («ДД.ММ.ГГГГ ЧЧ:мм»), «Кто назначил»,
«Кого назначили»; empty «Назначений не было»; «Закрыть». Users are named as on
«Участники» («неактивен» after the name when inactive, «Неизвестный
пользователь» when unknown).

## Acceptance criteria

```gherkin
Scenario: An appointment leaves a record
  Given I appointed Вера to «Бета» on 03.11.2026 14:05
  When I open «История назначений» on «Бета»
  Then I see one row: «03.11.2026 14:05», «<my name>», «Вера Иванова»
  And «Последнее назначение» of «Бета» in the list shows «03.11.2026»

Scenario: Newest first, every appointment kept
  Given «Бета» was rescued on 03.11.2026 by me and again on 20.11.2026 by Сергей
  When I open «История назначений» on «Бета»
  Then I see the 20.11.2026 row above the 03.11.2026 row

Scenario: The record outlives the membership
  Given I appointed Вера to «Бета»
  When an administrator of «Бета» later changes Вера's role to «Участник» or removes her
  Then «История назначений» on «Бета» still shows my appointment of Вера, unchanged

Scenario: Users who left are still named
  Given the system administrator who made an appointment has since been deleted in Keycloak
  When I open «История назначений»
  Then «Кто назначил» shows their last known name with «неактивен»

Scenario: A refused appointment leaves no record
  Given an appointment to «Альфа» was refused with «В организации уже есть активный администратор»
  When I open «История назначений» on «Альфа»
  Then I see «Назначений не было»

Scenario: No appointment without its record
  Given writing the record fails during an appointment
  Then the appointment is refused and the organization's memberships are unchanged

Scenario: Nobody edits or deletes a record
  When I open «История назначений»
  Then it offers no action other than «Закрыть»

Scenario: The organization's members see the who/when, not the history
  Given I appointed Вера to «Бета»
  When any member of «Бета» opens «Участники»
  Then Вера's «Добавлен» (or «Роль изменена», if she was a member) names me with the moment
  And there is no «История назначений» inside the organization

Scenario: Only the right's holders read the history
  Given a user without `system-admin`, an administrator of «Бета» included
  When they ask for «Бета»'s appointment history
  Then it is refused for lack of the right
```
