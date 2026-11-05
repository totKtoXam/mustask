# US-2: A user reads the card's header, band and tab as the reference's

- story: US-2
- depends-on: US-1
- shippable: no — the header and the band frame the body of US-3
- status: pending

As a **signed-in user**, I want **the card's top to be laid out as the
reference's — the type's colour bar, the type and number, the number and the
title on one line, the assignee and the actions, the grey band of state and
area, the tab**, so that **I know at a glance what I am looking at and where
each control is, as in the tracker I know**.

## Acceptance criteria

Data: US-1's. Spec BR-C-7, BR-C-12, BR-C-13, BR-C-18, BR-C-22; `design.md`
§ 2 (measures), § 4.1..§ 4.4, § 4.8 (what is not drawn), § 8, § 9. The
dialog is open on MSK-US-2 in a window of 1 536 × 790 CSS px at a pixel ratio
of 1.25, the navigation expanded.

```gherkin
Scenario: The dialog stands as the reference's
  Then the dialog is 90 % of the window wide and 90 % high, centred
    (about 1 382 × 711 px), with the theme's corner radius
  And the header and the band stay in place while the body scrolls under them
  And the page behind is not dimmed: the backdrop is transparent, the dialog's
    shadow alone sets it apart, and a click outside it still closes it (US-6)

Scenario: The type's colour bar and the type line
  Then the header has, at its left edge, a bar about 10 px wide in the type's
    colour, as high as the header
  And the first line holds the story's icon in its colour and «Пользовательская
    история MSK-US-2» in capitals, 12 px, in the link blue, with «Развернуть на
    страницу» and «Закрыть» as two thin icon buttons side by side at the right
    of the same line, the ✕ about 5 px from the dialog's edge
  And on the full page that line is the page's `h1`

Scenario: The number and the title stand on one line
  Then the second line reads «MSK-US-2» and, after a 16 px gap, the editable
    title «Письмо со ссылкой» in 17 px, one line, with no frame
  And the title's accessible name is «Заголовок»

Scenario: The action row
  Then the third line holds, at the left, a 24 px initials avatar with «Анна
    Смирнова» (an unassigned item shows a thin person glyph and «Не назначен»
    in the secondary colour) and, at the right, «Сохранить» with its arrow,
    «Обновить», «Отменить все изменения» and «Ещё действия», each 32 px high
    and about 11 px apart, in thin glyphs
  And «Сохранить» and «Отменить все изменения» are disabled while nothing is
    changed, the Save group then in the reference's pale grey (`#F0F0F0` with
    a `#959595` label), not MUI's darker disabled grey
  And «Сохранить» is the only filled button of the card

Scenario: The grey band
  Then below the header a band on the application background holds «Статус»
    with its category dot and «Новая», and «Область» with «Платформа», each
    label at the left of its value, the value's text 88 px from the label's
    left edge in both blocks
  And the dot of «Статус» stands in the label column, 19 px before «Новая»,
    and «Область»'s label is about 303 px from the dialog's left edge, as
    the reference's «Area» is 302
  And at the band's right end «Изменён: 05.10.2026 14:30» stands above the
    tab strip, and the band is about 76 px high
  And nothing says who changed the item: «Обновил <имя>» is feat-015's to add
    (D-4)

Scenario: One tab
  Then the tab strip holds one tab, «Детали», selected, boxed with the thin
    top and side lines and the 2 px accent line under its label, 4 px above
    the band's lower edge, in a tab list named «Разделы рабочего элемента»
  And the strip stands against the band's lower edge, 36 px high, 8 px from the
    right

Scenario: Nothing of the places kept is drawn
  Then the card shows no comments count, no tags, no «+» for tags, no «Следить»
    and no settings button, no «Причина», no «Итерация», no tab «История»,
    «Связи» or «Вложения», no «Обсуждение», no «Развёртывание», no
    «Разработка», and no disabled stand-in for any of them
  And no resize grip at the dialog's corner and no underlined access-key
    letters in the labels: this task has neither resizing nor access keys

Scenario: The state's dot keeps its meaning with the colour off
  Then «Статус» shows the state's name beside the dot, so the colour is not
    the only carrier

Scenario: An item with no assignee and an unknown assignee
  Given MSK-BUG-4 has no assignee
  Then its action row reads «Не назначен»
  Given an assignee the directory does not know
  Then the row reads «Неизвестный пользователь»

Scenario: A bug's bar and icon are the bug's
  When the user opens MSK-BUG-4
  Then the bar and the icon are in the bug's colour, the line reads «Баг
    MSK-BUG-4» (drawn in capitals), and the rest of the top is as above
```
