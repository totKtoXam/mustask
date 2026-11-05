# tech-059-deploy-run-as-user

- type: tech
- size: S
- branch: tech/059-deploy-run-as-user
- stage: 04-backend
- epic: platform/deploy
- deps: tech-057
- gates: gate-3

## Gates

gate-3: pending

## Plan

The deploy kit's service account is a parameter instead of the hardcoded `mustask`.

1. `install.sh prepare --run-as <user>`. The default is `mustask`, created as before. Any other account must already exist; it is never created, and root is refused. The env file goes to that user's group, `/var/lib/mustask` goes to the user, and the choice is kept in `/etc/mustask/run-as`.
2. `install.sh deploy` renders `User=`/`Group=` of `mustask-api.service` from `/etc/mustask/run-as`, and stops if that user is gone.
3. `vm.md` and `vm.ru.md`: sections 2 and 6.

Check: a throwaway Ubuntu 24.04 container with systemd. `prepare` runs with the default and with `--run-as` an existing user. The refusals for a missing user and for root are checked. The unit is rendered and passes `systemd-analyze verify`. `scripts/verify.sh` is green in CI.

## Log

- 2026-11-06 [orchestrator] started from the parent session after tech-057 merged (#85, main `4142e9de`). The human confirmed the route «⭐ S, как предложено»: tech, S, Dev → container check → one review → gate-3 → PR. This is not the light cycle, because `deploy/` is outside its directories. Queue take: PR #90. Found by tech-058 Part 2: on portal.devexpert.kz the human chose «Оставь ubuntu».
- 2026-11-06 [orchestrator] kit changed; checked on a throwaway ubuntu:24.04 container (systemctl, curl, dotnet and efbundle stubbed). Refused: a missing user, root, a bad name. Default prepare creates `mustask` (env root:mustask 0640). A second prepare with `--run-as ubuntu` moves env and state to ubuntu and records it. Deploy renders User=/Group=ubuntu and `systemd-analyze verify` passes. Deploy with the user removed is refused. Container removed.
- 2026-11-06 [code-reviewer] review on 4ca4aa51: changes requested (3 major, 3 minor open; review.md Round 1)
- 2026-11-06 [orchestrator] review F1–F6 fixed: F1 a re-run of prepare keeps the recorded account; F2 `chown -R` of the state dir; F3 section 5 names the service account's group; F4 Group= dropped (systemd takes the primary group), the group name is checked, deploy re-checks the run-as name and refuses root; F5 the cost of a shared account in both runbooks; F6 header grammar. Re-checked on a throwaway container: a re-run keeps ubuntu, switching account moves the keys, deploy renders User= only, `systemd-analyze verify` passes, root and a bad name in run-as refused. Container removed.
