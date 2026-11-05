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

## Log

- 2026-11-05 [orchestrator] carried over from the tech-056 session by the human's choice (relayed by doc-012: «⭐ Сессия tech-056», order «⭐ Сначала tech-057, потом деплой»). Queue take: PR #84.
- 2026-11-05 [orchestrator] route: tech, L (thresholds: an ADR; sign-in cookie and callback paths change). The human chose «⭐ L, ADR вместо spec»: no spec, gate-2 has no subject (no screens). E2E: no.
- 2026-11-05 [orchestrator] binding: worktree `.claude/worktrees/unruffled-banach-65f877`, branch `tech/057-panel-api-sub-path` from origin/main 0f53bec8.
- 2026-11-05 [orchestrator] ADR-0037 proposed (path base in config, nginx passes the path, location-only snippet for the portal); plan above. Stage gate-1.

## Notes

Summary (from the queue line): serve MUSTASK under a sub-path (https://portal.devexpert.kz/mustask), not only a domain root. This means:
- a configurable path base for the API (UsePathBase; the session cookie path and the OIDC redirect/callback addresses follow it);
- Vite base and the router basename for the panel;
- returnUrl and copied links relative to the base.

docs/deploy/vm.md gets the sub-path nginx layout and is rehearsed again. An ADR records the path base.
