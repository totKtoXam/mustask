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

<!-- S/M: implementation plan, 3-10 lines. L: see plan-backend.md / plan-frontend.md -->

## Log

- 2026-11-13 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/tech-070-deploy-run-title

## Notes

Summary (from the queue line or an argument): The Deploy workflow's run title shows the commit message (Merge pull request #...), not the release tag, so the human cannot see which version a run deploys: add run-name with the tag (inputs.tag for workflow_dispatch, github.ref_name for a tag push)
