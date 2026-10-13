# tech-028-frontend-tests-speedup

- type: tech
- size: S
- branch: tech/028-frontend-tests-speedup
- stage: complete
- epic: workflow
- deps: -
- gates: gate-3

## Gates

gate-3: approved

## Plan

1. verify.sh: frontend tests run as `vitest run --changed <merge-base>`; full suite on `VERIFY_FULL=1` or an empty merge-base; finish-task.sh sets `VERIFY_FULL=1`; `forceRerunTriggers` gets `src/test`, `src/app/theme`, `src/shared/ui`. Header of verify.sh and `src/frontend/AGENTS.md` updated.
2. `isolate: false` in the test config; leaks between files fixed where found (setup.ts resets). Done when the full suite is green twice in a row with `--sequence.shuffle`; otherwise revert and report what leaked.
3. One shared emotion cache with `speedy: true` in the test wrappers (testingLibrary, renderWithProviders, renderShell); `rulesFor` and friends read the CSSOM. Revert and report if it cannot be stabilised quickly.
4. Full runs take a mkdir lock in `<git-common-dir>/vitest-full.lock` (owner.txt; stale after 60 min or dead pid); affected-only runs take none. One line for agents in docs/ai-workflow.
Check: `bash scripts/verify.sh` and `VERIFY_FULL=1 bash scripts/verify.sh` green; one review round. No before/after measurements, no E2E.

## Log

- 2026-10-13 [new-task] task started
- 2026-10-13 route: tech, S, short route under the human's standing approval (human asked for it in the task statement); no QA session, one review round; no E2E (human: not needed)
- 2026-10-13 worktree: the session's own worktree was switched to this branch (hook forbids writing to a sibling worktree)

- 2026-10-13 review round 1 (review.md): R1-1 guard tests always run in affected mode, R1-2 pre-push sets VERIFY_FULL=1, R1-3 takeover checks the owner, R1-4 this log — fixed
- 2026-10-13 queued follow-up tech-029-vitest-full-run-workers (maxWorkers under the lock, measure once); peers asked to pause full runs while this lands

- 2026-10-13 gate-3: approved by the human in chat ("approve"); tech-028, tech-029 and tech-030 land in ONE squash from branch tech/030-green-stamp (the human asked for a single verified merge instead of three full runs); the branches tech/028 and tech/029 are kept

## Notes

Summary (from the queue line or an argument): Speed up the frontend tests: verify.sh runs only affected tests, shared test environment, one emotion cache, one full run at a time

### Item 2 (`isolate: false`) — reverted, what leaked

Tried on the whole suite (228 files) with `--sequence.shuffle`:

- Vitest's built-in `jsdom` environment builds a new window per file and closes it,
  while the module cache stays shared: Testing Library's `screen` and React DOM kept
  the first, closed window — 1798 failures, every file after a worker's first saw
  `<body />`. Fixed by a custom environment that keeps one window per worker.
- With one shared window: 10 files / 70 tests still failed, and the run was not
  faster (25 min against 14 with isolation). Two causes seen: `vi.mock`ed modules
  stay cached from an earlier file (`PropertyField.test.tsx` — the stub never
  receives the props), and the heavy form/backlog pages (`WorkItemFormPage.*`,
  `BacklogPage.*`, `WorkItemCard.*`) time out at 20 s, since the shared window,
  listeners and timers pile up. Not stabilisable in a reasonable time; the fix
  would be a per-file module-mock reset and an audit of every file's globals.
- Kept: items 1, 3 and 4.
