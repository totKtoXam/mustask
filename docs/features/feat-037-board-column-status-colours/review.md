# feat-037 — code review

## Round 1

Scope: `git diff 514c6af 33553de` — `src/frontend/src/features/boards/components/BoardColumns.tsx`,
`src/frontend/src/features/boards/pages/BoardPage.test.tsx`, the feature's `status.md`.
The new test was run alone (`-t "coloured by their status"`): green.

Verdict: **approve** — no blocker, no major. One minor.

### Findings

- **F1 — minor** — `BoardPage.test.tsx:55`. `box.querySelector(...) as HTMLElement` cast away a
  possible `null`; a missing dot would fail inside `getComputedStyle` with a `TypeError`
  instead of a message saying the dot is absent. **Resolved** in the fix pass: the dot is
  looked up without the cast and `expect(dot, …).not.toBeNull()` names the column.

### Checked, no finding

- Spec (D-1): a stripe on top and a dot in the header, both from
  `theme.workTracking.categoryColour(column.category.code)`; the category code is never
  rendered as text. No API change, no new token.
- MUI outlined border: `sx` follows the variant's border, so `borderTop` replaces only the top
  edge; the test pins 4 px and the category's colour and would catch a revert.
- Drop area: `dropAreaSx` sets only `outline`, `outlineOffset` and `backgroundColor`, so it
  does not reset the stripe; the dashed outline covers the stripe's outer half while a drag
  is on, as the plan expects.
- Unknown category code: `categoryColour` falls back to `palette.text.secondary`;
  `CategoryMarker` still draws a dot; nothing throws.
- Accessibility: the dot is `aria-hidden`; the `h2` header and the list's `aria-label` name
  the state, so colour is not the only carrier.
- UI kit: no new import, no MUI-internals selector, no colour literal, no hand-made heading;
  the kit's `CategoryMarker` is reused.
- Conventions: English identifiers and comments; no UI copy added; the reference tracker is
  not named.
- Prose: no document says board columns are uncoloured (mod-006 `spec.md:307` lists the
  header and count only, which stays true).
