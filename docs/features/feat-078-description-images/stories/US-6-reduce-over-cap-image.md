# US-6: A file over 5 MB is reduced in the system, with the member's consent

- story: US-6
- depends-on: US-1, US-2
- shippable: no — the offer lives inside US-1's insert and stores through US-2's checks
- status: pending

As a **user who may edit a work item**, I want **an image over 5 MB reduced
by the system and shown to me before it goes in**, so that **a big phone photo
or a wide screenshot still gets into Описание, and I decide whether the smaller
picture is good enough**.

Rules: spec BR-IM-3 (b), BR-IM-20, D-7. The reduction runs in the system, not
in the browser: the editor sends the original file (up to 20 MB) and shows what
the system made of it.

## Copy

`{размер}` is in МБ with one decimal and a comma («7,4 МБ»); `{ш} × {в}` in pixels.

| Where | Text |
|---|---|
| Offer dialog, title | «Изображение больше 5 МБ» |
| Offer, text | «Размер файла — {размер}, а в описание можно вставить изображение не больше 5 МБ. Система может уменьшить его: файл станет легче, изображение может стать немного менее чётким.» |
| Offer, buttons | «Уменьшить» (default), «Отмена» |
| While reducing | «Уменьшаем изображение…» (the dialog stays, «Отмена» stays enabled) |
| Result, lines | «Было: {размер}, {ш} × {в}», «Стало: {размер}, {ш} × {в}» |
| Result, preview | the reduced image, scaled to fit the dialog; its text alternative «Уменьшенное изображение» |
| Result, buttons | «Вставить» (default), «Отмена» |
| Cannot reduce | «Не удалось уменьшить изображение до 5 МБ без сильной потери качества. Уменьшите его в другой программе и вставьте снова.»; button «Закрыть» |
| Reduction failed (network, server) | «Не удалось уменьшить изображение. Попробуйте ещё раз.»; buttons «Повторить», «Отмена» |

## Acceptance criteria

```gherkin
Scenario: A big photo is offered a reduction and inserted
  Given a member pastes a 7,4 MB JPEG of 8 160 × 6 120 into X's Описание
  Then nothing is inserted yet and «Изображение больше 5 МБ» opens with «Размер файла — 7,4 МБ…»
  When they choose «Уменьшить»
  Then «Уменьшаем изображение…» shows while the system reduces it
  And the result shows «Было: 7,4 МБ, 8160 × 6120», «Стало: <at most 5,0> МБ, <w> × <h>» and a preview
  When they choose «Вставить»
  Then the reduced image is inserted where the file was pasted, as an unbound upload of theirs for X

Scenario: Cancel at the offer
  When the member chooses «Отмена» in «Изображение больше 5 МБ»
  Then nothing is sent, nothing is inserted, and focus returns to the editor where it was

Scenario: Cancel after seeing the result
  Given the result is shown
  When the member chooses «Отмена» (or Esc, or a click outside)
  Then nothing is inserted
  And the reduced upload is left unbound and the sweep removes it (US-5)

Scenario: The result passes every check
  When the system reduces any file
  Then the stored result is at most 5 242 880 bytes, of the uploaded format, within BR-IM-3 (c)'s pixel caps
  And its metadata is removed (BR-IM-4 (1))
  And the original file is not stored

Scenario: A lossy photo loses quality before it loses size
  Given a 12 MB JPEG of 4 000 × 3 000
  When it is reduced
  Then the result keeps 4 000 × 3 000 if a quality with similarity at least 0.95 fits 5 MB
  And only otherwise is it scaled down, never below 2 000 × 1 500

Scenario: A screenshot stays lossless
  Given a 7 MB PNG screenshot of 5 120 × 2 880
  When it is reduced
  Then the result is a PNG, scaled down proportionally, never below 2 560 × 1 440
  And it decodes to exactly the pixels of the source scaled to its dimensions

Scenario: Reduction cannot reach 5 MB within the floors
  Given a 19 MB PNG of noise that is over 5 MB even at half its width and height
  When the member chooses «Уменьшить»
  Then «Не удалось уменьшить изображение до 5 МБ без сильной потери качества…» shows
  And nothing is stored and nothing is inserted

Scenario Outline: No offer
  When the member inserts <file>
  Then no offer opens, nothing is sent, and <notice> is announced under the editor
  Examples:
    | file                    | notice                                                                     |
    | a 6 MB animated GIF     | «Анимация больше 5 МБ. Уменьшите её в другой программе и вставьте снова.»   |
    | a 21 MB JPEG            | «Файл больше 20 МБ. Уменьшите изображение в другой программе и вставьте снова.» |

Scenario: The server holds the line
  When a client sends a 6 MB still image as an ordinary upload, not as a reduction
  Then it is refused for size and nothing is stored
  And a reduction request of 20 971 521 bytes is refused the same way
  And a reduction request of an animated image is refused

Scenario: Several over-cap files at once
  Given the member drops a 2 MB PNG, a 7 MB JPEG and an 8 MB JPEG
  Then the 2 MB PNG is uploaded and inserted at once
  And the 7 MB JPEG's offer opens, then the 8 MB JPEG's after the first is closed

Scenario: The reduction fails on the network
  Given the reduction does not answer or answers a server error
  Then «Не удалось уменьшить изображение. Попробуйте ещё раз.» shows with «Повторить» and «Отмена»
  And «Повторить» sends the file again

Scenario: The 50-image limit counts at the insert
  Given X's Описание holds 50 images
  When the member pastes a 7 MB JPEG
  Then no offer opens and «В описании может быть не больше 50 изображений.» is announced

Scenario: Keyboard and screen reader
  Then the offer and the result dialogs are reachable and operable by keyboard alone
  And the before and after lines are read out with the dialog
```

## Verification

| What | How |
|---|---|
| Reduction rules, floors, result checks, server refusals | integration (API) tests with checked-in fixtures; the similarity and pixel equality measured by the test |
| Offer, result, cancel paths, queue of offers, failure, keyboard | jsdom component tests with a stubbed reduction |
| Speed of a 20 MB reduction; how the preview looks | stand observation and manual QA in a browser |
