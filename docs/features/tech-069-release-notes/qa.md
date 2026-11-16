# QA: tech-069-release-notes (light, S route)

Checked from the plan (items 1-4) and D-1, then the diff `origin/main...HEAD`. E2E: no (standing answer, no panel change). `verify.sh` not run (economy mode, CI is the gate). actionlint is not installed here, so deploy.yml was read by hand and run through `cd-runner-cases.sh` only.

## Test list and results

| # | Check | Result |
|---|-------|--------|
| 1 | `release-notes-cases.sh` (36 developer cases) | pass |
| 2 | Real repo `v0.4.1 v0.4.0` lists #157 (Features) and #169 (Documentation) | pass |
| 3 | `v0.4.0`, default previous tag (v0.3.1): only a squash queue commit in range | pass ("No pull requests in this range.") |
| 4 | `origin/main` preview: heading "Unreleased", since v0.4.1 | pass |
| 5 | Unknown ref `v9.9` | pass (exit 1, message) |
| 6 | Title with `%s %n \n \` | pass, printed verbatim (new case) |
| 7 | Merge commit with an empty body | pass, falls back to "Pull request #N (#N)" (new case) |
| 8 | CRLF in the title | pass, no `\r` in output (new case) |
| 9 | Branch without a prefix | pass, goes to "Other" (new case) |
| 10 | Markdown characters, shell metacharacters in titles | pass, printed as data |
| 11 | First tag in history | pass ("First release: the whole history.", covered by existing case) |
| 12 | `cd-runner-cases.sh` | pass |
| 13 | deploy.yml: `contents: write` only on `notes`; default `contents: read`; tag only via `env`; `deploy` needs `[build, notes]`; run-name; create vs edit for push and dispatch | pass (read) |
| 14 | ADR-0043, index entry, git.md § Releases, vm.md § 11 vs vm.ru.md, no reference tracker named | pass |

Cases script now: 41 passed, 0 failed (5 cases added).

## Defects

- Blocker: none.
- Major: none.
- Minor:
  1. An empty merge body yields the awkward line "Pull request #14 (#14)" (number twice). Cosmetic.
  2. `release-notes.sh vX vX` (previous tag equal to the ref) prints "Changes since vX" with an empty range. Harmless.
  3. Titles are not escaped for Markdown: a title with `@name` on the release page can mention a user. Titles come from our own PRs; accepted risk, noted.
  4. A redeploy overwrites hand-edited release notes. Already stated in ADR-0043 (Consequences).
