# tech-065-mod019-review-debts

- type: tech
- size: S
- branch: tech/065-mod019-review-debts
- stage: 04-backend
- epic: core/org-access
- deps: mod-019
- gates: gate-3

## Gates

gate-3: pending

## Plan

- D-5 (backend): integration tests for the grant-change races of mod-019 US-1..US-3 (a membership removal racing a grant, a node move racing the inherit switch on the locked row, a grant removal during a role save) and the refusal log line; real Postgres via Testcontainers.
- G-1 (frontend): `src/test/scopeGuard.test.ts` also catches a destructuring of a permission-constants object (`const { x } = fooPermissions`); a negative fixture proves it.
- G-2 (frontend): per mod-019 design § 2 and § 7 the team grants section drops the project/department info notice, and the team page announces «Ваш доступ в команде «…» изменился.» on a codes difference.
- G-3 (frontend): the switch-off dialog's given-grants table caps at 6 visible rows and scrolls (D-12, up to 50 rows); loading draws `FieldsSkeleton`.
- G-4 (frontend): the inherit switch does not show the read-only fact while the parent's read is pending (a skeleton instead).
- Check: component tests per item; `bash scripts/verify.sh` green; QA and an Opus review in separate sessions; E2E NO (standing).

## Decisions

- E2E: standing answer NO (the human's standing answer; not asked, not run)

## Log

- 2026-11-10 [new-task] task started; branch checked out in the session worktree `.claude/worktrees/kanban-test-case-2c0b58` (new-task.sh's own worktree removed: the session cannot rebind)
- 2026-11-10 [orchestrator] route confirmed by the human in chat: tech, S, 04 → QA → 06 → QA → 08 review → gate-3 → PR; queue line taken (sync PR #120)
- 2026-11-10 [backend-developer] D-5: `ScopedAccessRacesTests` (8 race tests on commit gates and Postgres locks: give vs removal both orders, role change vs revoke both orders, move vs switch both orders, a move out of the manager's department refuses the waiting switch); no defect, no production change. Refusal log line already pinned by `RefusalLogTests`; the 500 ms seed not run (pinned by `ScopedAccessStatementCountTests`). File landed in bddd8a3c
- 2026-11-10 [frontend-developer] G-1 guard matches destructuring (bddd8a3c); G-3 table `maxRows={6}` (new `TableCard`/`DataTable` prop) and `FieldsSkeleton` (871105d7); G-2 no info notice on the team section, team codes notice pinned by a test (680e4b6d); G-4 `deciding` skeleton while the parent read is pending (109cda63). `ScopedAccessDebts.test.tsx` (8) + scopeGuard fixtures; 6 fail with the fixes reverted
- 2026-11-10 [orchestrator] `bash scripts/verify.sh` → exit 0 (green: backend build, tests incl. integration, coverage floors, frontend lint, build, affected tests, guards)

- 2026-11-10 [qa-engineer] light QA: plan covered, affected tests green (frontend 522, backend 10); `VERIFY_FULL=1 bash scripts/verify.sh` -> exit 1 on load-induced flakes outside the diff (workitems 60 s timeouts, AggregateViewVolume 2 s), re-runs alone green; no blocker/major, 3 minors in review.md; E2E: standing answer NO

## Notes

Summary (from the queue line or an argument): mod-019's QA debts accepted at gate-3: D-5 concurrency of grant changes not exercised by a test; G-1 the BR-SA-18 source guard is blind to destructuring; G-2 no codes-changed notice on the team page; G-3 the switch-off impact table is not capped; G-4 the switch flickers while the impact read loads
