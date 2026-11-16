# US-1: A member inserts an image into Описание by button, paste or drop

- story: US-1
- depends-on: US-2, US-3
- shippable: no — the editor's insert needs US-2's upload and US-3's save to store anything
- status: pending

As a **user who may edit a work item**, I want **to paste a screenshot, drop
an image file or choose one from the toolbar into Описание**, so that **the
picture sits in the description next to the words that explain it**.

Rules: spec BR-IM-1, -2, -3, -5, -13, -14, -15, -19 (the editor's host seam).

## Copy

| Where | Text |
|---|---|
| Toolbar button (feat-018's reserved slot) | «Изображение»; tooltip «Вставить изображение» |
| Placeholder while uploading | «Загрузка изображения…» |
| Image menu (on a selected image) | «Открыть в полном размере», «Удалить изображение» |
| Notice: wrong format | «Можно вставить только изображения PNG, JPEG, GIF или WebP.» |
| Notice: file too big | «Файл больше 10 МБ. Уменьшите изображение и вставьте снова.» |
| Notice: too many pixels | «Изображение слишком большое: не больше 60 мегапикселей и 20 000 пикселей по стороне.» |
| Notice: animation over the frame caps (BR-IM-3) | «Анимация слишком большая: не больше 1 000 кадров.» |
| Notice: over 50 images | «В описании может быть не больше 50 изображений.» |
| Notice: upload failed (network, server) | «Не удалось загрузить изображение. Попробуйте ещё раз.» |
| Notice: image from another site in pasted content | «Изображение с другого сайта не вставлено. Сохраните его как файл и вставьте снова.» |

Notices use feat-018's polite live region under the editor and its rules
(cleared on the next edit or blur). feat-018's «Изображения в описание не
вставляются.» is retired.

## Acceptance criteria

```gherkin
Scenario: A screenshot pasted from the clipboard
  Given a member who may edit the item has its card open with the cursor in Описание
  When they paste an image from the clipboard (a PNG screenshot)
  Then a placeholder «Загрузка изображения…» appears at the cursor as a block of its own
  And when the upload succeeds the placeholder becomes the image
  And its text alternative is «Изображение»
  And the description is marked as changed

Scenario: Several files chosen through the toolbar
  Given the cursor is in Описание
  When the member presses «Изображение» and chooses three JPEG files in the file chooser
  Then three images are inserted at the cursor in the order chosen, each as a block of its own
  And each one's text alternative is its file name without the extension

Scenario Outline: Where an insert lands when the cursor cannot hold an image
  Given the cursor is <place>
  When the member inserts an image
  Then the image is inserted right after <block>, as a block of its own
  Examples:
    | place                 | block           |
    | inside a heading      | the heading     |
    | in a table cell       | the table       |
    | inside a code block   | the code block  |

Scenario: An image file dropped on the editor
  When the member drops a PNG file on the editor
  Then it is inserted where it was dropped
  And the browser does not navigate to the file

Scenario Outline: A file is refused before upload
  When the member pastes, drops or chooses <file>
  Then nothing is inserted and nothing is uploaded
  And «<notice>» is announced under the editor
  Examples:
    | file                              | notice                                                                          |
    | a PDF                             | Можно вставить только изображения PNG, JPEG, GIF или WebP.                      |
    | an SVG                            | Можно вставить только изображения PNG, JPEG, GIF или WebP.                      |
    | a 10 485 761-byte JPEG            | Файл больше 10 МБ. Уменьшите изображение и вставьте снова.                      |
    | a 51st image into an Описание of 50 | В описании может быть не больше 50 изображений.                              |

Scenario: Of several dropped files only the valid ones go in
  When the member drops a PNG, a PDF and a WebP together
  Then the PNG and the WebP are inserted
  And the format notice is announced once

Scenario: The server refuses an upload
  Given the client checks pass but the server refuses the file (US-2: a renamed non-image, too many pixels)
  When the upload answers
  Then the placeholder is removed and the server's reason is announced
  And the rest of the description is unchanged

Scenario: The upload fails on the network
  When the upload does not answer or answers a server error
  Then the placeholder is removed and «Не удалось загрузить изображение. Попробуйте ещё раз.» is announced

Scenario: A save waits for running uploads
  Given an upload is still running
  When the member saves the card (or the card's save of Описание fires)
  Then the description is not sent until the upload ends
  And if the upload succeeded the sent description holds the image
  And if it failed the sent description holds no trace of it

Scenario: Rich content with images pasted from elsewhere
  Given clipboard html with text, an image with an https address on another site, and an image with a data address
  When the member pastes it
  Then the text is pasted as feat-018 US-1 says
  And the data-address image is uploaded as a file and inserted
  And the other site's image is dropped and «Изображение с другого сайта не вставлено. Сохраните его как файл и вставьте снова.» is announced
  And no request is made to the other site

Scenario: An image copied from another work item's description
  Given the member copied an image from item A's Описание
  When they paste it into item B's Описание
  Then the editor uploads a copy of its bytes as the member's upload for B
  And the stored description of B never references A's image

Scenario: A pasted image of the store the member cannot read
  Given the clipboard holds an image of an item the member may not read (or no longer exists)
  When they paste it into item B's Описание
  Then that image is left out and «Не удалось загрузить изображение. Попробуйте ещё раз.» is announced
  And the rest of the pasted content goes in

Scenario: An image moved inside the same description
  When the member cuts an image of this item's Описание and pastes it elsewhere in it
  Then the same image is kept, nothing is uploaded again

Scenario: Removing an image and undoing it
  Given an image is selected (by click, or by the arrow keys)
  When the member presses Delete, Backspace or «Удалить изображение»
  Then the image leaves the text
  And Ctrl/Cmd+Z restores it

Scenario: The hosting form supplies the upload and the image address (BR-IM-19)
  Given the editor is hosted by a form that supplies its own upload operation and its own reference-to-address rule
  When an image is inserted and shown
  Then the upload goes through the host's operation and the image loads from the host's address
  And the editor itself names no route of the work item's image read or upload

Scenario: No insert without the right to edit
  Given a reader with a read-only role, or an item in «Удалена»
  When the card shows Описание
  Then there is no «Изображение» button, and paste or drop inserts no image
```

## Verification

| What | How |
|---|---|
| Insert, placement, refusals, notices, save waits, pasted html, undo, the host seam | jsdom component tests with the real editor (stubbed clipboard and drop `files`, a stubbed upload) |
| Real clipboard screenshot (Windows, macOS), drag from the desktop, an image copied from another browser tab | manual QA in a browser |
