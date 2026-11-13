# bug-006-smoke-behind-edge-proxy

- type: bugfix
- size: S
- branch: bug/006-smoke-behind-edge-proxy
- stage: 05-qa
- epic: platform/ci
- deps: tech-066
- gates: gate-3

## Gates

gate-3: pending

## Plan

Diagnosis on the VM (2026-11-13, read-only, over Warpgate):

- `/etc/hosts` has the distribution's hostname line `127.0.1.1 portal.devexpert.kz portal`,
  so on the VM the site's name resolves to the VM itself, not to the edge.
- The local nginx (sites `portal-tls`, `portal.devexpert.kz`) listens on 80 and 443:
  443 serves a self-signed `CN=portal.devexpert.kz` (2026-09-30..2036), port 80 answers
  200 without a redirect. Public TLS and the http->https redirect are the edge's.
- The VM reaches the public edge 91.244.102.30: `--resolve portal.devexpert.kz:80:91.244.102.30`
  gives `301 https://portal.devexpert.kz/mustask/`, `:443:` gives 200 with the
  certificate verified.

So nothing is wrong with the deployment; smoke.sh checks the wrong hop.

Plan:

1. `smoke.sh --resolve-to=<IPv4>`: send the site's host name to that address (the
   public edge) for every check through the proxy, exactly as `--resolve-local`
   does for 127.0.0.1. Every check stays as it is, nothing is relaxed; the
   default (no flag) is unchanged. `--resolve-local` and `--resolve-to` together,
   a repeat, or a value that is not a dotted IPv4 address: exit 2.
2. `mustask-cd.sh` `smoke_args_of` and `runner-setup.sh --smoke-args` accept
   `--resolve-to=<IPv4>` (one word, so the flags file stays a word list), at most one.
3. Tests: `scripts/tests/cd-runner-cases.sh` — flag-file cases, and smoke.sh run
   against a stub curl that models this VM (own name -> self-signed local hop,
   edge -> healthy): fails without the flag, passes with `--resolve-to=<edge>`,
   bad flags exit 2 before any request. verify.sh runs the cases when smoke.sh changes.
4. Docs: vm.md sections 7.2, 8, 10, 11.2 and vm.ru.md in the same change.
5. Rollout after merge: re-run runner-setup.sh with `--smoke-args --resolve-to=91.244.102.30`,
   then redeploy a tag (the new smoke.sh comes with the release kit).

## Log

- 2026-11-12 [new-task] task started; new-task.sh made its own worktree, which was removed again and the branch checked out in the worktree this desktop session is bound to (.claude/worktrees/consolidate-project-requirements-1fc945): the session cannot relocate, branch and files are the same
- 2026-11-12 [orchestrator] route confirmed by the human (relayed by the parent session): bugfix, S, take -> VM diagnosis -> plan -> dev -> QA -> review -> gate-3 -> PR; gate-1/gate-2 have no subject; artifacts = this status.md with the plan. Economy mode: one agent per stage, verify.sh in CI only
- 2026-11-12 [orchestrator] queue line taken: PR #141 (auto-merge)

## Notes

Summary (from the queue line or an argument): smoke.sh fails on the production VM although the site is fine (tech-066's first CD run, v0.3.0, run 43265897076): from the VM portal.devexpert.kz reaches a self-signed certificate and http:// answers 200 instead of 301, while from outside the edge proxy (91.244.102.30) serves a Let's Encrypt certificate, redirects http and passes every check. TLS and the redirect are done by a proxy in front of the VM, which smoke.sh does not allow for. First find on the VM where the name resolves and what serves 80/443; then let smoke.sh (and mustask-cd smoke via /etc/mustask/cd-smoke-args) check this layout without weakening the checks for a VM that terminates TLS itself; update vm.md and vm.ru.md
