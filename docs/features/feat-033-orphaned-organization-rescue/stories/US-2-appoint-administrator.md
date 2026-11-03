# US-2: A system administrator appoints an administrator to an organization that has none

- story: US-2
- depends-on: US-1
- shippable: no — "audited" is not met until US-3 writes the record in the same operation
- status: pending

As a **system administrator**, I want **to appoint an active user as
administrator of an organization that has no active administrator**, so that
**the organization can be administered again without my getting into its
content**.

Copy: row action «Назначить администратора» (only on rows with «Нет активного
администратора»); dialog «Назначение администратора» with the text
«Организация «<название>» осталась без активного администратора. Выбранный
пользователь станет её администратором. Вы не получите доступа к содержимому
организации.», field «Пользователь» (search by name or e-mail, the search
states of mod-016's «Добавление участника»), «Назначить», «Отмена»; success
«Администратор назначен: <имя>». Refusals: «В организации уже есть активный
администратор», «Этого пользователя нельзя назначить администратором»,
«Нельзя назначить администратором себя», «Организация не найдена».

## Acceptance criteria

```gherkin
Scenario: Appoint a non-member
  Given «Бета» has no known active administrator
  And Вера is an active projected user who is not a member of «Бета»
  When I choose «Назначить администратора» on «Бета», pick Вера and press «Назначить»
  Then I see «Администратор назначен: Вера Иванова»
  And «Бета» shows «Есть активный администратор»
  And on «Участники» of «Бета» Вера has «Роль» «Администратор», «Добавлен» «<my name>, <now>», «Роль изменена» «—»
  And Вера is in none of «Бета»'s teams

Scenario: Appoint an existing member
  Given Пётр is a «Читатель» of «Бета», added by Борис on 01.10.2026 10:00 and in team «Ядро»
  When I appoint Пётр to «Бета»
  Then Пётр's «Роль» is «Администратор», «Добавлен» stays «Борис …, 01.10.2026 10:00»
  And «Роль изменена» is «<my name>, <now>», and he is still in team «Ядро»

Scenario: The appointment takes effect on the next request
  Given Вера has «Бета» open in the panel as a non-member
  When I appoint her
  Then her next request to «Бета» is answered under the administrator's codes, with no re-login

Scenario: I still see no content
  When I appoint Вера to «Бета»
  Then opening «Бета» by address still answers me «Организация не найдена»

Scenario: Refused while an active administrator exists
  Given «Альфа» shows «Есть активный администратор»
  Then its row offers no «Назначить администратора»
  When an appointment to «Альфа» is attempted anyway
  Then it is refused with «В организации уже есть активный администратор» and nothing changes

Scenario: Only active known users are offered and accepted
  Given Олег's account is disabled and Юлия is not yet known to the projection
  When I search «Олег» or «Юлия» in «Пользователь»
  Then neither is offered
  When an appointment of either is attempted anyway
  Then it is refused with «Этого пользователя нельзя назначить администратором» and nothing changes

Scenario: The search does not reveal membership
  When I search for users in the dialog
  Then a candidate shows only the name and the e-mail, whether or not they are a member of the organization

Scenario: No self-appointment
  When I try to appoint myself to «Бета»
  Then it is refused with «Нельзя назначить администратором себя» and nothing changes
  But another system administrator may appoint me

Scenario: A deactivated organization
  Given «Гамма» is deactivated and has no active administrator
  When I appoint Вера to «Гамма»
  Then «Гамма» stays «Деактивирована»
  And Вера sees «Гамма» in her deactivated organizations and can restore it

Scenario: Two appointments at once
  Given «Бета» has no active administrator
  When I appoint Вера and another system administrator appoints Пётр at the same moment
  Then exactly one succeeds
  And the other is refused with «В организации уже есть активный администратор», and the list is read again

Scenario: An administrator came back in between
  Given my list shows «Бета» without an active administrator
  And Борис's account is enabled again and the projection has received it
  When I press «Назначить» for Вера
  Then it is refused with «В организации уже есть активный администратор» and nothing changes

Scenario: Without the right
  Given a user without `system-admin`
  When they attempt an appointment
  Then it is refused for lack of the right and nothing changes

Scenario: Cancel
  When I open the dialog, pick Вера and press «Отмена»
  Then nothing changes
```
