# ADR-0027: Derive organization access from the membership of the resource's organization, and keep Keycloak to instance roles

- Status: accepted (by the human in chat, 2026-10-12)
- Date: 2026-10-11
- Context: `mod-016-organization-access`, stage 4a:
  [spec](../features/mod-016-organization-access/spec.md),
  [plan](../features/mod-016-organization-access/plan-backend.md),
  [api-contract](../features/mod-016-organization-access/api-contract.md),
  decisions D-1..D-15 in its `status.md`. **Amends ADR-0018** (owed under
  doc-010 D-25); it is bound by doc-006 § Roles, constraints 1–8, and states
  doc-010 D-11's ownership predicate (D-12). Accepted only by the human
  (`docs/adr/README.md`, rule 2).

## Problem

ADR-0018 gave MUSTASK a three-layer access model: a permission is a client
role of `mustask-api`, a role is a composite realm role, a group maps roles,
and the whole chain collapses into `resource_access.mustask-api.roles` in the
token. It said in plain words what that model lacks: "a role, but only for one
project" — a role assignment applies to the whole realm.

MUSTASK is multi-organization (doc-001 D-2). A user is `admin` in one
organization and `viewer` in another, an organization is invisible to anyone
who is not in it, and an administrator changes a role and the user feels it on
the next request. A realm-wide role cannot say any of that, and a token carries
a role until it expires (ADR-0018, "Revocation is NOT instant"). Until now the
gap was covered by an interim guard: every signed-in user could do everything
in every organization (mod-002 BR-M1-19). `mod-016` ends it, and with it the
realm roles `admin`, `member` and `viewer`, which have promised organization
rights they never enforced.

The decision has to answer, at once: where organization-scoped codes come
from; how every endpoint is held to them; what a non-member is told; how the
panel learns what it may show; which roles Keycloak keeps; and how the stand
that already holds users and data is moved without a reset. It also has to
leave room for the ownership checks that the next tasks bring (own comments,
own attachments).

## Options considered

### Option 1: organization roles as Keycloak groups, one subtree per organization

A group per organization and role, mapped to realm roles; the token carries
the groups or a group attribute, and the check reads it.

- Pros: no table of our own; Keycloak stays the one place roles live.
- Cons: every membership change becomes a Keycloak write, which means the
  admin REST API from shipping code — ADR-0023 forbids it, and a production
  client with a service account is exactly what ADR-0018 and ADR-0025 keep out.
  The token carries the roles until it expires, so "applies on the next
  request" (doc-006 BR-M1-6) cannot be met without a call to Keycloak per
  request. Group names would encode organization ids, and the token grows with
  every organization a user joins. Rejected.

### Option 2: Keycloak Authorization Services

Resource-based permissions evaluated by Keycloak.

