# feat-040-team-surfaces-across-projects

- type: feature
- size: L
- branch: feat/040-team-surfaces-across-projects
- stage: complete
- epic: core/backlogs-boards
- deps: feat-028
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: approved (2026-10-18, human in chat, one question per open question: OQ-1..OQ-3 A; OQ-3 A queues the 2 000 drawing bound as its own M line)
gate-2: approved (2026-10-18, human in chat, one question per design decision: D-1..D-7 A)
gate-3: pending

## Plan

Route L with UI on the light route (the human's choice of 2026-10-17 for the
lines after mod-017): business analysis → requirements review (one round) →
UX design (the new pieces only: the project filter on team views, «Проект» on
rows and cards and in the creation dialog, the page heads and the switcher's
team level, the refused drop) → gate-1 + gate-2 put to the human as one
package, one question per open question and per design decision → backend
(plan, then code) → frontend → one QA pass over both layers alongside the code
review and the security pass → one fix round → at most one delta read →
gate-3 → PR to main. Plans: `plan-backend.md`, `plan-frontend.md`.

## Decisions

- E2E = NO: the standing answer since 2026-10-12; not asked, not run. The MR
  names the E2E specs this line breaks (the team views' addresses).
- Light route: the human's choice of 2026-10-17, see Plan.
- Started under the human's standing approval of 2026-10-18 evening («Ты
  работай, я спать буду. Пр-ы сам мерджи»): the route confirmation was not
  asked. It removes ceremony, not the gates that carry a decision: gate-1 and
  gate-2 go to the human as one package.
- Takes over, by its queue line: tech-011 m2 and m3 (mod-006's review), both
  restated over today's code in spec BR-TS-7 and BR-TS-13 (the `.Single` that
  m2 named left with mod-017, so m2 now reads: a read that meets the team's
  deletion is never a server error; m3: a test pins the organization boundary
  on every team-view route); and feat-028's card-dialog re-check (feat-029),
  spec BR-TS-4.
- P-10 notes from feat-039 (its spec § Decomposition): its OQ-3, where the
  project views are reached from once «Весь проект» leaves, is this spec's
  OQ-1; its OQ-4, the 2 000 drawing bound now covering the project views too,
  is OQ-3; its OQ-6, the switch rules BR-PB-9 adds on project pages, which
  this line keeps there and replaces on team pages (BR-TS-10).
- doc-013 OQ-3 A (2026-10-18): the switcher's team level here is the minimal
  interim one: the organization's teams the user may see, own teams first,
  with no grouping, per-kind memory or vanish notice (those are feat-043's,
  BR-MX-28). Whichever of this line and mod-020 merges second keeps mod-020's
  fallback team on that level (BR-TS-10); feat-043 retires both (doc-013
  BR-NE-9).
- The vanish notice above is feat-043's fallback to the organization alone
  (ADR-0028 § 7). Today's mod-002 notice for a gone team stays (BR-TS-10;
  review.md A-2, the fix round). It becomes a question only if the human
  reads doc-013 OQ-3 A as dropping it.
- mod-017 OQ-3 A: «Проект» on rows and cards comes with this line, on every
  team and project view, as doc-012 US-7 reads (BR-TS-3).
- Design (2026-10-18): D-1..D-7 of `design.md` § 15 went to the package,
  each with a recommended option A; the design stands on A. The prototype
  follows the stage file's rule for an L.
- Gate-1 + gate-2 (the human in chat, 2026-10-18, one question each): OQ-1..OQ-3
  A and D-1..D-7 A; spec and design stand as written. OQ-3 A: the 2 000
  rows-or-cards drawing bound leaves this line for its own M queue line
  (`feat-NNN-large-view-drawing-bound`), added by the orchestrator's queue PR;
  this line keeps the 2 s read.

## Log

