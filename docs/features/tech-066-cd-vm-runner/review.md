# tech-066-cd-vm-runner: review

## QA

Light S-route QA (2026-11-12, clean session, commit 6a82fc00). Verdict: no
blocker, no major; two minors. Plan items 1-4 are covered: the wrapper's
listing and flags by `cd-runner-cases.sh`, the workflow guard by the same
file, the sudoers surface by the recorded container rehearsal, the runbook by
a line-by-line read against the scripts (paths, modes 0755/0440/0644, the
sudoers line, flags, group list) and an en/ru comparison (no drift).

Checks run: `sh scripts/tests/cd-runner-cases.sh` (36/36 before, 53/53 after
the added cases), shellcheck (`-e SC2015`) on the wrapper, runner-setup and the
cases file, actionlint on deploy.yml with `.github/actionlint.yaml`: all clean.

Mutation probe on a scratch copy of the tree. Caught from the start:
`pull_request:` key, `on: [push, pull_request]`, `on: {…pull_request…}`, a
quoted `'on':`, `runs-on: self-hosted` in a second deploy.yml job, another
workflow with `self-hosted` or `mustask-prod`, an expression `runs-on` in
ci.yml, an extra `push: branches`, a checkout in the deploy job, the removed
production environment, `cancel-in-progress: true`. NOT caught by the original
file (now pinned, see "Tests added"): a second deploy.yml job with
`runs-on: mustask-prod` (no `self-hosted` word) or `runs-on: {group: …}`; the
removed `if:` main / v* guard; `sudo -n bash -c …` in place of the smoke
command; the removed sha256 check; a per-ref concurrency group; `inputs.tag`
inlined into a `run:` script; write permissions; `needs: build` removed.

### Backend

- minor: `deploy/vm/mustask-cd.sh:99-103`. `tar -tzf` checks member names only;
  a symlink or hardlink member (`mustask-v1/kit -> /etc`, then
  `mustask-v1/kit/x`) is extracted as root without a link check. Not a boundary
  crossing today: whoever hands the wrapper a tarball already gets root through
  the release's `install.sh` (the file's header and vm.md 11.4 say so), so the
  listing check is defence against a mistake, not an attacker. Debt: either say
  so in the comment at line 34, or add `--no-overwrite-dir` and refuse non-regular
  members via `tar -tvzf`.
- minor: not walked by anyone (status.md Log says so): real registration, the
  runner's systemd service (does `sudo -n` work inside it; the unit sets no
  NoNewPrivileges today, but a runner upgrade could), and a real run of
  deploy.yml with the production approval. The first tag after setup is that
  check, and 11.2 says so. Failure scenario: the service unit blocks setuid and
  every deploy fails with `sudo: effective uid is not 0`; nothing is installed,
  so the cost is a failed first release, not an outage.

### Frontend

Not applicable (no UI change).

### Tests added (by QA)

`scripts/tests/cd-runner-cases.sh`, 36 -> 53 cases: listing refuses a tag
starting with a dash and a trailing `..` member; deploy refuses a symlink as
the release and a release without `kit/install.sh`, propagates a failing
install.sh, and removes the stage after a refusal; `cmd_smoke` passes the
recorded flags to smoke.sh, refuses a bad flags file and a missing live
release; deploy.yml: exactly two `runs-on` lines (ubuntu-latest and the VM),
`needs: build`, the main / v* ref guard, the concurrency group name,
`contents: read` permissions, the sha256 check, sudo limited to the two
wrapper commands, `inputs.` only through env.

## Review Round 1

Stage 8, clean session, commit 005e201e (diff `origin/main...HEAD`). Verdict:
**changes requested** (1 blocker, 3 minors). Checked and sound: sudoers
argument matching (exact args, `env_reset`, no SETENV so `-E` is refused,
stdin passes through); the wrapper's root-owned 0700 stage (no TOCTOU against
the runner user); `${{ }}` reaches `run:` only through `env:`; permissions;
all five action pins resolve to the tagged commits (`gh api`, 2026-11-12);
`ACTIONS_RUNNER_INPUT_TOKEN` is read by the runner's `CommandSettings.cs`
(prefix `ACTIONS_RUNNER_INPUT_`, cleared after reading; checked on GitHub) and
`runuser -u` without `-l` keeps the environment (util-linux, from memory);
`fetch-depth: 0` brings the tags `build-release.sh` needs; dotnet-ef on PATH
through `$GITHUB_PATH`; artifact name/path round trip; smoke runs as root
through the wrapper. `sh scripts/tests/cd-runner-cases.sh`: all passed.

