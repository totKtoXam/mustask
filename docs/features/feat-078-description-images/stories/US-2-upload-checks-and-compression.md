# US-2: The server accepts only real images within the caps and compresses large ones

- story: US-2
- depends-on: -
- shippable: no — an upload nothing can save or show fills the database for nothing (US-1, US-3, US-4)
- status: pending

As a **user who may edit a work item**, I want **my image accepted when it
is a real picture within the caps, and stored small without looking worse**,
so that **my screenshots stay readable and the product does not keep
oversized or dangerous files**.

Rules: spec BR-IM-2, -3, -4, -5, -6, -10. The server decides, whatever the
client sent; the client's checks in US-1 are a convenience.

## Acceptance criteria

```gherkin
Scenario: A valid upload
  Given a user who may edit item X's Описание
  When they upload a 2 MB PNG for X
  Then it is accepted and stored as an unbound upload of that user for X
  And the answer holds an opaque reference and nothing else about X, its project or its organization (BR-IM-10)

Scenario Outline: The format is read from the content
  When a user uploads <file>
  Then the answer is <result>
  Examples:
    | file                                               | result                     |
    | a PNG named photo.jpg, declared image/jpeg         | accepted, stored as PNG    |
    | a JPEG, a GIF, a WebP                              | accepted, format kept      |
    | an SVG named a.png                                 | refused: format            |
    | an HTML page named a.png                           | refused: format            |
    | a HEIC, an AVIF, a BMP, a TIFF                     | refused: format            |
    | a PNG truncated in the middle                      | refused: format            |

Scenario Outline: The caps
  When a user uploads <file>
  Then the answer is <result>
  Examples:
    | file                                                  | result                    |
    | 5 242 880 bytes                                       | accepted                  |
    | 5 242 881 bytes, sent as an ordinary upload           | refused: size (the reduction is US-6's) |
    | a PNG declaring 20 001 × 100 pixels                   | refused: dimensions       |
    | a PNG declaring 20 000 × 3 000 (60 000 000 pixels)    | accepted                  |
    | a PNG declaring 8 000 × 8 000 (64 000 000 pixels)     | refused: dimensions       |
    | a 40 KB PNG declaring 20 000 × 20 000 (a bomb)        | refused: dimensions, not decoded |
    | a GIF of 1 001 frames of 10 × 10                      | refused: dimensions, not decoded |
    | a GIF of 100 frames of 3 000 × 3 000 (900 000 000 pixels in all) | refused: dimensions, not decoded |

Scenario Outline: Metadata is removed and orientation kept
  Given a <size> JPEG with camera data, a location, a comment and an orientation flag "rotated 90°"
  When it is uploaded
  Then the stored image holds no camera data, no location and no comment
  And a browser shows it the same way as the source (<orientation>)
  Examples:
    | size   | orientation                                  |
    | 600 KB | pixels as uploaded, the orientation flag kept |
    | 3 MB   | re-encoded with rotated pixels, no flag       |

Scenario: Colours are kept
  Given a 3 MB JPEG photo with an embedded Display P3 colour profile
  When it is uploaded
  Then the stored image keeps the profile, or its pixels are converted to sRGB
  And rendered to sRGB it is at least 0.98 similar to the source rendered to sRGB

Scenario: A screenshot stays exact
  Given a 3 MB PNG screenshot of 2 560 × 1 440
  When it is uploaded
  Then the stored image decodes to exactly the same pixels at 2 560 × 1 440
  And it is not larger than the upload

Scenario: A large photo is compressed without visible loss
  Given a 4.8 MB JPEG photo of 4 032 × 3 024
  When it is uploaded
  Then the stored image is a JPEG of 4 032 × 3 024, smaller than 4.8 MB
  And its structural similarity to the source (BR-IM-4 (4): SSIM on luminance, 8×8 windows) is at least 0.98

Scenario: A small photo is left alone
  Given a 600 KB JPEG
  When it is uploaded
  Then its pixels are not re-encoded, only metadata is removed

Scenario: A very large picture is scaled down
  Given a 4.9 MB JPEG of 8 160 × 6 120 (49 939 200 pixels)
  When it is uploaded
  Then the stored image keeps the proportions and holds at most 16 777 216 pixels
  And its similarity to the source scaled to the same dimensions is at least 0.98

Scenario: A large PNG is never encoded lossily
  Given a 4.9 MB PNG of 3 000 × 2 000
  When it is uploaded
  Then the stored image is a PNG decoding to exactly the same pixels, at most 4.9 MB

Scenario: Compression never makes it bigger
  Given an already well-compressed 1.5 MB JPEG whose re-encoding would come out larger
  When it is uploaded
  Then the stored image is the source with only metadata removed

Scenario: An animated image keeps its animation
  Given an animated GIF of 40 frames
  When it is uploaded
  Then the stored image has 40 frames with the same timing, without metadata

Scenario Outline: Who may upload
  Given <caller> and item X
  When they upload an image for X
  Then the answer is the one an edit of X's Описание would get, and nothing is stored
  Examples:
    | caller                                              |
    | a member with a read-only role                      |
    | a member, X in «Удалена»                            |
    | a member, X's project deactivated                   |
    | a user who may not read X, or X does not exist      |

Scenario: No token
  When an upload is sent without a token
  Then it answers 401 and nothing is stored

Scenario: Hostile files
  Given the hostile corpus (spec § Non-functional requirements)
  When each is uploaded
  Then each is refused or stored with its metadata removed
  And none is decoded past the caps, and the process stays up
```

New refusal reasons (format, size, dimensions) get codes and Russian texts in
`docs/error-codes.md` and `contracts/errors/` at the backend stage; their
texts are US-1's notices.

## Verification

| What | How |
|---|---|
| Formats, caps, processing, similarity, who may upload | integration (API) tests with checked-in image fixtures; the similarity is measured by the test |
| Speed of a 5 MB upload | integration test with a budget, plus a stand observation |
| How a compressed photo looks | manual QA: the source and the stored image side by side |
