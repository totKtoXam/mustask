# US-3: A user adds a task to a card from its checklist

- story: US-3
- depends-on: US-1
- shippable: no — the button stands in US-1's task list
- status: pending

As a **signed-in user**, I want **to press «Добавить задачу» in a story card's
task list and type a title**, so that **I break a story down where I see it,
with the task already under the story**.

## Acceptance criteria

Data: US-1's; MSK-US-2's Область is «Платформа / API». Spec BR-C-8, BR-C-9, BR-C-12;
feat-027 BR-T-9, BR-T-10 (the inline row, the create with a parent) and its
US-4 are the model; the field's copy is theirs.

```gherkin
Scenario: The button is at the top of the task list
  Given the task list of MSK-US-2 is open
  Then «Добавить задачу» is the first line of the list, above MSK-TASK-3
  And its accessible name is «Добавить задачу (MSK-US-2)», its visible text «Добавить задачу»
  And it works by Tab to it, then Enter or Space

Scenario: Only a story offers it
  Then the task list of a story card has «Добавить задачу»
  And the lists of a card of a feature, an epic or a bug have none

Scenario: A task is added with a title
  When the user activates «Добавить задачу» and types «Проверить ссылку» in the
    field «Заголовок: Задача (MSK-US-2)», placeholder «Введите заголовок», and
    presses Enter
  Then the field is read-only with a progress indicator until the board is read again
  And «Рабочий элемент MSK-TASK-11 создан.» is shown
  And the task list ends with «Проверить ссылку» (not checked) and the counter
    reads «1/4»
  And focus is back on «Добавить задачу»

Scenario: The new task takes the parent's area and defaults
  Then MSK-TASK-11's form shows «Родитель» MSK-US-2, «Область» «Платформа / API»,
    «Статус» «Новая», «Исполнитель» «Не назначен»
  And MSK-US-2's «Изменён» time has moved

Scenario: A story with no task can still get one
  Given MSK-US-9 shows «0/0»
  When the user opens the counter and adds «Первая задача»
  Then the counter reads «0/1» and the list holds the task

Scenario: Escape or an empty title cancels
  Given the add field with «Проверить» typed
  When the user presses Escape
  Then the field is gone, nothing was sent and focus is on «Добавить задачу»
  When the user opens it again and presses Enter with nothing typed
  Then the field is gone and nothing was sent, with no error text

Scenario: A double Enter creates one task
  When the user presses Enter twice, or the request is sent again after a lost answer
  Then one task is created, with one identifier

Scenario: A refusal shows at the field
  Given MSK-US-2 was deleted after the board was read
  When the user adds a task to it
  Then the code's text shows under the field, marked invalid, the title is kept
    and nothing is created

Scenario: The title rule holds
  When the user types a title of 256 characters and presses Enter
  Then «Не более 255 символов.» shows under the field and nothing is sent

Scenario: Typing in the field does not drag the card
  Given the add field is open inside the card
  When the user selects its text with the pointer and types
  Then text is selected and typed and no card drag starts (spec BR-C-12)

Scenario: One write at a time
  Given a task being created
  Then every checkbox, «Ещё действия: …», «Добавить задачу» and card dragging is
    off until the board is read again

Scenario: An open field waits while another write is saved
  Given the add field is open with «Проверить» typed and a checkbox tick is being saved
  When the user presses Enter
  Then nothing is sent, the text is kept, and Enter works once the board is read again
```
