# US-1: A user opens a work item from the backlog or the kanban in a dialog

- story: US-1
- depends-on: -
- shippable: no — the dialog hosts the card of US-2/US-3
- status: pending

As a **signed-in user**, I want **a work item to open as a window over the
backlog or the kanban I am working on**, so that **I read and change it
without leaving the board, and come back to exactly where I was**.

## Acceptance criteria

Data for every story of this task, as feat-027's US-1: organization «Пример
организации»; project «Мастаск», key `MSK`; team «Платформа» (default area
«Платформа», «Платформа / API» below it). In «Платформа»: MSK-EPIC-1 «Вход и
регистрация» (Трудоёмкость 20, Бизнес-ценность 300, «Бизнес», Приоритет 1);
MSK-FEAT-6 «Вход по ссылке»; MSK-US-2 «Письмо со ссылкой» — user story, state
«Новая», Приоритет 2, Оценка в пунктах 5, Вид ценности «Бизнес», assignee
«Анна Смирнова», «Описание» «Пользователь получает письмо со ссылкой для
входа.», «Критерии приёмки» «Ссылка действует 15 минут.», changed 05.10.2026
14:30, parent MSK-FEAT-6, children MSK-BUG-5 «Письмо не доходит», MSK-TASK-3
«Шаблон письма» and MSK-TASK-7 «Настроить SMTP» (area «Мастаск»); MSK-BUG-4
«Ссылка истекает раньше срока»; MSK-TASK-3 (Приоритет 3). The story's process
states are «Новая», «Активна», «Разрешена», «Закрыта». Spec BR-C-1, BR-C-2,
BR-C-4, BR-C-5, BR-C-21; `design.md` § 4.1, § 6, § 8, § 9.

```gherkin
Scenario: A backlog title opens the card over the backlog
  Given the «Истории» backlog of «Платформа»
  When the user clicks the title «Письмо со ссылкой»
  Then a dialog named «Пользовательская история MSK-US-2» opens over the
    backlog, which stays drawn behind it
  And the address is the backlog's with `?workitem=MSK-US-2`
  And focus is inside the dialog on its frame, in no field
  And the title is a link whose address is MSK-US-2's full page

Scenario: A kanban card's title does the same
  Given the «Истории» board of «Платформа»
  When the user clicks the title «Письмо со ссылкой» on its card
  Then the same dialog opens over the board, with the board's address and
    `?workitem=MSK-US-2`

Scenario: A modified click follows the link
  When the user clicks the title with Ctrl, Cmd, Shift or Alt held, or with the
    middle button
  Then no dialog opens and the browser follows the link to the full page

Scenario: Esc, «Закрыть» and Back close it
  Given the dialog of MSK-US-2 is open and nothing is changed
  When the user presses Esc
  Then the dialog is gone, the address has no `?workitem`, and focus is on the
    title «Письмо со ссылкой» that opened it
  When the user opens it again and activates «Закрыть»
  Then it closes the same way
  When the user opens it again and goes Back in the browser
  Then it closes and the backlog is as it was

Scenario: The address opens the dialog
  When the user loads the backlog's address with `?workitem=MSK-US-2`
  Then the backlog is drawn and the dialog of MSK-US-2 is open over it
  When the user closes it
  Then the address is the backlog's without the parameter, and Back leaves the
    backlog (no history entry was added by the load)

Scenario: Another team or level drops the dialog
  Given the dialog of MSK-US-2 is open
  When the address changes to another level or team of the same page
  Then the dialog is closed and the new page has no `?workitem`

Scenario: A link to a related item switches the dialog
  Given the dialog of MSK-US-2 is open
  When the user activates «Открыть MSK-FEAT-6» in «Связанная работа»
  Then the dialog now shows MSK-FEAT-6, its address has `?workitem=MSK-FEAT-6`
    and Back shows MSK-US-2 again
  And on the full page the same link goes to MSK-FEAT-6's page

Scenario: An item that is not there
  When the user loads the backlog's address with `?workitem=MSK-US-999`
  Then the dialog is named «Рабочий элемент MSK-US-999» and shows the frame
    «Рабочий элемент не найден» with the hint «Проверьте адрес или закройте
    окно.» and the action «Закрыть»
  And «Закрыть» closes the dialog and drops the parameter

Scenario: The page behind shows what the card changed
  Given the dialog of MSK-US-2 on the «Истории» backlog
  When the user changes «Заголовок» to «Письмо со ссылкой для входа» and saves
  Then the dialog stays open with «Изменения сохранены.»
  And once the dialog is closed the backlog row reads «Письмо со ссылкой для
    входа»

Scenario: On a narrow window the dialog is the whole window
  Given a window narrower than 1 024 px
  When the user opens the dialog
  Then it fills the window, with no margin and square corners
```
