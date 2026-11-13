# tech-070-deploy-run-title

- type: tech
- size: S
- branch: tech/070-deploy-run-title
- stage: 04-backend
- epic: platform/ci
- deps: tech-066
- gates: gate-3

## Gates

gate-3: pending

## Plan

1. `.github/workflows/deploy.yml`: top-level `run-name: Deploy ${{ inputs.tag || github.ref_name }}` — a tag push shows its tag, a hand-started run the tag typed in.
2. `scripts/tests/cd-runner-cases.sh`: pin that line; let exactly it through the "inputs only via env" guard (a rendered title, not a shell command).
3. Check: `sh scripts/tests/cd-runner-cases.sh`; CI runs verify.sh on the PR. By hand: the next Deploy run is titled `Deploy v<tag>`.

## Log

- 2026-11-13 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/tech-070-deploy-run-title
- 2026-11-13 [orchestrator] route confirmed by the human: tech, S, light cycle (no QA and review sessions; the human reviews the diff in the PR). `.github/` is outside the light-cycle list — an exception the human chose for a one-line title change
- 2026-11-13 [orchestrator] `sh scripts/tests/cd-runner-cases.sh` → exit 0 (all passed); verify.sh left to CI (economy mode)

## Notes

Summary (from the queue line or an argument): The Deploy workflow's run title shows the commit message (Merge pull request #...), not the release tag, so the human cannot see which version a run deploys: add run-name with the tag (inputs.tag for workflow_dispatch, github.ref_name for a tag push)
