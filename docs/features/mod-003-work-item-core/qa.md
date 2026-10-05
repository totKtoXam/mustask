# QA: mod-003-work-item-core

By D-1 (the short route) there are no QA sessions for this task: the
developers' tests, `scripts/verify.sh` and the E2E run below stand in for
them. This section records commands and facts, not a test plan, so it has no
coverage matrix and no verdict.

## Frontend — E2E results, 2026-10-05

A task-executor run on branch tip d428cb9, worktree of its own, database
`mustask_mod003` on the external PostgreSQL (port 5432), with the stand lock
held from the reset to the stop. Times are UTC.

| Step | Command | Result |
|------|---------|--------|
| 1 | `bash scripts/stand-reset.sh --yes`, `ConnectionStrings__mustask` naming `mustask_mod003` | exit 0. Database dropped and created empty; Keycloak store `mustask-keycloak-5c9b758fa966ffbd` "not present", so the pair started from nothing |
| 2 | `dotnet ef database update --project src/backend/Mustask.Infrastructure`, same variable | exit 0: InitialCreate through AddWorkItems (20261005100819) applied |
| 3 | `bash scripts/warm-worktree.sh` | node_modules copied from the cache, no `npm ci` |
| 4 | `dotnet run --project src/backend/Mustask.AppHost`, default launch profile, same variable, started 12:49:34 | log: "Database: external PostgreSQL", "Keycloak store: volume mustask-keycloak-5c9b758fa966ffbd (pairs with localhost:5432/mustask_mod003)", "the live realm matches the realm import", `mustask-e2e` and `e2e-fixture` provisioned. The wait polled the realm, `/alive` and the panel, not `/health`. All three answered 200 at 12:51:08, 94 s after the start |
| 5 | `bash scripts/keycloak-seed.sh` (`KEYCLOAK_ADMIN_PASSWORD`, `MUSTASK_PASSWORD`, `MEMBER_PASSWORD` from AppHost's user secrets), 12:51:39 | exit 0: `mustask` → «Администраторы», `member` → «Сотрудники / Участники» |
| 6 | `cd src/frontend && E2E_ALLOW_DOCKER=1 npm run e2e`, 12:51:46 | exit 1: 49 tests in 21 files, **47 passed, 2 failed, 0 skipped, 0 flaky**, 549 s. Failed: 19.5, 21.7. Specs 22.1, 22.2 and 22.3 passed on the first run |
| 7 | re-run 1 of the failed tests only, after the two spec fixes: `E2E_ALLOW_DOCKER=1 npx playwright test --grep "19\.5 \|21\.7 "`, 13:04:31 | exit 0: 2 passed, 23 s. The second allowed re-run was not needed |
| 8 | `VITE_API_BASE_URL=http://verify.invalid npm run build` (`tsc -b` covers `e2e/`), Prettier check and ESLint on both changed specs | exit 0 each |
| 9 | stand stopped: `taskkill //PID <AppHost> //T //F`, then the leftover container `mustask-e2e-keycloak-5010` removed with `docker rm -f` | ports 3000, 5000 and 5010 free, no `dotnet`/`dcp` process left, lock released. `mustask_mod003` left as it is |

**Totals after the re-run:** 49 tests in 21 files, 49 passed, 0 failed,
0 skipped. Specs 5 and 6 ran because Docker control was allowed.

**Stand record**, `src/frontend/e2e-results/stand.json` after step 6. The
run started at 12:51:46, so this `checkedAt` belongs to it:

```json
{
  "stand": "warm",
  "waitedMs": 13,
  "url": "http://127.0.0.1:5000/health",
  "checkedAt": "2026-10-05T12:51:51.204Z"
}
```

After step 7 the record read `"stand": "warm"`, `"waitedMs": 5`,
`"checkedAt": "2026-10-05T13:04:33.709Z"`. **The run was warm.** The stand
was brought up from nothing, but `Mustask.Api` had already reached the realm
before the suite's first check (the seed ran in between). So this run did not
exercise the cold-start window of 05-frontend § 7. The brief allowed one run.
Whether to repeat it once on a second stand from nothing is the
orchestrator's call. If it is not repeated, this is a deviation for the
gate-3 summary and the MR's `## Deliberately left open`.

**22.1 and the assignee picker.** The search for «e2e-fixture@» found
«E2E Fixture» on the first run. AppHost provisions that user at stand start,
before the suite runs. No timing question arose.

### Failures and their classification

| Test | Classification | Evidence | Done |
|------|----------------|----------|------|
| 19.5 keyboard: skip link, tab order with a visible focus ring, menus (`19-shell-in-a-browser.spec.ts`) | spec defect, stale assumption | The tab stops on «Главная» read «…, Главная, Рабочие элементы, Настройки». The spec listed nine stops ending «Главная, Настройки, Свернуть меню». This task's entry «Рабочие элементы» is in the work group after «Главная» once a project is chosen (spec § Navigation; `app/navigation/model.ts`), and the spec was written before it existed | Both tab-order lists in 19.5 now carry «Рабочие элементы» after «Главная»: «Главная» has 9 → 10 stops, «Языки» 17 → 18. Passed in re-run 1 |
| 21.7 the selection is remembered across a reload… (`21-organizations.spec.ts`) | spec defect, timing | After a reload of `/home` the switcher showed «Организация E2E 1514e34b» (the spec's own `keep`, first by name), with no fallback notice. The trace explains it. The spec left the organization's «Общие» as soon as the organization label matched. The organizations read answered at 13:00:15.814 and the teams read of the addressed project started at 15.974, and the navigation aborted that teams read (`GET …/teams`, status -1). `useOrganizationsContextSource` stores an address sync only once every level is read (`settled`; mod-002 design § 3.4), so nothing was remembered. In mod-002's run the race went the other way | Before leaving, the spec now waits for the team level «Команда: Пример проекта». Nothing it asserts changed. Passed in re-run 1 |

No stand flake, and no product defect: every failure was the spec's own.

### Defects

None.

### Specs changed

- `src/frontend/e2e/specs/19-shell-in-a-browser.spec.ts`, 19.5: added
  «Рабочие элементы» after «Главная» to both expected tab orders, and raised
  the counts 9 → 10 and 17 → 18. The reason is in the comment beside it.
- `src/frontend/e2e/specs/21-organizations.spec.ts`, 21.7: added an
  `expect(…'Команда:'…).toHaveAttribute('aria-label', 'Команда: Пример проекта')`
  before `page.goto('/home')`. The reason is in the comment beside it.
