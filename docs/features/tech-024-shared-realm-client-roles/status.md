# tech-024-shared-realm-client-roles

- type: tech
- size: M
- branch: tech/024-shared-realm-client-roles
- stage: 04-backend
- epic: core/org-access
- deps: mod-016
- gates: gate-3

## Gates

gate-3: pending

## Plan

Slices touched: no feature slice, no endpoint, no migration; `api-contract.md` is not needed (the one new wire fact is `403 session.no_access` on `POST /api/session/token` and `authError=no_access` on the callback's redirect, both in the error catalog and in `docs/error-codes.md`).
1. Domain: `Mustask.Domain/Identity/InstanceRoles` — the one table role → instance codes (four roles, descriptions), `CodesOf(roles)` (union of known names; empty without `user`); unit tests incl. "every instance code is reachable by a role".
2. Api: `KeycloakPermissionClaims` maps `resource_access.<audience>.roles` through the table; `Features/Session`: `SessionAccessGate` + the callback refuses a token without `user` (`authError=no_access`, no cookie), a renewal that lost `user` ends the session (403 `session.no_access`, cookie cleared).
3. Stand: realm import = four client roles with descriptions, no realm roles/groups/defaultRole/`groups` mapper, realm name `${MUSTASK_REALM:mustask}`; AppHost reads `Keycloak:Realm` (default `mustask`) once and passes it everywhere; provisioner grants the fixture user `user` + `system-admin` as client roles (and `user` to the stand's default role); `keycloak-seed.sh` grants client roles, takes `KEYCLOAK_REALM`.
4. Retired: `--migrate-live-realm` (command, migration, 2 scripts, 3 test files, the fake write-capable admin surface, the pre-mod-016 test data -> replaced by a pre-tech-024 import used by one drift test); the drift check loses its group comparison.
5. Frontend: `tokenClaims.ts` maps the four roles to codes (mirror table), `no_access` login-failure text, 403 `session.no_access` on renewal; test fixtures build tokens from roles.
6. Docs: ADR-0033 (proposed; amends ADR-0018/0027), `src/backend/AGENTS.md`, `docs/runbooks/local-migrations.md`, `docs/error-codes.md` (generated).
Tests: Domain (table), Api claims mapping + session callback/renewal refusal + dictionaries by client role, Contracts (realm import contents, seed script, drift check with the configurable realm, provisioning), Live (opt-in) realm import.

## Log

- 2026-10-20 [new-task] task started; queue line taken in main by c9709930
- 2026-10-20 [orchestrator] route confirmed by the human in chat (relayed by the launching session): type tech, size M, Dev -> QA -> Review -> gate-3 -> MR, no spec, no design. Economy mode: one agent per stage, one Opus review for tech-024 and tech-025 together. The queue line says size:M (no divergence here). Note: the ADR and the authorization-rule change are `L` thresholds in routing.md; the human chose M knowingly. Session bound by switching the pool-made worktree `app-deployment-keycloak-9df488` onto the task branch (the session cannot relocate; the empty task worktree made by new-task.sh was removed by the human)

## Decisions

- D-1 (dev): a bearer token without `user` still authenticates (it passes JwtBearer validation) but holds no `perm` claim; the refusal with a clear code is at the SESSION (callback and renewal), where the human meets it. A client calling the API with such a token gets the existing `http.forbidden` where a right is needed.
- D-2 (dev): the callback refusal is `authError=no_access` (the closed vocabulary of the browser redirect is now four values) with a Russian line on the panel's sign-in screen; the catalog code `session.no_access` (403) is the answer of `POST /api/session/token` when a renewed token lost `user` (the panel treats it as an ended session). Listed in `contracts/errors/session.json` and `docs/error-codes.md`.
- D-3 (dev): the stand's provisioning adds `user` to the stand realm's default role (stand only, never in the import), so the E2E suite's throwaway accounts and the seeded people sign in without a mapping of their own; the fixture user and the seed script's users get their roles directly as client roles. Reason: `mustask-e2e` has `manage-users` but not `view-clients`, so the E2E helpers cannot look up the role ids, and widening a stand client for this was not warranted (ADR-0023). Revisit if the human wants a stand without any default role.
- D-4 (dev): `KeycloakRealmImport`/drift check no longer compare groups (the import declares none; groups of the live realm were already ignored). Realm roles still compare, with Keycloak's built-ins and `default-roles-<realm>` exempt; a store made from the pre-tech-024 import fails the drift check and the findings name the stand reset (tested against the old import kept as test data).
- D-5 (dev): the realm name is `Keycloak:Realm` (env `Keycloak__Realm`), default `mustask`, validated to letters, digits, `-`, `_`, `.`; the import file reads it as `${MUSTASK_REALM:mustask}` (Keycloak substitutes placeholders in any string, `realm` included) and AppHost bind-mounts the one file as `/opt/keycloak/data/import/<realm>-realm.json`, because Keycloak refuses a file whose name is not `<realm>-realm.json` (found by the opt-in `LiveRealmImportTests.TheRealmNameComesFromTheEnvironment`, which now passes against a real Keycloak). `scripts/keycloak-seed.sh` reads `KEYCLOAK_REALM`.
- D-6 (dev): no machine check that the frontend's mirror table equals the backend one — the repository has no role-bundle contract mechanism yet (`tech-026` OP-7, taken over by `mod-019`); both tables carry comments pointing at each other, the backend side is the one unit-tested against the realm import.
- D-7 (dev): test token helper `KeycloakTestRealm.CreateToken(codes)` keeps its code-based shape for the ~100 existing call sites and translates codes to the smallest role set that covers them, always including `user` (so it may add the dictionaries' read); tests that need an exact role set use `TokenOptions.Roles`.
- D-8 (dev): `LiveRealmImportTests` (opt-in, provider bucket) is rewritten for the new import and the configurable realm name; `LiveKeycloakFixture` and the user-projection live tests are untouched (tech-025). They need Docker and a built Keycloak image and were not run in this stage (see the [dev] Log line).

## Notes

Summary: MUSTASK in the company-wide Keycloak realm: only clients and client roles are ours. mustask-api holds exactly four client roles — user (sign-in, dictionaries read; no session without it), organization-creator, dictionaries-admin, system-admin; no raw permission codes, no realm roles, groups or default roles. Role-to-code mapping moves into code; the realm name becomes configuration; new ADR amending ADR-0018 and ADR-0027.
