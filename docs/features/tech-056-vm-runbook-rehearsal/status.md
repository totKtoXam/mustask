# tech-056-vm-runbook-rehearsal

- type: tech
- size: S
- branch: tech/056-vm-runbook-rehearsal
- stage: complete
- epic: platform/ci
- deps: tech-050
- gates: gate-3

## Gates

gate-3: approved (human, chat 2026-11-05: «Посмотрю и солью»; the merge itself is the human's)

## Plan

Walk `docs/deploy/vm.md` literally on a clean Linux box standing in for the VM (an Ubuntu 24.04 container with systemd as PID 1) against a separate Keycloak whose realm is set up by hand exactly as section 1 asks (two clients, four client roles, nothing else). Sections 2–8 in order, deploying main as it is. Every divergence goes into the table below and into a fix of `vm.md`; "What this runbook does not verify" is reworded to what is still unverified. Runtime code is not touched: a code defect found here becomes its own queue line. Check: section 8 passes on the box, `scripts/verify.sh` (CI) green.

## Divergences

| # | Section | What the runbook says | What happened | Fix |
|---|---|---|---|---|
| 1 | 2 | "Install the .NET 10 runtime, PostgreSQL, nginx", no commands | On Ubuntu 24.04 all three are in the distribution archive (`aspnetcore-runtime-10.0` 10.0.14, PostgreSQL 16, nginx 1.24); `/usr/bin/dotnet` links to `/usr/lib/dotnet` | The `apt-get install` line added |
| 2 | 2 | Silent on the Keycloak certificate | The API calls Keycloak over HTTPS and checks the chain against the system store; the rehearsal's self-signed Keycloak certificate had to go into the VM's CA store | A paragraph: install the company CA on the VM before the first start; a troubleshooting row |
| 3 | 3 | Run the SQL; no word on how | On a local PostgreSQL it is `sudo -u postgres psql` | Said in the text |
| 4 | 3 | `dotnet ef database update` from a checkout | Fresh checkout: `NETSDK1004 project.assets.json not found`, the tool never reached the database; `dotnet-ef` is not a local tool of the repo | `dotnet tool install --global dotnet-ef` and `dotnet restore` added; a troubleshooting row |
| 5 | 3 | "network access to the database" | A packaged PostgreSQL on the VM listens on 127.0.0.1 only; the build machine cannot reach it | An SSH port-forward recipe (the rehearsal used a temporary port mapping instead; the SSH form is listed as unverified) |
| 6 | 4 | Copy `publish/api/` | Works from a Windows build machine (framework-dependent); `publish/` is not in `.gitignore` and shows up untracked | Both said in the text |
| 7 | 7 | `listen 443 ssl;` + `http2 on;` | nginx 1.24 (Ubuntu 24.04): `unknown directive "http2"`, `nginx -t` fails | `listen 443 ssl http2;` with a comment for 1.25.1+; a paragraph and a troubleshooting row |
| 8 | 7 | `location /api/` without buffer settings | **502** on `POST /api/session/callback` for every user who gets a session; nginx: "upstream sent too big header" (the session cookie alone is ~3.4 KB on a bare realm) | `proxy_buffer_size 32k; proxy_buffers 8 32k; proxy_busy_buffers_size 64k;` with the reason; a troubleshooting row |
| 9 | 1 | Silent on how the sign-in talks to Keycloak | The API uses PAR and `response_mode=form_post`; both are on by default in Keycloak 26.4 | One line in section 1: nothing to switch on, do not disable them |
| 10 | 8 | "The role takes effect" left implicit; no Evaluate step | Checked: only-`user` reads dictionaries (200) and is refused organization creation (403); `user` + `organization-creator` gets 201; no token 401 | Step 4 added (roles, Evaluate as the diagnosis); step 5 says how to see member search |
| 11 | "does not verify" | "has not been executed" | Executed now | Reworded to what is still unverified: the company network and realm, a browser sign-in, the SSH tunnel, bundle/upgrade/rollback, a sub-path |
| 12 | 1 | Silent on how the first organization appears | In a shared realm no account is created: an existing employee gets all four `mustask-api` client roles. Checked on an empty database: that user creates the first organization (201), finds a `user`-only colleague and adds them as a member (201); the colleague then sees the organization; a `user`-only account is refused creation (403) | A "first administrator" bullet in section 1; section 8 step 4 uses that user |
| 13 | top | Silent on the address shape | The target address is a sub-path (`https://portal.devexpert.kz/mustask`); the release only works at a host root (Vite base, router basename, API paths, cookie path, redirect addresses) | A paragraph at the top: own host name until tech-057; a line in "does not verify" |

## Log

- 2026-11-05 [orchestrator] started from the parent session (queue line via PR #76). The human chose «⭐ S, лёгкий цикл»: tech, S, docs only, no QA/review sessions; standing approval for this task.
- 2026-11-05 [orchestrator] binding: this session's worktree `.claude/worktrees/unruffled-banach-65f877`, its branch renamed to `tech/056-vm-runbook-rehearsal`; new-task.sh was not run (it makes a worktree this session cannot use). Queue take: PR #77.
- 2026-11-05 [orchestrator] stand: Docker container `ubuntu:24.04` + systemd as the VM, Keycloak 26.4 `start-dev` on HTTPS, own network/ports, self-signed certs, secrets in the scratchpad. Human's DB, stand and volumes untouched.
- 2026-11-05 [orchestrator] realm `company` by hand via the Admin REST API (no import); Evaluate gave `aud` mustask-api, `resource_access.mustask-api.roles`, name, email. MUSTASK added no realm roles, groups or default roles.
- 2026-11-05 [orchestrator] sections 2–8 on `main` 927cf1d7: divergences 1–11; section 8 passed (sign-ins scripted through the real login form — the built-in browser refuses self-signed certs). PR #78, Auto-fix on.
- 2026-11-05 [orchestrator] scope addition via the doc-012 session: sub-path target and the first admin. Sub-path needs auth-touching code, so L. The human chose «B: под-путь отдельной L»: tech-057 queued (PR #80); tech-056 stays S.
- 2026-11-05 [orchestrator] second rehearsal on a rebuilt stand: the first admin (all four roles) created the first organization and added a colleague; divergences 12–13. Stand removed.

## Notes

Summary (from the queue line): VM runbook rehearsal: walk docs/deploy/vm.md step by step on a clean Linux (WSL2 or a container standing in for the VM) against a separate Keycloak whose realm is set up by hand exactly as section 1 asks (two clients, four client roles, nothing else), deploy main as it is, pass section 8's smoke check, fix the runbook wherever reality differs; the request to the Keycloak admin goes out in parallel.
