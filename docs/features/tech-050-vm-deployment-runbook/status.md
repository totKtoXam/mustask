# tech-050-vm-deployment-runbook

- type: tech
- size: S
- branch: tech/050-vm-deployment-runbook
- stage: gate-3
- epic: platform/deploy
- deps: tech-024,tech-025
- gates: gate-3

## Gates

gate-3: approved (human, chat 2026-11-03)

## Plan

One new file, `docs/deploy/vm.md`, written from the code (Keycloak/Session options, realm import, ADR-0033/0034, local-migrations runbook). Docs only, no runtime code.

## Log

- 2026-11-03 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/tech-050-vm-deployment-runbook

## Notes

Summary (from the queue line or an argument): Runbook docs/deploy/vm.md: deploy MUSTASK on one plain VM (no Docker/k8s) against the company's shared Keycloak realm: env vars, reverse proxy + TLS under one domain, migrations step, systemd unit, and the exact client/role spec for the Keycloak admin
- 2026-11-03 [orchestrator] route confirmed by the human in chat ("go"): type tech, size S, light cycle (docs-only: no QA, no review session; verification is `scripts/verify.sh`; the review is the human seeing the diff in the PR). Queue line added by `scripts/queue.sh add` (PR #51, merged); the number is 050.
- 2026-11-03 [orchestrator] binding: the empty task worktree made by new-task.sh was removed with the human's yes (chat form) and this session's worktree was switched onto the task branch, as for tech-024.
- 2026-11-03 [orchestrator] `docs/deploy/vm.md` written. Facts taken from the code: `Cors__AllowedOrigins__0` is read only in Development, so the runbook says it is not needed under one domain; there is no ForwardedHeaders code, so the runbook sets `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`; session cookie keys live in the service user's home. ADR-0034 is named in plain text (not linked) because it reaches main with #48. Not executed on a real VM (stated in the document).
