# US-3: A user reads the body in three columns, by type, in fields without frames

- story: US-3
- depends-on: US-2
- shippable: no — the fields are edited and saved in US-4
- status: pending

As a **signed-in user**, I want **the card's body in three columns — the texts,
the planning and classification, the related work — with each field's label
above its value and no frame until I point at it**, so that **I read an item
like a page, and edit it like a form, in the layout I know**.

## Acceptance criteria

Data: US-1's, and MSK-EPIC-1, MSK-BUG-4 (Серьёзность «3 — Средняя», «Шаги
воспроизведения» «Открыть письмо через 20 минут», no «Сведения о системе») and
MSK-TASK-3 (Приоритет 3, Вид работ «Разработка», Исходная оценка 8). Spec
BR-C-14, BR-C-15, BR-C-18, BR-C-20; feat-022's field table; `design.md` § 4.5..
§ 4.7, § 8, § 9. The window is 1 536 × 790 unless a scenario says otherwise.

```gherkin
Scenario Outline: Each type has its sections in its columns
  When the user opens <item> in the dialog
  Then the first column holds <first>, the second <second>, the third
    «Связанная работа»
  And each section has its heading, a thin rule under it, and no frame round it

  Examples:
    | item        | first                                          | second                                                                                       |
    | MSK-US-2    | «Описание», «Критерии приёмки»                 | «Планирование» (Оценка в пунктах, Приоритет, Риск), «Классификация» (Вид ценности)            |
    | MSK-BUG-4   | «Описание», «Шаги воспроизведения», «Сведения о системе» | «Планирование» (Оценка в пунктах, Приоритет, Серьёзность, Вид работ), «Трудозатраты (часы)» |
    | MSK-TASK-3  | «Описание»                                     | «Планирование» (Приоритет, Вид работ), «Трудозатраты (часы)» (Исходная оценка, Оставшаяся работа, Выполненная работа) |
    | MSK-EPIC-1  | «Описание»                                     | «Планирование» (Приоритет, Риск, Трудоёмкость, Бизнес-ценность, Критичность по времени, Дата начала, Целевая дата), «Классификация» (Вид ценности) |

Scenario: The columns' widths
  Given the dialog is 1 382 px wide
  Then the body's content is about 1 343 px: the first column about 671 px,
    the second and the third 320 px each, 16 px between them
  And a section's heading is 16 px semibold with a rule under it, the next
    section standing about 32 px below the last field of the one above

Scenario: A field shows its label above its value, with no frame
  Then in «Планирование» each field is a 12 px label in the secondary colour
    and, under it, the value as 14 px text
  And «Приоритет» reads «2» under «Приоритет», «Вид ценности» reads «Бизнес»
  And no input box, no underline and no floating label is drawn

Scenario: An empty optional text, number or date is blank at rest
  Given MSK-US-2 has no «Оценка в пунктах»
  Then «Оценка в пунктах» shows its label and nothing under it
  When the pointer is over it or it has focus
  Then a frame is drawn and its placeholder «Не оценено» shows inside

Scenario: An empty optional list says «Не указан» at rest
  Given MSK-US-2 has no «Риск»
  Then «Риск» shows its label and, under it, «Не указан» as its value, with no frame
  And this is a recorded difference from the reference, which shows nothing there (KD-13)
  When the pointer is over it or it has focus
  Then a frame is drawn

Scenario: A frame on hover and on focus
  When the pointer is over «Оценка в пунктах»
  Then a 1 px frame is drawn round its value, and goes when the pointer leaves
  When the user tabs to it
  Then a 2 px accent frame is drawn, the same as the focus ring of any control
  And a field with an error keeps its frame in the error colour at rest

Scenario: The hint shows on focus without moving anything
  When «Приоритет» has focus
  Then «1 — самый высокий» shows on the label's own line, at its right end,
    covering nothing, and no field below it moves
  And the field's accessible description is «1 — самый высокий» also when it
    has no focus

Scenario: The texts are plain, grow, and stay readable
  Then «Описание» shows «Пользователь получает письмо со ссылкой для входа.»
    as 14 px text with its line breaks, in a block at least 2 lines high that
    grows with its text
  And no second «Описание» label is drawn under the heading: the heading
    names the text, the first line's cap top is about 30 px below the heading's
    rule and the text 16 px from the column's edge
  And, for a text of up to two lines, the next heading («Критерии приёмки» of
    a story) stands with its cap top about 107 px below the «Описание» rule: 9 px
    higher than the reference's 116, a recorded difference (KD-12)
  And an empty text shows «Добавьте описание» when the pointer is over it or
    it has focus, and its hint «До 100 000 символов» shows on focus, under the
    text

Scenario: «Связанная работа» is the third column
  Then it shows «Родитель» (MSK-FEAT-6 «Вход по ссылке», with its state and
    «Открыть MSK-FEAT-6»), and «Дочерние элементы (3)» listing MSK-BUG-5,
    MSK-TASK-3 and MSK-TASK-7, as feat-001 has them
  And nothing stands above it where the reference has «Deployment» and
    «Development»
  And there is no «Add link» menu: the picker of «Родитель» is the one link
    operation the card has now (D-3); the reference's block, with the menu and
    the groups «Родитель» and «Дочерние элементы (n)» over link rows, is
    feat-030-work-item-related-work

Scenario Outline: The columns fold by the card's own width
  Given the card's body is <width> px wide
  Then it stands in <columns>

  Examples:
    | width | columns |
    | 1 343 | three columns: texts, sections, related work |
    | 1 040 | three columns |
    | 1 039 | two columns: the texts, and a column of the sections with «Связанная работа» under them |
    | 680   | two columns |
    | 679   | one column: the texts, the sections, «Связанная работа» |

Scenario: One column keeps the controls from stretching
  Given the body stands in one column
  Then no field is wider than 480 px, and the texts take the column
```
