# US-4: Whoever may read the item sees its images; nobody else can

- story: US-4
- depends-on: US-3
- shippable: no — there is nothing to read until US-3 stores references, and stored references without it show broken images
- status: pending

As a **user who may read a work item**, I want **to see the pictures in its
Описание and open one full size**, so that **I read a screenshot as clearly as
its author saw it**; and as **the item's owner organization**, we want **an
image unreadable by anyone who may not read the item**, so that **a picture
never leaks past the item's access**.

Rules: spec BR-IM-10, -11, -12.

## Copy

| Where | Text |
|---|---|
| Image that cannot be shown | «Изображение недоступно» |
| Image menu item | «Открыть в полном размере» |
| Full-size view | title «Просмотр изображения», button «Закрыть» |

## Acceptance criteria

```gherkin
Scenario: A reader sees the images
  Given item X's Описание holds a 600 px wide image and a 3 000 px wide one
  And a member with a read-only role opens X's card
  Then both images show where they stand in the text
  And the 600 px one at its natural width, the 3 000 px one scaled down to the editor's width, proportions kept
  And the card shows no editing control for them

Scenario: Text shows before images arrive
  Given X's Описание holds text and an image whose read is slow
  When the card opens
  Then the text shows at once and the image fills in when it arrives

Scenario: Full size inside the panel
  Given an image is shown in Описание
  When the reader chooses «Открыть в полном размере» (by mouse, or by keyboard on the selected image)
  Then «Просмотр изображения» shows it at its natural size, scrollable when larger than the window
  And «Закрыть» or Esc returns focus to the image, and the card stays open

Scenario: An image that cannot be shown
  Given one of three images cannot be read (removed, or the read fails)
  When the card opens
  Then that image shows «Изображение недоступно» and the other two show

Scenario Outline: Who reads an image
  Given image M bound to item X
  When <caller> reads M
  Then <result>
  Examples:
    | caller                                                          | result                         |
    | a user who may read X                                           | the image                      |
    | a user who may read X, X in «Удалена»                           | as X's own read answers        |
    | a user who may not read X, holding M's address                  | not found                      |
    | a user whose right to read X was revoked a moment ago           | not found                      |
    | no token                                                        | 401                            |

Scenario: An unbound upload is its uploader's only
  Given member A uploaded image U for X and has not saved
  When A reads U, then member B who may read and edit X reads U
  Then A gets the image and B gets not found

Scenario: The answers tell nothing
  When a user reads an image of an item they may not read, a swept upload, and a made-up reference
  Then the three answers are equal in status and body

Scenario: No shared cache keeps an image
  When a reader reads an image
  Then the answer declares the stored format's type, forbids content sniffing and is not cacheable by a shared cache

Scenario: The address names nothing
  When a reader inspects an image's address in the page
  Then it holds no organization, project or item id or key and no user

Scenario: Accessibility
  Given an image with the text alternative «Скриншот ошибки»
  When a screen reader reaches it
  Then it is announced as an image «Скриншот ошибки»
  And it can be selected and its menu opened by keyboard alone
```

## Verification

| What | How |
|---|---|
| Who reads, equal answers, the address, the answer's type and cache headers | integration (API) tests |
| Display rules, placeholder, full-size view, keyboard | jsdom component tests (stubbed image reads) |
| Scaling, scroll in the full view, light and dark | manual QA in a browser |
