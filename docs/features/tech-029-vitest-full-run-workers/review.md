# tech-029-vitest-full-run-workers — code review

## Round 1

Scope: `git diff tech/028-frontend-tests-speedup...tech/029-vitest-full-run-workers` (10 files). Vitest was not run (a full verify run was in progress in this tree); `tsc -p tsconfig.app.json --noEmit` and ESLint on the changed files are clean. Vitest internals read: 3.2.7, `node_modules/vitest/dist`.

**Verdict: changes requested** — 2 major, 5 minor.

### F1 — major — a `vi.mock` factory's result outlives its file

`src/frontend/src/test/setup.ts:4-16`, status.md Notes "Result" bullet 2. Both say `vi.resetModules()` keeps a `vi.mock` of one file from reaching the next. It only partly does. Vitest caches a manual mock's exports under `mock:<id>` in the worker's module cache (`vitest/dist/chunks/execute.B7h3T_Hc.js:319-342, 416-433`), and `vi.resetModules()` skips `^mock:` keys (`utils.XdZDrNZV.js:34-46`, `resetMocks=false`). The only reset of those keys is `executor.mocker.reset(); resetModules(cache, true)`, and it runs only when `isolate` is true (`runBaseTests.9Ij9_de-.js:112-115`). The registration itself is scoped to each file (`getMockerRegistry` is keyed by the file path). But when a later file in the same worker mocks the same module, `callFunctionMock` returns the earlier file's cached exports and never calls the second file's factory. That is the tech-028 `PropertyField` symptom ("the stub never receives the props"). Today only `App.test.tsx` (`./routes`) and `PropertyField.test.tsx` (`shared/ui`) use `vi.mock`, with different targets, so nothing fails yet. The next file that mocks `shared/ui` would fail or pass depending on how files are scheduled on workers.
**Fix:** in `setup.ts`, before the test file loads, drop the `mock:` entries from the worker module cache. If that internal access is unacceptable, run the files that call `vi.mock` in a separate `isolate: true` project, or add a guard test that fails when two test files `vi.mock` the same resolved target. Either way, narrow the comment and the Notes to what actually holds.

### F2 — major — the harness does not reset what a test leaves on the shared window

`src/frontend/src/test/sharedJsdom.ts:10` says "`setup.ts` resets what a test itself leaves behind". `setup.ts:47-73` resets the DOM, MSW handlers, the style cache, the session module, `localStorage`, spies, stubbed globals and env. It does not reset:
- fake timers (`vi.useFakeTimers` patches `globalThis`; the run loop restores mocks and config between files but not timers, `runBaseTests.9Ij9_de-.js:119-122`). Timers are one of the two leak causes tech-028 named. Today both users restore them (`session.test.ts` with try/finally, `NoticeProvider.test.tsx:40-41` in `afterEach`), but nothing enforces that for the next file;
- `sessionStorage`, the URL / `history` (only `AuthContext.test.tsx:49-54` resets it itself), and the attributes of `<body>`. `sharedJsdom.ts:47-51` clears the attributes of `<html>` only, and MUI's scroll lock writes `style` onto `body`.
**Fix:** in setup's `afterEach`, add `vi.useRealTimers()`, `sessionStorage.clear()` and `window.history.replaceState(null, '', '/')`. In `sharedJsdom.ts`, also strip the `body` attributes. Then make the sentence list what is actually reset.

### F3 — minor — an inherited `VITEST_FAST=1` silently thins the gating full run

`vite.config.ts:9-14` reads `VITEST_FAST` from any environment. `scripts/verify.sh:664` (`npm test`, the `VERIFY_FULL` run behind pre-push and `finish-task.sh`) does not clear it. So a shell or CI that exports it skips 30 files and still reports green. The claims "every other entry point runs all" (`vite.config.ts:72-73`) and "verify.sh runs everything" (Notes, `test-fast.mjs:2`) hold only while the variable is unset.
**Fix:** `unset VITEST_FAST` in `verify.sh` before the Vitest calls, and/or print a one-line notice from `vite.config.ts` whenever the fast tier is active.

### F4 — minor — "about a fifth of the time" is about a third

`src/frontend/AGENTS.md:26-27`. Notes: `test:fast` takes 155 s and the full suite on this branch 475 s, a ratio of 0.33. "A fifth" holds only against the pre-change 820 s.
**Fix:** say "about a third of the full run".

### F5 — minor — the stated reason for raising `testTimeout` to 60 s is unanchored

