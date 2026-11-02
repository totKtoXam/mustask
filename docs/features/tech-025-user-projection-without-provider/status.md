# tech-025-user-projection-without-provider

- type: tech
- size: M
- branch: tech/025-user-projection-without-provider
- stage: 04-backend
- epic: core/org-access
- deps: mod-016, tech-024
- gates: gate-3

## Gates

gate-3: pending

## Plan

1. Domain: `SignInProfile`/`SignInProfileValidation`/`SignInOutcome`; `ProjectedUser.SignIn`/`RecordSignIn` replace the push lifecycle.
2. Application: slice `Features/UserDirectory/RecordSignIn` (lock, load, add or update, one transaction); the four push handlers and `IDirectorySnapshotStore` go.
3. Infrastructure: `ProjectedUserStore` becomes single-id `LockAndLoadAsync` + `Add`; snapshot store and snapshot lock go; no migration (dormant tables and columns stay).
4. Api: `SessionUserProjection` called from the OIDC `OnTokenValidated` and from `POST /api/session/token` (fingerprint in the ticket); push endpoints, signature check, push options go.
5. Stand: AppHost push secret, provider env and reconcile period go; `CompareListener` and `EventsListeners` go; `providers/user-projection` and the Dockerfile provider stage go; realm `eventsListeners` drops the provider.
6. Contracts/docs: seven push codes `deprecated` in the catalog; ADR-0034 (amends ADR-0025); `src/backend/AGENTS.md`, the migrations runbook.
7. Tests: Domain and Application unit tests; session integration tests (callback, renewal, failure rule, concurrency); the push-era tests and live provider tests deleted; `verify.sh` `provider` bucket becomes `keycloak` (live realm import kept).

## Log

- 2026-11-02 [orchestrator] task started; queue line taken by PR #46 (sync/queue-2026-11-02-take-tech-025). The branch is stacked on tech/024-shared-realm-client-roles (tech-024 is in review and not merged: the same session works both; the PR of this task carries tech-024's commits until that one merges)
- 2026-11-02 [orchestrator] size divergence: the queue line says size:L, the human confirmed M (chat 2026-10-20, relayed by the launching session, and again by the form answer «M, как в чате» on 2026-11-02). Route: Dev -> QA -> Review -> gate-3 -> PR, no spec, no design; one Opus review covers tech-024 and tech-025 together. routing.md would make it L (an ADR amending ADR-0025 and a change of the authentication rules); the human chose M knowingly. Economy mode: one agent per stage
- 2026-11-02 [orchestrator] session binding: this session cannot relocate; it switched the pool-made checkout onto the task branch (as for tech-024)

- 2026-11-02 [dev] stage 4 done in code: build of Mustask.sln green; no test was run (the machine's test lane belongs to another session); ADR-0034 proposed; the E2E suite is untouched and its members specs still wait for the push-fed projection (follow-up for the human: E2E fixture users must sign in once)

## Decisions

- D-1 The upsert is a vertical-slice use case `Features/UserDirectory/RecordSignIn`; it runs at the session callback and at a renewal whose claims differ (fingerprint of the claims in the cookie ticket; an unchanged renewal does not touch the database).
- D-2 Failure rule: a failed upsert of a person the projection does not know fails the sign-in on the existing generic path (`authError=sign_in_failed`); for a known person it is logged (user id only) and the sign-in goes on. A renewal never ends a session on a failed upsert. A failed "is known" check counts as unknown.
- D-3 No destructive migration. Left on purpose: tables `directory_snapshots`, `directory_snapshot_pages`, columns `directory_users.moment`, `last_snapshot_id`; the entity classes are data-only. Follow-up line suggested in ADR-0034 (not queued).
- D-4 The seven push error codes stay in `contracts/errors/user_directory.json` as `deprecated`; their constants are gone.
- D-5 A legacy absent, deleted or disabled row becomes present and enabled at the person's next sign-in.
- D-6 The "email" scope is not added to the OIDC request: the stand's panel client has it in `defaultClientScopes`; the company realm's client must carry `profile` and `email` (ADR-0034).
- D-7 ADR-0034 (proposed) amends ADR-0025; 0032 is feat-043's proposal, 0033 is tech-024's.
- D-8 `LiveRealmImportTests` (tech-024's real-Keycloak check of the realm import) is not provider-specific and stays, moved to `Features/Realm/Live`; the `provider` verify bucket is renamed `keycloak` and runs it.
- D-9 The Dockerfile stays (AppHost builds it) as `FROM keycloak` plus the store directory's ownership.

## Notes

Summary: Users reach MUSTASK without our own Keycloak provider (the company realm takes no JAR): upsert the user on first sign-in from token claims instead of the ADR-0025 event-listener; member search reads that projection, so only users who signed in once can be added to an organization. Amends ADR-0025.
