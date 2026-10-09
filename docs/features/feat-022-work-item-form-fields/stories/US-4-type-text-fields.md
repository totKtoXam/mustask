# US-4: A story carries acceptance criteria, a bug repro steps and system info

- story: US-4
- depends-on: US-1
- shippable: no — the fields live in US-1's left column
- status: pending

As a **signed-in user**, I want **a user story to have its own «Критерии
приёмки» and a bug its own «Шаги воспроизведения» and «Сведения о
системе»**, so that **what "done" means, how to reproduce a defect and where
it shows do not get lost inside «Описание»**.

## Acceptance criteria

Spec § The field table; BR-M3-30, BR-M3-36; § Controls and copy. doc-002
US-12, as plain text (D-2); «Сведения о системе» per status.md D-6 (OQ-4 (b)).

```gherkin
Scenario Outline: Each text field appears on its own type only
  Given a «<type>» opened on the form
  Then «Критерии приёмки» is <ac>, «Шаги воспроизведения» is <repro> and
    «Сведения о системе» is <sys>

  Examples:
    | type                     | ac      | repro   | sys     |
    | Эпик                     | absent  | absent  | absent  |
    | Фича                     | absent  | absent  | absent  |
    | Пользовательская история | shown   | absent  | absent  |
    | Задача                   | absent  | absent  | absent  |
    | Баг                      | absent  | shown   | shown   |

Scenario: The section sits under «Описание»
  Given a «Баг» opened on the form
  Then «Шаги воспроизведения» is an h2 section right under «Описание», with
    the placeholder «Опишите шаги, ожидаемый и фактический результат» and
    the helper «До 100 000 символов»
  And «Сведения о системе» is an h2 section right under «Шаги
    воспроизведения», with the placeholder «Опишите окружение: ОС, браузер,
    версию приложения» and the helper «До 100 000 символов»
  And a «Пользовательская история» shows «Критерии приёмки» there, with the
    placeholder «Опишите, по каким признакам история считается готовой»

Scenario: The field starts empty and is saved with the other fields
  Given a «Баг» just created from the dialog
  Then «Шаги воспроизведения» and «Сведения о системе» are empty
  When the user fills both in and changes «Приоритет» to «1», then saves once
  Then the three values are stored
  And the list's «Изменён» shows the time of that one save

Scenario: The text is plain
  When the user saves «Критерии приёмки» «<b>важно</b>», a line break and
    «шаг 2»
  Then the form shows «<b>важно</b>» literally, with «шаг 2» on the next line

Scenario: The length limit holds in the browser and on the server
  Given «Шаги воспроизведения» or «Сведения о системе» of 100 001 characters
  When the user activates «Сохранить»
  Then «Не более 100 000 символов.» is shown under the field and nothing is
    sent
  And a save sent directly to the server with that text is refused with the
    same text, the item unchanged

Scenario: The field cannot be written onto another type
  When a save sent directly to the server sets «Критерии приёмки» on a
    «Задача», «Шаги воспроизведения» on a «Пользовательская история» or
    «Сведения о системе» on an «Эпик»
  Then it is refused with «Это поле недоступно для рабочего элемента этого
    типа.» and the item is unchanged
```
