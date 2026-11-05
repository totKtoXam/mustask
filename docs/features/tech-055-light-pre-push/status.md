# tech-055-light-pre-push

- type: tech
- size: S
- branch: tech/055-light-pre-push
- stage: complete
- epic: platform/ci
- deps: -
- gates: gate-3

## Gates

gate-3: approved (human, chat 2026-11-05: "сделай задача сразу сам и запушь")

## Plan

`scripts/verify.sh` gets `VERIFY_TESTS=0`: every check but the test runs (backend build without `dotnet test`, frontend lint and build without Vitest, no KeycloakLive run). `.githooks/pre-push` sets it unless `VERIFY_FULL=1`. CI is unchanged: it sets `VERIFY_FULL=1`, which wins. The docs that describe the hook follow.

## Log

- 2026-11-05 [orchestrator] the human asked why a push reran the full test suite when CI already does; offered a light hook / no hook / as is (ask-format), the human chose "⭐ Лёгкий хук", then ordered: pause the other sessions, do it here, push, let them follow. Route: tech, S, light cycle (scripts and docs only; no QA, no review session; the review is the human seeing the diff in the PR). Queue line by `scripts/queue.sh add` (PR #71); the number is 055.
- 2026-11-05 [orchestrator] why: on 2026-11-04/05 feat-033 needed six pushes. The hook ran the tests the whole branch diff reached (3000+ integration, volume and the whole Vitest after a `src/shared/ui` change, ~20 min a push) and went red on timing-sensitive tests under load or sleep (`*VolumeTests`, `WorkItemFormPage.landing`) while the same code was green alone; feat-048 and feat-049 queued behind it.
- 2026-11-05 [orchestrator] binding: done in the doc-012 orchestrator session's worktree switched onto the task branch, as for tech-024/tech-050; new-task.sh was not run (it makes a worktree this session cannot use).

## Notes

Summary (from the queue line): Light pre-push hook: run only the fast checks (build, tsc, ESLint, format, docs rules) and no tests — integration, volume and Vitest run in CI on the PR only; keep VERIFY_FULL=1 as an opt-in full local run; update the docs that describe the hook
