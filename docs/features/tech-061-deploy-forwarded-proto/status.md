# tech-061-deploy-forwarded-proto

- type: tech
- size: S
- branch: tech/061-deploy-forwarded-proto
- stage: review
- epic: platform/deploy
- deps: -
- gates: gate-3

## Gates

gate-3: pending

## Plan

The `X-Forwarded-Proto` that the kit's shared-host snippet sends becomes a parameter instead of the hardcoded `$scheme`.

1. `install.sh nginx --path /<path> --forwarded-proto https|http|scheme`. The default is `scheme`, which is nginx's own `$scheme`. The choice is kept in `/etc/mustask/forwarded-proto`, so a re-run without the flag keeps it. With `--host` (a whole site that ends TLS itself) the flag is refused.
2. `nginx-mustask-locations.conf`: `@FORWARDED_PROTO@` replaces `$scheme`.
3. `vm.md` and `vm.ru.md`, section 7.2: when to pass `https`.

Check: run `install.sh nginx --path` on a throwaway Ubuntu 24.04 container with nginx (systemctl stubbed). Cases: the default, `https`, a re-run, the include with a reload, back to `scheme`, refusals, and an empty state file. `scripts/verify.sh` must be green in CI.

## Log

- 2026-11-06 [orchestrator] The human chose «⭐ nginx-параметр в tech-058». Queue add: PR #97. The human confirmed the route «⭐ S с ревью». This is not the light cycle, because `deploy/` is outside its directories. Found by tech-058 Part 2: the portal's TLS ends on an external proxy.
- 2026-11-06 [orchestrator] Kit changed and checked on a throwaway ubuntu:24.04 container with nginx 1.24 and systemctl stubbed. The default gives `$scheme`; `https` is written and kept on a re-run; an include of the snippet reloads; `scheme` restores the default. Refused: a bad value, the flag with `--host`, a missing value. An empty state file falls back to `$scheme`. The container is removed.
