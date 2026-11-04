# ADR-0035: Let the instance administrator appoint organization administrators on request, on a permanent record

- Status: proposed
- Date: 2026-11-03
- Context: `feat-033-orphaned-organization-rescue`
  ([spec](../features/feat-033-orphaned-organization-rescue/spec.md),
  [design](../features/feat-033-orphaned-organization-rescue/design.md)), gate-1
  answers R-1 = A and R-6 in its `status.md`. Decides only what ADR-0029 § 9
  (accepted) leaves open; **amends the wording of ADR-0027 rule 4 and
  ADR-0033 § 1.** Accepted only by the human (`docs/adr/README.md`, rule 2).

## Problem

ADR-0029 § 9 sanctions the rescue of an organization nobody can administer: an
instance operation that appoints an organization admin, writes a membership, is
audited, reads no content and holds no scoped code, with its routes registered
as inventory exceptions. It does not say when the operation may be used, what
the instance administrator learns by using it, or what the audit is.

Two facts bound the answer. First, MUSTASK cannot tell that an organization has
no usable administrator: since tech-025 the user projection is filled from the
sign-in only and never learns that an account was disabled or deleted (ADR-0034,
Consequences), so a trigger such as "no known active administrator" would never
fire in the very case it exists for. Second, the production Keycloak realm is
company-wide (ADR-0033): MUSTASK may add only clients and client roles there, and
this operation must write nothing to it.

## Options considered

### Option 1: Rescue only when the organization shows no active administrator

- Pros: narrowest power; matches the queue line's wording.
- Cons: the condition cannot be computed after ADR-0034; the rescue would be
  dead on arrival or would wrongly refuse the real case (the accounts are
  disabled, the projection does not know).

### Option 2: Rescue on request in any organization, adding or raising only, with a required reason

- Pros: works with what MUSTASK can know; removes nothing and downgrades nobody,
  so the power cannot be used to take an organization from its administrators;
  the reason and the record make each use answerable; nothing in Keycloak.
- Cons: wider than the queue line; the instance administrator can add an
  administrator to a healthy organization, which the organization's own
  administrators must be able to see (the second reader below).

### Option 3: Let the instance administrator become a member of any organization

- Pros: no new operation.
- Cons: gives access to the organization's content, which doc-006 BR-M1-8 and
  ADR-0029 § 9 forbid; membership would no longer mean "chosen by the
  organization".

### Option 4: A Keycloak realm role or group for the rescuer

- Pros: nothing in MUSTASK to store.
- Cons: not allowed in the company realm (ADR-0033); not auditable inside
  MUSTASK.

## Decision

Option 2.

1. **Trigger.** An appointment is allowed on request in any organization, active
   or deactivated, with or without administrators. It only adds an `admin`
   membership or raises an existing member to `admin`; it never removes,
   downgrades or otherwise changes another membership, and does not restore a
   deactivated organization. No detection of an "orphaned" state is attempted
   (ADR-0034). A required reason (1–500 Unicode code points after trimming) is
   kept in the record. Appointing oneself is refused.
2. **Right.** One new instance permission code,
   `organizations.organization.rescue` (`PermissionScope.Instance`), reached
   through `Mustask.Domain.Identity.InstanceRoles` by **`system-admin` only** —
   no other client role and no organization role reaches it. Granting or revoking
   it is assigning `system-admin` in the company realm; MUSTASK adds nothing to
   Keycloak (no role, group or setting) and calls it not at all. The panel's
   mirror in `shared/api/tokenClaims.ts` changes in the same commit (ADR-0033
   § 2). The code is not an organization code and opens no organization route;
   the rescue routes are inventory exceptions with ADR-0029 § 9's reason
   (mod-016 BR-M1-33).
3. **Accepted disclosures.** By this operation the instance administrator learns,
   and only this: the name and status of every organization; through the appointee
   search, the name and e-mail of every user the projection knows; the appointment
   record; and, from one refusal, that the named user is already an administrator
   of that organization (OQ-6). Not members, administrators, projects or any
   content; opening an organization still answers «Организация не найдена» without
   a membership (ADR-0027 rule 4 as amended below).
4. **The appointment record.** Each appointment writes, in the same transaction
   as the membership change, one row: organization, appointee, acting
   administrator, moment, reason. It is never edited or deleted, outlives later
   membership changes, and a refused appointment writes none; if the row cannot
   be written, the appointment is refused. Readers: holders of the instance code,
   for any organization; and the holders of `organizations.membership.write` in
   that organization (its administrators), for their own organization only — so
   an organization can always see who was put into it. The membership's «Добавлен»
   / «Роль изменена» name the acting administrator as for any change.
5. **Concurrency and order.** Concurrent appointments of one user to one
   organization: exactly one succeeds. A concurrent mod-016 change to the same
   membership is serialized on the organization row lock (the lock mod-016's
   change and remove take, ADR-0011), and the later request is judged against the
   committed state. A mod-016 add takes no lock and is not serialized: one that
   commits first fails the appointment's insert on the primary key, which answers
   `membership.already_member` with nothing written (D-1). Refusals are ordered as the spec
   fixes them (BR-RS-6a), starting from ADR-0029 § 8's not-found-first habit.

**Amendments (wording only).**

- ADR-0027 rule 4 ("Not found for non-members") gains: *an instance
  administrator with no membership is told the same, except through the rescue
  operation of ADR-0035, which reads and writes nothing of the organization's
  content.*
- ADR-0033 § 1 ("later tasks use it for cross-organization administration") is
  read as: the one such use is this operation; any further one needs an ADR.

## Consequences

- Easier: a stranded organization can be given an administrator without touching
  Keycloak; each use is on a record the organization itself can read.
- Harder: one more instance code and a hand-mirrored line in the panel's role
  table; one more table and a second reader of it; a power that can add an
  administrator to a healthy organization, whose only guards are the reason, the
  record and the organization's own ability to remove the appointee (mod-016).
- Locked in: add-or-raise only; the record is permanent. Removing, downgrading or
  reading content by an instance administrator is a different decision, needing
  its own ADR.
- Operations: when the appointee turns out unable to sign in (MUSTASK cannot see
  it), the administrator appoints someone else; a person who has never signed in
  must sign in once first (ADR-0034).
- Not decided here: a general audit log of instance actions and the M1 audit log
  (release 2).
- **Amends:** ADR-0027 rule 4 and ADR-0033 § 1, by the wording above. When this
  ADR is accepted, the index lines of ADR-0027 and ADR-0033 get "amended by
  ADR-0035".