**CR-1 — blocker — `docs/deploy/vm.md:586-589, 696-700` (vm.ru.md:589-592,
700-704); no enforcement on the VM.** An approved fork PR runs the workflow
files *of the PR* (`pull_request` uses the merge ref). A fork that adds
`on: pull_request` + `runs-on: [self-hosted, linux, mustask-prod]` to any
workflow (and edits `cd-runner-cases.sh` to match, or simply does not care
that CI goes red afterwards) gets its job on the VM the moment the owner
clicks "Approve and run"; the job then runs `sudo -n /usr/local/sbin/mustask-cd
deploy < own.tar.gz`, whose `kit/install.sh` runs as root. A user-owned repo
has no runner group to restrict workflows, and the `production` environment
fences only jobs that name it. The runbook tells the owner the opposite —
"Even approved, it runs only `ci.yml`", "No pull request reaches the runner …
cd-runner-cases.sh enforces all of this in CI" — so a routine approval of an
outside PR is root on production. Fix: (a) enforce on the VM, outside
anything a PR controls: a root-owned job-started hook
(`ACTIONS_RUNNER_HOOK_JOB_STARTED` in the runner's `.env`, written by
runner-setup.sh) that exits non-zero unless `GITHUB_REPOSITORY` is the repo,
`GITHUB_EVENT_NAME` is `push` or `workflow_dispatch` and `GITHUB_WORKFLOW_REF`
is `<repo>/.github/workflows/deploy.yml@refs/heads/main` or `…@refs/tags/v*`
(the hook's env vars: from memory, confirm in the runner docs), plus a case
for it; (b) rewrite 11.1 item 1 and 11.4 in both languages: approving a fork
PR run is granting it the runner unless (a) holds, read its `.github/` diff
first; the cases file is a guard against our own mistakes, not against a
fork.

**CR-2 — minor — `deploy/vm/runner-setup.sh:126`.** Registration is the
only idempotency key: if `config.sh` succeeded and `svc.sh install`/`start`
(line 166) failed, every re-run says "registered already, left as it is" and
the service is never installed. Fix: after the early-exit check, install and
start the service when `$dir/.service` is missing (or `systemctl is-enabled`
fails), and say so in 11.2.

**CR-3 — minor — `docs/deploy/vm.md:687-688` (vm.ru.md:691-692),
`.github/workflows/deploy.yml:40-42`.** "A second run waits until the first
ends": true for one; with `cancel-in-progress: false` GitHub keeps one
*pending* run per group and cancels the older pending one when a newer run
arrives, so of three quick tags the middle one is never deployed. Fix: say so
in 11.3 (the latest pending tag wins; re-dispatch a skipped one).

**CR-4 — minor — `docs/deploy/vm.md:284` (vm.ru.md:283).** Direction 2:
the section-4 table of `kit/` contents still lists install.sh, smoke.sh, unit,
nginx files and env template; `build-release.sh:135` now also ships
`mustask-cd.sh` and `runner-setup.sh` (and 11.2 relies on that). Fix: add
both to the row in both files.

### Claims checked (item 6)

- vm.md 11 "VM gets no SDK/Node/checkout": deploy.yml:125-160 (no checkout, only download) — holds.
- 11 "may run exactly two commands as root": runner-setup.sh:113 sudoers line — holds (for the runner user's own sudo; see CR-1 for what those two allow).
- 11.1 item 1 "Even approved, it runs only ci.yml": CR-1.
- 11.1 item 2 "a run from any other branch cannot reach the deploy job": deploy.yml:49 `if:` + environment ref rule (GitHub setting) — holds.
- 11.1 item 3 "install.sh deploy refuses a release built for another address": install.sh:378-380 — holds.
- 11.1 item 5 "guard is that only deploy.yml names the label; cases fail CI otherwise": cd-runner-cases.sh:175-205 — holds for same-repo changes, not for a fork's PR run: CR-1.
- 11.2 "download refused if checksum differs": runner-setup.sh:138-139 — holds.
- 11.2 "token never on a command line or in a file": runner-setup.sh:145-161 + CommandSettings.cs env read — holds.
- 11.2 "refuses if the user is in sudo/docker/lxd": runner-setup.sh:95-97 — holds.
- 11.2 "re-run refreshes only wrapper, sudoers, flags; keeps flags without --smoke-args": runner-setup.sh:102-129 — holds (CR-2 is the gap beside it).
- 11.2 service name `actions.runner.<owner>-<repo>.<name>.service`: svc.sh naming (from memory) — not verifiable here, plausible.
- 11.3 "failed smoke fails the run": deploy.yml:159-160, `set -e` default shell — holds.
- 11.3 "runner has no right to run rollback": sudoers line runner-setup.sh:113 — holds.
- 11.3 "Two runs never overlap … never cancelled halfway": deploy.yml:40-42 — holds; pending replacement: CR-3.
- 11.4 "No pull request reaches the runner": CR-1.
- 11.4 "wrapper accepts only one mustask-<tag>/ with matching VERSION, fresh root-owned dir": mustask-cd.sh:37-69, 92-111 — holds.
- Troubleshooting rows (sudo password, refused, cd-smoke-args): mustask-cd.sh:100-110,117 — hold.
- mustask-cd.sh header "nothing else: no other argument": mustask-cd.sh:122-130 + sudoers exact args — holds.
- Direction 2: behaviour changed = kit contents (`build-release.sh` kit list), new CD path. Grepped `self-hosted`, `deploy.yml`, `kit/`, `mustask-cd`, `runner-setup` under docs/, .github/, AGENTS.md files: one stale hit, vm.md:284 / vm.ru.md:283 (CR-4); no other prose describes CD or the kit list. Not swept: ci.yml's own comments (file untouched, no CD claim expected).

### Fixes after round 1 (author)

- **CR-1:** a job-started hook on the VM. `deploy/vm/runner-job-guard.sh`,
  installed by runner-setup.sh as root-owned
  `/usr/local/libexec/mustask-runner/job-started.sh`, is set as
  `ACTIONS_RUNNER_HOOK_JOB_STARTED` by a root-owned systemd drop-in of the
  runner unit. It fails every job unless the repository matches
  `/etc/mustask/cd-repository`, the event is `push` or `workflow_dispatch`,
  and `GITHUB_WORKFLOW_REF` is that repository's
  `.github/workflows/deploy.yml` at `refs/heads/main` or `refs/tags/v*`. A
  missing variable is a refusal. The runbook's 11.1 item 1, item 5, 11.2,
  11.4, 11.5, a troubleshooting row and the "does not verify" bullet say so,
  in both languages. Cases: 17 `job` and 3 `hook` cases in
  cd-runner-cases.sh.
- **CR-2:** the service step runs on every run: `svc.sh install` when
  `$dir/.service` is missing, then the drop-in and a restart when it changed
  or the unit is down.
- **CR-3:** 11.3 says a newer run replaces a waiting one; push tags one at a
  time, re-run a cancelled one.
- **CR-4:** the `kit/` row lists the three section 11 scripts.
- QA M1: a comment in mustask-cd.sh says why member types are not checked.

Checked locally: cd-runner-cases.sh (73 cases), shellcheck and actionlint
(Docker), and the container rehearsal, 53/53 (guard and drop-in with
`systemctl` and `svc.sh` stubbed). verify.sh runs in CI only.

## Review Round 2 (delta)

Stage 8 delta, clean session, commit 5a629ab6 (diff `2423a8d5..5a629ab6`).
Verdict: **approved** (CR-1..CR-4 resolved; 1 new minor, a debt).
`sh scripts/tests/cd-runner-cases.sh`: all passed. The guard also run by hand
under the runner's own shell line (`bash --noprofile --norc -e -o pipefail`):
tag push 0, `refs/heads/mainx` 1, pull_request 1, no variables 1.

Runner behaviour **confirmed in actions/runner source** (`gh api`, 2026-11-12):
`JobExtension.cs:301-315` reads `ACTIONS_RUNNER_HOOK_JOB_STARTED` from the
worker's process env and adds the hook *before* container setup steps; only
`PrepareActionsAsync` (downloads, no execution) precedes it. `JobHookProvider`
runs it through `ScriptHandler` with `bash` for `.sh` (`HostContext.cs:812`),
and `ScriptHandler.cs:291-299` sets `GITHUB_*` from the github context
allowlist (`GitHubContext.cs`: `event_name`, `repository`, `workflow_ref`)
*after* any other env, so a workflow's `env:` cannot spoof them. Step name
"Set up runner" (`Constants.cs:245`) matches the troubleshooting row.
`RunnerService.js:50-58` passes the service env to the listener.
**Inferred, not confirmed:** GitHub fills `workflow_ref` as
`<repo>/<path>@<ref>` for push/dispatch and `refs/pull/N/merge` for PRs.

Guard logic: pull_request, pull_request_target, workflow_run, schedule,
merge_group all fail the event check; the path compare is exact (a
`deploy.yml` in another directory or repository fails); `main` is matched
exactly; a fork cannot push tags or dispatch. CR-1 is closed for its threat.

**CR-5 — minor — `docs/deploy/vm.md:728-729` (vm.ru.md:730-731),
`deploy/vm/runner-setup.sh:152`.** `Runner.Listener/Program.cs:176-194` loads
`$dir/.env` with `Environment.SetEnvironmentVariable` on start, after systemd's
env, and `runsvc.sh:7-9` exports `PATH` from `$dir/.path`; both files are owned
by `mustask-runner`. A line `ACTIONS_RUNNER_HOOK_JOB_STARTED=` in `.env`
unsets the hook at the next restart, without touching the root-owned drop-in.
Not reachable by a fork today (only a job that already passed the guard runs
as that user), so the guard holds; but "mustask-runner can change neither"
reads as "cannot disable the guard", which is not so. Fix: `chown root:root
$dir/.env $dir/.path` (mode 0644, the runner only reads them) after
registration, or say in 11.4 that the runner user can unset the hook through
`.env` and the guard assumes no foreign code ever ran as it.

- CR-1: resolved (above; runbook 11.1 item 1, item 5, 11.4 rewritten in both).
- CR-2: resolved, runner-setup.sh:185-208 installs the service when `.service`
  is missing and restarts on a changed drop-in or a stopped unit.
- CR-3: resolved, vm.md:702-706 / vm.ru.md 11.3.
- CR-4: resolved, vm.md:284 / vm.ru.md:283 (and build-release.sh:135 ships the guard).
- QA M1: comment at mustask-cd.sh:102-104.

### Claims checked (item 6, delta)

- 11.1 item 1 "guard fails any such job before its first step": JobExtension.cs:304-315 + guard — holds.
- 11.1 item 5 "the guard does what a group would": guard repo/workflow/ref checks — holds.
- 11.2 paths, owners, modes of guard, repository file, drop-in: runner-setup.sh:120-127, 197-203 — hold.
- 11.2 "restarts the service when the drop-in is new or changed": runner-setup.sh:200-206 — holds (also when the unit is down).
- 11.2 re-run "keeps the registration, installs the service if missing": runner-setup.sh:146-192 — holds.
- 11.3 "a third run replaces a waiting second": GitHub concurrency semantics — holds.
- 11.4 three conditions, "missing variable is a refusal": runner-job-guard.sh:26-57 — holds.
- 11.4 "mustask-runner can change neither": CR-5.
- Troubleshooting row "fails in Set up runner": Constants.cs:245 + guard:61 — holds.
- Uninstall block removes guard, repository file, drop-in: matches runner-setup.sh paths — holds.
- "Does not verify" bullet: matches the Log's rehearsal line — holds.
- vm.ru.md: every changed paragraph read against vm.md — same content.
- Direction 2: changed behaviour = the job guard and the kit list. Grepped `runner-job-guard`, `job-started`, `cd-repository`, `ACTIONS_RUNNER_HOOK`, `mustask-prod` under docs/, .github/, .claude/, scripts/, deploy/, AGENTS.md files: hits only in files of this diff plus `.github/actionlint.yaml` (label list) and verify.sh:833-835 (comment now accurate, the cases include the guard). None stale.
- Tests: 17 `job` cases cover each refusal branch; 3 `hook` cases cover `main()` and the repository file. They run under plain bash, not the runner's `-e -o pipefail`; checked by hand above.
