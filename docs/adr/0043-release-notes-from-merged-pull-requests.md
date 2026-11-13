# ADR-0043: Generate release notes from the merged pull requests and publish them as a GitHub Release

- Status: accepted (by the human in chat, decision D-1 of tech-069, 2026-11-13)
- Date: 2026-11-13
- Context: `tech-069-release-notes`: [status](../features/tech-069-release-notes/status.md). It builds on tech-066 (the Deploy workflow, `docs/deploy/vm.md` section 11) and `docs/conventions/git.md` § "Releases and versions".

## Problem

A release tag `v*` deploys itself, but nothing records what the release carries. The Deploy runs are named after nothing, the tag is only a number, and the approver of the `production` environment has nothing to read before approving. Reconstructing a release from `git log` by hand is slow and gets skipped. The repository already has the raw material: every task lands as a pull request with a merge commit, and the queue's own pull requests are squash commits.

## Options considered

### Option 1: a hand-written CHANGELOG.md

- Pros: the text is curated; it lives in the repository.
- Cons: one more pull request per release, written by someone who is not the author of the changes; it drifts from what was merged; it conflicts when two branches touch it.

### Option 2: GitHub's own `gh release create --generate-notes`

- Pros: no code of ours.
- Cons: it cannot drop the queue's pull requests without labels on every one of them, and it cannot group by our branch prefixes (`feat/`, `bug/`, `tech/`, `doc/`, `mod/`); it needs the network to preview, so a human cannot see the notes before tagging.

### Option 3: only name the run after the tag (`run-name`)

- Pros: trivial.
- Cons: there is no room in a run name for the changes.

### Option 4: a script of ours over the merge commits, published by the Deploy workflow (chosen)

- Pros: pure git, so it runs the same on a laptop and in CI; it groups by branch prefix and leaves queue pull requests out by construction; no hand-written file, no extra pull request.
- Cons: the notes are only as good as the pull request titles.

## Decision

Option 4.

1. `scripts/release-notes.sh <ref> [<previous-tag>]` prints Markdown from the first-parent merge commits `Merge pull request #N from <owner>/<prefix>/<rest>` between the previous `v*` tag and the ref, one line `- <title> (#N)` per pull request, grouped by the branch prefix (Features, Fixes, Modules, Technical, Documentation, Other). The title is the first line of the merge commit body, which is the pull request title. Squash commits (the queue) are not merge commits and a `sync/` branch is skipped, so queue pull requests drop out. Titles are printed as data, never evaluated.
2. The Deploy workflow has a `notes` job, before `deploy`: it writes the notes to the run summary and creates the GitHub Release of the tag (`gh release create --verify-tag --notes-file`), or rewrites the notes of the existing release on a `workflow_dispatch` redeploy. The job alone holds `contents: write`; the workflow's default stays `contents: read`. The run is named `Deploy <tag>`.
3. The release page on GitHub is the changelog. There is no CHANGELOG.md.
4. The pull request title is therefore the changelog line: it says what changed, in plain words, for a reader who has not seen the task. Before tagging, preview the notes with `sh scripts/release-notes.sh origin/main` (a ref that is not a tag is headed "Unreleased").

## Consequences

- Every release has a page that says what it carries, and the approver reads it before approving the deploy.
- A vague pull request title (`fix stuff`) is now a vague changelog line. `docs/conventions/git.md` § "Releases and versions" carries the rule.
- A pull request that is merged without a merge commit (a squash) does not appear in the notes. The merge policy of tech-063 makes task pull requests merge commits; revisit this ADR if that changes.
- The notes rely on GitHub's default merge-commit message: subject `Merge pull request #N from <owner>/<branch>`, body = the pull request title. A change of that repository setting (Settings → General → merge commit message) or a hand-edited merge message drops the pull request from the notes or mislabels it.
- The `notes` job can fail (the tag is missing, the token lacks the right), and then `deploy` does not start: a release without notes does not go out unnoticed.
- Revisit if the notes need curation by hand (then edit the release page; a redeploy would overwrite it) or if releases move off GitHub.
