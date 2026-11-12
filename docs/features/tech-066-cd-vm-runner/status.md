# tech-066-cd-vm-runner

- type: tech
- size: S
- branch: tech/066-cd-vm-runner
- stage: 04-backend
- epic: platform/ci
- deps: -
- gates: gate-3

## Gates

gate-3: pending

## Plan

CD for the one production VM: a release tag deploys itself through a self-hosted runner on the VM. The repository is public, so a fork PR must never reach that runner.

1. `.github/workflows/deploy.yml`, triggered only by `push: tags: [v*]` and `workflow_dispatch` (input `tag`; from `main` or a `v*` tag only). Two jobs. `build` runs on `ubuntu-latest`: `deploy/vm/build-release.sh <tag> ${{ vars.MUSTASK_SITE_URL }}`, then the tarball and its sha256 go up as an artifact. `deploy` runs on `[self-hosted, linux, mustask-prod]` with `environment: production`: it downloads the artifact, checks the sha256, then `sudo -n mustask-cd deploy < tarball` and `sudo -n mustask-cd smoke`. One `concurrency` group, no cancel. Actions are pinned by SHA. The build stays on a hosted runner, so the VM carries no .NET SDK, Node or npm cache, and no repository code runs on it outside the release.
2. `deploy/vm/mustask-cd.sh`, installed as root-owned `/usr/local/sbin/mustask-cd`. It reads the tarball from stdin, checks the listing (one top directory `mustask-<tag>`, nothing absolute, no `..`, a matching `VERSION`), then runs that release's `kit/install.sh deploy`. `smoke` runs `/opt/mustask/current/kit/smoke.sh` with the flags recorded in root-owned `/etc/mustask/cd-smoke-args`. Stdin keeps the sudoers rule free of wildcards: exactly `mustask-cd deploy` and `mustask-cd smoke`.
3. `deploy/vm/runner-setup.sh` (root, once per VM). It creates the unprivileged user `mustask-runner`, installs the wrapper, writes the sudoers file and checks it with `visudo -cf`, and writes the smoke flags. It downloads the runner of the given version and checks it against the given sha256. It registers the runner with the label `mustask-prod`, the token read from a prompt (never argv, never stored), and installs the runner as a systemd service. A re-run on a registered runner refreshes only the wrapper, sudoers and flags. Both scripts join the release kit.
4. Runbook: `docs/deploy/vm.md` § 11 "Continuous deployment" and the same change in `vm.ru.md`. It covers the repository settings (fork PR approval for all outside collaborators, the `production` environment with the human as required reviewer and `main` + `v*` as deployment refs, tag ruleset for `v*`, runner group limited to `deploy.yml` where the plan allows), the variable `MUSTASK_SITE_URL`, the exact commands for the VM, and the trust model.

Check: `scripts/tests/cd-runner-cases.sh`. It tests the wrapper's listing and flag validation (the script is sourced) and asserts that `deploy.yml` has no pull_request trigger, that only the deploy job targets the self-hosted labels, and that `ci.yml` stays on `ubuntu-latest`. verify.sh runs it when these files change, and runs actionlint when it is on PATH. A throwaway ubuntu:24.04 container walks runner-setup (runner part skipped) and `sudo -n` from the runner user: the two allowed commands pass, everything else is refused. `scripts/verify.sh` must be green in CI.

## Log

- 2026-11-10 [orchestrator] The human confirmed the route on 2026-11-10 in the session that started this one: type tech, size S, route queue add → plan → dev → QA → review → gate-3 → PR. Gate-1 and gate-2 have no subject. Choice already made by the human: a self-hosted runner on the VM, not SSH from a hosted runner, so no SSH key lives in GitHub secrets. Queue add: PR #131.
- 2026-11-12 [orchestrator] Take: PR #134. The human repeated that verify.sh runs only in GitHub Actions. Locally: cd-runner-cases.sh (36/36), shellcheck and actionlint (Docker images, both clean). CI's verify.sh is the gate.
- 2026-11-12 [orchestrator] Rehearsal in a throwaway ubuntu:24.04 container (removed): runner-setup.sh with the runner marked registered, then sudo -n as mustask-runner. 33/33: the two mustask-cd commands run as root; another subcommand, an argument, bash, install.sh, -E, another user and junk are refused. Not walked: a real registration.
- 2026-11-12 [qa] Light QA: no blocker or major, 2 minors in review.md; 17 cases added (36 to 53, mutations of deploy.yml now caught); shellcheck and actionlint clean; vm.md and vm.ru.md agree.
- 2026-11-12 [review] Round 1 on 005e201e: changes requested. Open: 1 blocker (CR-1: an approved fork PR can target the VM runner and get root via mustask-cd deploy; runbook says it cannot), 3 minors. Findings in review.md.
- 2026-11-12 [orchestrator] Round 1 fixed (see review.md, "Fixes after round 1"): CR-1 by a job-started hook on the VM (runner-job-guard.sh + a root-owned systemd drop-in), CR-2 to CR-4 and QA M1. Locally: cd-runner-cases.sh 73 cases, shellcheck, actionlint, rehearsal 53/53. Next: a delta review.
- 2026-11-12 [review] Round 2 (delta) on 5a629ab6: approved. CR-1 to CR-4 resolved; open: 1 minor (CR-5: runner-owned .env can unset the hook; runbook overstates). Findings in review.md.

## Decisions

- E2E: not applicable (no UI change). Recorded by QA, the human was not asked.
