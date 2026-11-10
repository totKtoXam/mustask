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

## Log

- 2026-11-10 [new-task] task started; branch checked out in the session worktree `.claude/worktrees/kanban-test-case-2c0b58` (new-task.sh's own worktree removed: the session cannot rebind)
- 2026-11-10 [orchestrator] route confirmed by the human in chat: tech, S, 04 → QA → 06 → QA → 08 review → gate-3 → PR; queue line taken (sync PR #120)

## Notes

Summary (from the queue line or an argument): mod-019's QA debts accepted at gate-3: D-5 concurrency of grant changes not exercised by a test; G-1 the BR-SA-18 source guard is blind to destructuring; G-2 no codes-changed notice on the team page; G-3 the switch-off impact table is not capped; G-4 the switch flickers while the impact read loads
