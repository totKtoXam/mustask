# US-3: A saved Описание holds images by reference, only the item's own

- story: US-3
- depends-on: US-2
- shippable: no — references need US-4's read to show and US-1's editor to be written
- status: pending

As a **user who may edit a work item**, I want **the images I put in
Описание saved with it, and nothing that points elsewhere**, so that **the
description shows the same pictures to everyone who may read the item, and
cannot be used to reach another item's images**.

Rules: spec BR-IM-6, -7, -8, -9, -10, amending feat-018 BR-RT-1, -3, -4, -6,
-8. The canonical cases below join feat-018's shared fixture file (BR-RT-5),
run by the server and the client suites.

## Canonical cases

| Input | Stored |
|---|---|
| an image element with a valid reference, a text alternative, a width, a style, a class and an onerror attribute | the image with the reference and the text alternative only |
| a text alternative of 300 code points | cut to the first 255 code points |
| an image with an https, http, data, relative or `//host` source | dropped, the text around kept |
| `<p>a<img>b</p>` | `<p>a</p>`, the image, `<p>b</p>` |
| `<p><img>b</p>` | the image, `<p>b</p>` (no empty paragraph) |
| a list item `<li><p>a<img>b</p></li>` | the list item holding `<p>a</p>`, the image, `<p>b</p>` |
| `<h3><img>Заголовок</h3>` | the heading, then the image |
| `<p><a href="https://e.org">x<img></a></p>` | the paragraph with the link "x", then the image without a link |
| an image in a list item | kept in the list item, as a block of its own |
| an image in a table cell or a code block | dropped |
| two images in a row | two images, nothing between them |

## Acceptance criteria

```gherkin
Scenario: The writer's own uploads are bound by the save
  Given a member uploaded two images for item X (unbound, theirs)
  When they save X's Описание referencing both
  Then the description is stored with both images
  And both become images of X (bound), readable by X's readers (US-4)

Scenario: Re-saving keeps images already bound
  Given X's stored Описание holds an image of X uploaded by another member
  When a third member edits the text and saves
  Then the save is accepted and the image stays

Scenario Outline: A reference that is not the item's own refuses the whole save
  Given a member edits X's Описание
  When they save it referencing <image>
  Then the save is refused with «Изображение больше недоступно. Удалите его и вставьте снова.»
  And nothing is stored, the earlier description stays
  Examples:
    | image                                                     |
    | an image bound to item Y                                  |
    | another member's unbound upload for X                     |
    | the member's own unbound upload made for item Y           |
    | the member's own upload for X already swept (US-5)        |
    | a made-up reference                                       |

Scenario: Over 50 images
  When a member saves X's Описание with 51 image elements
  Then the save is refused with «В описании может быть не больше 50 изображений.», nothing stored

Scenario: An image alone is a description
  When a member saves an Описание holding one image and no text
  Then it is stored as that image, not as the empty description ""
  And an Описание of empty paragraphs and no image is still stored as "" (BR-RT-6)

Scenario: Images add nothing to the visible count
  Given an Описание of 100 000 visible characters and 50 images with 255-character text alternatives
  When it is saved
  Then it is accepted: images and their text alternatives add no character and no line break
  And "a", an image, "b" counts as "a\nb", 3 code points

Scenario: The stored markup names nothing but the image
  When a description with images is stored and read back
  Then each image element holds an opaque reference with no organization, project or item id or key and no user

Scenario: A plain-format write cannot carry images
  When a client writes Описание with format plain holding "<img src=…>"
  Then it is stored as those characters, as feat-018 BR-RT-10 says, and binds nothing

Scenario: A save that does not touch Описание binds nothing
  Given the member uploaded an image and then removed it from the editor
  When they save the card
  Then the upload stays unbound and is left to the sweep (US-5)

Scenario: The canonical form is stable
  Given each canonical case above
  When it is saved, read, loaded in the editor and serialised without edits
  Then the result equals the stored value byte for byte, and the form is not dirty
```

The refusal reasons (an unavailable image, too many images) get codes and
texts in `docs/error-codes.md` and `contracts/errors/` at the backend stage.

## Verification

| What | How |
|---|---|
| Canonical cases, both sides | the shared fixture file, server and client suites |
| Binding, refusals, counts, non-disclosure | integration (API) tests |
| Round trip without a spurious change | jsdom component test with the real editor |
