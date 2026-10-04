# tech-007-review-loop-economy

- type: tech
- size: S
- branch: tech/007-review-loop-economy
- stage: complete
- epic: workflow
- deps: -
- gates: gate-3

## Gates

gate-3: approved · standing approval by the human 2026-10-04 (D-2), light cycle · local squash via scripts/finish-task.sh

## Plan

1. `docs/ai-workflow/stages/06-code-review.md`, "Every finding": the loop economy made a rule — blocker/major → one fix + a delta round (QA re-run only over runtime/test changes, none for prose fixes); minor → a debt ("Deliberately left open", a queue line), never a round; a finding that waits on a decision → `blocked-on-decision`, no round until the orchestrator records it; at most 2 fix rounds; the L security pass follows the same rules.
2. `04-backend.md` § 5 item 3 and `05-frontend.md` item 5: the same economy for QA defects (blocker/major fixed and re-checked at most twice; minor → gate-3 debt).
3. `docs/conventions/git.md`, "Parallel work": reading roles (review, security pass, QA that adds no test, requirements review) work on a detached checkout and hand their sections to the orchestrator; only a session that changes code holds the branch.
4. `scripts/warm-worktree.sh` + notes in `src/frontend/AGENTS.md` and `git.md`: a machine-wide `node_modules` cache keyed by the lock file's hash, copied into a fresh worktree instead of `npm ci`.
5. Check: `bash scripts/verify.sh` (docs-only S, light cycle); the script run once on this machine with an empty cache and once with a warm one.

## Decisions

- D-1 Route: tech, S, docs-only (`docs/`, `scripts/`, `src/frontend/AGENTS.md`; no runtime code) under standing approval → the light cycle (s-route.md § "Light cycle"): no QA and no review sessions, verification is `scripts/verify.sh`, review is the diff in the squash. Gates: gate-3 only.
- D-2 Standing approval (human, 2026-10-04, in chat: «останови все текущие работы, выполни свои изменения сразу вне ai-workflow, чтобы быстро применить, затем запусти работы уже по новой ai-workflow»): the human asked for these process changes to be applied at once; the agent does them on this branch and merges via scripts/finish-task.sh. The changes are the four items the agent proposed in chat and the human saw before answering; nothing else is in scope.
- D-3 Why not a direct commit to main: the pre-commit hook allows only queue files on main and `--no-verify` is forbidden (AGENTS.md); the light cycle is the fastest path the repository itself permits.

## Log

- 2026-10-04 [orchestrator] gate-3 approved under the standing approval (D-2); stage: complete; squash via scripts/finish-task.sh
- 2026-10-04 [orchestrator] changes done in 92ce020; `bash scripts/verify.sh` — exit 0, RESULT: green (docs, scripts and AI-workflow checks); scripts/warm-worktree.sh tried on this machine: empty cache 1 min 21 s (npm ci + caching), warm cache 32 s (copy), identical listings
- 2026-10-04 [orchestrator] standing approval for this docs-only S task recorded (D-2); plan and route (D-1, D-3); queue line removed from main
- 2026-10-04 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/tech-007-review-loop-economy

## Notes

Summary (from the queue line or an argument): Review and QA loops spend rounds on minors, on findings that wait for an orchestrator decision, and on reading roles holding the branch; worktrees rebuild node_modules from scratch. Make the loop rules explicit (minor = debt, decision = stop, delta re-check), let reading roles work detached, add a shared node_modules cache for agent worktrees
