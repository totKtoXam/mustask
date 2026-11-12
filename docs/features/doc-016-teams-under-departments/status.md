# doc-016-teams-under-departments

- type: docs
- size: L
- branch: doc/016-teams-under-departments
- stage: gate-2
- epic: core/org-access
- deps: mod-019
- gates: gate-1, gate-2

## Gates

gate-1: approved (by the human in chat, 2026-11-12)
gate-2: pending

## Plan

No backend/frontend plan: a docs task. Stage 1 (deep) writes `spec.md`,
`stories/` and the decomposition into delivery lines (proposals only, not
queued); stage 2 is one Opus requirements review in a clean context; gate-1;
stage 3 writes `design.md` and an HTML prototype of the cascading header
selectors and the scoped lists; the prototype is clicked through in a browser;
gate-2. The task stops at gate-2: nothing is implemented here.

## Log

- 2026-11-10 [orchestrator] task started on branch doc/016-teams-under-departments, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/magical-hopper-3c2a47 (session bound to it, branch confirmed with git branch --show-current), cut from origin/main 97969aa2 after PR #128 reworded the line.
- 2026-11-10 [orchestrator] route confirmed by the human in chat (relayed by the starting session): docs, L (reworks the hierarchy of feat-028, mod-018/ADR-0031 and mod-019/ADR-0038 with a data migration), BA → requirements review → gate-1 → UX design + HTML prototype → gate-2. Both gates are the human's. Stop after gate-2; the delivery lines are proposals for the human, not queue entries. Economy mode: one agent per stage, one Opus review.
- 2026-11-10 [orchestrator] queue line taken by scripts/queue.sh take doc-016 (ce5c4935), synced by PR #130 with squash auto-merge.
- 2026-11-10 [orchestrator] stage 1 started: one business-analyst run on the deep tier.
- 2026-11-10 [business-analyst] stage 1 done: spec.md, US-1..US-6, ADR-0040 drafted as Proposed (indexed), OQ-1..OQ-7, decomposition of seven lines (proposals, not queued). Stage 2 next: one Opus requirements review in a clean context.
- 2026-11-12 [orchestrator] the 2026-11-10 review run was cut off by an app restart before writing anything; rerun in a clean context.
- 2026-11-12 [requirements-reviewer] round 1: needs-human-decision. R-1..R-16 (7 major, 9 minor) in review.md, all fixed but R-3, which became OQ-8; decomposition now nine lines. gate-1 asks OQ-1..OQ-8.
- 2026-11-12 [orchestrator] human answers in chat: OQ-1 A, OQ-2 B (a team links only to projects of its own department), OQ-3 A, OQ-4 A, OQ-5 A, OQ-6 A, OQ-7 A (moot, see below).
- 2026-11-12 [orchestrator] OQ-8, after two clarifications, the human: «проекты пусть будут привязаны к департаменту» → "every project stands in a department": no organization-level projects; this replaces point 3 of the request. Upgrade of today's top-level projects (asked separately, ⭐ chosen): into the department of most of its linked teams, otherwise into «Проекты без департамента»; links to teams of other departments are cut per OQ-2 B. Spec rework by the analyst, then gate-1 again.
- 2026-11-12 [business-analyst] rework done: strict tree Organization → Department → {Teams, Projects}, BR-TD-19 upgrade (teams, projects, link cuts), OQ-1..8 ticked, new OQ-9 (moving a linked team or project), ten delivery lines. No round-2 review (economy mode); gate-1 asks OQ-9 and the approval.
- 2026-11-12 [orchestrator] gate-1 approved by the human in chat with OQ-9 A (refuse a move while links exist). ADR-0040 stays proposed until the gate of the line that implements it. Stage 3 started: one ux-designer run.
- 2026-11-12 [ux-designer] design.md (26 KB) and prototype/index.html written; six spec gaps reported for gate-2 (see design.md).
- 2026-11-12 [orchestrator] prototype clicked through in the built-in browser: cascade, parent auto-select, reset notice, Olga's scoped sight, VENUS move refusal, «Видим всей организации» confirm and its effect; no console errors. gate-2 requested.

## Notes

Summary (from the queue line): Analysis of the hierarchy rework: (1) a team belongs strictly to a department instead of standing alone at the organization level, with the migration of existing teams; (2) the header context selectors (organization, department, team, project) cascade: «все» on a level offers every child of every accessible parent, a chosen parent narrows each lower level to its whole subtree (an organization: its departments, all their teams, all their projects), and choosing a lower object selects its parents automatically; (3) a project is created at the organization level or inside a department; (4) a user sees only the organizations, departments, teams and projects they are a participant of or their access level allows, in the selectors and the lists. Amends feat-028 (org-level teams), the team-area links of mod-018/ADR-0031 and mod-019's grants (ADR-0038); decides how feat-041 project visibility fits.
