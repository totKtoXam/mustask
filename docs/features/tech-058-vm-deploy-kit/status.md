# tech-058-vm-deploy-kit

- type: tech
- size: M
- branch: tech/058-vm-deploy-kit
- stage: gate-3
- epic: platform/ci
- deps: tech-056
- gates: gate-3

## Gates

gate-3: pending

## Plan

Part 1, the kit, in `deploy/vm/` (no runtime code touched):

1. `build-release.sh <tag> <site-url>` — builds the tag in a throwaway worktree: `dotnet publish` of Mustask.Api, the panel with `VITE_API_BASE_URL`, an EF migrations bundle (linux-x64, self-contained) so the VM needs no SDK; one tarball with the kit and a `VERSION`.
2. `mustask.env.template` (no secrets), `mustask-api.service`, `nginx-mustask.conf` as real files; `.gitattributes` keeps them LF.
3. `install.sh prepare|nginx|deploy|rollback|status` — the machine once (explicit `--install-packages`, `--create-db` with a generated password written into the env file, never printed), the site, then each release: bundle → unit → `current` symlink → restart → wait for `/health`; `previous` kept for rollback.
4. `smoke.sh` — the browser-free part of section 8, the rest printed as a checklist.
5. `vm.md` / `vm.ru.md` rewritten around the kit.
6. Rehearsal on a throwaway Ubuntu 24.04 container with systemd against a hand-built realm, as tech-056 did.

Part 2, the deployment to the company VM, waits for the human: SSH access, the host name, the Keycloak values; every irreversible step on the server is confirmed first.

Check: the rehearsal passes; `scripts/verify.sh` in CI green.

## Rehearsal findings

| # | Step | What happened | Fix |
|---|---|---|---|
| 1 | build | `dotnet ef migrations bundle` stops: the design-time factory throws without `ConnectionStrings__mustask` | The build passes a password-less placeholder string; building the bundle never connects |
| 2 | build | A Windows checkout writes CRLF (`core.autocrlf=true`); a CRLF env file or unit would put `\r` into every value on Linux | `deploy/vm/* text eol=lf` in `.gitattributes`; the build strips `\r` from the kit files too |
| 3 | prepare | `--create-db` right after `apt-get` found PostgreSQL stopped (the container's `policy-rc.d` blocks the package's start) and the role check failed silently into the create branch | `prepare` runs `systemctl enable --now postgresql` and stops with a clear message if it cannot reach it; `nginx` does the same for nginx |
| 4 | deploy | The placeholder check matched `CHANGE_ME` in a comment of the template, so `deploy` would refuse forever | Only non-comment lines count |
| 5 | deploy | `wait_health` printed a curl error for every second the API was still starting | Silent retries |

Rehearsed on 2026-11-05: Ubuntu 24.04 container with systemd, Keycloak 26.4 `start-dev` on HTTPS with a rehearsal CA, realm `company` built by `kcadm` exactly as section 1 asks (two clients, four client roles, the audience mapper, nothing else). Release `v0.1.0` = `8aed774b` built on Windows (Git Bash). On the box: CA → `prepare --install-packages --create-db` → secret into the env file by hand → `nginx` → `deploy` (all migrations applied by the bundle, `/health` healthy) → `smoke.sh --resolve-local` passed (7/7). Then `deploy` of the same build under a second tag, `rollback`, a redeploy of `v0.1.0` in place, smoke again passed. Refusals checked: `deploy` without a release dir, `deploy` with `CHANGE_ME` left. The DB password and the client secret appear neither in the service journal nor in `ps`. Not rehearsed here: a scripted sign-in (tech-056 did it on the same release line and the same nginx site), an update with a new migration.

## Log

- 2026-11-05 [orchestrator] started from the parent session (queue line via PR #81). The human confirmed «⭐ M, как предложено»: tech, M, Dev → QA → Review → gate-3 → PR, then Part 2; and «⭐ Да, Ubuntu 24.04» for the VM.
- 2026-11-05 [orchestrator] binding: this session's worktree `.claude/worktrees/mod-018-build-9e3722`, its branch renamed to `tech/058-vm-deploy-kit` and reset to `origin/main` `eb191942`; new-task.sh was not run (it makes a worktree this session cannot use). Queue take: PR #82.
- 2026-11-05 [orchestrator] kit written, rehearsal on a throwaway stand (containers `t058-vm`, `t058-kc`, network `t058-net`, no host ports; secrets in the scratchpad). Findings 1–5 fixed. Human's DB, stand and Keycloak volume untouched.
- 2026-11-05 [qa-engineer] single QA pass on e210a170: not ready, 2 majors (DB password in sed argv; a site failing `nginx -t` left enabled) and 15 minors (`qa.md`).
- 2026-11-05 [orchestrator] fixed both majors and minors 3–15: password via awk/ENVIRON, the site disabled again on a failed test and cert paths made absolute, `--create-db` writes the env file first and creates role and database separately, `previous` is only a release that turned healthy, redeploy by rename, rollback checks, flag values, quoted env values, site-vs-release check, tag and site-url validation, `+dirty` kit commit, `/out/` ignored, smoke false pass on a curl failure; the port 5000 documented as fixed (16). 17 (no pruning) accepted as documented. Re-rehearsed on a rebuilt stand: two `prepare` runs, nginx with a broken cert (not enabled) and relative paths, deploy, site mismatch refused, a failed release then a fix (`previous` stays good), rollback refuses onto itself, smoke passed. Stand removed.
- 2026-11-05 [code-reviewer] review round 1 on 0e19fbbb: needs-fixes, 3 majors and 7 minors open (`review.md`).
- 2026-11-05 [orchestrator] QA majors re-checked by the orchestrator on the rebuilt stand (entry above), not by a second QA session: the M cap is one QA pass. Review F1–F10 fixed: the temp env file 0600 and removed on exit, `pwd -P` for the release path, `--install-packages` installs only missing packages with `--no-upgrade`, the password kept out of the server log, the nginx site restored on a failed re-run, quote stripping in smoke, site URL without a port, host/realm/cert/tag validation, `out/` at the repo root; docs F3/F4/F6 reworded. Checked on a VM container: both `prepare` runs, a bad host refused, nginx bad-over-good restored, deploy through `/opt/mustask/current` keeps the release, tag `..` refused. Stand removed.
- 2026-11-05 [code-reviewer] review round 2 on 55647772: approved, F1–F10 fixed, 0 findings open (`review.md`).
- 2026-11-05 [orchestrator] review round 2 approved; stage gate-3, PR opened for the human's gate-3 decision. Part 2 waits for the human.
