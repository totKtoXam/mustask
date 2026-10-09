# QA: feat-022-work-item-form-fields

By D-1 (the short route) there are no QA sessions for this task: the
developers' tests, `scripts/verify.sh` and the E2E run below stand in for
them. This section records commands and facts, not a test plan, so it has no
coverage matrix and no verdict. The scope is `spec.md` and stories US-1..US-6
by reference; the file under test is `25-work-item-fields.spec.ts` (new, with
`e2e/support/workItemsApi.ts` extended by the developer). Files 01–24 are
unchanged and ran as the regression.

## Frontend — E2E results, 2026-10-09

A task-executor run on branch tip `1054df3` (the frontend developer's last
commit), worktree of its own, database `mustask_feat022` on the external
PostgreSQL (port 5432), with the stand lock held from before the build to the
stop. Times are UTC. One spec defect was found and fixed (`59f1447`, below);
the file was re-run on that tip, the product code did not change.

**The ports changed for this run.** The human's other project owns 3000 and
5000 on this machine (both were listening before the start, under other
processes; they were not touched and their owners changed during the run, so
they were never ours). AppHost's user secrets (`Ports:Frontend`,
`Ports:Api`) move the stand to the **panel 3100, the API 5100 and Keycloak
5010**; all three, and the Aspire dashboard's 17125, were free before the
start. The suite ran with `E2E_PANEL_URL=http://localhost:3100` and
`E2E_API_URL=http://localhost:5100` (the defaults in `playwright.config.ts`
and `e2e/support/env.ts` still say 3000 and 5000; `E2E_KEYCLOAK_URL`'s
default is 5010 already). Nothing under `src/frontend/e2e/` hard-codes 3000
or 5000 outside those two default reads (searched: no other match), so there
is no spec defect of that kind.

