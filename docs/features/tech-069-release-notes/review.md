# Code review: tech-069-release-notes

## Round 1

Reviewed: `git diff origin/main...HEAD` at 8fdfc516 (12 files), against `status.md` Plan 1-4 and D-1. Ran `scripts/tests/release-notes-cases.sh` (41 passed) and `scripts/tests/cd-runner-cases.sh` (all passed); `release-notes.sh origin/main` on the real repo ("Unreleased", since v0.4.1, empty range — correct: only queue squashes since). Full `verify.sh` not run (CI is the gate).

**Verdict: approve.** No blocker, no major. Seven minors, all debts.

Plan conformance: items 1-4 and D-1 are implemented, nothing extra. Security of `deploy.yml`: `contents: write` only on `notes` (ubuntu-latest), workflow default stays `contents: read`; the token is in the env of the `gh` step only, not of the step that runs repository code; `persist-credentials: false`; the tag reaches every shell through `env` and was already validated by `build` (`^v[A-Za-z0-9._-]+$`, no `..`); `run-name` is an expression, not shell; titles reach GitHub only through `--notes-file` and the step summary, never a shell. `notes` needs `build`, so it inherits the main/`v*` ref guard; `deploy` needs `[build, notes]` and its environment approval is requested only once `notes` succeeded. Nothing new reaches the VM: `runs-on` set is still pinned exactly.

`cd-runner-cases.sh` changes: the `runs-on` check stays an exact string (one more `ubuntu-latest`, required by the new job); the `needs` check is tighter than before (exact line `needs: [build, notes]`); the inline-inputs exemption covers only a top-level `run-name:` line, which is not a shell context. No guard was weakened beyond what the change needs (but see m5).

QA minors: 1 (`Pull request #14 (#14)`), 2 (`vX vX` empty range), 3 (`@name` in a title mentions a user on the release page) and 4 (a redeploy overwrites hand-edited notes, stated in ADR-0043) — confirmed, all cosmetic or accepted. QA check 8 is disputed: see m1.

### Findings

- **m1 (minor)** `scripts/tests/release-notes-cases.sh:143,148,150` — the CRLF case passes because `git merge -m` cleans trailing whitespace (CR included) out of the message, not because of the script: `release-notes.sh:87` trims only space and tab. The case tests git, and QA check 8 reports a property the script does not have. Fix: in the awk, `sub(/\r$/, "")` on every line; build that fixture with `git commit-tree -F` (no cleanup) so the CR reaches the script.
- **m2 (minor)** `scripts/release-notes.sh:66` — `--first-parent` is pinned by no case: the fixture has no PR merge off the first-parent line (a stacked PR merged into a feature branch), so dropping the flag fails nothing. Tags in the fixture are lightweight only; annotated tags (`refs/tags/X` + `X^` peeling) are untested, though they work by reading. Fix: add a merge into a side branch and one annotated tag to the fixture.
- **m3 (minor)** `docs/adr/0043-release-notes-from-merged-pull-requests.md:34,43` — the notes depend on GitHub's default merge commit message (subject `Merge pull request #N from <owner>/<branch>`, body = PR title). If the repository's merge-commit title setting becomes "Pull request title", or a merger edits the message, PRs silently drop out or get a wrong line. The Consequences name only squash merges. Fix: one Consequences bullet naming the setting (Settings → General → "Allow merge commits" → default message) and the effect.
- **m4 (minor)** `.github/workflows/deploy.yml:162-166`, `docs/deploy/vm.md:727` (`vm.ru.md:729`) — "a redeploy only rewrites the release's notes" holds only when the release exists. A dispatch redeploy (rollback) of a tag that has no release — every tag before this change — creates one, and `gh release create` without `--latest=false` makes the older tag the repository's "Latest" release over a newer one. Fix: pass `--latest=false` when `EVENT` is `workflow_dispatch` (or always, letting the tag push mark latest explicitly), and word vm.md/vm.ru.md "creates it if it is missing".
- **m5 (minor)** `scripts/tests/cd-runner-cases.sh:333-334` vs `.github/workflows/deploy.yml:22` — the comment "Its token may write contents; no other job's may" is true today (anchored at `deploy.yml:136-137,177-178`, `build` has no override) but nothing enforces it: the guard pins only the workflow-level block. A later `contents: write` on `deploy` (the VM job) would pass CI. Fix: assert every job-level `write` permission line falls inside the `notes` job.
- **m6 (minor)** `scripts/release-notes.sh:66,85` — the record marker `@@@` is plain text: any body line starting with `@@@` (a PR description, if the merge message ever carries it) starts a new record and can forge an entry. Fix: use a control character, `--format='%x1e%s%n%b'` and `/^\036/`.
- **m7 (minor)** `docs/features/tech-069-release-notes/status.md:36` — the QA line was appended under `## Notes`, not `## Log`. Fix: move it.

### Behavioural claims (item 6)

- `release-notes.sh:15-20` (first-parent merges, sync skipped, queue squashes drop out) → `release-notes.sh:66,69,73`; cases `release-notes-cases.sh:100-101`.
- `release-notes.sh:10-13` (default previous tag; tag → from its parent; none → first release) → `release-notes.sh:37-57`; cases `:86,120,131`.
- `release-notes.sh:7-9` (non-tag ref → "Unreleased") → `release-notes.sh:37-43`; case `:130`.
- `release-notes.sh:22` "titles are data, never evaluated" → awk prints with `%s` (`:95`), workflow uses `--notes-file`; cases `:99,146`.
- `deploy.yml:18-22` (notes before deploy, approver reads first; write scope only on notes) → `deploy.yml:131-137,170`; enforcement gap m5.
- ADR-0043 Decision 1-4 → `release-notes.sh`, `deploy.yml:2,131-170`, `git.md:437-448`. Decision 1 "the title is ... the pull request title" → depends on a repo setting, m3.
- ADR-0043 Consequences "notes job can fail ... deploy does not start" → `release-notes.sh:34-35` (exit 1), `--verify-tag` `deploy.yml:165`, `needs` `deploy.yml:170`.
- `git.md:444-446` preview command, `sync/queue-*` excluded → `release-notes.sh:73`, squash commits not `--merges`.
- `vm.md:600` (notes job on ubuntu-latest) → `deploy.yml:134`; `vm.md:725-730` → `deploy.yml:2,151-166`; "redeploy only rewrites" → m4. `vm.ru.md` matches `vm.md` line for line.
- Direction 2: changed behaviour — deploy.yml's job set and the deploy job's `needs`, the run name. Grepped `deploy.yml`, `needs: build`, `two jobs`, `contents:` under `docs/`, `deploy/vm/`, `.claude/`, `AGENTS.md`. Hits: `vm.md` § 11.4 (`:755-786`: "the VM runs only our deploy job", "the VM job runs no repository code", guard by `cd-runner-cases.sh`) — still true, notes runs on ubuntu-latest; `deploy/vm/runner-job-guard.sh:13,41`, `runner-setup.sh:36`, `mustask-cd.sh:21` — about the workflow file and the deploy job, unaffected; tech-066 `review.md:26,58` — history, not swept. None stale.
- Reference tracker: not named anywhere in the diff.
