# ADR-0031: Run in the company-wide realm on four client roles, and keep the role-to-right table in code

- Status: proposed
- Date: 2026-10-20
- Context: `tech-024-shared-realm-client-roles` ([status](../features/tech-024-shared-realm-client-roles/status.md)),
  decided in chat 2026-10-12 (queue line in `docs/backlog.md`). **Amends
  ADR-0018 and ADR-0027.** Accepted only by the human (`docs/adr/README.md`,
  rule 2).

## Problem

ADR-0018 gave MUSTASK its own realm, and its access model used everything a
realm owner can create: a permission is a client role of `mustask-api`, a role
is a composite *realm* role, a group maps roles, the realm's default role gives
every user the dictionaries' read, and the token carries the permission codes
themselves in `resource_access.mustask-api.roles`. ADR-0027 kept that chain for
the instance rights and moved the organization rights out of Keycloak.

MUSTASK is going to run in the company-wide Keycloak realm that other
applications use. There we may create **only clients and client roles**: no
realm roles, no groups, no change to the default roles, no provider JAR (the
last one is `tech-025`'s). The chain of ADR-0018 cannot be imported into such a
realm, and a token that carries raw permission codes ties every other
application's administrators to our catalog. The login theme is a realm setting
too; it stays on the stand only. The stand still has to work from a realm import
that is ours alone, and the realm's name can no longer be assumed to be
`mustask`.

## Options considered

### Option 1: Keep the realm roles and groups, ask the company to create them

- Pros: no change to the model.
- Cons: not allowed in a realm we do not own; every new right is a request to
  another team; the stand and the server would diverge.

### Option 2: Client roles named like the permission codes, no table in code

- Pros: nothing to add in code: the token already carries codes.
- Cons: the company realm would hold one client role per code of our catalog
  (every new code is a request to the realm's owner), and there is no way to give
  "an administrator" without listing each code on each user.

### Option 3: Four client roles; the code table lives in our code

- Pros: the realm holds a fixed, tiny set; a new right changes our code and
  needs no change in the realm; the mapping is one table with unit tests; the
  realm owner assigns roles to users directly, which is all the realm allows.
- Cons: the panel keeps a hand-mirrored copy of the table; a token carries
  roles, not rights, so a reader of the token needs the table.

## Decision

Option 3.

1. **Four client roles of `mustask-api`, and nothing else of ours in the realm.**
   `user` (signing in; the dictionaries' read; without it there is no session and
   no right), `organization-creator` (`organizations.organization.create`),
   `dictionaries-admin` (the dictionaries' read and write) and `system-admin`
   (every instance code, `PermissionCatalog.InstanceCodes`; later tasks use it
   for cross-organization administration). The realm import declares exactly
   these, with descriptions, and **no realm roles** (Keycloak's own aside), **no
   groups, no `defaultRole`, no composites**. Roles are given to users directly
   as client roles.
2. **The table role → codes is code**: `Mustask.Domain.Identity.InstanceRoles`,
   next to `PermissionCatalog` and `OrganizationRoleBundles`, one table, unit-tested;
   a test fails for an instance code no role reaches. The token carries role names
   only. `KeycloakPermissionClaims` turns `resource_access.<audience>.roles` into
   `perm` claims through the table: unknown names are ignored (a raw code, a role of
   the old model, a Keycloak built-in), and a principal **without `user` gets no `perm`
   claim at all**. ADR-0027's organization rights are untouched: they still come from
   the membership.
3. **No `user`, no session.** The session callback refuses a sign-in whose access
   token lacks `user` (`authError=no_access`, the panel shows «У вашей учётной записи
   нет доступа к MUSTASK. Обратитесь к администратору.»); a renewal whose new token lost
   `user` ends the session with 403 `session.no_access`. A bearer token without `user`
   authenticates and holds no right (403 where a right is required, as before).
4. **The realm's name is configuration.** `Mustask.Api` already takes it only as the
   tail of `Keycloak__Authority`. The stand takes it from `Keycloak:Realm` (default
   `mustask`): the import file reads `${MUSTASK_REALM:mustask}` and is mounted as
   `<realm>-realm.json` (Keycloak rejects any other file name), and the health check,
   the drift check, the provisioning and the seed script use the same value; nothing
   derived from the name (`default-roles-mustask`) is spelled out.
5. **The live-realm migration is retired.** `--migrate-live-realm`, its scripts and
   tests converted the pre-mod-016 model and have nothing left to convert. A stand whose
   Keycloak store was made from an older import is brought up by the existing stand reset
   (`bash scripts/stand-reset.sh`), which the drift check's findings name.
6. **The stand differs from the company realm only in what it must.** It is made from
   our import, with our login theme (`loginTheme` is stand-only; the company realm keeps
   its own), the provider of ADR-0025 (until `tech-025` removes it), and, added by the
   stand's provisioning and never by the import, `user` as a composite of the realm's
   default role so that the E2E suite's throwaway accounts can sign in. In the company
   realm an administrator gives `user` to each person.

## Consequences

- Easier: a new right needs no change in the realm; moving to another realm is
  one configuration value and the four roles; the token is small and says what the
  user *is*, not what the catalog contains today.
- Harder: the table is mirrored by hand in `src/frontend/src/shared/api/tokenClaims.ts`
  (a comment points both ways; a drift test needs the machine-readable contract that
  `mod-019` takes over from `tech-026` OP-7). A role change reaches a user at the next
  token, up to the access token's lifetime (ADR-0018, "Revocation is NOT instant").
- Locked in: the four names. Renaming one is a change in the company realm and in the
  table; adding a role is an ADR amendment.
- Operations: the first person of a new installation gets `user` and `system-admin` from
  the realm's administrator; `scripts/keycloak-seed.sh` does it on the stand.
- Not decided here: the user projection (ADR-0025) and the provider JAR, which `tech-025`
  replaces; the deploy runbook (`docs/deploy/vm.md`, a separate task).
- **Amends:** ADR-0018 (the "three-layer model" and its realm roles, groups and default
  roles; the token's content) and ADR-0027 rule 1 (the instance rights come from the four
  roles through the table, not from codes in the token). When this ADR is accepted, the
  index lines of ADR-0018 and ADR-0027 get "amended by ADR-0031".
