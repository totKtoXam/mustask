# Withdrawn tasks (number taken, task not done)

Split out of `docs/backlog.md`. This is the one section of the
queue that only ever grows — a number is never reused, so a line put here
stays forever — and the one nobody reads to pick up work. It lives in
`main` under the same rule as the queue itself.

A task's line moves here when it is withdrawn **before it starts** (a change
of mind): the number has already been issued by being put in the queue and is
not reused — the counter over the "numbered queue lines" source keeps seeing
it, and references to the id do not start resolving to someone else's task.
Format: `- [x] <id> — withdrawn: <reason, date>`. A task withdrawn **after it
starts** is not written here — its number is held by the branch and the
`docs/features/<id>/` folder ([features/numbering.md](../features/numbering.md)).

- [x] doc-008-personal-object-ownership — withdrawn: absorbed by doc-010 (cross-module normalization amends doc-001 Roles/BR-5 for D-14 there), 2026-09-24
- [x] doc-009-teams-per-project-carry-back — withdrawn: absorbed by doc-010 (cross-module normalization carries doc-004 E-5 into doc-006 there), 2026-09-24
- [x] feat-019-work-item-priority — withdrawn: absorbed by feat-022-work-item-form-fields (human, 2026-10-05), 2026-10-05
- [x] feat-007-work-item-type-fields — withdrawn: absorbed by feat-022-work-item-form-fields (human, 2026-10-05), 2026-10-05
- [x] feat-014-work-item-estimation-fields — withdrawn: absorbed by feat-022-work-item-form-fields (human, 2026-10-05), 2026-10-05
- [x] feat-004-backlog-hierarchy — withdrawn: folded into feat-001-work-item-links: the human's order of 2026-10-05 makes the hierarchy one cut (feat-001 D-2), 2026-10-06
- [x] tech-022-stage-reading-diet — withdrawn: re-scoped: process docs are already read per stage (feature.md step 1); the reading weight is in feature artifacts, now tech-023, 2026-10-12
- [x] feat-010-move-unfinished-work — withdrawn: Absorbed by closing a team sprint (doc-012; mod-007 reworded), 2026-10-17
- [x] bug-005-creation-default-team-deadlock — withdrawn: superseded by feat-028 (plan D-4: a creation that may default locks every link of the project FOR SHARE in one statement ordered by team id, pinned by a no-deadlock regression test), the human at feat-028 gate-3, 2026-10-18
- [x] feat-045-organization-home — withdrawn: absorbed by feat-043-area-project-team-navigation, which now runs right after mod-018 and makes the organization's «Обзор» its home page (doc-013 OQ-1 A), 2026-10-18
