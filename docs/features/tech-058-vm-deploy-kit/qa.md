# QA — tech-058-vm-deploy-kit

## Backend (kit: shell + config, single QA pass of an M)

Commit `e210a170`, branch `tech/058-vm-deploy-kit`. A skeleton-style `tech` task: a
checklist, not a coverage matrix (04-backend.md, "Run economy").

### Test plan (written from the plan and the queue line, before reading the code)

| # | Check | How |
|---|---|---|
| 1 | Scripts parse; LF endings; `.gitattributes` applies to the kit | `bash -n`, `git check-attr`, `file` |
| 2 | install.sh: argument handling, re-runs, `prepare` on an existing env file, `--create-db` with the role present, `deploy` of the current tag, `rollback` without `previous`, `set -e` pitfalls | read + extracted-logic throwaways (never run as root) |
| 3 | Secrets: never printed, not in argv/ps, env file 0640 root:mustask, nothing committed | read, `git grep` |
| 4 | smoke.sh: false pass / false fail | run against a fake env (`MUSTASK_ENV_FILE`) with nothing listening |
| 5 | build-release.sh: tag not commit-ish, cleanup on failure, site-url validation | read, throwaway checks |
| 6 | vm.md vs vm.ru.md: code blocks, headings, numbers, flags, paths, section refs vs the scripts | scripted diff + read |
| 7 | verify.sh classification of `deploy/vm/*` | read |

Not run (by instruction): release build, stand, Docker, install.sh as root, full verify.sh.
E2E: the human's standing answer is NO (not asked, recorded here).

### Results

Pass: all three scripts parse; kit files are LF (`eol: lf` through `deploy/vm/*`); no secret in
the repo (`CHANGE_ME` placeholders only); `placeholders` ignores comments and prints keys only,
never values; `prepare` leaves an existing env file and re-applies 0640 root:mustask; the
role-exists branch touches nothing; `deploy` of the already-current tag does not overwrite
`previous`; `rollback` without `previous` dies cleanly; the two vm docs have identical code blocks
(translated comments only), identical headings 1-10, same numbers and flags; section references in
the scripts, unit, template and site match vm.md (4, 5, 6, 7, 8); smoke exits 1 and prints the
checklist when nothing is up; the verify.sh `shell` bucket covers `deploy/vm/*.sh` (syntax + exec bit).

### Defects

No blockers. Two majors (sent back); the rest are minors (carried to gate-3 as debt).