| Step | Command | Result |
|------|---------|--------|
| 1 | `bash scripts/warm-worktree.sh` | node_modules copied from the cache, no `npm ci` (took over two minutes) |
| 2 | `dotnet build src/backend/Mustask.AppHost` | 0 warnings, 0 errors, 1 min 37 s |
| 3 | `bash scripts/stand-reset.sh --yes`, `ConnectionStrings__mustask` naming `mustask_feat022` | exit 0. Database "not present", created empty; Keycloak store `mustask-keycloak-d400f2cf388dc5d9` "not present", so the pair started from nothing and took its ports from the realm import. It printed that it could not delete the stale E2E stand file (`DirectoryNotFoundException`, the `.e2e-stand` folder did not exist yet), harmless on a first start |
| 4 | `dotnet ef database update --project src/backend/Mustask.Infrastructure`, same variable | exit 0: `InitialCreate` through `AddWorkItemFormFields` (20261009055512) applied, eight migrations. The tools are 10.0.10 against runtime 10.0.11, a warning only; the first `SELECT` from `__EFMigrationsHistory` is logged as failed because the table did not exist yet |
| 5 | `dotnet run --project src/backend/Mustask.AppHost`, default launch profile, same variable, started 10:33 (detached) | log: "Database: external PostgreSQL", "Keycloak store: volume mustask-keycloak-d400f2cf388dc5d9 (pairs with localhost:5432/mustask_feat022)", "the live realm matches the realm import", `mustask-e2e` and `e2e-fixture` provisioned. The API on 5100 and the panel on 3100 answered 200 at 10:35:17 |
| 6 | `bash scripts/keycloak-seed.sh` (the three passwords from AppHost's user secrets), 10:35:24–10:35:33 | exit 0: `mustask` → «Администраторы», `member` → «Сотрудники / Участники», both created |
| 7 | `cd src/frontend && E2E_PANEL_URL=http://localhost:3100 E2E_API_URL=http://localhost:5100 E2E_ALLOW_DOCKER=1 npm run e2e`, started 10:35:40 | exit 1: 63 tests in 24 files, **62 passed, 1 failed, 0 skipped, 0 flaky**, 16.9 min. The failure is 25.6, a spec defect |
| 8 | the fix `59f1447`, then `E2E_… npx playwright test e2e/specs/25-work-item-fields.spec.ts`, 10:53:29–10:55:44 | exit 0: **6 passed**, 1.6 min. One re-run cycle used of the two allowed. `tsc -p tsconfig.e2e.json`, ESLint and Prettier on the spec are clean |
| 9 | stand stopped: `taskkill //PID <Mustask.AppHost.exe> //T //F` (the `dotnet run` parent, `Mustask.Api` and Vite went with it), `docker rm -f mustask-e2e-keycloak-5010` | ports 3100, 5100, 5010 and 17125 free; no `Mustask.*` process left; lock released. `mustask_feat022` and the Keycloak volume left as they are; `postgres-5432` untouched and running. The 3000 and 5000 listeners are the human's other project and were not touched |

**What happened to the run in the middle.** The Claude process that ran this
task exited while step 7 was running. The stand and the suite are detached
processes and lived on, with the suite's output going to a log file in the
session scratchpad, so nothing was lost; the coordinator told the executor to
wait, no second stand and no second suite were started, and the run was
carried on from the finished first suite's log and `e2e-results/results.json`
(copied to the scratchpad before the re-run overwrote it).

**How the secrets reached the run.** AppHost's user secrets were read with
exactly `dotnet user-secrets list --project src/backend/Mustask.AppHost`,
inside a command substitution of a small helper script kept outside the
repository, in the session's scratchpad (`feat022-e2e-step.sh
reset|migrate|start|seed`), run as `cd <worktree>; bash <helper> <step>`. The
helper holds no value: it derives `ConnectionStrings__mustask` (the user
secret's connection string with `Database=mustask` replaced by
`Database=mustask_feat022`) and the three passwords into its own process
environment. Nothing was printed or written to a file. The permission layer
accepted the read. The E2E run itself needs no secret: the suite signs in
through the stand-only client `mustask-e2e` (ADR-0023), whose secret AppHost
writes to `.e2e-stand/` (gitignored) at each start.

**Totals.** First run: 63 tests in 24 files, 62 passed, 1 failed. After the
spec fix and the re-run of file 25: 63 passed. Tests per file: one each in
01–11, 13, 14, 16, 17 and 18; 3 in 15; 13 in 19; 6 in 20; 8 in 21; 3 in 22;
3 in 23; 5 in 24; **6 in 25** (the new file). Against feat-026's run (57
tests) the difference is file 25 (+6). Specs 5 and 6 ran because Docker
control was allowed.

| New | Test | First run | After the fix |
|---|---|---|---|
| 25.1 | «Эпик»: two columns at 1440 px, one at 1279 px, no sideways scroll at 640 px; planning and classification saved and kept; a target date before the start refused (US-1, US-2, US-5) | passed, 16.5 s | passed, 27.8 s |
| 25.2 | «Фича»: the epic's fields; «Риск» emptied to «Не указан»; a number emptied and 0 kept (US-2) | passed, 13.4 s | passed, 22.4 s |
| 25.3 | «Пользовательская история»: «Критерии приёмки» under «Описание»; points, risk and criteria in one save; text that is not a number never leaves the browser (US-1, US-2, US-4) | passed, 9.8 s | passed, 6.7 s |
| 25.4 | «Задача»: «Трудозатраты (часы)» kept with «,»; hours beyond two decimals refused, never rounded (US-3) | passed, 9.9 s | passed, 6.3 s |
| 25.5 | «Баг»: repro steps and system info under «Описание»; «Серьёзность» «3 — Средняя» by default; points, severity and hours saved together (US-3, US-4, US-5) | passed, 6.8 s | passed, 8.2 s |
| 25.6 | «Приоритет» on the backlog row and the kanban card, changed on the form (US-6) | **failed**, 6.0 s | passed, 13.9 s |

None of file 25 had run before this run. Files 01–24 (57 tests) passed in
the first run, including 22, 23 and 24, which read the form, the backlog and
the board feat-022 changed; they were not re-run after the spec fix, since
nothing but file 25 changed.

**Stand record.** The run printed `e2e global setup: warm stand —
http://127.0.0.1:5100/health was green at the first check`.
`src/frontend/e2e-results/stand.json` (gitignored) reads `"stand": "warm"`,
`"waitedMs": 6`, `"checkedAt": "2026-10-09T10:35:45.660Z"`. **The run was
warm.** As in feat-026's runs, the stand was brought up from nothing, but
`Mustask.Api` had already reached the realm before the suite's first check
(the stand answered at 10:35:17 and the seed ran before the suite), so this
run did not exercise the cold-start window of 05-frontend § 7. Whether to
repeat it on a stand whose first check is red is the orchestrator's call; if
it is not repeated, this is a deviation for the gate-3 summary and the MR's
`## Deliberately left open`.

### Failures and their classification

One test failed in the first run, and it is a **spec defect**.

**25.6 (line 375).** `expect(headers[headers.indexOf('Статус') + 1]).toBe('Приоритет')`
received `undefined`. `headers` is read with `allInnerTexts()`, which does
not wait for anything, straight after `page.goto` to the backlog; the page
was still loading, so the list was empty, `indexOf('Статус')` was -1 and
`headers[0]` is `undefined`. The product is right: the failure's own
screenshot (`test-failed-1.png`, taken a moment later) shows the backlog of
«Истории» with the column «Приоритет» right after «Статус», and the values
the test had set, 1 for the story and 3 for the bug. That is US-6's first
scenario ("the backlog shows a «Приоритет» column") as the story and the api-
contract word it. The test failed in 6.0 s, before any of its later
assertions ran.

The fix (`59f1447`, `test(e2e): 25.6 waits for the backlog headers before
reading them`): one assertion before the read,
`await expect(table.getByRole('columnheader', { name: 'Статус' })).toBeVisible()`,
which waits for the table's header row (all headers draw in one render).
Re-run of file 25: 6 of 6 passed; 25.6 passed in 13.9 s, reading the column,
both rows' values on the backlog, both cards' third lines and the changed
value on both again.

### Defects

None in the product: no test failed because of it.

### Specs changed

`src/frontend/e2e/specs/25-work-item-fields.spec.ts`, 2 lines (`59f1447`),
as above. Otherwise the spec files are as the frontend developer committed
them (`87abca2`).

### What was not checked

- **The refusals the server alone enforces.** In a browser: a target date
  before the start (25.1), text that is not a number (25.3) and hours beyond
  two decimals (25.4). The rest — a date outside 2000–2099, a number over
  9 999 or a non-integer where an integer is due, a value outside a list, a
  priority outside 1..4, a field of another type (`field_not_on_type`), the
  100 000-character limit, an emptied «Серьёзность» or «Приоритет» — are the
  component tests and the backend's integration tests only (US-2, US-3, US-4,
  US-5).
