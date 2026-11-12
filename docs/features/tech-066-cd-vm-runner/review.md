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