| # | Sev | Where | Defect and reproduction |
|---|---|---|---|
| 1 | major | install.sh:118 | The generated DB password is in **argv**: `sed -i "s\|...Password=$pw\|..." "$env_file"`. The comment at :113, vm.md section 3 and status.md say it never shows in `ps`; any local user reading `ps`/`/proc/*/cmdline` in the moment sed runs sees it. Repro: run `prepare --create-db` while `while :; do ps -eo args \| grep -F Password=; done` runs. Fix: keep the secret out of argv (read it from the environment inside awk/sed, or write the line by redirect). |
| 2 | major | install.sh:145-150 | `nginx` enables the site (`ln -sfn`) **before** `nginx -t`. If the test fails (e.g. relative `--cert fullchain.pem`: passes `[ -f ]` at :141 against cwd, nginx resolves it against /etc/nginx) the script exits with the broken site left in `sites-enabled`; the next reload, reboot or package restart takes nginx down, with every other site on the box. Repro: `install.sh nginx --host h --cert fullchain.pem --key key.pem` from the directory holding them. Fix: on `nginx -t` failure remove the symlink and restore the previous file; require absolute paths. |
| 3 | minor | install.sh:149 | `systemctl enable --now nginx >/dev/null 2>&1` under `set -e`: if nginx cannot start (port 80 taken) the script exits 1 with no message. status.md finding 3 says `nginx` "does the same" (clear message); only `prepare` does. |
| 4 | minor | install.sh:189-191 | `previous` is overwritten by a release that then fails `/health`. Deploy X (fails), then fixed Y: `previous` = X, so `rollback` goes to the broken one, not the last healthy. Set `previous` only after `wait_health` passes, or keep a last-good link. |
| 5 | minor | install.sh:171-176 | Redeploy of the current tag does `rm -rf "$target"` on the live release: `current/api` and `current/panel` are missing between `rm` and `mv`; a failing step after that (migration bundle) leaves new files under the old process. Docs call it "replaced in place". Swap through a `.new` + symlink or refuse for the active tag. |
| 6 | minor | install.sh:206-214 | `rollback` does not check `previous` still exists (docs tell the operator to delete old releases by hand) nor that `current` exists; with a dangling target it repoints `current`, restarts, fails health and leaves `current` dangling. Add `[ -d "$prev" ]` first. |
| 7 | minor | install.sh:105-120 | `--create-db` is not atomic: role created, then `CREATE DATABASE` or the `sed` fails -> a re-run says "role exists", never writes the password, and `deploy` stays on `CHANGE_ME` with the password lost. The `has_role` test ignores a missing database. The doc only says "if the role already exists, nothing is touched"; add the recovery (`ALTER ROLE`) or check the database too. |
| 8 | minor | install.sh:64-68, 133-137 | A flag as the last argument (`prepare --host`) dies with the shell's `$2: unbound variable` (set -u) instead of the `die` message. Reproduced on an extracted copy of the parser. |
| 9 | minor | install.sh:45,182 | `env_value` returns raw text; systemd strips surrounding quotes from `EnvironmentFile` values. A quoted `ConnectionStrings__mustask="..."` starts the service but reaches `efbundle` with the quotes (migration fails). Reproduced on an extracted copy. Doc is silent on quoting. |
| 10 | minor | install.sh:158-161, build-release.sh:102-108 | `VERSION` records `site=` but `deploy` never compares it with `Session__PanelUrl`: build for one host, `prepare` with another, and the panel calls the wrong origin; only the smoke notices. A warning in `deploy` would do. |
| 11 | minor | build-release.sh:31,46-48,111 | The tag is not validated as a plain name: `release/v1` passes the tag check, the whole build runs, then `tar` fails writing `out/mustask-release/v1.tar.gz` (no such directory); the same string becomes `/opt/mustask/releases/<tag>`. A tag that is not a commit dies with git's raw `bad revision`. Validate `^[A-Za-z0-9._-]+$` and `cat-file -t` up front. |
| 12 | minor | build-release.sh:35-41 | `https://` (empty host) and hosts with spaces or a query pass the site-url validation; `https://host//` keeps one slash. |
| 13 | minor | build-release.sh:49,99 | `kit_commit` records HEAD while the kit files come from the working tree: uncommitted edits ship under a clean-looking `kit_commit`. Mark `-dirty` when `git status --porcelain deploy/vm` is non-empty. |
| 14 | minor | build-release.sh:33 / .gitignore | Default out-dir `./out` is not ignored (`git status` shows `?? out/`); a tarball with the panel and a self-contained bundle is one careless `git add` away. Ignore `/out/`. |
| 15 | minor | smoke.sh:198-202, 181-185 | "/health is not exposed" prints `ok` for anything that is not `Healthy*`, including a curl failure: a false pass (reproduced with a fake env and nothing listening: that line says `ok`, the run still exits 1 through the others). The client-route FAIL text says "try_files is not in effect" when the real cause was a connection error. Require HTTP 200 + `id="root"` for the "not exposed" check. |
| 16 | minor | install.sh:52, smoke.sh:163 | `127.0.0.1:5000` is hard-coded although `ASPNETCORE_URLS` is a documented env value: changing it gives a false timeout; any other listener on 5000 gives a false "healthy". Read the port from the env file. |
| 17 | minor | install.sh (whole) | No pruning of old releases (a self-contained bundle per release); documented as manual and accepted; noted only. |

### Not covered / residual risk

An update that applies a new migration, a browser sign-in and the real company realm (already
listed in vm.md, "What this runbook does not verify"). Defects 1, 2, 4 and 5 cannot show on a
happy-path rehearsal, which is why they were missed.

Verdict: **not ready for review** until majors 1 and 2 are fixed; the re-check covers those two fixes
and what they could have broken, nothing else.