- **Existing items get the defaults (US-5).** The stand started from an
  empty database and every item was created after the migration, so the
  back-fill of items that existed before `AddWorkItemFormFields` is covered by
  the migration test in `Mustask.Infrastructure.Tests` only.
- **What the save sends (US-1).** "Only the fields the user changed" and
  "nothing to save" are asserted by the component tests; the E2E reads the
  stored values after a reload, not the request body.
- **Closing a task leaves its remaining work as entered, and no field
  changes another (US-3)**: backend integration tests only.
- **Widths.** 1440 px, 1279 px and 640 px on an epic's form (25.1) only; the
  other four types are read at 1440 px. Exactly 1280 px, the other widths of
  the shell (1024, 1023) and a screen reader (the `region`, `combobox` and
  `toHaveAccessibleDescription` assertions are the browser's accessibility
  tree, not a reader's speech) were not checked.
- **Loading and error states.** The form's per-column skeleton, the
  backlog's «Приоритет» skeleton, a failed read and a stale write are
  component tests; the E2E never holds a request back.
- **«Приоритет» on nested backlog rows and on the other levels.** 25.6 reads
  the flat «Истории» backlog and the kanban of «Истории»; a nested row under
  an epic and the «Эпики» and «Фичи» levels are in the component tests.
- **"Nothing else changes" (US-1, US-5, US-6)**: stood on files 19, 22, 23
  and 24 passing unchanged, not on a pixel comparison.
- **A second user changing the same item** (a stale revision) is feat-021's.
- Access is mod-002 D-a (everyone inside an active organization): there is
  no denied path to test before `mod-016` (D-3).
- A cold-stand run (above).
