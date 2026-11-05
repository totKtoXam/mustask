# US-4: A user edits fields in place and saves only what they touched

- story: US-4
- depends-on: US-3
- shippable: no — the dialog of US-1 and the page of US-7 are where it is used
- status: pending

As a **signed-in user**, I want **to change a field right where it stands and
save, with only the fields I touched sent**, so that **someone else's change to
another field, made while my card was open, is not overwritten with an old
value**.

## Acceptance criteria

Data: US-1's, and MSK-EPIC-1 with «Дата начала» 01.10.2026. Spec BR-C-8,
BR-C-15..BR-C-17, BR-C-20; mod-003 BR-M3-25; feat-022 BR-M3-30..-38; feat-001
BR-H-4; `design.md` § 4.2, § 4.7, § 6, § 8, § 9. The card is the dialog of
MSK-US-2 unless a scenario says otherwise; every scenario holds on the page.

```gherkin
Scenario: Nothing changed, nothing to save
  Then «Сохранить» and «Отменить все изменения» are disabled
  When the user changes «Приоритет» to «1»
  Then both are enabled
  When the user changes it back to «2»
  Then both are disabled again, the value «2» being no change

Scenario: Only the touched fields are sent
  Given another user changed MSK-US-2's «Область» to «Платформа / API» after
    the card was read
  When the user changes «Заголовок» to «Письмо со ссылкой для входа» and
    «Приоритет» to «1», and saves
  Then the request carries «Заголовок» and «Приоритет» and no other field
  And «Область» then shows «Платформа / API», the stored value, and not the
    value the card was read with

Scenario: A save shows the stored values
  When the save is accepted
  Then the card shows what the server answered, «Сохранить» and «Отменить все
    изменения» are disabled, «Изменён: <дата>» shows the new time, and the
    notice «Изменения сохранены.» shows for 6 seconds at the bottom left
  And the dialog stays open

Scenario: «Сохранить и закрыть»
  Given «Заголовок» is changed
  When the user opens «Ещё варианты сохранения» and chooses «Сохранить и
    закрыть»
  Then the card saves, and once it is accepted the dialog closes
  And on the full page the same choice saves and then goes back in history,
    or to «Рабочие элементы» when the page was the first the user opened

Scenario: The browser's checks come first
  Given «Заголовок» is emptied
  When the user saves
  Then nothing is sent, «Введите заголовок.» shows under the title with the
    title framed in the error colour, and focus is on the title
  And with «Оценка в пунктах» typed as «пять» and the title fixed, saving
    shows «Введите число.» under that field and moves focus there

Scenario: A refusal for a field lands on it
  Given MSK-EPIC-1 with «Дата начала» 01.10.2026
  When the user sets «Целевая дата» to 30.09.2026 and saves
  Then the server refuses, «Целевая дата не может быть раньше даты начала.»
    shows under «Целевая дата», its frame is the error colour, focus is on it,
    and every other edit is still there
  And editing the field clears its error

Scenario: A refusal for the card as a whole
  When a save is refused with an answer no field owns
  Then an error alert with the server's text shows between the band and the
    body, the edits stay, and the next save clears it

Scenario: The parent changed meanwhile
  Given another user gave MSK-US-2 another parent after the card was read
  When the user changes «Описание» and «Родитель» and saves
  Then the server refuses with `workitems.link.parent_changed`
  And the card reads the item again: «Родитель» shows the stored parent with
    «Родитель уже изменён другим участником. Данные обновлены — проверьте и
    повторите.» under it, «Описание» keeps the user's text, and the other
    fields show the stored values

Scenario: While it saves
  When the user saves
  Then «Сохранить» shows a progress indicator in place of its icon and does
    nothing when pressed, yet keeps the focus (it is inert, not removed from
    the Tab order), every field is a value with no frame and no editing,
    «Обновить» and «Отменить все изменения» do nothing, and the card is marked
    busy
  And Esc and a click outside do not close the dialog, and «Закрыть» does
    nothing

Scenario: The assignee and the state are fields like the rest
  When the user chooses «Активна» in «Статус»
  Then the band shows the state's dot and name, and «Сохранить» is enabled
  When the user types «Ан» in «Исполнитель» and picks «Анна Смирнова»
  Then the card shows her name, and picking «Не назначен» sends
    `assignee: { userId: null }`

Scenario: Enter in the title saves
  Given «Заголовок» is changed and has focus
  When the user presses Enter
  Then the card saves, as «Сохранить» does

Scenario: A save of an item that has gone
  When the server answers that the item, its project or its organization is not
    found
  Then the card is replaced by the frame of that level, and in the dialog its
    action is «Закрыть»
```
