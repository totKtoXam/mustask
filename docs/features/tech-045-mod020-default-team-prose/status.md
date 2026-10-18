# tech-045-mod020-default-team-prose

- type: tech
- size: S
- branch: tech/045-mod020-default-team-prose
- stage: complete
- epic: core/backlogs-boards
- deps: mod-020
- gates: gate-3

## Gates

gate-3: pending

## Plan

1. Bring mod-020's delta read (round 2, `ebee5f8f`, pushed after PR #25 was merged) onto main: `git cherry-pick -x`, its own commit. Its `review.md` round 2 and its `status.md` Log line are the only content.
2. CR-5 (major, prose): amend mod-017 `spec.md` flowchart node N3 in place and add an «Amended by mod-020 (2026-10-18)» note under feat-028 `spec.md` BR-OL-7, so that both say what mod-020 built: the project's default team also picks the team the team views' switcher opens when none is chosen; below it BR-OT-11's step stays (the user's own first team, else the first by name; the human's Q-1 A). One note line each in mod-017 and feat-028 `status.md`.
3. mod-020's own records: gate-3 approved in its `status.md` (merged by the human as #25), CR-5 marked resolved in `review.md` round 2, CR-2's rest, CR-3's rest and CR-6 marked "moved to feat-043 (the human, 2026-10-18)".
4. Check: `BASE=origin/main bash scripts/verify.sh` (docs only), exit code into the Log. No `src/` file is touched.

## Decisions

- E2E = NO (standing answer since 2026-10-12); the light cycle does not ask it, since there is no runtime change.
- Amendments are inline, as mod-017 BR-OT-4 and BR-OT-11 carry theirs: the merged sentences keep their words and the note says what changed and when; flowchart node N3 is amended in place, as the queue line asks.
- The feat-028 and mod-017 amendment notes sit in each `status.md` `## Log` as «[dev] note from …» lines, where mod-016 and mod-017 recorded theirs (P-10 notes); neither file has a Decisions entry for amendments.

## Log

- 2026-10-18 [task-executor] stage complete, gate-3 pending: the auto-merge by the orchestrator once CI is green is the merge. Light cycle: no QA or review session, no E2E (no runtime change)
- 2026-10-18 [task-executor] `BASE=origin/main bash scripts/verify.sh` exit 0, RESULT: green (prose bucket only: links in documents intact, backlog ids unique, status.md within its budget, spec.md and its stories hold requirements only). Run over the final tree, status.md included
- 2026-10-18 [task-executor] dev: `ebee5f8f` cherry-picked (`-x`) as its own commit; CR-5 fixed in mod-017 `spec.md` (N3) and feat-028 `spec.md` (BR-OL-7), note lines in both `status.md`; mod-020 `review.md` (CR-5 resolved; CR-2's rest, CR-3's rest and CR-6 moved to feat-043) and `status.md` (gate-3 approved, Q-1 A, Log line). No file under `src/` touched
- 2026-10-18 [orchestrator] route S, LIGHT CYCLE (`docs/ai-workflow/stages/s-route.md`, § Light cycle): docs only, no QA session, no review session; the check is `scripts/verify.sh`, its command and exit code in this Log. E2E is not asked on the light cycle. Taken under the human's standing approval of 2026-10-18 («Ты работай, я спать буду. Пр-ы сам мерджи», then «Включай auto-merge для всех пр-ов»): no route confirmation; the PR is auto-merged once CI is green. The human chose this line and its light cycle at tech-041's and mod-020's gate-3 questions (2026-10-18)
- 2026-10-18 [new-task] task started, worktree a workflow agent's worktree

## Notes

Summary (from the queue line or an argument): mod-020's delta read (round 2, ebee5f8f, pushed after the merge) left CR-5 major, prose only: mod-017 spec.md flowchart node N3 («it only starts new items») and feat-028 spec.md BR-OL-7 («The default starts new items and nothing else») still contradict mod-020's amendment of BR-MX-11 and BR-OT-4 — amend N3, add an «Amended by mod-020» note to BR-OL-7 and a note line in feat-028's status.md; bring the delta read's review.md round 2 and its Log line onto main; record mod-020's gate-3 (merged by the human, 2026-10-18; Q-1 A: the user's own team stays before the first by name; the debts CR-2, CR-3 and CR-6 go to feat-043)
