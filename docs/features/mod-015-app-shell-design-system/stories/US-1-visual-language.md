# US-1: The panel speaks one visual language

- story: US-1
- depends-on: -
- shippable: no — the tokens are fixed by the single gate-2 design together with the shell (D-1); shipped ahead of it the panel would be restyled twice
- status: pending

As **any user of the panel**, I want **one calm, dense, consistent look
modelled on the reference tracker**, so that **I can work a full day in the
panel and read status and type at a glance on every screen**.

Every scenario here reads the theme itself, in `npm test`. The same rules
measured on the restyled screens in a browser are in US-11 ("In a browser").

## Acceptance criteria

```gherkin
Scenario: Density tokens (BR-3)
  Given the theme in `app/theme`
  When a unit test reads its tokens
  Then the body text size is 14 px
  And the top bar's height is 48 px
  And the dense table row height is at most 40 px

Scenario: Colour roles (BR-2)
  Given the theme
  When a unit test reads its colour-role tokens
  Then it finds the application background, the navigation surface, the
    content surface, the neutral text colour and the accent
  And the navigation surface differs from the application background
  And the top bar's surface equals the content surface
  And the secondary button variants (text, outlined) use the neutral text
    colour for their label

Scenario: No colour literal outside the theme (BR-1)
  Given the files under `src/frontend/src/` except `app/theme/`, files named
    `*.test.*` and `src/test/`
  When the check that runs with `npm test` scans them
  Then no string literal is a hex colour (`#` and 3, 4, 6 or 8 hex digits)
    or begins with `rgb(`, `rgba(`, `hsl(` or `hsla(`
  And the check fails the run when such a literal is added to a slice
  And a string such as «#12» in copy, or `#` in an address, does not trip it

Scenario: Contrast (BR-5)
  Given every text/background pair, every semantic colour with its contrast
    text, and every type and category colour that the theme defines
  When a unit test computes their contrast ratios
  Then each text pair is at least 4.5:1
  And the focus indicator, input borders, selected markers and every type
    and category colour are at least 3:1 against the content surface

Scenario: Appearance tokens (BR-4, D-6, D-16)
  # Superseded in part by feat-032 (2026-10-12): the set of appearance tokens is six, `testcase` joins the five (see feat-032's api-contract § 4 and spec BR-C-11).
  Given the theme
  When a unit test reads its work-tracking tokens
  Then there is exactly one icon and one colour for each appearance token
    `epic`, `feature`, `story`, `task` and `bug`, and no two tokens share
    a colour or an icon
  And there is exactly one colour for each category code `Proposed`,
    `InProgress`, `Resolved`, `Completed` and `Removed`, and no two share one
  And no type or category colour equals the accent

Scenario: The lookups take a token name and a category code only (BR-4)
  Given a type whose key is `BUG` and whose stored token is `task`
  When its icon and colour are asked of the theme
  Then the theme answers the `task` icon and colour
  Given a state named «Готово» whose category code is `InProgress`
  When its colour is asked of the theme
  Then the theme answers the `InProgress` colour

Scenario: Unknown appearance token
  Given a work item type whose stored token name is not in the set
  When its icon and colour are asked of the theme
  Then the theme answers a neutral fallback icon and the neutral text colour
  And nothing fails

Scenario: Russian locale (NFR Localization)
  Given a table paging control rendered under the theme
  When it shows the first page of 25 rows, 10 per page
  Then it reads «1–10 из 25»
```
