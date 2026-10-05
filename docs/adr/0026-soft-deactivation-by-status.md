# ADR-0026: Remove organizations, projects and teams softly by a status on the object itself, and decide reachability by containment

- Status: accepted
- Date: 2026-10-02
- Context: `mod-002-organizations-projects-teams`, stage 3 (design):
  [spec](../features/mod-002-organizations-projects-teams/spec.md) BR-M1-1,
  BR-M1-18, BR-M1-20, NFR Concurrency, and
  [design](../features/mod-002-organizations-projects-teams/design.md) § 12.
  Owed by mod-002 under doc-010 § ADR debt, row "Soft deactivation by
  status". Its sources are doc-001 BR-6 and § Deletion lifecycle, doc-006
  BR-M1-18, doc-010 D-6, D-13, D-23 and PR-9.

## Problem

M1 is the first module that removes things, and it has to do so in two
different ways. An organization or a project is deactivated: it leaves every
list, it can be restored exactly as it was, and its name, and a project's
key, stay reserved for good. A team is deleted: it leaves every list, it is
never restored, and its name is freed, after a confirmation, with a cascade
over its areas. Nothing is ever purged (doc-010 D-13). Later lines reuse
the same pattern: mod-007's iterations (no restore, the name freed, a
sprint's number reserved), mod-005's recycle bin, and the M6 objects of
releases 2 and 3, which keep a deleted status of their own apart from the
one their project gives them.

The pattern has to answer six questions at once. Where the removal is
stored. How removed objects are hidden. How restore stays exact when a
project was deactivated on its own before its organization was. How names
and keys stay reserved or are freed. What the API answers for a removed
object and anything under it. And how other modules, including ones written
later, learn of a removal for their own data (doc-010 PR-9). It has to hold
under concurrent requests (doc-001 BR-11, M1 check-then-act) within
ADR-0011's READ COMMITTED model.

## Options considered

### Option 1: hard delete, with an archive or tombstone table

- Pros: live tables hold only live rows, so no query can forget a filter.
- Cons: restore must re-insert rows and their foreign keys. Other modules'
  references, such as a work item's area or a membership's team, dangle or
  must be deleted too, and that contradicts "nothing under it is deleted".
  A reserved name needs a second uniqueness source that spans two tables.
  Rejected.

### Option 2: a removed flag on every row, hidden by an EF Core global query filter

- Pros: hidden by default, so a forgotten filter cannot leak a removed row.
- Cons: about half of the reads need the hidden rows: the deactivated
  lists, restore, the reserved-name checks, the "is this team deleted?"
  answer to other modules, and telling an unknown id from a removed one in
  logs and tests. Each of them would opt out with `IgnoreQueryFilters`,
  which also switches off every other filter on the query. One flag cannot
  say that a team is removed for good while a project can come back. A
  child's flag also does not say whether its container is removed, so the
  filter alone still shows a project of a deactivated organization.
  Rejected.

### Option 3: cascade the removal into the rows beneath

Deactivating an organization writes "deactivated" on its projects, teams
and areas.

- Pros: every row answers alone whether it is reachable.
- Cons: restore cannot tell a project deactivated on its own from one
  deactivated with its organization, so BR-M1-20's exact restore needs a
  second marker. The write grows with the subtree. A deactivation also has
  to read what is beneath it, so it races with every write beneath it and
  needs locks across the subtree. Rejected.

### Option 4: a status on each object, with its own lifecycle per entity type, and reachability by containment (chosen)

- Pros: a removal or a restore writes one row, so restore is exact by
  construction. Each type's lifecycle is explicit. Reservation is
  declarative. Other modules read one status and need no events.
- Cons: every read of a nested object must check its containers. That
  check is easy to forget, so it has to live in one place per module (see
  Consequences).

## Decision

Option 4, with these rules.

**1. Status per object, closed per type.**

| Entity | Statuses | Transitions | Reserved afterwards |
|---|---|---|---|
| Organization | `Active`, `Deactivated` | Deactivate; Restore (back to `Active`) | the name, instance-wide |
| Project | `Active`, `Deactivated` | Deactivate; Restore | the name and the key, in the organization |
| Team | `Active`, `Deleted` | Delete; `Deleted` is terminal | nothing: the name is freed in the project |
| Area | none | an empty leaf is removed outright (doc-001 § Deletion lifecycle) | nothing |

The status is a column of the object's own table, stored as text naming the
value. No "who and when" is recorded for a status change in the MVP. M1's
only change record is the creator (BR-M1-21), and M1's audit log is release
2 (doc-006 BR-M1-15).

**2. A removal writes one row.** Deactivating an organization or a project,
or restoring it, changes that row's status and nothing else. Nothing beneath
it changes status or is deleted. Deleting a team changes its status and, in
the same transaction, re-owns every area it owns explicitly to the project's
first team (BR-M1-18). That is a domain consequence of the deletion, not a
status cascade.

**3. Reachability is computed, never stored.** An object is reachable when
its own status is `Active` and every container above it is `Active`. Its
containers are the organization for a project, and the organization and the
project for a team or an area. Restoring a container makes reachable again
exactly what was reachable before it was deactivated. A project deactivated
on its own stays deactivated (BR-M1-20).

**4. Hiding is explicit, not a global filter.** No EF Core global query
filter on status. A read states what it wants:

- lists and pickers: reachable, active objects;
- the deactivated lists: the deactivated objects of a reachable container;
- by-id reads for other modules: the object with its status, reachable or
  not.

