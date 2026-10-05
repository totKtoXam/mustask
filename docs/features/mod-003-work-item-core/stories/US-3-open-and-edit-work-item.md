# US-3: A user opens a work item by its address and edits it on the form

- story: US-3
- depends-on: US-1
- shippable: no — the form edits items that only US-1 creates
- status: pending

As a **signed-in user**, I want **a stable address for every work item and
one form that changes it in one save**, so that **I can share a link to an
item and keep its title and description current**.

## Acceptance criteria

The address follows `status.md` D-5; spec § The identifier and its number,
BR-M3-1, BR-M3-3, BR-M3-5, BR-M3-19, BR-M3-25.

```gherkin
Scenario: The form opens at the item's address
  Given MSK-BUG-4 «Падение при входе» in «Пример организации»
  When the user opens it from the list
  Then the address is /organizations/<id of «Пример организации»>/projects/<id of
    «Мастаск»>/workitems/MSK-BUG-4
  And the heading reads «Баг MSK-BUG-4», the tab «Баг MSK-BUG-4 — Mustask»
  And the trail «Рабочие элементы» leads to the list of «Мастаск»
  And Тип is shown and cannot be changed anywhere on the form

Scenario: The identifier is resolved inside the address's organization
  Given organizations «Орг А» and «Орг Б», each with a project keyed MSK
    and an item MSK-BUG-4
  When the address of «Орг А» and its project MSK with MSK-BUG-4 is opened
  Then the form shows «Орг А»'s item and never «Орг Б»'s
  And the address of «Орг А» with the id of «Орг Б»'s project shows the frame
    «Проект не найден»

Scenario Outline: An identifier that resolves to nothing
  When /organizations/<id of «Пример организации»>/projects/<project
    id>/workitems/<identifier> is opened, where <case>
  Then the page shows the frame <frame>

  Examples:
    | identifier  | case                                                   | frame                       |
    | msk-bug-4   | the letters are not capitals                           | «Рабочий элемент не найден» |
    | MSK-BUG-04  | the number has a leading zero                          | «Рабочий элемент не найден» |
    | MSK-BUG-999 | «Мастаск» has no item 999                              | «Рабочий элемент не найден» |
    | MSK-TASK-4  | item 4 of «Мастаск» is a «Баг»                         | «Рабочий элемент не найден» |
    | ABC-BUG-4   | ABC is not the key of the addressed project «Мастаск»  | «Рабочий элемент не найден» |
    | MSK-BUG-4   | the project id is malformed or names no project        | «Проект не найден»          |
    | MSK-BUG-4   | «Мастаск» is deactivated                               | «Проект не найден»          |
    | MSK-BUG-4   | the project id is of another organization's project    | «Проект не найден»          |

Scenario: A title and a description are changed in one save
  Given the form of MSK-BUG-4
  When the user changes the title and the description and activates
    «Сохранить»
  Then the notice «Изменения сохранены.» is shown and «Сохранить» is disabled
  And reopening the form shows both new values
  And the list's «Изменён» shows the time of that save
  And the identifier is still MSK-BUG-4

Scenario: Nothing to save
  Given the form just opened, or a value changed and changed back
  Then «Сохранить» is disabled

Scenario Outline: An invalid value is refused and nothing is stored
  When the user saves <value> on the form
  Then <message> is shown under that field and the input stays
  And a save sent directly to the server with that value is refused with
    the same text
  And the stored item is unchanged

  Examples:
    | value                                | message                                |
    | an empty title                       | «Введите заголовок.»                   |
    | a title of 256 characters            | «Не более 255 символов.»               |
    | a description of 100 001 characters  | «Не более 100 000 символов.»           |

Scenario: The description is plain text
  When the user saves the description «<b>важно</b>» followed by a line
    break and «шаг 2»
  Then the form shows «<b>важно</b>» literally, with «шаг 2» on the next line

Scenario: Two users save the same form one after the other
  Given users A and B opened the form of MSK-BUG-4 at the same time
  When A saves the title «Первый» and then B saves the description «Шаги»
  Then the item holds the title «Первый» and the description «Шаги», and no
    conflict is shown
  And had B saved the title «Второй» instead, the title would be «Второй»
```
