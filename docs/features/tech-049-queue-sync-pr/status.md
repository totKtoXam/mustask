# tech-049-queue-sync-pr

- type: tech
- size: S
- branch: tech/049-queue-sync-pr
- stage: gate-3
- epic: workflow
- deps: -
- gates: gate-3

## Gates

gate-3: pending

## Plan

1. `scripts/queue.sh`: with an `origin`, build the queue commit in a throwaway worktree detached at `origin/main`, push it to a new `sync/queue-<date>-<op>-<short-id>` branch (no local branch), open a squash PR with `gh`, ask for `gh pr merge --auto --squash`, print the PR URL; without `gh`, or when the PR or auto-merge is refused, print what is left for a human. The local `main` is never touched. `--local` keeps the old commit on the local `main`; it is chosen by itself when there is no `origin`.
2. Number rules kept: only `add` issues a number; `backlog.sh --ids` already reads the queue files of every remote-tracking branch, so a pushed but unmerged sync branch holds its number with no change there. `take` reads `origin/main`, so a line whose add PR is open is refused with a hint.
3. Cases: new `scripts/tests/queue-cases.sh` (bare origin, stub `gh`), wired into `verify.sh`; `queue-numbering-cases.sh` moves to `--local`, since its cases are about unpushed commits on the local `main`.
4. Docs: the `queue.sh` header, `docs/conventions/git.md`, `docs/features/numbering.md`, `docs/ai-workflow/feature.md`, the AGENTS.md hard rule.
5. Check: `scripts/verify.sh`, exit code into the Log.

## Decisions

- Number and landing in one PR, by the orchestrator's brief: the id came from `scripts/queue.sh next tech` and is held by this branch; the task was queued and taken in the same breath, so `docs/backlog.md` has no line for it.
- Light cycle under standing approval (AGENTS.md, docs-only S): only `scripts/`, `docs/` and `AGENTS.md` change; no QA or review session, E2E not asked.
- A throwaway worktree, not plumbing: the commit still goes through the repository's `pre-commit` and `commit-msg`, and the push through `pre-push` (`verify.sh`, which sees only `docs/backlog.md`). Cost: about 15 s for the checkout on Windows.
- Known limit, said and not solved: two queue PRs open at once both edit `docs/backlog.md`, so the second can conflict once the first merges. The script lists the other open queue PRs when there are any; auto-merge keeps the window to one CI run.

## Log

- 2026-10-29 [dev] `bash scripts/verify.sh` exit 0, RESULT: green
- 2026-10-29 [dev] `sh scripts/tests/queue-cases.sh`: 24 of 24 pass; against `origin/main`'s `queue.sh` 11 fail, so the cases tell the two apart
- 2026-10-29 [orchestrator] route S, LIGHT CYCLE (`docs/ai-workflow/stages/s-route.md`, § Light cycle), taken under the human's request «сделай сам и запусти сразу агента, чтобы сделать её, потом сразу вместе запушим в main»: number, implementation and landing in one PR; the human presses Merge
