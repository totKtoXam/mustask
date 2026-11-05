# US-3: Every appointment is recorded and readable

- story: US-3
- depends-on: US-2
- shippable: no — the record is written by US-2's appointment and has nothing to show without it
- status: pending

As a **system administrator** or **an organization's administrator**, I want
**every appointment to leave a record of who appointed whom, when and why,
which nothing later erases**, so that **the use of this cross-organization
power can always be checked, by the instance and by the organization it was
used on**.

Copy: action «История назначений» — on each row of «Все организации» (system
administrators) and on «Участники» of the organization (its administrators
only); dialog «История назначений — «<название>»» with columns «Когда»
(«ДД.ММ.ГГГГ ЧЧ:мм», BR-RS-12), «Кто назначил», «Кого назначили»,
«Причина»; empty «Назначений не было»; «Закрыть». Users are named as on
«Участники» (BR-RS-10).

## Acceptance criteria

```gherkin
Scenario: An appointment leaves a record
  Given I appointed Вера to «Бета» on 03.11.2026 14:05 with «Причина» «Борис уволился»
  When I open «История назначений» on «Бета» in «Все организации»
  Then I see one row: «03.11.2026 14:05», «<my name>», «Вера Иванова», «Борис уволился»
  And «Последнее назначение» of «Бета» in the list shows «03.11.2026»

Scenario: The organization's administrators read their own record
  Given I appointed Вера to «Бета» with a reason
  When Анна, an administrator of «Бета», opens «Участники» of «Бета» and chooses «История назначений»
  Then she sees the same row, the reason included

Scenario: Other members do not
  Given Пётр is a «Участник» of «Бета» and Ольга a «Читатель»
  When either opens «Участники» of «Бета»
  Then there is no «История назначений»
  When either asks for «Бета»'s appointment record anyway, outside the panel
  Then it is refused with 403 «Недостаточно прав»
  But both see Вера's «Добавлен» (or «Роль изменена», if she was a member) naming me with the moment

Scenario: An organization's administrator reads only their own organization
  Given Анна administers «Бета» and is not a member of «Альфа»
  When she asks for «Альфа»'s appointment record
  Then it is refused with «Организация не найдена»

Scenario: Newest first, every appointment kept
  Given «Бета» received appointments on 03.11.2026 by me and on 20.11.2026 by Сергей
  When I open «История назначений» on «Бета»
  Then I see the 20.11.2026 row above the 03.11.2026 row

Scenario: The record outlives the membership
  Given I appointed Вера to «Бета»
  When an administrator of «Бета» later changes Вера's role to «Участник» or removes her
  Then «История назначений» on «Бета» still shows my appointment of Вера, unchanged

Scenario: A refused appointment leaves no record
  Given an appointment of Анна to «Альфа» was refused with «Пользователь уже администратор этой организации»
  And «Альфа» never had a successful appointment
  When I open «История назначений» on «Альфа»
  Then I see «Назначений не было»

Scenario: No appointment without its record
  Given writing the record fails during an appointment
  Then the appointment is refused and the organization's memberships are unchanged

Scenario: Nobody edits or deletes a record
  When a system administrator or an organization's administrator opens «История назначений»
  Then it offers no action other than «Закрыть»

Scenario: Without the right
  Given a user without `system-admin` who is not an administrator of «Бета»
  When they ask for «Бета»'s appointment record
  Then it is refused, with «Организация не найдена» if they are not a member of «Бета» and 403 «Недостаточно прав» if they are

Scenario: Volumes
  Given «Бета» has 100 appointment records
  When I open «История назначений» on «Бета»
  Then the record is shown within 2 s
```