`vite.config.ts:90-93` and Notes say a module-scope `vi.setConfig` "does not reliably reach a worker that runs many files". The code says otherwise. `vi.setConfig` assigns into `workerState.config` (`vi.bdSIJ99Y.js:3986-3989`) before `test()` reads `runner.config.testTimeout` (`@vitest/runner/dist/chunk-hooks.js:606`), and the config is reset per file (`runBaseTests:120`). The mechanism is not shown. Meanwhile the raise applies to all 228 files.
**Fix:** reword it as an observation ("heavy files timed out at 20 s under shuffle; cause not established"). Or find the cause and keep 20 s globally.

### F6 — minor — `slow-files.mjs` can silently emit absolute paths

`src/frontend/scripts/slow-files.mjs:22-24`. `.replace(root, '')` is case-sensitive. On Windows, a drive-letter case mismatch between `fileURLToPath` and Vitest's report leaves absolute paths. Those never match `exclude`, so the fast tier quietly becomes the full suite.
**Fix:** compare lowercase on win32 (or use `path.relative`), and fail if a resulting path is not relative.

### F7 — minor — candidates.md credits a script with a filter it does not contain

`candidates.md:3` says "`scripts/slow-files.mjs`, the thin-and-heavy filter in the session". The ≥ 5 s / ≤ 3 `expect` filter exists in no committed script, so the list cannot be regenerated.
**Fix:** state that it was a one-off session filter, or commit it.

### Checked, no finding

- Lazy `await import()` in the hooks: setup runs (and is re-executed per file, `runners.js:132-134`) before the test file is imported. The hooks run after collection, so they resolve the instances the test file already put in the registry, including the aliased `@testing-library/react` → `testingLibrary.tsx`. No test calls `vi.resetModules`/`doMock` itself.
- `restoreAllMocks` after every test: no test file has a `beforeAll`, and no `vi.spyOn` sits at module or describe scope (`App.test.tsx:17-19` creates its spy in `beforeEach`). The two `stubGlobal` calls are inside tests. `matchMedia` is installed with `defineProperty`, not a stub, so `unstubAllGlobals` does not remove it.
- `src/frontend/e2e/**`: untouched. `exclude` keeps `'e2e/**'`. No script or hook runs `test:fast` or sets `VITEST_FAST`.
- Notes numbers: 228 − 30 = 198 files ✓. `candidates.md` file seconds sum to ≈1671 s against "1673 s" ✓ (rounding). Its order matches `slowFiles.json` ✓. The 820/836/475/155 s figures are internally consistent; they were not re-measured.
- Not yet recorded: Plan item 3's `VERIFY_FULL=1 bash scripts/verify.sh` green (the run was in progress at review time).

### Behavioural claims

- `setup.ts:4-16`, "a `vi.mock` of one file is not seen by the next" → **F1**.
- `setup.ts:13-16`, "hooks get the same instance the test file uses" → `runners.js:132-134` plus collection before hooks ✓.
- `sharedJsdom.ts:10`, "`setup.ts` resets what a test itself leaves behind" → **F2**.
- `sharedJsdom.ts:5-9`, "the built-in env closes the window per file; `screen` keeps the first" → `runBaseTests:78-91`, `withEnv` per worker run; corroborated by tech-028 Notes; not reproduced.
- `vite.config.ts:72-73`, "every other entry point runs all" → `verify.sh:664,674,686` use `npm test` ✓, conditional on env → **F3**.
- `vite.config.ts:90-93`, the `vi.setConfig` mechanism → **F5**.
- `AGENTS.md:25-28`, "about a fifth of the time" → **F4**. The regenerate command → `slow-files.mjs:3-4` ✓.
- `slow-files.mjs:6-8` / Notes, "a heavy file missing only makes the fast tier slower" → `vite.config.ts:9-14` (exclude only) ✓.
- `test-fast.mjs:3`, "arguments go to `vitest run` as they are" → `test-fast.mjs:8` ✓.
- `candidates.md:4`, "no test was edited" → the diff touches no `*.test.*` ✓. `candidates.md:3`, provenance → **F7**.
- Direction 2: the changed behaviours are per-file isolation (`isolate`, `environment: 'jsdom'`), `testTimeout` 20 s, and the fast tier (`test:fast`, `VITEST_FAST`). I grepped `isolate|testTimeout|20000|setConfig|useFakeTimers|restoreAllMocks|vi.mock|jsdom window|per file` over `docs/conventions`, `docs/ai-workflow`, `.claude`, `AGENTS.md`, `src/frontend/AGENTS.md` and `src/frontend/src/test`. The only stale hits are tech-028's `status.md:18,36` and `review.md:81`, which are historical records of that task and left as is. Nothing else describes the old behaviour.
