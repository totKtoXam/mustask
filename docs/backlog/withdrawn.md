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
