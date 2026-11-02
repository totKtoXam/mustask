# ADR-0034: Fill the user projection from the sign-in claims instead of a Keycloak event-listener provider

- Status: proposed
- Date: 2026-11-02
- Context: `tech-025-user-projection-without-provider`
  ([status](../features/tech-025-user-projection-without-provider/status.md)),
  decided in chat 2026-10-20 (queue line in `docs/backlog.md`). **Amends
  ADR-0025.** Builds on ADR-0033 (the company-wide realm takes only clients and
  client roles) and ADR-0022 (the panel's session through the OIDC code flow).
  Accepted only by the human (`docs/adr/README.md`, rule 2).

## Problem

ADR-0025 fills the user projection (`directory_users`) with a Java
event-listener provider that runs inside Keycloak and pushes every change to
`Mustask.Api` over a signed channel, with a snapshot reconciliation. ADR-0033
moves MUSTASK into the company-wide realm, where we may create only clients and
client roles: **no provider JAR can be installed there**. The projection must be
filled without anything of ours inside Keycloak.

## Options considered

### Option 1: Call Keycloak's admin REST surface from `Mustask.Api`

- Pros: sees every user, including those who never signed in; sees deactivation.
- Cons: needs a service account with `view-users` in a realm we do not own;
  ADR-0023 keeps the admin surface out of production; reads would depend on
  Keycloak being up.

### Option 2: Upsert the user at sign-in from the id token's claims

- Pros: nothing of ours in Keycloak; the data is already in the token the
  session callback has validated; no secret, no push endpoint, no reconciliation;
  the read model, the search and every consumer stay as they are.
- Cons: the projection knows only people who have signed in; it cannot learn
  of a deactivated or deleted user (below).

### Option 3: Both: sign-in upsert and an admin-REST backfill

- Pros: complete.
- Cons: carries Option 1's service account; not needed by the current scope.

## Decision

Option 2.

1. **One use case writes the projection: `RecordSignIn`.** A vertical slice
   `Mustask.Application/Features/UserDirectory/RecordSignIn` takes a
   `SignInProfile` (`sub`, `preferred_username`, `given_name`, `family_name`,
   `email` of the id token), validates it and, in one transaction, takes the
   per-user advisory lock and the row lock (`UserDirectoryLocks`, kept from
   ADR-0025), then inserts the row or updates a changed one. Two first sign-ins of
   one person at once leave one row. Rules: `sub` and `preferred_username` are
   required; every field is at most 255 code points (`UserDirectoryLimits.FieldMaxLength`);
   U+0000 and noncharacters are refused (`user_directory.validation_failed`). The
   outcome is `Added`, `Updated` or `Unchanged`; an unchanged profile writes
   nothing. A row left over from the push era as absent, deleted or disabled
   becomes present and enabled again on the person's next sign-in.
2. **When it runs.** At the session callback (`OnTokenValidated`, after the
   `user`-role gate of ADR-0033) and at a renewal in `POST /api/session/token`
   whose id token's claims differ from the ones stored last. The cookie ticket
   carries a short fingerprint of the recorded claims, so an unchanged renewal does
   not touch the database. The id token is read there, not validated again: the
   OIDC handler and `RenewedIdTokenValidator` have already done it.
3. **The failure rule.** If the upsert fails for a person the projection does not
   know, **the sign-in fails** on the existing generic path (`authError=sign_in_failed`,
   no cookie, no cache entry): a person without a row cannot be added to an
   organization, and letting them in would hide that until someone searched for them
   in vain. If the person is in the projection already, the failure is logged
   (user id only, never a name or an e-mail) and the sign-in goes on. A failed
   check of whether the person is known counts as unknown. A **renewal never ends a
   session** on a failed upsert: it is logged, no fingerprint is stored, and the next
   renewal tries again.
4. **Removed with the provider.** `deploy/keycloak/providers/user-projection`, the
   provider stage of `deploy/keycloak/Dockerfile` (the image stays Keycloak plus the
   store directory's ownership), the `mustask-user-projection` entry of the realm's
   `eventsListeners`, the push secret and the provider's environment in `AppHost.cs`,
   the push endpoints (`/api/user-directory/feed/*`) and everything only they used
   (signature verification, snapshots, batch handling), the seven push error codes
   (kept in `contracts/errors/user_directory.json` as `deprecated`, their constants
   removed), their tests, the live provider tests and the `provider` bucket of
   `scripts/verify.sh`. The live realm-import test stays (renamed to the `keycloak`
   bucket); it never depended on the provider.
5. **Consumers do not change.** Member search, the candidate lists and every other
   reader still go through `IUserDirectory` over `directory_users`.

## Consequences

- **Only people who have signed in once can be added to an organization.** The
  member and team search reads the projection; a colleague who has never opened
  MUSTASK is not found. The documents that describe the search say so (`src/backend/AGENTS.md`, the
  runbooks). Asking such a person to sign in once is the whole procedure.
- **What the projection no longer knows.** A user who was deactivated or deleted in
  Keycloak stays in the projection as of their last sign-in: it shows as active and is
  found by search. This changes the "неактивен" status of ADR-0025 for new data: it is
  no longer fed. A disabled Keycloak user cannot sign in (Keycloak refuses it), so the
  gap is a stale label and a stale search hit, never an access: access is decided by the
  token and the membership, as ADR-0027 says, and the projection authorizes nothing
  (BR-UP-12). No feature is added for it here.
- **Names and e-mails refresh only at sign-in**, and a renewal whose claims changed.
  A person who never signs in again keeps the old text; the collation sort key of such a
  row is not refreshed either (the order of a list, never its membership).
- **The client must release the claims.** The panel client needs the `profile` and
  `email` scopes: the stand's import has them in `defaultClientScopes`; in the company
  realm the client of MUSTASK must carry them. A token without `preferred_username` makes
  a new user's sign-in fail by rule 3.
- **Deliberate non-removal of the schema.** No destructive migration. The tables
  `directory_snapshots` and `directory_snapshot_pages`, and the columns
  `directory_users.moment` and `directory_users.last_snapshot_id`, stay: nothing reads or
  writes them, their entity classes are data-only, and the EF model still matches the
  database. Dropping them is irreversible on a database that has the rows; it needs its own
  task with a backup note. Suggested follow-up line (not queued by this task):
  `tech: drop the dormant push-era tables and columns of the user projection (ADR-0034)`.
- **A stand made before this change** still lists `mustask-user-projection` in the live
  realm's `eventsListeners`. the drift check no longer compares the listener, so the stand does not turn
  unhealthy because of it. What Keycloak itself does with a listener id that has no provider
  was not run here; a stand reset (`bash scripts/stand-reset.sh`) removes the entry.
- Operations: nothing to configure. No secret, no endpoint, no reconciliation period
  (`Keycloak:UserProjection:ReconcilePeriodMinutes` and the push secret are gone).
- **Amends:** ADR-0025 (all of it, except § 9: the sub-slice, the read model and the
  rule that consumers read `IUserDirectory` only, which stay). When this ADR is accepted,
  the index line of ADR-0025 is already marked "amended by ADR-0034".
