# feat-040-team-surfaces-across-projects

- type: feature
- size: L
- branch: feat/040-team-surfaces-across-projects
- stage: 02-requirements-review
- epic: core/backlogs-boards
- deps: feat-028
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: pending
gate-2: pending
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
- mod-017 OQ-3 A: «Проект» on rows and cards comes with this line, on every
  team and project view, as doc-012 US-7 reads (BR-TS-3).

## Log

- 2026-10-18 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/feat-040-team-surfaces-across-projects
- 2026-10-18 [orchestrator] route L, breaking (queue size), light route (the human's choice of 2026-10-17 for the lines after mod-017); started under the human's standing approval of 2026-10-18 evening («Ты работай, я спать буду. Пр-ы сам мерджи»), the route confirmation not asked; gate-1 + gate-2 go to the human as one package, one question per open question. E2E: no.
- 2026-10-18 [business-analyst] spec.md + US-1..US-7; OQ-1..OQ-3 left for the human at gate-1, each going on with A (OQ-3 A spins the 2 000 drawing bound out into its own line). No split of this L; stage → 02-requirements-review.

## Notes

Summary (from the queue line or an argument): Team backlogs and boards addressed by the team alone, over all its projects, with a project filter; rows and cards carry their project and owning team; a card opens in its own project; the tie-break; the switcher's team level lists the organization's teams (doc-012 BR-MX-17, -18; US-7)
