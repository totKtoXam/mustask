# tech-028-frontend-tests-speedup — code review

## Round 1

Scope: `git diff main...HEAD` (one commit, 0946e28), S, short route. Verdict:
**changes requested** — 2 major, 2 minor.

### Findings

**R1-1 · major · `--changed` passes vacuously for the guard tests that scan the
sources on disk** — `src/frontend/vite.config.ts:64-74`, `scripts/verify.sh:657-664`.
Vitest's `--changed` picks a test when a changed file is in that test's
*import graph* (`getTestDependencies`). `forceRerunTriggers` only re-runs
everything when the harness, theme or kit **itself** changes. When a *scanned*
file changes, nothing re-runs the scanners:
`src/test/colourLiterals.test.ts` (all of `src/`), `src/test/kitGuard.test.ts`
(`src/` and `docs/features`), `src/test/e2eSessionGuard.test.ts` (`e2e/`),
`src/features/uikit/pages/UiKitShowcasePage.test.tsx`, and the
`errorCodes*.test.ts` / `*/testFixtures.ts` readers of `contracts/errors/*.json`.
For example, a branch that adds `color: '#fff'` to `src/features/x/Foo.tsx`
passes `bash scripts/verify.sh`. The BR-1 colour rule
(`docs/conventions/frontend.md:57`) and the kit rule (`src/frontend/AGENTS.md:46`)
are then not checked until a full run. The comment at `vite.config.ts:66-67`
says these globs cover "what no import graph shows (the kit guard and the
colour scan read sources off disk)". They cover only edits to the guards.
**Fix:** in the affected branch of `verify.sh`, always run the disk-reading
tests as well. Either add a second `vitest run <explicit list>`, or adopt a
naming convention such as `*.guard.test.ts` and pass that glob. Also add
`**/contracts/errors/**` (or the fixture readers) to the always-run set. Then
reword the comment so it says exactly what the triggers cover.

**R1-2 · major · the GitLab MR path is no longer gated by a full run, but
three files say it is** — `.githooks/pre-push:23` (not in the diff),
`scripts/verify.sh:21-22`, `src/frontend/AGENTS.md:29-30`.
`pre-push` runs `sh scripts/verify.sh` without `VERIFY_FULL`, so after this
branch it runs only the affected tests. Only `finish-task.sh` sets
`VERIFY_FULL=1`, and that is the no-remote path. "So a merge is always gated by
a full run" is therefore false whenever a remote exists. Direction 2: the
untouched `docs/ai-workflow/stages/07-mr-complete.md:32-35` tells the agent
"Do not run the full suite by hand here: `pre-push` runs `scripts/verify.sh`
... it is the same run". An agent who follows it opens an MR that no full run
has seen. Together with R1-1, a colour literal or a kit violation can reach an
MR. This also conflicts with AGENTS.md "an MR with failing tests is not opened".
**Fix:** set `VERIFY_FULL=1` in `.githooks/pre-push`. That makes the 07 sentence
and both claims true. Otherwise, narrow the two claims to "a local squash
(`finish-task.sh`)" and change 07 step 1 to require `VERIFY_FULL=1` before the
push.

**R1-3 · minor · a lock takeover can remove a lock that another process just
took** — `scripts/verify.sh:127-139`. Suppose waiter B judges the lock stale
(dead pid). Waiter A then renames it, removes it, runs `mkdir` and writes
`owner.txt`. B's later `mv "$vitest_lock_dir" "$grave"` succeeds on **A's live
lock** and deletes it, and A and B both run the full suite. A's release
correctly leaves B's lock alone. The cost is a lost serialization, not a wrong
result. However, the comment "of several waiters that all judged the lock stale
only one gets to remove it" overstates what the code guarantees. The same
window exists on the "no owner.txt" path. A related gap: a pid written by a
different shell runtime (WSL bash against the same `.git`) fails `kill -0`, so
that lock is taken over as dead.
**Fix:** after the `mv`, compare `$grave/owner.txt` with the `pid`/`started`
that were judged stale. If they differ, `mv` the directory back (best effort).
At minimum, reword the comment.

**R1-4 · minor · the Plan's check is not recorded** —
`docs/features/tech-028-frontend-tests-speedup/status.md` Plan, "Check". The Log
has no line for `bash scripts/verify.sh` or `VERIFY_FULL=1 bash
scripts/verify.sh` with its exit code. The speedy-cache change (item 3) touches
every component test through the alias, so the full green run is the evidence
it needs. **Fix:** log both commands and their exit codes before gate-3.

