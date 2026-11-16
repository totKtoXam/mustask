# tech-071-release-notes-accuracy

- type: tech
- size: S
- branch: tech/071-release-notes-accuracy
- stage: complete
- epic: platform/ci
- deps: tech-069
- gates: gate-3

## Gates

gate-3: requested · 2026-11-16 (light cycle: the human reviews the diff in the PR)

## Plan

1. `scripts/release-notes.sh`: a merge whose every changed file (`git diff --name-only <merge>^1 <merge>`) is under `docs/` goes to Documentation whatever its branch prefix (v0.6.0 listed the feat-065 analysis, #178, under Features).
2. An empty merge body: the title is the branch made readable, `feat/065-global-search` → `feat-065: global search` (the shape PR titles have here), offline; a branch not shaped `<prefix>/<NNN>-<slug>` stays as is.
3. A two-parent first-parent merge whose subject was rewritten to `<title> (#N)` is listed (v0.6.0 dropped mod-021, #188): the title is the subject without ` (#N)`, the group is the prefix of the first task id in it. Squash commits still drop out (one parent); `sync/` merges and rewritten queue subjects (`Queue `, `Take `, `Withdraw `) are skipped.
4. `scripts/tests/release-notes-cases.sh`: cases for all three.

## Decisions

- 2026-11-16 D-1 (orchestrator): docs-only PRs join the existing Documentation group rather than a new section — one place for "no code shipped", no change to the group order.
- 2026-11-16 D-2 (orchestrator): the title for an empty body comes from the branch, not from gh — the script stays pure git and runs offline (ADR-0043); no workflow change.
- 2026-11-16 ADR-0043 is not edited: its decision (notes from merged PRs, pure git) stands; its consequence "a hand-edited merge message drops the PR" is now narrower, and the script header states the accepted shapes.
- 2026-11-16 E2E: no (standing answer; no panel change).

## Log

- 2026-11-16 [orchestrator] task started on branch tech/071-release-notes-accuracy in worktree dreamy-einstein-34d195 (from origin/main after queue PR #190); queue line taken via queue PR #192.
- 2026-11-16 [orchestrator] route: tech, S, scripts-only (one script + its case tests + this folder, no runtime code) → light cycle under the human's standing approval for docs/scripts-only S tasks (AGENTS.md "Standing approval", s-route.md § Light cycle): no QA/review sessions, the human sees the diff in the PR. Economy mode: CI is the verification, verify.sh not run locally.
- 2026-11-16 [developer] done: release-notes.sh (docs-only group, readable branch title, rewritten-subject merges), release-notes-cases.sh 64 green (12 new). Real repo: v0.6.0 now lists mod-021 under Modules and feat-065 under Documentation; v0.1.0 also regains #49 and #50 (rewritten subjects) and moves #53 (docs-only) to Documentation; v0.2.0..v0.5.0 unchanged.
