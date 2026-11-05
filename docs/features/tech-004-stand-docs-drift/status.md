# tech-004-stand-docs-drift

- type: tech
- size: S
- branch: tech/004-stand-docs-drift
- stage: gate-3
- epic: platform/stand
- deps: -
- gates: gate-3

## Gates

gate-3: pending

## Plan

- `src/backend/AGENTS.md`: replace the "missing configuration value" outcome of a fresh clone without the three AppHost secrets with what Aspire 13 does (the parameters are unresolved and the resources that need them do not start).
- `MustaskDbContextFactory.cs` XML comment and the matching comment in `MustaskDbContextFactoryTests.cs`: drop "random port and password"; name `Ports:Db` and the `Parameters:mustask-db-password` user secret, and point to `docs/runbooks/local-migrations.md`.
- ADR-0002, "Local workflow": the same fact, with a dated correction note in the ADR's own style; the decision is unchanged (non-substantive, docs/adr/README.md rule 3).
- Check: `bash scripts/verify.sh` (light cycle; comments only, no runtime change).

## Log

- 2026-11-06 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/tech-004-stand-docs-drift
- 2026-11-06 [orchestrator] route confirmed by the human (question form): S, light cycle (verify.sh, no QA or review sessions, the human reviews the diff at gate-3), accepted with the caveat that two .cs files change in comments only; queue take PR #95
- 2026-11-06 [orchestrator] done in the orchestrator session: four files (AGENTS.md, ADR-0002, factory and its test comments); the test comment was outside the queue line but carried the same random-port wording

## Notes

Summary (from the queue line or an argument): Stand docs drift: the old 'missing configuration value' wording in src/backend/AGENTS.md and the random-port/password wording in MustaskDbContextFactory.cs and ADR-0002 no longer match the fixed ports and secrets (deferred from tech-002 review CR-follow-up)