### Checked and fine

- (1) Default `--changed "$merge_base"` with `--passWithNoTests`. The full suite
  runs on `VERIFY_FULL=1` or an empty merge-base (`verify.sh:643-664`), and
  `finish-task.sh:186` sets it. The triggers are appended to
  `configDefaults.forceRerunTriggers`. Vitest 3.2 also adds `setupFiles`
  (`coverage.*.js:3669`) and matches triggers with picomatch against absolute
  paths, the same form as the defaults. Vitest's `--changed <sha>` uses
  `sha...HEAD` plus staged, unstaged and untracked files, so it matches
  verify.sh's own `changed` set.
- (2) `isolate: false` is reverted. The only trace is the explanatory comment at
  `vite.config.ts:79-81`, and `sharedJsdom`, a custom environment and `isolate`
  appear nowhere else in `src/` or `scripts/`. The Notes are consistent with
  the diff. Their timings were not re-measured: this round ran no tests, by
  instruction.
- (3) `styleCache.ts` holds one `speedy: true` cache per test (key `css`), and
  `setup.ts:30-31` flushes it after `cleanup()`. `testingLibrary.tsx` wraps it
  in `CacheProvider`. `renderWithProviders.tsx:4` and `renderShell.tsx:6`
  import the aliased `@testing-library/react` (`vite.config.ts:53-57`), so they
  are covered too. `kitTesting.ts:102` already reads `document.styleSheets`,
  and no test reads `<style>` text or `data-emotion`. `@emotion/cache ^11.14.0`
  is in `package.json`, in the lock's root `packages[""]`, and in
  `node_modules/@emotion/cache` 11.14.0, which is consistent.
- (4) The lock is a `mkdir` in `--git-common-dir`, with `owner.txt` holding
  pid, worktree and start time. Waiting is unbounded, and the lock goes stale
  after 3600 s or when its pid is dead. An affected run takes no lock. The
  EXIT trap releases only a lock it holds and owns, INT/TERM go to `exit 130`,
  and both test outcomes release before `pass`/`fail`. No other `trap` exists
  in verify.sh. Everything used is POSIX sh (`$(( ))`, `kill -0`, `printf
  '%.9s'`; `--path-format` needs git >= 2.31). The agents' line is in
  `docs/ai-workflow/README.md:104-107`.

### Behavioural claims (item 6)

- verify.sh:16-19 "only the tests that reach a changed file": anchored to
  `verify.sh:661`.
- verify.sh:19-20, vite.config.ts:64-68 "the triggers run everything, also
  what no import graph shows": became R1-1.
- verify.sh:21-22, AGENTS.md:29-30 "a merge is always gated by a full run":
  became R1-2.
- verify.sh:22-25 "one full run at a time, a stale lock is taken over":
  anchored to `verify.sh:101-145`. Weakened by R1-3.
- verify.sh:25 "affected-only runs take no lock": anchored to `verify.sh:657-664`.
- verify.sh:130-131 "only one waiter gets to remove it": became R1-3.
- README.md:104-107 "full runs queue on a lock": anchored to
  `verify.sh:649`. "Agents run Vitest on their own files" is an instruction,
  anchored to that line plus the stage that reads README. This is the weakest
  kind of anchor.
- styleCache.ts:6-11 "a test must not read `<style>` text": no test does
  (grep for `querySelector('style'` / `data-emotion` found nothing). The CSSOM
  reader is `kitTesting.ts:102`.
- vite.config.ts:79-81 "isolate:false rejected": matches the Notes, and no
  remnant was found.
- Direction 2: the changed behaviours were the verify.sh frontend test scope
  and the merge gate. Grepped `verify.sh`, `npm test`, `full suite`, `whole
  suite` and `vitest run` in `docs/ai-workflow`, `.claude/agents`,
  `docs/conventions/frontend.md`, `src/frontend/AGENTS.md` and `.githooks`.
  Hits: `07-mr-complete.md:32-35` and `.githooks/pre-push:23` became R1-2.
  `frontend.md:57` and `AGENTS.md:46` say `npm test` and are still true, but
  verify.sh no longer guarantees them (R1-1). `s-route.md:38,59,74` and
  `task-executor.md:20,34` say only "verify.sh green" and are unaffected.
  Not swept: `scripts/verify.ps1` (a pass-through wrapper) and
  `scripts/tests/*`.
