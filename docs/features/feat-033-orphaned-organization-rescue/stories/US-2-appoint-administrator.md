# US-2: A system administrator appoints an administrator to an organization

- story: US-2
- depends-on: US-1
- shippable: no — "audited" is not met until US-3 writes the record in the same operation
- status: pending

As a **system administrator**, I want **to appoint a user as administrator of
any organization, adding without taking anything away**, so that **an
organization nobody can administer any more can be administered again without
my getting into its content**.

Copy: row action «Назначить администратора» (on every row); dialog
«Назначение администратора» with the text «Выбранный пользователь станет
администратором организации «<название>». Никто не будет удалён, ничья роль
не будет понижена. Вы не получите доступа к содержимому организации.» and the
warning «MUSTASK не видит, отключена ли учётная запись в Keycloak: убедитесь,
что пользователь может войти.»; field «Пользователь» (search by name or
e-mail, the search states of mod-016's «Добавление участника»); field
«Причина» (required, up to 500 characters; errors «Укажите причину», «Не более
500 символов»); «Назначить», «Отмена»; success «Администратор назначен: <имя>».
Refusals: «Пользователь уже администратор этой организации», «Этого
пользователя нельзя назначить администратором», «Нельзя назначить
администратором себя», «Организация не найдена», «Недостаточно прав».

## Acceptance criteria

```gherkin
Scenario: Appoint a non-member
  Given members of «Бета» report that its only administrator Борис has left
  And Вера has signed in to MUSTASK and is not a member of «Бета»
  When I choose «Назначить администратора» on «Бета», pick Вера, enter «Причина» «Борис уволился» and press «Назначить»
  Then I see «Администратор назначен: Вера Иванова»
  And on «Участники» of «Бета» Вера has «Роль» «Администратор», «Добавлен» «<my name>, <now>», «Роль изменена» «—»
  And Вера is in none of «Бета»'s teams
  And Борис's membership is unchanged

Scenario: Appoint an existing member
  Given Пётр is a «Читатель» of «Бета», added by Борис on 01.10.2026 10:00 and in team «Ядро»
  When I appoint Пётр to «Бета» with a reason
  Then Пётр's «Роль» is «Администратор», «Добавлен» stays «Борис …, 01.10.2026 10:00»
  And «Роль изменена» is «<my name>, <now>», and he is still in team «Ядро»

Scenario: An organization that has administrators
  Given «Альфа» has the administrator Анна
  When I appoint Вера to «Альфа» with a reason
  Then Вера is an administrator of «Альфа» and Анна still is, with her membership unchanged

Scenario: Nobody is removed or downgraded
  Given «Бета» has administrators, members and readers
  When I appoint Вера to «Бета»
  Then every other membership of «Бета» is exactly as before

Scenario Outline: The reason is required
  When I fill «Причина» with "<reason>" and press «Назначить»
  Then <result>

  Examples:
    | reason                     | result                                          |
    | (empty)                    | «Укажите причину» and nothing changes           |
    | (only spaces)              | «Укажите причину» and nothing changes           |
    | 500 characters             | the appointment succeeds                        |
    | 501 characters             | «Не более 500 символов» and nothing changes     |
    | «  Борис уволился  »       | the appointment succeeds with «Борис уволился»  |

Scenario: The dialog warns about Keycloak accounts
  When I open «Назначить администратора»
  Then the dialog shows «MUSTASK не видит, отключена ли учётная запись в Keycloak: убедитесь, что пользователь может войти.»

Scenario: The appointment takes effect on the next request
  Given Вера has the panel open and is not a member of «Бета»
  When I appoint her
  Then her next request to «Бета» is answered under the administrator's codes, with no re-login

Scenario: I still see no content
  When I appoint Вера to «Бета»
  Then opening «Бета» by address still answers me «Организация не найдена»

Scenario: Already an administrator
  Given Анна is an administrator of «Альфа»
  When I appoint Анна to «Альфа»
  Then it is refused with «Пользователь уже администратор этой организации» and nothing changes

Scenario: Only users who have signed in are found
  Given Юлия has never signed in to MUSTASK
  When I search «Юлия» in «Пользователь»
  Then she is not offered
  When an appointment of her is attempted anyway, outside the panel
  Then it is refused with «Этого пользователя нельзя назначить администратором» and nothing changes

Scenario: The search does not reveal membership
  When I search for users in the dialog
  Then each candidate shows only the name and the e-mail, whether or not they are a member of the organization

Scenario: No self-appointment
  When I try to appoint myself to «Бета»
  Then it is refused with «Нельзя назначить администратором себя» and nothing changes
  But another system administrator may appoint me

Scenario: A deactivated organization
  Given «Гамма» is deactivated
  When I appoint Вера to «Гамма»
  Then «Гамма» stays «Деактивирована»
  And Вера sees «Гамма» in her deactivated organizations and can restore it

Scenario: The rescue is repeatable
  Given I appointed Вера to «Бета» and she cannot sign in
  When I appoint Пётр to «Бета» with a reason
  Then Пётр is an administrator of «Бета» and Вера's membership is unchanged

Scenario: The same user twice at once
  When I and another system administrator appoint Вера to «Бета» at the same moment
  Then exactly one succeeds
  And the other is refused with «Пользователь уже администратор этой организации»

Scenario: Different users at once
  When I appoint Вера and another system administrator appoints Пётр to «Бета» at the same moment
  Then both succeed and both are administrators of «Бета»

Scenario: An organization that does not exist
  When an appointment to an organization id that does not exist is attempted
  Then it is refused with «Организация не найдена»

Scenario: Without the right
  Given a user without `system-admin`, an administrator of «Бета» included
  When they attempt an appointment
  Then it is refused with 403 «Недостаточно прав» and nothing changes

Scenario: Cancel
  When I open the dialog, pick Вера, enter a reason and press «Отмена»
  Then nothing changes
```
