# QA — feat-070 search dialog kit

Spec and design are feat-065's (§ 1, D-9, D-13, D-14); no feature uses the parts yet, so the plan covers the kit parts only. Backend: none (no backend change).

## Frontend

### Coverage matrix

| Criterion (design) | Check | Test type | Status |
|---|---|---|---|
| § 1 ThinSearchIcon | thin magnifier exported, aria-hidden | component (`ThinIcons.test`) | done |
| § 1 HighlightedText | `<mark>` from text, never markup; ranges or query; bold plus tint | component (`HighlightedText.test`) | done |
| § 1 HighlightedText edges | merge/touch/overlap, clamp, unsorted, NaN, empty text, İ offsets, Latin case | component (`HighlightedText.edge.test`) | done |
| D-14 SearchField | combobox named «Поиск», placeholder, hint while empty, clear ✕ and refocus, maxLength, Tab to clear | component (`SearchField.test`) | done |
| SearchField edges | no hint chip, hint aria-hidden, aria-keyshortcuts/haspopup, value-not-event, composition/focus/blur forwarded, id/data attr | component (`SearchField.edge.test`) | done (D-1 found and fixed) |
| § 1 SearchOption | role=option link, not a Tab stop, active `↵` hint, stripe, `more` variant | component (`SearchOption.test`) | done |
| SearchOption edges | not selected by default, router navigation, no `to`, active more | component (`SearchOption.edge.test`) | done |
| D-9 SearchPanel | non-modal Popper, scope/body/keys slots, click-away (not anchor, not inside), busy, compact | component (`SearchPanel.test`) | done |
| SearchPanel edges | no anchor, no footer without keys, compact hides keys, Esc outside does not close (host owns it, D-13), no scroll lock/aria-hidden | component (`SearchPanel.edge.test`) | done |
| Theme tokens, exports | `layout.searchField|searchPanel|searchOption`, `palette.search.match`; kit guard | `kitGuard.test` | done |
| Showcase «Поиск», guide block | present | reading only; not rendered by QA | not checked in a browser |

### Additional scenarios

| # | Scenario | Expectation | Status |
|---|---|---|---|
| 1 | Host passes `onFocus`/`onBlur` to SearchField | called (D-13: panel opens on any focus) | was broken, fixed (D-1) |
| 2 | Markup in `text` | rendered as text | covered by developer test |
| 3 | Visual: 28 px / 300 to 360 px, underline | not asserted in jsdom; needs eyes on `/ui-kit` | open (gate-3 human check) |

### Runs

| Date | What was run | Result |
|---|---|---|
| 2026-11-16 | vitest: `shared/ui/dialog/Search*`, `fields/SearchField*`, `text/Highlighted*`, `icons` | first run 92/93, 1 failed (D-1) |
| 2026-11-16 | after fix: `fields/SearchField*`, `src/test/kitGuard` | 376 passed, 1 skipped |
| 2026-11-16 | `eslint src/shared/ui` | 0 errors |
| 2026-11-16 | `tsc -b` | no errors |

E2E: no (standing answer, paused). The whole suite and `verify.sh` are left to CI (economy mode).

### Defects

#### D-1: SearchField dropped the host's onFocus and onBlur

- Severity: major (D-13 needs the host to open the panel on focus)
- Steps: render `<SearchField onFocus={spy} …/>`, click the input.
- Expected / Actual: spy called / never called. MUI `InputBase` sets its own `onFocus`/`onBlur` on the input after spreading `slotProps.input`, overwriting the pass-through ones.
- Status: fixed (QA, trivial, in scope): `onFocus`/`onBlur` are now given to `InputBase` itself; the edge test pins it.

### Verdict

blockers: 0 · result: pass (minor debt: visual look unchecked in a browser)
