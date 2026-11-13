# tech-069-release-notes

- type: tech
- size: S
- branch: tech/069-release-notes
- stage: 05-backend-qa
- epic: platform/ci
- deps: tech-066
- gates: gate-3

## Gates

gate-3: pending

## Plan

1. `scripts/release-notes.sh <tag> [<previous-tag>]`: Markdown notes from git alone — the first-parent merge commits `Merge pull request #N from <owner>/<prefix>/<NNN>-<slug>` between the previous `v*` tag and `<tag>`, one line per PR (its title from the merge commit body, `(#N)`), grouped by prefix (features, fixes, tech, modules, docs); queue squash PRs (`sync/queue-*`) are left out by construction; no previous tag → "first release". Runs locally too, so a human previews the notes before tagging. Case tests in `scripts/tests/`.
2. `deploy.yml`: `run-name: Deploy <tag>`; a `notes` job (ubuntu-latest, `contents: write`, needs `build`) writes the notes to the job summary and creates the GitHub Release `<tag>` (`gh release create --verify-tag --notes-file`), or updates its notes on a `workflow_dispatch` redeploy; `deploy` needs it, so the approver reads the notes before approving. PR titles are data: never evaluated by the shell.
3. ADR (next free number): release notes are generated from merged PRs by the Deploy workflow; the PR title is the changelog line, so it must say what changed.
4. `docs/conventions/git.md` § Releases: the rule for humans and agents — preview with the script, then tag; the release page is the changelog. `docs/deploy/vm.md` § 11 (and `vm.ru.md`) mention the release page.

## Decisions

- 2026-11-13 D-1 (human, «⭐ GitHub Release из PR»): notes come from the merged PRs and are published as a GitHub Release by the Deploy workflow; no hand-written CHANGELOG.md.
- 2026-11-13 E2E: no (standing answer; no panel change).

## Log

- 2026-11-13 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/tech-069-release-notes
- 2026-11-13 [orchestrator] route confirmed by the human («⭐ Подтверждаю S»): tech, S (ADR + git.md + deploy.yml + one script with case tests, no runtime code), developer → QA (light) → code-reviewer → gate-3 → PR. Gates: gate-3 only. Session bound to this worktree (EnterWorktree); queue line taken via queue PR #172. Economy mode.
- 2026-11-13 [developer] done: scripts/release-notes.sh (ref may be any ref; a non-tag is "Unreleased"), scripts/tests/release-notes-cases.sh (36 cases green, wired into verify.sh), deploy.yml (run-name, notes job, deploy needs [build, notes]), cd-runner-cases.sh adjusted to the new job (all green), ADR-0043 accepted + index, git.md § Releases, vm.md/vm.ru.md § 11. Commits 80d2612d fd41b2da d191a7c4 9d8c6863 91094e19. verify.sh not run (CI is the gate). Real repo: release-notes.sh v0.4.1 v0.4.0 lists #157 and #169.

## Notes

Summary (from the queue line or an argument): Every release describes its changes: a v* tag makes the Deploy workflow publish a GitHub Release with notes generated from the merged PRs since the previous tag (grouped feat/fix/tech, queue sync PRs left out) and name its run after the tag; an ADR records the decision and docs/conventions/git.md § Releases gets the rule for humans and agents
