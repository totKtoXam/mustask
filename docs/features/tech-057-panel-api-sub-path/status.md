# tech-057-panel-api-sub-path

- type: tech
- size: L
- branch: tech/057-panel-api-sub-path
- stage: 04-backend
- epic: platform/ci
- deps: tech-056
- gates: gate-1, gate-3

## Gates

gate-1: approved (human, chat 2026-11-05: «⭐ Принять ADR и план»)
gate-3: pending

## Plan

The decision is [ADR-0037](../../adr/0037-serve-under-a-configured-path-base.md): one configured path base, where empty means the root. It is checked against `Session:PanelUrl` at start, and nginx passes the path unchanged. There is no spec (no user-facing behaviour changes) and no design (no screens), so gate-1 approves the ADR and this plan. Work after gate-1:

1. **API** (`Mustask.Api`):
   - `Hosting:PathBase` read and validated at start;
   - `UsePathBase` first in the pipeline;
   - the session cookie path follows the base.
   - Tests: the callback `redirect_uri` and the cookie path under `/mustask`; root behaviour unchanged; a malformed base or a mismatch with `PanelUrl` refuses to start.
2. **Panel** (`src/frontend`):
   - `VITE_BASE_PATH` → Vite `base`;
   - the router `basename` from `BASE_URL`;
   - `returnUrl` and the `/signed-out` check relative to the base;
   - the copied work-item link with the base.
   - Tests cover the root and `/mustask`.
3. **Kit** (`deploy/vm/`, the tech-058 list in the notes):
   - `build-release.sh` accepts `https://<host>/<path>` and passes the base to the build;
   - the env template gets `Hosting__PathBase` and the `PanelUrl` with its path;
   - a location-only nginx snippet for a shared server, and `install.sh nginx` installs it;
   - `smoke.sh` handles the prefix.
4. **Docs:**
   - `vm.md` + `vm.ru.md`: the sub-path layout, the Keycloak addresses with the prefix, the "sub-paths not supported" text removed;
   - `src/backend/AGENTS.md` / `src/frontend/AGENTS.md` if a command changes.
5. **Rehearsal:** the tech-056/058 stand (an Ubuntu 24.04 systemd container and Keycloak 26.4) with a server block that plays the portal. The release is built with the kit at `https://<portal>/mustask`, and section 8 passes under the prefix.
6. **QA** (one agent), **review** (one Opus session), then gate-3 and a squash PR with Auto-fix.

Check: the rehearsal passes; `scripts/verify.sh` in CI green; the Aspire stand and CI at the root unchanged.

## Rehearsal findings

Stand `t057`: an Ubuntu 24.04 systemd container plays the VM and runs nginx as the portal (its own `/`, 80→443 keeping the path). Keycloak 26.4 runs on HTTPS with a realm set up by hand. The release is `build-release.sh` at 5e7e2e35 for `https://portal.t057.localhost/mustask`, walked per vm.md 7.2. All checks pass:
- smoke;
- sign-ins: the cookie path is `/mustask/api/session`; the admin gets 201 with `Location` under `/mustask`; the `user`-only colleague gets 200/403; the no-role user gets `/mustask/?authError=no_access`; sign-out lands on `/mustask/signed-out`;
- the deep link works, the portal's `/` is untouched, `/mustask/health` is not exposed;
- 139 API tests (Session, KeycloakOptions, HealthEndpoint, PathBase).

| # | Where | What happened | Fix |
|---|---|---|---|
| 1 | `build-release.sh` | On Git Bash, `VITE_BASE_PATH=/mustask` reached node as `C:/Program Files/Git/mustask`. The panel came out blank. | `MSYS_NO_PATHCONV=1` on the npm build line only. The build fails unless `index.html` loads `<base>/assets/`. |
| 2 | `smoke.sh` | The panel check (`id="root"`) passed on that blank build. | A check fetches the first `<script src>` and expects 200 JavaScript. It fails on the broken build. |
| 3 | vm.md § 4 | `MSYS_NO_PATHCONV` set for the whole script breaks its `git -C /c/...`. | Said in vm.md / vm.ru.md. |

`mustask-panel` addresses that worked:
- root URL `https://<host>/mustask`;
- redirect URI `https://<host>/mustask/api/session/callback`;
- post-logout redirect URI `https://<host>/mustask/*`;
- web origins empty.

## Log

- 2026-11-05 [orchestrator] carried over from the tech-056 session by the human's choice (relayed by doc-012: «⭐ Сессия tech-056», order «⭐ Сначала tech-057, потом деплой»). Queue take: PR #84.
- 2026-11-05 [orchestrator] route: tech, L (thresholds: an ADR; sign-in cookie and callback paths change). The human chose «⭐ L, ADR вместо spec»: no spec, gate-2 has no subject (no screens). E2E: no.
- 2026-11-05 [orchestrator] binding: worktree `.claude/worktrees/unruffled-banach-65f877`, branch `tech/057-panel-api-sub-path` from origin/main 0f53bec8.
- 2026-11-05 [orchestrator] ADR-0037 proposed (path base in config, nginx passes the path, location-only snippet for the portal); plan above. Stage gate-1.
- 2026-11-05 [orchestrator] gate-1 approved by the human: «⭐ Принять ADR и план»; ADR-0037 accepted. Stage 04: API, panel and kit, one agent each, on disjoint files.

## Notes

Summary (from the queue line): serve MUSTASK under a sub-path (https://portal.devexpert.kz/mustask), not only a domain root. This means:
- a configurable path base for the API (UsePathBase; the session cookie path and the OIDC redirect/callback addresses follow it);
- Vite base and the router basename for the panel;
- returnUrl and copied links relative to the base.

docs/deploy/vm.md gets the sub-path nginx layout and is rehearsed again. An ADR records the path base.
- 2026-11-05 [orchestrator] rehearsal of the sub-path layout on stand t057: all checks pass; 3 kit/runbook divergences fixed (blank panel from a Git Bash build). Stand removed. Next: QA, then review.
