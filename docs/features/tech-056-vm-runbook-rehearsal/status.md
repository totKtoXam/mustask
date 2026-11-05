# tech-056-vm-runbook-rehearsal

- type: tech
- size: S
- branch: tech/056-vm-runbook-rehearsal
- stage: gate-3
- epic: platform/ci
- deps: tech-050
- gates: gate-3

## Gates

gate-3: pending

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
| 11 | "does not verify" | "has not been executed" | Executed now | Reworded to what is still unverified: the company network and realm, a browser sign-in, the SSH tunnel, bundle/upgrade/rollback |

## Log

- 2026-11-05 [orchestrator] started from the parent session (tech-056 queue line via PR #76). Route shown, the human chose «⭐ S, лёгкий цикл»: tech, S, light cycle (docs only; no QA, no review session; the review is the human seeing the diff in the PR). Standing approval for this docs-only S task recorded here.
- 2026-11-05 [orchestrator] binding: this session's worktree `.claude/worktrees/unruffled-banach-65f877`, its branch renamed to `tech/056-vm-runbook-rehearsal`; new-task.sh was not run (it makes a worktree this session cannot use). Queue take: PR #77.
- 2026-11-05 [orchestrator] rehearsal box: Docker (no WSL distro on the machine besides docker-desktop). `t056-vm` = stock `ubuntu:24.04` + systemd as PID 1 (privileged, own cgroup namespace), ports 80/443 and 127.0.0.1:15433 (temporary, for migrations); `t056-keycloak` = `quay.io/keycloak/keycloak:26.4` `start-dev`, HTTPS 18444, no volume; network `t056-net` 172.30.56.0/24. Names `mustask.t056.localhost` / `keycloak.t056.localhost`, self-signed certificates. Secrets generated into the session scratchpad, never printed. The human's Postgres 5432, the stand 3100/5100/5010 and its Keycloak volume were not touched.
- 2026-11-05 [orchestrator] realm `company` set up by hand through the Admin REST API (no realm import): `mustask-api` (confidential, every flow off, roles user / organization-creator / dictionaries-admin / system-admin), `mustask-panel` (confidential, standard flow, PKCE S256, exact redirect URI, empty web origins, post-logout `https://mustask.t056.localhost/*`), audience mapper on the access token only. Default scopes came out exactly as section 1 lists; the `roles` scope's client-roles mapper puts `resource_access.${client_id}.roles` into the access token unchanged. MUSTASK added no realm roles, groups or default roles. Users alice (user + organization-creator), carol (user), bob (none). Evaluate (generate-example-access-token, the console's Evaluate) for alice: `aud` [mustask-api, account], `resource_access.mustask-api.roles` [user, organization-creator], name and email present.
- 2026-11-05 [orchestrator] sections 2–8 walked in order on `main` 927cf1d7; divergences 1–11 above. Section 8: `/health` Healthy; panel and a deep link 200, http→https 301; alice and carol signed in through the real Keycloak login form → form_post callback → panel, cookie `mustask.session` Secure, HttpOnly, path `/api/session`; bob came back with `authError=no_access` and no cookie; carol 200 on languages, 403 on organization creation; alice 201; alice finds carol in member-candidates; no token 401. The built-in browser refuses the self-signed certificate (ERR_CERT_AUTHORITY_INVALID), so the sign-ins ran as a script inside the VM, not in a browser. After the fixes the nginx block was re-extracted from the edited `vm.md` and installed verbatim: `nginx -t` ok, sign-ins pass again.

## Notes

Summary (from the queue line): VM runbook rehearsal: walk docs/deploy/vm.md step by step on a clean Linux (WSL2 or a container standing in for the VM) against a separate Keycloak whose realm is set up by hand exactly as section 1 asks (two clients, four client roles, nothing else), deploy main as it is, pass section 8's smoke check, fix the runbook wherever reality differs; the request to the Keycloak admin goes out in parallel.
