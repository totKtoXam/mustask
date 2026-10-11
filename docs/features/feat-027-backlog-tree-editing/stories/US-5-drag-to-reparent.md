# US-5: A user drops a row onto another to re-parent it

- story: US-5
- depends-on: US-2
- shippable: no — the drop needs US-6's keyboard path beside it (mod-006 BR-M4-8)
- status: pending

As a **signed-in user**, I want **to drag a row onto the item it belongs
under**, so that **I restructure the hierarchy right in the backlog, as I do
in the reference tracker**.

## Acceptance criteria

Data: US-1's. Spec BR-T-11..-13, BR-T-15; `design.md` § 4.9 (the grip and
the dragging), § 4.6 (the cues), the spec's § Notices. A pointer drag, not on
a touch screen (feat-025). D-7 (2026-10-09): not from a handle column; D-11
(the human on the stand, 2026-10-11): the whole row drags, the grip is a hint.

```gherkin
Scenario: The grip is a dotted line at the row's edge, not a column
  Then no column holds a handle, and no header names one
  When the pointer is over a row or the row is selected
  Then a dotted vertical line of four dots shows at the left edge of its
    hierarchy cell, the cursor over it a grab hand
  And it is only the hint that the row can be taken, not a drag source of its
    own, not a tab stop, with no accessible name: the keyboard path is
    «Ещё действия» (US-6)

Scenario: The whole row is the drag source (D-11)
  Given no write is being saved
  Then every row, top-level and nested, is draggable
  When the user presses on the row's «Заголовок», «Тип» or «Статус» cell, a
    value, its title (a link that does not drag its address instead), its
    grip, or a button in it (the «+», the chevron, «Ещё действия») and drags
  Then the row is dragged, and a plain click on any of those still does only
    what it did
  And the inline add row and its field are never draggable

Scenario: A story dropped onto a feature becomes its child
  Given the «Эпики» backlog with MSK-EPIC-1 expanded
  When the user drags MSK-US-22, by its «Заголовок» cell, onto the MSK-FEAT-8 row
  Then the dragged row is selected and dimmed, and MSK-FEAT-8 is framed
    in the accent colour as an allowed target while the pointer is over it
  When the user releases
  Then the parent write names MSK-FEAT-8 as the new parent and MSK-EPIC-1
    as the parent seen
  And after the backlog is read again MSK-FEAT-8 is expanded with MSK-US-22
    beneath it, MSK-US-22 is gone from beneath MSK-EPIC-1, and «У MSK-US-22
    теперь родитель MSK-FEAT-8.» is shown

Scenario: A drop changes no place and no other field
  When MSK-US-22 is dropped onto MSK-FEAT-8
  Then its place on «Истории», its «Статус», «Исполнитель», «Область» and
    planning values are unchanged

Scenario: A bug dropped onto a story leaves the top level
  Given the «Истории» backlog: MSK-US-2 «1», MSK-BUG-4 «2», MSK-US-22 «3»
  When the user drops MSK-BUG-4 onto MSK-US-22
  Then after the read MSK-US-2 reads «1» and MSK-US-22 «2»
  And MSK-BUG-4 shows only beneath an expanded MSK-US-22, with no
    «Порядок»

Scenario: A nested row can be dragged onto another row
  Given the «Эпики» backlog with MSK-EPIC-1 and MSK-FEAT-6 expanded
  When the user drops MSK-BUG-4, nested under MSK-FEAT-6, onto MSK-EPIC-20
  Then MSK-BUG-4 stands beneath MSK-EPIC-20 and no longer beneath MSK-FEAT-6

Scenario Outline: A refused target says why and sends nothing
  Given the «Эпики» backlog with every row expanded
  When the user drags <item> over <target>
  Then <target> is framed with a dashed red line as refused
  When the user releases
  Then no request is sent and the error notice reads <text>

  Examples:
    | item        | target      | text |
    | MSK-FEAT-6  | MSK-US-22   | «Родителем может быть только рабочий элемент того же или более высокого уровня.» |
    | MSK-EPIC-20 | MSK-FEAT-6  | «Родителем может быть только рабочий элемент того же или более высокого уровня.» |
    | MSK-US-22   | MSK-TASK-3  | «Родителем может быть только рабочий элемент того же или более высокого уровня.» |
    | MSK-FEAT-8  | MSK-FEAT-23 | «Нельзя выбрать родителем MSK-FEAT-23: он вложен в MSK-FEAT-8 (MSK-FEAT-8 → MSK-FEAT-23).» |

Scenario: The item itself and its current parent are no targets
  When the user drags MSK-FEAT-6 over its own row, then over MSK-EPIC-1
  Then neither is framed, and releasing there sends nothing and shows
    nothing

Scenario: A descendant on another team is refused by the server
  Given MSK-FEAT-8 is the parent of MSK-FEAT-21 of «Мастаск», and MSK-FEAT-21
    of MSK-FEAT-26 «Отчёт о входах» of «Платформа»
  And the «Фичи» backlog of «Платформа», where MSK-FEAT-26 is a top-level
    row and MSK-FEAT-21 shows nowhere
  When the user drops MSK-FEAT-8 onto MSK-FEAT-26, which is outlined as
    allowed
  Then the error notice reads «Нельзя выбрать родителем MSK-FEAT-26: он
    вложен в MSK-FEAT-8 (MSK-FEAT-8 → MSK-FEAT-21 → MSK-FEAT-26).»
  And the backlog is read again, no link changed

Scenario: A parent changed meanwhile refuses the drop
  Given Борис's «Эпики» backlog shows MSK-US-22 beneath MSK-EPIC-1
  And Анна has since given MSK-US-22 the parent MSK-FEAT-6
  When Борис drops MSK-US-22 onto MSK-FEAT-8
  Then the backlog is read again and «Родитель уже изменён другим
    участником. Данные обновлены — проверьте и повторите.» is shown
  And MSK-US-22's parent is MSK-FEAT-6

Scenario: No answer
  Given the server does not answer a drop
  Then «Не удалось сохранить родителя. Попробуйте ещё раз.» is shown and
    the backlog is read again

Scenario: Between rows is still a reorder, for top-level rows only
  When the user drags MSK-EPIC-20 over the upper quarter of MSK-EPIC-1
  Then mod-006's drop line shows above MSK-EPIC-1's block, not a frame
  And releasing reorders MSK-EPIC-20 to «1», changing no link
  When the user drags the nested MSK-FEAT-6 between MSK-FEAT-8 and
    MSK-FEAT-23
  Then no drop line shows: a nested row has only row targets

Scenario: One write at a time
  Given a drop being saved
  Then every row is undraggable and every row «+», «Ещё действия» and both
    header buttons is off until the backlog is read again
  And Escape during a drag cancels it with nothing sent
```
