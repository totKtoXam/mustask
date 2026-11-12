# bug-006-smoke-behind-edge-proxy

- type: bugfix
- size: S
- branch: bug/006-smoke-behind-edge-proxy
- stage: 04-backend
- epic: platform/ci
- deps: tech-066
- gates: gate-3

## Gates

gate-3: pending

## Plan

<!-- S/M: implementation plan, 3-10 lines. L: see plan-backend.md / plan-frontend.md -->

## Log

- 2026-11-12 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/bug-006-smoke-behind-edge-proxy

## Notes

Summary (from the queue line or an argument): smoke.sh fails on the production VM although the site is fine (tech-066's first CD run, v0.3.0, run 43265897076): from the VM portal.devexpert.kz reaches a self-signed certificate and http:// answers 200 instead of 301, while from outside the edge proxy (91.244.102.30) serves a Let's Encrypt certificate, redirects http and passes every check. TLS and the redirect are done by a proxy in front of the VM, which smoke.sh does not allow for. First find on the VM where the name resolves and what serves 80/443; then let smoke.sh (and mustask-cd smoke via /etc/mustask/cd-smoke-args) check this layout without weakening the checks for a VM that terminates TLS itself; update vm.md and vm.ru.md