Each M1 read model names its intent in one place, a query specification or
repository method, so a handler cannot drop the condition by accident.

**5. A removed object answers "not found".** For a page or a request, an
object that is not reachable answers exactly like one that never existed.
The answer is the not-found code of the outermost level that is not found:
`organizations.organization.not_found`, then the project's, then the team's.
They are registered in `contracts/errors/organizations.json` (ADR-0005).
"Deactivated" or "deleted" is never revealed through an addressed request.
This is the shape mod-016's non-member answer (doc-006 BR-M1-8) needs.

There is one exception. Restore addresses a deactivated organization or
project directly, and it still needs every container above it to be
reachable: restoring a project of a deactivated organization answers
`organizations.organization.not_found`. A write addressed under an
unreachable container is refused with that container's not-found code. The
organization-context endpoint filter applies this to every
organization-addressed endpoint (mod-002 D-g, BR-M1-20), and each handler
applies it for the project and the team.

**6. Reservation and freeing are unique indexes** (ADR-0011, echelon 1). A
name or key that stays reserved has a unique index over all rows, whatever
their status. A name that is freed has a partial unique index over active
rows only. Comparisons ignore letter case and surrounding spaces, so both
kinds index the normalized value.

| Rule | Index |
|---|---|
| Organization name, instance-wide, reserved | unique on the normalized name, all rows |
| Project name per organization, reserved | unique on (organization, normalized name), all rows |
| Project key per organization, reserved | unique on (organization, key), all rows |
| Team name per project, freed on delete | unique on (project, normalized name) where the status is `Active` |
| Area name among siblings | unique on (parent, normalized name); areas have no status |

A violation maps to the business refusal through SQLSTATE `23505`, as
ADR-0011 prescribes.

**7. The status is the cross-module contract** (doc-010 PR-9). M1 exposes
the status through its read interface and publishes no event in the MVP.
Another module never copies the status. It applies the status to its own
data when it reads: mod-006 hides a deleted team's boards and backlog
entries, mod-007 ignores its iteration settings, mod-008 and mod-009 stop
its queries and dashboards. A module that must act on a removal does so in
its own write path by reading the status, as mod-016 does when it ends a
deleted team's memberships. Data older than the reading module is covered
without a backfill, because the reading module asks at read time.

**8. Concurrency.**

- *Under a deactivating organization or project:* no lock. A write under it
  only reads the container's status. The deactivation reads nothing beneath
  and writes one row. Their order is therefore always serializable: a write
  whose check passed before the deactivation committed is ordered before
  the deactivation, which the rules allow.
- *Against a deleting team:* row locks (ADR-0011, echelon 3). The deletion
  reads the team's areas, and a concurrent write may bind an area to the
  team (a re-own, a creation with that owner, a default-area change). Those
  two can form a cycle no serial order explains, which would leave an area
  owned by a deleted team. The deletion takes `SELECT … FOR UPDATE` on the
  team row. Every write that binds something to a team locks that team row
  with `SELECT … FOR SHARE`, and re-checks `Active` before it writes. The
  loser is refused (US-7, NFR Concurrency). `FOR SHARE` is not among
  ADR-0011's listed primitives, but it is a row lock of echelon 3's kind
  under the same READ COMMITTED level: binders to one team run side by side,
  and each of them queues against a deletion of that team. The backend plan
  may use `FOR UPDATE` for the binders instead, if it shows that serializing
  them per team costs nothing.

**9. Reuse.** mod-007's iterations follow rules 1, 2 and 6 with `Active` and
`Removed`, terminal: the name is freed, and a sprint's number stays
reserved. The M6 objects of releases 2 and 3 keep their own deleted status
under rule 3: what their project's deactivation hides comes back with its
restore, and their own deletion does not. mod-005's recycle bin keeps the
work item's own lifecycle (the state «Удалена», doc-001 BR-10). It
references this ADR for reachability: an item in a deactivated project is
unreachable, while its state stays unchanged.

**Convention.** When this ADR is accepted, mod-002's backend stage adds a
short § "Soft removal" to `docs/conventions/backend.md` that points here:
status per object, no global filter, reachability checked in one place per
module, and indexes for reservation.

## Consequences

- **Simpler:** a removal and a restore are one-row writes, and restore is
  exact without bookkeeping. Reservation is declarative and race-free. Other
  modules need no subscription, and data older than them is covered. Not
  found is one answer for unknown, malformed, removed and, from mod-016,
  forbidden.
- **Harder:** every read of a nested object joins or checks its containers.
  A read that forgets it leaks a deactivated organization's content. This
  is contained by the organization-context endpoint filter (D-g), by one
  reachability specification per module, and by a test per
  organization-addressed endpoint against a deactivated organization and
  project. Writes that bind to a team need the team-row lock.
- **Locked in for the long term:** the stored status values (a rename is a
  data migration); names and keys reserved for as long as the object exists,
  which is for ever, since nothing is purged; and "not found", never
  "deactivated", on addressed requests.
- **Spawned or owed elsewhere:** mod-016 ends a deleted team's memberships
  and answers non-members with the same not-found codes. mod-006, mod-007,
  mod-008 and mod-009 read the team status (PR-9). mod-005 references rule 5
  for its recycle bin. mod-007 reuses rules 1, 2 and 6 for iterations.
- **Revisit when:** a purge of organizations or projects is wanted (doc-010
  D-13 rules it out); a deleted team must be restorable; a module needs to
  be told of a removal rather than read it, which would mean an event through
  ADR-0013's outbox; or reachability checks become costly at the tree depths
  doc-006 allows.