- Pros: Keycloak already has the vocabulary (resource, scope, policy).
- Cons: ADR-0018 considered and rejected it ("not Keycloak Authorization
  Services"): a policy-decision call to Keycloak on every request, a service
  account for the resource server, and a rule engine we would configure
  through the console instead of the code and tests. Rejected again for the
  same reasons.

### Option 3: the membership in MUSTASK's database, codes derived on every request (chosen)

`ORG_MEMBERSHIP` (user, organization, role, who/when) lives in our database.
The route's organization is resolved, the caller's membership is read from the
database, and the role's bundle of codes is checked. Keycloak keeps
authentication and the instance-level roles.

- Pros: a change applies on the next request; nothing about an organization
  is in the token; no call to Keycloak; the bundles are code, reviewed and
  tested like any other code; the same table answers "is this user a member"
  for the assignee rule and the member lists.
- Cons: one indexed read per organization-scoped request; the check must be
  attached to every route, so it needs an inventory that fails when a route
  forgets it; two places now hold "who may do what" (the token for instance
  codes, the database for organization codes).

### Where the check runs, inside Option 3

- **An endpoint filter** (the shape of mod-002's organization-context filter).
  Rejected for the scoped check: a minimal-API endpoint filter runs after the
  parameters are bound, so a non-member who sends a malformed body would get a
  400 before the 404, and the order the spec fixes (401, 404, 403, then the
  rest) could not be held.
- **An authorization requirement with its own handler (chosen).** It runs in
  the authorization middleware, before the body is read, in the same place
  every other access decision is made. Its failure reasons are mapped to the
  404 and the 403 by the result handler that already maps a policy failure to a
  business code (mod-002 D-p). A route cannot take the category without the
  enforcement.

## Decision

Option 3, with a requirement handler for the check. These rules satisfy
doc-006 § Roles constraints 1–8, in their order.

**1. Two kinds of code, one catalog.** `PermissionCatalog` is the only
registry, and every code in it carries its scope: **instance** or
**organization**. Instance codes (`dictionaries.languages.read`,
`dictionaries.languages.write`, `organizations.organization.create`) stay
where ADR-0018 put them: client roles of `mustask-api`, bundled by realm
roles and groups, carried in the token. Organization codes (every other code,
and every code of later modules) are never declared in the realm import,
never mapped onto a Keycloak role or group, and never read from the token; the
membership is their only source. `OrganizationPermissionCatalog` (mod-001) is
folded into the catalog. The realm contract test "the `mustask-api` client's
roles equal the catalog" now compares the **instance** codes only. A code is
added to the catalog with its scope and its bundles, in the same change that
first checks it.

**2. The bundles.** The role is a column of the membership: `admin`, `member`,
`viewer`. A role's codes are a fixed bundle in the domain:

| Role | Organization codes |
|---|---|
| `admin` | every organization code |
| `member` | every `*.read` code, plus `workitems.workitem.write`, `workitems.link.write`, `boards.backlog.reorder`, and the write codes later modules mark as member-level |
| `viewer` | every `*.read` code |

The full table is the spec's matrix. Team membership grants no code. A user's
codes in an organization are exactly their bundle there; `admin` in one
organization and `viewer` in another is normal.

**3. One check, derived from the address.** Every route that has
`{organizationId}` in its address declares exactly one organization code
(`RequiresOrganizationPermission(code)`), and the authorization requirement it
adds does, per request and before the body is read:

1. parse the organization id; read the organization's status and the caller's
   membership in one query; the caller's id is the token's `sub`;
2. **no valid id, no such organization, an organization that is not active
   (the restore of an organization accepts a deactivated one), or no
   membership: 404 `organizations.organization.not_found`, the same answer for
   every cause;**
3. the role's bundle does not hold the route's code: 403
   `organizations.access.forbidden`;
4. otherwise the request goes on, carrying the resolved role for the handler.

Nested ids (project, team, area, work item) are resolved by the handlers from
the outside in, each inside the organization of the address (mod-002
BR-M1-22); a handler never trusts a nested id's own organization. The chain is
therefore: organization from the address, then project ∈ organization, then
team or area ∈ project, then work item ∈ project, then ids in the body inside
the same project or organization. The membership is read from the database on
every request and cached nowhere beyond it; no check calls Keycloak and none
reads the user projection — an account's projected status refuses and grants
nothing here (mod-014 BR-UP-12; constraint 5).

A second code on one route is checked by the operation against the same role
bundle (a work-item save that carries a parent also needs
`workitems.link.write`). The endpoint declares one code; the operation names
the other.

**4. Not found for non-members.** A user with no membership is told nothing
about an organization or anything under it: «Организация не найдена»,
indistinguishable from an organization that never existed (doc-006 BR-M1-8;
ADR-0026 rule 5, which this extends from "removed" to "not yours").
«Недостаточно прав» only ever reaches a member whose role lacks the code. The
organization-context endpoint filter of mod-002 D-g is retired: the
requirement handler does its work, including the "active or the restore
request" rule, and ADR-0026's rule 5 and rule 8 are unchanged in effect. The
instance administrator holds no membership by default and so sees no
organization content (doc-006 US-4).

**5. Codes served to the panel.** `GET /api/organizations/{organizationId}/access`
answers the caller's role and the organization codes of that role
(`Cache-Control: no-store`), under `organizations.organization.read`. The panel
re-reads it on an organization switch, on window refocus, after the caller's
own role changes, and after a 403 or 404 from any other organization-scoped
call; navigation and controls use the same answer without a reload (mod-015
BR-8, BR-9). The panel's answer is a convenience: the server checks every
request itself.

**6. The ownership predicate (doc-010 D-11, D-12).** A code may carry an
optional **ownership predicate**. The order is fixed: the code is checked first,
by the rule above; then, only for a code that has one, the predicate is checked
on the resolved object ("is this comment, this attachment, this private query
the caller's own"). A predicate can only refuse; it never grants a code the
role lacks. A role code narrowed to one's own objects (editing or deleting
one's own comment, removing one's own attachment) is in the `member` bundle,
the unrestricted moderation code is `admin`'s, and the right goes with the
role: an author downgraded to `viewer` no longer edits their comments. A
personal-object code, registered in release 2, is in every bundle. This ADR
ships no mechanism: `feat-002` and `mod-004` build the first predicate under
this rule, and a failed predicate answers after the 404 of the organization
level and is never reported as a missing organization.

**7. The realm inventory (constraint 8, D-7).** The realm import holds:

- realm roles `instance-admin` (every instance code and no organization code),
  `dictionaries-admin` (the dictionaries codes) and `organization-creator`
  (only `organizations.organization.create`), and the realm's default roles;
- the default roles carry `dictionaries.languages.read`, so **every signed-in
  user reads the dictionaries**, a user in no group included (this widens
  constraint 4 for the read code only; a languages dictionary is not
  sensitive, and `…write` is unchanged);
- groups `/Администраторы` (→ `instance-admin`), `/Администраторы справочников`
  (→ `dictionaries-admin`) and `/Создатели организаций` (→ `organization-creator`).

The realm roles `admin`, `member`, `viewer` and the groups `/Сотрудники` and
`/Сотрудники/Участники` leave the import. `admin`'s bundle passes to
`instance-admin`. Granting "create organizations" stays Keycloak realm
administration (doc-006 BR-M1-2). `scripts/keycloak-seed.sh`, the E2E
fixture's group and `src/backend/AGENTS.md` follow the inventory. The realm
contract tests are restated, not loosened: the instance administrator holds
every instance code, no organization code, and is granted to at least one
group; no realm role is named `admin`, `member` or `viewer`; the default roles'
Keycloak built-in composites are listed and asserted, and the read code is among them.

**8. Moving a store that already exists.** Keycloak keeps an imported realm
and never overwrites it, so the stand's drift check (ADR-0025 § 7) holds the
stand not ready after the merge until the live realm is changed. A stand-only
command edits the live realm to match the new import — every grant before any
removal, idempotent, completing an interrupted run when repeated, secrets never
on a command line or in output — and the database migration gives every
existing organization its creator as administrator (D-3, D-6). The drift check
and its remedy text are unchanged; `src/backend/AGENTS.md` names the command
and the order. A deployment whose realm is created from the new import needs
neither.

**Departures from `docs/conventions/*`:** none. `docs/conventions/backend.md`
§ "Soft removal" names "the organization-context endpoint filter"; it is
updated to point at the requirement handler of rule 3.

## Consequences

- **Simpler:** an access question has one answer in one place; a role change,
  an addition and a removal take effect on the next request; the token carries
  no organization state and does not grow with the user's organizations; the
  panel reads one endpoint; the same membership table serves the member lists,
  the assignee rule and the team defaults.
- **Harder:** every organization-scoped route must declare an organization code
  and carries one extra read; an inventory test fails `scripts/verify.sh` when
  a route with `{organizationId}` declares none, so later lines enforce their
  own codes or fail the build. Roles are code: a new bundle or a changed
  bundle is a release, not a console edit. A Keycloak realm administrator no
  longer sees organization roles in the console — by design.
- **Locked in for the long term:** the catalog's scope split; the stored role
  names `admin`, `member`, `viewer` and the code strings; "not found" for
  non-members on every organization-scoped route; the order 401, 404, 403, then
  the operation's own answers; the position of the ownership predicate after the
  code.
- **Superseded in ADR-0018:** the sentence "What the model does not have is
  [a role scoped to one project]; a role assignment applies to the whole realm"
  describes only instance codes now — scope comes from the membership. Its
  contract test "the `admin` role holds every permission" is restated for
  `instance-admin` (rule 7). Everything else in ADR-0018 stands, revocation of
  instance codes still waits for the token to expire included. When this ADR is
  accepted, ADR-0018's line in the index gets "amended by ADR-0027".
- **Spawned:** `feat-002` and `mod-004` implement the ownership predicate;
  `feat-017` shows the assignee marks on the flag the reads now carry; `feat-028`
  moves teams to the organization and carries the default-team rule; later
  modules register their codes in the catalog. The live-realm command stays in
  the repository for stands that predate the inventory.
- **Revisit when:** a user needs a different role per project or team inside
  one organization (the bundle would then need a scope below the organization);
  the membership read shows up in profiling at the volumes of doc-006 (hundreds
  of memberships, tens of organizations per instance); or a second system
  shares the realm and needs organization roles in its own tokens.
