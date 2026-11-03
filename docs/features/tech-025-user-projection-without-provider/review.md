# Review — tech-025-user-projection-without-provider

## Round 1

Scope: `git diff tech/024-shared-realm-client-roles..HEAD` at 211b2d5e (198 files), reviewed together with tech-024
in one session (economy mode, route M). Acceptance source: the queue line (status.md Notes) + ADR-0034 + status.md
D-1..D-9. No tests run (lane belongs to another session).

Verdict: **changes-needed** — 0 blockers, 1 major (prose in runtime output; fix is text only: delta read, no QA),
3 minors. QA's D-1..D-3 stand as recorded.

### Checked, no finding

- `RecordSignIn` (RecordSignInHandler.cs:27-60): validates first, then one READ COMMITTED transaction: per-user
  advisory lock (covers an id not held yet) + `FOR UPDATE`, load, add or update. Concurrent first sign-ins serialize on
  the advisory key; the second sees the committed row. Retry strategy clears the tracker.
- Trust: the row key is `sub` of an id token validated by the OIDC handler (callback) or `RenewedIdTokenValidator`
  (renewal, which also pins `sub`/`sid` to the ticket) before `RecordAsync`; `ProjectedUser.RecordSignIn` refuses a
  profile of another id. `directory_users` has no unique index on username/e-mail, so two subs sharing them get two
  rows (test N7); no claim can write another person's row.
- Gate order: the `user`-role gate runs before the upsert on both paths; a refused person is never projected.
- Failure rule (SessionUserProjection.cs:524-542, SessionAuthenticationSetup.cs:198-205): unknown → `sign_in_failed`,
  no ticket; known → logged, session goes on; failed is-known → unknown; renewal ignores the result, drops the
  fingerprint. Logs carry the user id only; `ValidationException` carries codes, not values.
- Fingerprint: SHA-256 prefix of the stored claims, kept in the data-protected cookie ticket; carried through
  `OnTicketReceived`; a forged value could at most skip a write of the holder's own row.
- Push surface: `UserDirectoryEndpoints`, signature verifier, options, startup notice, AppHost secret/env/host mapping
  all removed; `MapUserDirectoryEndpoints` gone from Program.cs; no route left under `/api/user-directory`.
- Session isolation: `ISessionSignInRecorder` in `Mustask.Api/Authentication`, implemented in
  `Features/UserDirectory`, bound in Program.cs; `Features/Session` names no Domain/Application type.
- Dormant schema: entities stay mapped, no migration; consistent with ADR-0034 Consequences.
- Seven codes `deprecated` in `contracts/errors/user_directory.json` and `docs/error-codes.md`; constants removed.

### Findings

- **M1 (major, prose in runtime output)** — `src/backend/Mustask.AppHost/Stand/StandReset.cs:234` and `:256` print
  "The first reconciliation of the user projection is the backfill." after every reset, and `scripts/stand-reset.sh:28`
  repeats it. There is no reconciliation any more (ADR-0034): an operator who reseeds after a reset and waits for the
  people to appear in member search waits forever. Fix: replace with "The user projection is filled by sign-in: each
  seeded user appears in member search after signing in once (ADR-0034)." in both plans and the script header.
- **m1 (minor)** — `scripts/verify.sh:870`: (a) the line continuation was lost (`-v q` + 21 spaces + `--filter` on one
  line; works, reads like a bug); (b) the loop still runs `Mustask.Contracts.Tests` with `Category=KeycloakLive`, which
  now matches no test; (c) `KeycloakImageTests` lost its category, and `classify` maps `deploy/keycloak/Dockerfile` to
  `keycloak` only, so a Dockerfile-only tag bump no longer runs the tag/image-equality check it exists for. Fix:
  classify the Dockerfile as `dotnet keycloak` (as the realm imports are), drop Contracts from the loop, restore the `\`.
- **m2 (minor, claim)** — ADR-0034 Decision 1 "an unchanged profile writes nothing" vs `ProjectedUser.cs:98-112`: with
  an unchanged profile but another `SortKeyVersion`, `Recompute()` rewrites the sort key while the outcome is
  `Unchanged` and `ChangedAt` is not stamped. Fix: count a version change as `Updated`, or weaken the sentence.
- **m3 (minor, carried from QA D-1)** — E2E specs 27-30 and `src/frontend/e2e/support/{membersApi,keycloakAdmin}.ts`
  still wait for a push-fed projection (e.g. `27-organization-members.spec.ts:105`, `:654`) and will time out when the
  suite is next run. Fix: the orchestrator queues the follow-up line (fixture users sign in once; drop the
  disabled-withdrawal case) before gate-3, so the debt is not only in a Log line.

### Behavioural claims (item 6)

- ADR-0034 D1 lock + one transaction + one row → ProjectedUserStore.cs:9-19, UserDirectoryLocks.cs; test
  `ConcurrentFirstSignIns_OfOnePerson_LeaveOneRow`.
- D1 limits (255 code points, U+0000, noncharacters, required sub/username) → SignInProfileValidation.cs.
- D1 "unchanged profile writes nothing" → m2.
- D1 legacy absent/deleted/disabled revived → ProjectedUser.cs:98-106.
- D2 callback after the role gate; renewal skipped on equal fingerprint → SessionAuthenticationSetup.cs:181-210,
  SessionEndpoints.cs:213-238, SessionUserProjection.cs:519-522.
- D2 "the id token is not validated again: the OIDC handler and RenewedIdTokenValidator did" → OIDC handler validates
  the token-endpoint id token before `OnTokenValidated`; SessionEndpoints.cs `idTokenValidator.ValidateAsync` precedes
  `RecordAsync`. Anchored.
- D3 failure rule incl. "no cookie, no cache entry" and "renewal never ends a session" → as above; tests C6-C8.
- D4 removals → grep below; verify bucket rename → verify.sh:290-317, 852-878 (m1).
- Consequences "documents that describe the search say so (AGENTS.md, runbooks)" → src/backend/AGENTS.md "Keycloak on
  the stand", local-migrations.md:196-199. "drift check no longer compares the listener" → KeycloakStandDriftCheck.cs
  (no `CompareListener`).
- Program.cs:179-182 and the Dockerfile header ("no provider") → anchored by the removals.
- Direction 2: grepped `reconciliation`, `backfill`, `push channel`, `mustask-user-projection`, `KeycloakProvider`,
  `provider bucket`, `UserDirectory:Push`, `feed/` over `docs/` (minus feature history, analyses, examples, ADR-0025/
  0034), `.claude/`, AGENTS files, `scripts/`, `src/backend/Mustask.AppHost`, `src/frontend/e2e`: hits =
  StandReset.cs + stand-reset.sh (M1), E2E specs/support (m3), `docs/backlog.md` tech-008 line (QA D-2),
  07-mr-complete.md (unrelated "reconciliation" of gates). Comments in Domain naming "the retired push channel" are
  correct history.