- 2026-10-18 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/feat-040-team-surfaces-across-projects
- 2026-10-18 [orchestrator] route L, breaking (queue size), light route (the human's choice of 2026-10-17 for the lines after mod-017); started under the human's standing approval of 2026-10-18 evening («Ты работай, я спать буду. Пр-ы сам мерджи»), the route confirmation not asked; gate-1 + gate-2 go to the human as one package, one question per open question. E2E: no.
- 2026-10-18 [business-analyst] spec.md + US-1..US-7; OQ-1..OQ-3 left for the human at gate-1, each going on with A (OQ-3 A spins the 2 000 drawing bound out into its own line). No split of this L; stage → 02-requirements-review.
- 2026-10-18 [requirements-reviewer] the one round of the light route: approved-by-ai, R-1, R-2 major and R-3..R-12 minor, all fixed in place in spec.md and US-1, -2, -3, -5, -6 (review.md); OQ-1..OQ-3 go to the human with the gate package.
- 2026-10-18 [business-analyst] fix round: R-1..R-12 all stand; R-1, R-3, R-7 worded tighter; A-1 major (tests and clauses R-2 missed) and A-2 minor (today's fallback notice kept) fixed in spec.md and US-6 (review.md). OQ-1..OQ-3 unchanged, none added.
- 2026-10-18 [ux-designer] design.md (32.3 KB) + prototype/index.html; D-1..D-7 open, each with a recommended A. Package for the human: OQ-1..OQ-3, D-1..D-7; stage gate-1+gate-2. No gate set.
- 2026-10-18 [orchestrator] the human answered in chat, one question per open question and per design decision: OQ-1..OQ-3 A, D-1..D-7 A. gate-1 and gate-2 approved; stage → 04-backend. OQ-3 A's line is queued by the orchestrator's queue PR.
- 2026-10-18 [backend-developer] main merged in; plan pass (L): `plan-backend.md` + `api-contract.md` fixed, no code, no lanes, no ADR, nothing for the human. Additive beyond the spec: `WorkItem.project`, `TeamListItem.isMember` (api-contract § 4).
- 2026-10-18 [backend-developer] code stage done, api-contract unchanged; main (mod-020) merged in, no backend overlap. Deviations: plan-backend § 14. P-10 notes added to mod-003, -006, -016, -017, feat-027, -028, -032, -039. E2E not run (NO); the specs it breaks: plan § 14. stage → 05-frontend.
- 2026-10-18 [frontend-developer] resumed stage (the human stopped the first run): the 40 uncommitted files saved from its worktree (salvage-feat-040, MANIFEST.txt) restored over the tip 3a111531, read against the plan and the design, kept where sound, completed. No contract discrepancy.
- 2026-10-18 [frontend-developer] frontend done (plan-frontend § 5), api-contract unchanged. E2E not run (NO); the specs it breaks: `support/membersUi.ts` and specs 23-29 (the team addresses, 26 also the menu «Команды проекта»); 19 and 21 by the switcher alone. stage → 06-code-review.
- 2026-10-19 [backend-developer] commits: backend plan `9ce0acb2`; code `309246e9`, `94e49bca`, `cb949a0a`, `202f9a6e`; tests `a543844e`..`798a870b`, `92f39bd3`; as built `239f1796` (plan-backend § 14). Verdict: done, api-contract unchanged.
- 2026-10-19 [frontend-developer] commits: frontend plan `3a111531`; code `b0316255`, `e826761f`, `377b6ff0`, `0d6d1efa`, `a28be013`; tests `39dbca54`; as built `8c931a4c` (plan-frontend § 5). Verdict: done, api-contract unchanged, E2E not run (NO).
- 2026-10-19 [qa-engineer] one pass over both layers (`56706191` plan, `04fc5280` and `2a5bb9df` tests, `8724b86e` qa.md): pass, 0 blocker, 0 major, 5 minors (D-B1 doc, D-F1..D-F4); its verify.sh run was green. E2E not run (standing NO); the broken specs are listed in qa.md.
- 2026-10-19 [code-reviewer] round 1 (`99eeee2e`, review-code.md, diff at `8b3d083f`): approve, 4 minors CR-1..CR-4 (reorder-vs-restore window, the departed drop outside the parity test, «Команды» with T unlisted, mod-015 P-10 note missing). None blocks gate-3.
- 2026-10-19 [security-reviewer] pass (`2afbd4b9`, review-security.md): approve, 2 minors: S-1 (testMembership.ts still lists the retired routes, same as QA D-F2), S-2 (BR-TS-13 'takes the caller' covers 2 of 4 cross-project reads). Nothing for the human.
- 2026-10-19 [orchestrator] fixes: none made, the three reads found no blocker and no major. Delta read: none, there are no fix commits.
- 2026-10-19 [task-executor] main merged in (`3eb053e9`, queue file only). mod-020 (#25) was already in main and on the branch since `d4c0564f`: its fallback team and its team filter in the address run on this line's team level (BR-TS-10); 13 test files, 215 tests green.
- 2026-10-19 [task-executor] left open, none for the human: minors D-B1, D-F1, D-F3, D-F4, D-F2 = S-1, CR-1..CR-4, S-2 (review-code.md, review-security.md, qa.md), debts for gate-3; feat-046 takes the drawing bound, feat-043 retires the interim team level. stage → complete; gate-3 pending.
- 2026-10-19 [task-executor] `BASE=$(git merge-base HEAD origin/main) bash scripts/verify.sh` at `414ce1aa`: exit 0, RESULT: green (docs checks, backend build, Api.IntegrationTests 2 936 passed and 17 skipped, Infrastructure 433, Vitest affected 285 files / 4 062 passed, guards 590 passed, lint, build).

## Notes

Summary (from the queue line or an argument): Team backlogs and boards addressed by the team alone, over all its projects, with a project filter; rows and cards carry their project and owning team; a card opens in its own project; the tie-break; the switcher's team level lists the organization's teams (doc-012 BR-MX-17, -18; US-7)
