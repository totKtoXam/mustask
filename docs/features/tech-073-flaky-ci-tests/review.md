# Code review: tech-073-flaky-ci-tests

## Round 1

Reviewed: `git diff origin/main...HEAD` at d9020822 (53 files), against `status.md` Plan 1-4 and D-1. Economy mode: `verify.sh` not run (CI is the gate). Ran `routes.members.test.tsx -t "больше не состоите"` once (green, the body failed as `it.fails` expects), and a throwaway probe copy of that test, 5 runs, logging every DOM batch through a `MutationObserver` (the same granularity `waitFor` sees). The probe file was deleted.

**Verdict: approve.** No blocker, no major. Three minors, all debts.

Plan conformance: items 1-4 and D-1 are implemented, nothing extra. 48 `vi.setConfig` calls removed (28 × 120 s, 20 × 60 s). 38 files dropped `vi` from the import; the 10 that kept it still use `vi.` (checked per file), so nothing is left unused or missing.

1. **`vi.setConfig` under `isolate: false`.** The claim holds, by the code. `vi` is built once at module load and binds the worker state of that moment (`node_modules/vitest/dist/chunks/vi.bdSIJ99Y.js:3801-3803,3986-3989,3996`). `vitest` is externalized, so later files get the same `vi`. With `isolate: false` the pool sends one file per `runFiles` call (`coverage.DfSpMS-b.js:2687`), and each call builds a fresh `state` with `config: ctx.config` (`worker.js:88-92`). From the second file on, `setConfig` writes into the first file's stale state, while `it()` reads the live `runner.config.testTimeout` (`@vitest/runner/dist/chunk-hooks.js:606`). Removing all 48 calls loses nothing: none of them took effect except in a worker's first file. Every value was ≤ 120 s, which is now the local floor. tech-029 review F5 said the opposite because it missed the stale binding. It is a historical record and is left as it is.
2. **`process.env.CI`.** GitHub Actions sets `CI=true` for every step. `ci.yml:90-96` and `scripts/verify.sh` neither unset nor override it. `vite.config.ts` is read in the main process and `setup.ts` in thread workers, which inherit `process.env`. Nothing asserts that a `findBy` fails quickly: no `findBy…/waitFor…).rejects` exists in `src`. The cost is a known one: the `it.fails` body now spends the full 15 s on CI before it fails. Five explicit waits are now shorter than the CI default (m2).
3. **`it.fails` determinism.** The probe gave the same sequence in all 5 runs. First the notice appears with the location still at `/members`, which is the transition navigation of React Router 7. Then, in the same batch where the location becomes `/organizations`, the notice is already gone, and the shell's «Ранее выбранный контекст недоступен.» follows one batch later. The single run of the real test agreed. No window was observed in which `waitFor(location)` resolves while the notice is still on screen. So the body fails every run while bug-007 stands. This is an observed React ordering, not a structural guarantee, but nothing points to a CI-only window. Test quality: m3.
4. **`Budget(5)`.** 5 s locally, unchanged; 50 s on CI. This matches the class's other budgets (`RichTextTestSupport.cs:30-35`, `ReducerBoundCostTests.cs:56,81`). The warm figure and `Exceeded` are still asserted.

### Findings

- **m1 (minor)** These six file headers still end with "…a test of several steps outlasts the default.":
  - `WorkItemCard.delete.test.tsx:27-28`
  - `WorkItemCard.deleted.test.tsx:19-20`
  - `WorkItemCard.purge.test.tsx:24-25`
  - `WorkItemCard.restore.test.tsx:26-27`
  - `WorkItemFormPage.deletion.test.tsx:15-16`
  - `WorkItemsPage.filters.test.tsx:23-24`

  That sentence justified the removed `vi.setConfig`, and "the default" is now the 120/180 s budget. A reader may put a per-file timeout back. Fix: drop the sentence (the other 12 files lost it together with the call).
- **m2 (minor)** Five explicit positive waits are now tighter than the default they used to exceed. On CI they still cut a slow render early, which is the same cause as Plan 1:
  - `routes.node.qa.test.tsx:245` (1 500)
  - `AggregateViews.test.tsx:123` (8 000)
  - `WorkItemsPage.keyword.test.tsx:29` (8 000)
  - `WorkItemsPage.qa.test.tsx:70,86` (3 000; a heavy 50-row list render)

  Fix: drop the overrides so the default applies, or scale them the same way.
- **m3 (minor)** `routes.members.test.tsx:178-209`: `it.fails` passes on any failure. While bug-007 stands, the scenario's other assertions (the move to `/organizations`, the empty-state h2 after leaving one's only organization) guard nothing. A regression that keeps the user on the members page would stay green. The sibling test (`:149-169`) covers the move only in the two-organization case. Fix: put the move and the heading in a plain `it`, and keep only the notice wait under `it.fails`.

### Behavioural claims (item 6)

- `vite.config.ts:224-228` (only a worker's first file gets `setConfig`) → `vi.bdSIJ99Y.js:3801-3803,3986-3989`, `worker.js:88-92`, `coverage.DfSpMS-b.js:2687`, `chunk-hooks.js:606`.
- `vite.config.ts:224` "one budget… set here and nowhere else" → no `setConfig` is left under `src/frontend/src` (grep).
- `vite.config.ts:228-229` "120 s… what the heaviest files asked for" → the largest removed value was `120_000`.
- `vite.config.ts:230-231`, `setup.ts:35` "UI waits scale the same way" → only the default does (`setup.ts:41`); explicit overrides do not → m2.
- `setup.ts:38-39` "the margin costs nothing on a pass" → there are no negative `findBy`/`waitFor` waits in `src`; positive waits return on success. Holds.
- `setup.ts:36` "three workers" → `vite.config.ts:238` `min(8, cores-1)` = 3 on 4 cores.
- `routes.members.test.tsx:171-177` (the notice is drawn before the move, and the fallback replaces it; it starts failing when bug-007 is fixed) → the probe sequence in item 3 above; `useNavigationSource.ts:451-457` raises the fallback once the address carries no organization; `noticeCorner.ts:46-47` closes the host's notice. Holds.
- `ReducerBoundCostTests.cs:101-102` "the CI margin of the other budgets" → `RichTextTestSupport.cs:30-35`, `:56,81`.
- Direction 2: the changed behaviours are the per-file test timeout, the global `testTimeout` and `asyncUtilTimeout`. I grepped `setConfig|testTimeout|asyncUtilTimeout` over `docs/`, `.claude/`, all `AGENTS.md`, `src/frontend/src` and `src/frontend/scripts`. The hits are the historical records of tech-029 (status, review F5), mod-016 status, tech-053 review and feat-029 review R1-10, left as they are. No live document describes the old values. The stale prose in test-file headers is m1.

## Round 1 follow-up (developer)

- m1 fixed: the six headers lose the sentence.
- m2 fixed: the 1 500 and 3 000 overrides go (the default covers them); the two 8 000 waits use `waitBudget(8000)` (`src/test/waits.ts`, ×3 on CI like the default).
- m3 fixed: `leaveTheOnlyOrganization()` is shared; a plain `it` holds the move and the empty-state heading, `it.fails` holds only the notice.
