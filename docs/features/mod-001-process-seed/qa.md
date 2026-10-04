# QA — mod-001-process-seed

## Backend

Pass 1, 2026-10-02, qa-engineer (deep tier, L task), clean context. The
plan below was written from `spec.md`, both stories and `api-contract.md`
before any line of the implementation or `plan-backend.md` was read, and
committed on its own (7d81aa1). The Status column was filled in afterwards,
from the test code and the runs. The frontend half of US-2 (what the screen
shows, the «Процесс» entry, the absence of controls) is the Frontend
section's, at stage 7. Tests added by this pass are marked **(QA)**; they
are in 96bfbdc.

### Coverage matrix

| Criterion (spec) | Check | Test type | Status |
|-----------------|----------|-----------|--------|
| US-1 / A new identifier receives the complete seeded process | `SeededProcessTests` (types and keys, states in order with category codes, «Задача» without Resolved, first initial and «Удалена» last, levels with rank and kind, each type's level, distinct tokens, 34 distinct ids incl. equal labels); `ProcessSeedTests.SeedAsync_NewOrganization_PersistsOneCompleteProcessAndReturnsItsId` (stored rows = blueprint, returned id = stored id); `SeedProcessHandlerTests` (the store gets the complete candidate, its id comes back) | unit + integration | ☑ |
| US-1 / Each identifier gets a process of its own | `SeedAsync_SecondOrganization_CreatesASeparateEqualProcess`; `SeededProcessTests.Seed_TwoOrganizations_ShareNoIdentity` | integration + unit | ☑ |
| US-1 / A failed seed leaves nothing behind and says so | `SeedAsync_StorageFailsMidway_ThrowsAndLeavesNoRowsEvenIfCallerCommits`, `SeedAsync_AfterAFailedAttempt_CreatesTheCompleteProcess` (statement failed before it is sent); **(QA)** `SeedAsync_DatabaseRejectsALateRow_LeavesNoRowsAndTheCallersTransactionUsable` (a trigger rejects TASK «Удалена» after the root, levels and types are rows of the database; the caller swallows, its own write commits, nothing of the process does; the next call seeds 1/4/5/24) | integration | ☑ |
| US-1 / A malformed identifier is refused | `SeedAsync_EmptyOrganizationId_WritesNothing`; `SeedProcessHandlerTests.SeedAsync_EmptyOrganizationId_ThrowsWithoutCallingTheStore`; `SeededProcessTests.Seed_EmptyOrganizationId_Throws` | integration + unit | ☑ |
| US-1 / A caller's failed operation takes the seed with it | `SeedAsync_CallerFailsAfterTheSeed_LeavesNoProcess` | integration | ☑ |
| US-1 / A repeated call creates nothing, hands back the same process | `SeedAsync_SameOrganizationTwice_ReturnsTheSameIdAndCreatesNothing` (ids and row counts unchanged); `SeedAsync_CalledTwiceInOneUnitOfWork_ReturnsTheSameId` | integration | ☑ |
| US-1 / Two racing calls leave exactly one process | `SeedAsync_TwoConcurrentCalls_LeaveExactlyOneProcess` (B blocks on A's lock, then returns A's id); `SeedAsync_RivalRollsBack_SecondCallCreatesTheProcess`; **(QA)** `SeedAsync_EightCallsReleasedAtOnce_LeaveOneProcessAndAllReturnItsId` (no orchestration, all eight return the one id) | integration (concurrency) | ☑ |
| US-1 / No person can seed or change a process | `OpenApiDocument_ProcessOperations_AreOnlyTheRead`; **(QA)** `WriteMethod_OnTheProcessRoute_Returns405AndSeedsNothing` (POST/PUT/PATCH/DELETE with every realm code: 405, and the id still answers not found) | integration | ☑ |
| US-2 / A seeded process is shown in full (backend half) | `Get_SeededOrganization_ReturnsFiveTypesInBacklogOrderWithStatesTokensAndLevels` (body = contract § 1.4, id = seeded id); `..._MarksOnlyTheFirstStateOfEachTypeAsInitial`; `..._HasExactlyTheContractShapeAndNoNulls`; `ProcessDefinitionReadModelTests` (order, identities, three queries); `GetProcessDefinitionHandlerTests.Handle_FoundProcess_*` | integration + unit | ☑ |
| US-2 / The Development stand shows a demonstration process | `DevelopmentHost_Started_HasTheDemonstrationProcess`; `Get_DemonstrationOrganization_ReturnsTheFullDefinition`; `RunAsync_Again_CreatesNothingNew`; `NonDevelopmentHost_DoesNotRegisterTheSeed` (Production, Staging); `DevelopmentHost_Started_RegistersTheSeedOnceAfterTheDictionariesSeed`; `DemonstrationOrganizationDocumentationTests` (id and the three events in `src/backend/AGENTS.md`) | integration + contract | ☑ |
| US-2 / An unmigrated Development stand starts without the demonstration process | `DevelopmentHost_AllButThisTasksMigration_StartsAndLogsSkipped` (whole host; one Skipped warning; no tables; the read answers 500); `RunAsync_NoMigrationApplied_LogsSkippedAndCreatesNothing`; `MigrationsStateTests` | integration | ☑ |
| US-2 / A failed demonstration seed does not stop the stand | `RunAsync_SeedFailsMidway_LogsFailureLeavesNothingAndNextRunSeeds` (rows written, then a failure: Failed logged, 0/0/0/0, next run 1/4/5/24) | integration | ☑ |
| US-2 / Every signed-in user sees the same definition | `Get_UserWithEveryRealmCodeAndUserWithNone_SeeIdenticalDefinitions`; `Get_TokenCarryingTheDeclaredCodeAndTokenWithoutResourceRoles_SeeIdenticalDefinitions` (byte-identical bodies) | integration | ☑ |
| US-2 / Categories are shown by their Russian labels (backend half) | `Get_SeededOrganization_TaskStatesCarryRussianCategoryLabels`; `WireNamesTests.StateCategoryLabels_EachCategory_HasItsDoc010Label` | integration + unit | ☑ |
| US-2 / One state label belongs to two categories (backend half) | `Get_SeededOrganization_ResolvedOfStoryAndBugDifferInCategory`; handler counterpart | integration + unit | ☑ |
| US-2 / An identifier with no process is not found | `Get_UnknownOrganization_Returns404WithNotFoundCodeAndNoProcessData` (**(QA)** now also asserts `traceId`); `Get_MalformedOrganizationId_ReturnsTheSameAnswer` (12 values, 5 of them **(QA)**: leading space, parentheses, Cyrillic, SQL text, an encoded `/` in the segment); **(QA)** `Get_VeryLongOrganizationId_ReturnsTheNotFoundCode` (2000 characters); `OrganizationKeyTests`; `Handle_MalformedId_ThrowsNotFoundWithoutReading` | integration + unit | ☑ |
| US-2 / The not-found code is registered with its texts | `OrganizationErrorCatalogTests` (organizations module, 404, ru «Организация не найдена.», kk differs from ru, row in `docs/error-codes.md`) | contract | ☑ |
| US-2 / A failed read is not taken for a missing organization (backend half) | `Get_ReadFails_Returns500WithoutTheNotFoundCode`; the real database failure (schema missing) in `DevelopmentHost_AllButThisTasksMigration_StartsAndLogsSkipped` answers 500 `http.internal_error`; `Handle_ReadFails_PropagatesTheFailureAndDoesNotBecomeNotFound` | integration + unit | ☑ |
| US-2 / A visitor who is not signed in sees nothing (backend half) | `Get_NoToken_Returns401WithNoProcessData`; `Get_NoToken_AndMalformedId_StillReturns401`; **(QA)** `Get_InvalidToken_Returns401WithNoProcessData` (expired, foreign issuer, foreign audience, not a token) | integration | ☑ |
| US-2 / The read's code is registered, and nothing grants it yet | `OrganizationPermissionCatalogTests` (exact name, outside `PermissionCatalog.AllCodes`); `OrganizationPermissionRealmTests` (no mention anywhere in the realm import; the scan is shown non-vacuous) | unit + contract | ☑ |
| US-2 / No visible entry or link leads to the screen before mod-002 | Frontend only | — (stage 7) | n/a |
| US-2 / Nothing on the screen changes the process | Frontend only; the backend half is US-1 "No person can seed or change a process" | — (stage 7) | n/a |

### Additional scenarios (beyond the spec)

| # | Scenario | Expectation | Status |
|---|----------|----------|--------|
| A-1 | GET with the seeded id in upper case | 200, the same process (contract § 1.1: hex digits in either case) | ☑ `Get_UppercaseCanonicalId_ReturnsTheSameDefinition` |
| A-2 | GET with non-canonical GUID spellings of a seeded id: 32 hex digits without hyphens, `{…}` braces, `(…)` parentheses, leading/trailing whitespace (`%20`) | 404 `organizations.organization.not_found` (contract § 1.1: only the canonical form is accepted) — not 200, not 400 | ☑ malformed theory, the demonstration id is the seeded one |
| A-3 | GET with `00000000-0000-0000-0000-000000000000` | 404 not-found code | ☑ malformed theory |
| A-4 | GET with garbage ids: `abc`, a 2000-character string, `'; drop table x; --`, Cyrillic text, an encoded `%2F` | 404 not-found code (or, for `%2F`, a route-level answer that is not 200 and not 500); never 400/500 | ☑ (QA) all answer 404 with the not-found code, `%2F` included |
| A-5 | POST, PUT, PATCH, DELETE on the process route | 405 (or 404 route), nothing written, row counts unchanged | ☑ (QA) 405; the id stays not found |
| A-6 | The read targets the right process: seed A and B, GET A and GET B | each answers its own `id`; no id from the other | ☑ every GET test seeds its own id in a database holding many processes and asserts its own `id` |
| A-7 | Two GETs for the same id | identical bodies, ids stable | ☑ the two identical-definition tests |
| A-8 | A process seeded in a still-open transaction, read from another session | 404 until the caller commits (contract § 2 "Until the caller commits, no other session sees the process") | ☑ (QA) `Get_WhileTheSeedingOperationIsOpen_IsNotFoundUntilItCommits` |
| A-9 | Racing seeds where the first transaction rolls back | the waiting call creates the process itself and returns its id; exactly one process | ☑ `SeedAsync_RivalRollsBack_SecondCallCreatesTheProcess` |
| A-10 | Seed with no ambient transaction | `InvalidOperationException`, nothing written | ☑ `SeedAsync_WithoutAmbientTransaction_ThrowsAndWritesNothing` |
| A-11 | Seed with pending changes in the context | `InvalidOperationException`, nothing written | ☑ `SeedAsync_WithPendingCallerChanges_ThrowsAndWritesNothing` |
| A-12 | Seed fails partway, the caller swallows the exception and commits its own unit of work | still nothing of a process for the id (the savepoint is rolled back and the change tracker holds no stray seed entity that the caller's save would flush) | ☑ interceptor case (existing) and database-side case (QA); the caller's own write commits |
| A-13 | Seed twice for one id inside one unit of work | same id both times, no duplicate rows | ☑ `SeedAsync_CalledTwiceInOneUnitOfWork_ReturnsTheSameId` |
| A-14 | Seed cancelled through its `CancellationToken` | treated as a failure, nothing remains | ☑ `SeedAsync_CancelledMidway_LeavesNoRowsAndTheUnitOfWorkUsable` |
| A-15 | Database constraints guard the invariants on their own: one process per organization id, unique key per process, unique state order per type | unique indexes exist in the migration | ☑ `ProcessSchemaGuardTests` (9 unique indexes, 3 restrict deletes); the gap D-2 is closed by the composite key |
| A-16 | Migration `AddProcessDefinitions` applies on a clean database, rolls back to the previous migration, and applies again | Up → Down → Up green; the Down drops exactly this task's tables | ☑ `MigrationsTests.Migrator_AddProcessDefinitions_CreatesTheFourTables_AndRollbackRemovesThem`, `MigrateAsync_CalledTwiceOnSameDatabase_IsIdempotent`; scripts generated both ways (Runs) |
| A-17 | Error body shape for 404/401/500 | `application/problem+json`, `status`, `detail`, `errorCode`, `traceId` | ☑ 404 full shape (QA added `traceId`); 401 and 500 assert `errorCode` |
| A-18 | Non-Development environments (Testing, Production) over a migrated database | no demonstration seed hook registered, no process after start | ☑ Production and Staging: the hook is not registered |
| A-19 | `DemonstrationSeedReady` logged when the process is created and when it already exists | one Information event per start | ☑ `RunAsync_Again_CreatesNothingNew`, `RunAsync_SeedFailsMidway_...` (second run) |
| A-20 | Organization-scoped registry vs. instance catalog | no intersection both ways; the registry is not empty (the test is not vacuous) | ☑ `AllCodes_DoNotIntersectPermissionCatalog`, `RealmScan_ReachesTheDictionaryCodesTheRealmDoesDeclare` |

### Runs

All on 2026-10-02, in this worktree, against Testcontainers PostgreSQL
(`postgres:18-alpine`); no stand was started.

| Date | What was run | Result |
|------|-----------------|-----------|
| 2026-10-02 | `dotnet build Mustask.sln` | exit 0, 0 warnings |
| 2026-10-02 | `dotnet test Mustask.Infrastructure.Tests --filter FullyQualifiedName~...Process` (before the additions), TRX | exit 0, 31/31; class order in the collection: `ProcessSeedTests` → `ProcessSchemaGuardTests` → `ProcessDefinitionReadModelTests` (the D-1 hypothesis) |
| 2026-10-02 | the same project, a 5-test subset (the guard's committing insert plus the four ratio-asserting seed tests), to test the D-1 hypothesis | exit 0, 5/5; xUnit kept `ProcessSeedTests` first, so D-1 is latent, not live |
| 2026-10-02 | `dotnet ef migrations has-pending-model-changes --project src/backend/Mustask.Infrastructure` | exit 0, "No changes have been made to the model since the last migration." |
| 2026-10-02 | `dotnet ef migrations script InitialCreate AddProcessDefinitions --idempotent` and the reverse | exit 0 both; Up: 4 tables, 9 unique indexes, no `xmin` column emitted; Down: drops exactly the 4 tables |
| 2026-10-02 | Infrastructure Process tests after the additions, plus a temporary probe for D-2 (removed before the commit) | exit 0, 34/34; the probe's insert of a type on another process's level succeeded (1 row) |
| 2026-10-02 | `dotnet test Mustask.Api.IntegrationTests --filter FullyQualifiedName~...Features.Process` after the additions | exit 0, 44/44 |
| 2026-10-02 | `bash scripts/verify.sh` on 96bfbdc plus this qa.md and the status.md Log line: links, backlog ids, status.md budget, spec hygiene, agent configuration, build, the whole backend suite | exit 0, RESULT: green; Domain 102, Architecture 68, Shared 53, Application 60, Contracts 61, Api.IntegrationTests 390, Infrastructure 112 — 846 passed, 0 failed, 0 skipped. Only this row was added after it. |

### Defects

#### D-1: A guard test commits extra rows into the shared database that the seed tests count

- Severity: minor (test only; no product effect)
- Steps: `ProcessSchemaGuardTests.Insert_SameStateNameInAnotherType_IsAllowed`
  inserted two `state_definitions` rows with autocommit into the "Process
  database" fixture that `ProcessSeedTests` shares.
  `ProcessSeedTests.AssertOnlyCompleteProcessesAsync` asserts
  `states == processes * 24` over the whole database, in four tests
  (`StorageFailsMidway`, `AfterAFailedAttempt`, `TwoConcurrentCalls`,
  `RivalRollsBack`). Run the guard test before any of them against the same
  fixture (any change of xUnit's class order in the collection: a renamed or
  added class) and each fails with `24n + 2` states.
- Expected / Actual: tests pass in any order / they pass only because xUnit
  currently runs `ProcessSeedTests` first (Runs, rows 2 and 3).
- Status: fixed by QA in 96bfbdc: the insert runs in a transaction that is
  rolled back (`ExecuteAndRollBackAsync`); what the test proves is unchanged.

#### D-2: The database accepts a type of one process on a backlog level of another

- Severity: minor
- Steps: seed processes A and B (any two organization ids), then on the
  database:
  `INSERT INTO work_item_types (id, process_id, backlog_level_id, key, name, appearance, position) SELECT gen_random_uuid(), '<A.id>', (SELECT id FROM backlog_levels WHERE process_id = '<B.id>' AND name = 'Эпики'), 'QAX', 'Чужой', 'qax', 99;`
- Expected / Actual: refused, as every other within-process invariant of
  this task is refused by an index of its own (`ProcessSchemaGuardTests`):
  BR-M2-1 says two identifiers never share a backlog level, and ADR-0011's
  first echelon holds an invariant the database can express with a
  constraint / 1 row inserted (verified against the Testcontainers database,
  rolled back). `fk_work_item_types_backlog_levels_backlog_level_id`
  references `backlog_levels (id)` alone. No writer other than the seed
  exists in MVP, which always builds a consistent process, so nothing
  reaches this today; the release-3 process editor is where it would.
- Suggested fix (the developer's call): a unique key on
  `backlog_levels (process_id, id)` and a composite foreign key
  `work_item_types (process_id, backlog_level_id)` onto it, in
  `AddProcessDefinitions` while it is unmerged; or "won't fix" recorded
  with the reason.
- Status: fixed by the developer (status.md D-14) in this task's own
  migration `AddProcessDefinitions`, regenerated and still the only one:
  `backlog_levels` gained the key `(process_id, id)` and the foreign key of
  `work_item_types` is `(process_id, backlog_level_id)` onto it, shown by
  `Insert_TypeOnABacklogLevelOfAnotherProcess_IsRejectedByTheCompositeForeignKey`
  (failed before, passes after), and `state_definitions` needs no
  counterpart because it holds no process id of its own.

### Verdict

blockers: 0 · majors: 0 · minors: 2 (D-1 fixed, D-2 fixed by the developer) · result: pass

## Frontend

Pass 1, 2026-10-04, qa-engineer (deep tier, L task), clean context. The
plan below (coverage matrix and additional scenarios) was written from
`spec.md`, US-2, `design.md` (layout B: `mod-015` is in this branch's base,
c48b565), `api-contract.md` § 1 and `mod-015`'s design § 3.4, § 4, § 7 and
§ 15 before any line of `src/frontend/src/features/process`, the route,
the navigation entry, `plan-frontend.md` or E2E spec 20 was read, and is
committed on its own. The Check column names what must prove the row; the
test names and the Status column are filled in afterwards. US-1 has no
screen (BR-M2-13) and is the Backend section's. E2E: consented, D-12.

### Coverage matrix

| Criterion (spec / design) | Check | Test type | Status |
|-----------------|----------|-----------|--------|
| US-2 / A seeded process is shown in full: five cards | the five types «Эпик», «Фича», «Пользовательская история», «Баг», «Задача» in the order received (DD-2, contract § 1.4); each a `section` named by its `h2` heading | component (MSW) + e2e | ☑ `ProcessDefinitionPage.test` "shows the five types as sections named by their level-2 headings, in the order received"; **(QA)** "renders types and states in the order the answer gives them and sorts nothing itself" (the fixture is already in § 1.4 order, so only a reversed answer proves the client does not sort); e2e 20.1 (`h2` texts against § 1.4 on the real backend) |
| … per card: key, backlog level | facts «Ключ» and «Уровень бэклога» with `EPIC`/`FEAT`/`US`/`BUG`/`TASK` and «Эпики»/«Фичи»/«Истории»/«Истории»/«Задачи» as a `dl` | component + e2e | ☑ "gives each type its key, its backlog level and its states in order, the initial one marked"; `ProcessTypeCard.test` "lists «Ключ» and «Уровень бэклога» as a description list, without «Оформление»". e2e 20.1 does not read keys or levels; the real body equals § 1.4 by the backend's `Get_SeededOrganization_ReturnsFiveTypesInBacklogOrder...`, and the component renders the body's values |
| … per card: states in order, initial marked | table «Статусы типа «<name>»», columns «Порядок», «Статус», «Категория статуса»; rows in `order`; «Начальный» on the first row only; «Задача» four rows, the others five | component + e2e | ☑ the same page test (every type: names, `order`, the chip on row 1 only, five chips in all); `ProcessTypeCard.test` "names the states table, heads its columns with scope=col and its rows by the status", "marks only the initial state with an inert «Начальный» chip"; e2e 20.1 («Задача»: four rows, order, «Начальный») |
| … per card: category label beside each state | the label from the answer in every row | component + e2e | ☑ the same page test (every row's label = the body's); e2e 20.1 |
| … layout B: token drawn through the theme | the type mark comes from the theme's `typeAppearance(token)`, before the name, `aria-hidden`; no token name (`epic`, `story`…) as text anywhere on the page; no «Оформление» fact | component | ☑ "draws each type’s token through the theme — its icon in its colour — and never prints the token"; `ProcessTypeCard.test` "is a section named by its level-2 heading, the type mark hidden before the name"; e2e 20.1 (an `svg` in each heading) |
| … layout B: category colour beside the label | a marker per row in the theme's `categoryColour(code)`, `Removed` a ring, `aria-hidden` | component | ☑ "puts each state’s category colour beside its label, «Удалённые» as a ring"; `CategoryCell.test` (four dots, the ring, an unknown code) |
| … layout B: the shell and canon frames | page header `h1` «Процесс» (the only `h1`), tab «Процесс — Mustask», trail «Настройки / Организация» (mod-015 § 3.4, § 15); description T-02, T-03; loading, read failed and not found through `mod-015`'s skeleton/`ErrorBanner`/`StateFrame` | component + e2e | ☑ `routes.process.test` "renders inside the shell with exactly one h1 «Процесс» and the tab «Процесс — Mustask»"; "renders the title and the two description sentences above the cards"; the three frames in `ProcessDefinitionPage.states.test`; e2e 20.1 (tab). The trail shows only once the context is real (`AppLayout.process.test`, stub real source); on the placeholder source there is none, which design point 2 and point 4 now say too (status.md D-15): D-3 closed in re-check 1 |
| US-2 / The Development stand shows a demonstration process (screen half) | opening `/organizations/d0000000-0000-4000-8000-000000000001/settings/process` on a cold stand shows the full definition | e2e | ☑ e2e 20.1 on the cold stand of run 2 (Runs) |
| US-2 / Every signed-in user sees the same definition | the page makes no `can(...)` call; a user with every realm code and one with none render identical content (component: two auth states; e2e: the admin and a user with no group) | component + e2e | ☑ `routes.process.test` "shows the identical page to a user holding every code and to one holding none" (the `main` text equal); the page test outside the shell, where the shared check refuses every code; e2e 20.4 (the fixture user and a throwaway user in no group, `main` inner text equal) |
| US-2 / Categories are shown by their Russian labels | «Задача»: «Новая» → «Новые», «Активна» → «В работе», «Закрыта» → «Завершённые», «Удалена» → «Удалённые»; no category code (`Proposed`, `InProgress`, `Resolved`, `Completed`, `Removed`) and no English name in the page text; the label shown is the answer's (no client map: a different label in the answer is what shows) | component + e2e | ☑ "shows «Задача»’s states with «Новые», «В работе», «Завершённые» and «Удалённые»", "shows no category by its code anywhere on the screen", "holds no map of its own: a label the server words differently is shown as sent"; e2e 20.1 |
| US-2 / One state label belongs to two categories | «Разрешена» of «Пользовательская история» → «В работе», of «Баг» → «Решённые», in the same render | component + e2e | ☑ "reads «Разрешена» as «В работе» on «Пользовательская история» and «Решённые» on «Баг», in two colours"; e2e 20.1 |
| US-2 / An identifier with no process is not found (screen half) | an unknown and a malformed id → the not-found frame: `h1` «Организация не найдена», hint «Проверьте адрес или вернитесь на главную.», link «На главную» to `/home`; no title «Процесс», no description, no type, state or level; tab «Организация не найдена — Mustask»; no trail; the server's `detail` not shown | component + e2e | ☑ `ProcessDefinitionPage.states.test` "replaces the whole page with the shared frame, its h1, hint and «На главную», and sets the tab", the malformed and encoded-slash cases; **(QA)** eight more addresses (F-A2); `routes.process.test` in the shell (no «Меню настроек», nothing current, no trail); e2e 20.2 (a random uuid and `not-a-uuid` on the real backend, «На главную» opens «Главная») |
| US-2 / A failed read is not taken for a missing organization (screen half) | `500`, network failure → title and description kept, alert «Не удалось загрузить процесс» with `apiErrorText` and «Повторить»; neither «Организация не найдена» nor any part of the process | component + e2e (failure injected) | ☑ the ten-answer outline in `ProcessDefinitionPage.states.test`; the unmigrated-stand 500; e2e 20.3 (the read refused at the network layer, then «Повторить» against the real backend) |
| US-2 / A visitor who is not signed in sees nothing (screen half) | the address opened with no session lands on the sign-in flow, nothing of the process shown, no read sent | e2e (+ the route sits under the auth guard) | ☑ `routes.process.test` "gets the sign-in screen, no part of the page, and no request for the process"; e2e 20.5 (to Keycloak's authorize endpoint, no read; the read without a token answers 401 with no process data) |
| US-2 / No visible entry or link leads to the screen before mod-002 | the navigation model holds «Процесс» in settings «Организация» with code `process.definition.read`, scope organization, address `/organizations/:o/settings/process`, hidden while the switcher reads placeholder data; on screen no link with that address in the main navigation, «Меню настроек», the home page or «Языки»; opening the address shows the process | unit (model) + e2e | ☑ `visibility.test` (the entry, its code and scope, landing, hidden on placeholder data for every and no code, shown once real); `AppLayout.process.test` («Главная», «Языки» and the process address: no «Процесс», no link to it); `routes.process.test` "opening the address with a seeded identifier shows the process"; the only references to the address in `src` are the route, the slice's `paths.ts` and the model (grep). Not in e2e: the screen part is the shell's filter, pinned by the component tests |
| US-2 / Nothing on the screen changes the process | in content state the main region holds no button, link, input, checkbox, menu or editable cell; the «Начальный» chip is inert (no click, no delete, not focusable); the only controls the page ever renders are «Повторить» (read failed) and «На главную» (not found) | component + e2e | ☑ "holds no button, link, field, checkbox, combobox or menu, and the chips never take focus", "gives Tab nothing to reach inside the content after the title", "sends exactly one GET to the read, and nothing else"; the failed-read and not-found tests assert their single control; e2e 20.1 (no button, link or textbox in `main`) |
| design / Loading | while the read is pending: the title and description, a region `role="status"` named «Загрузка данных» with the real column headers; no type name; no spinner | component | ☑ "shows the title, the description and one status «Загрузка данных» with the real headers, and no type"; `ProcessDefinitionSkeleton.test` (incl. reduced motion) |
| design / Read failed: branch on `errorCode` only | `404` with `http.not_found`, `404` with no problem body, `403`, `503`, a `200` with no types, a `200` that is not JSON → read failed, never not found and never «Доступ ограничен» | component | ☑ the outline covers all of them plus a `404` whose text alone reads «Организация не найдена.» and a `401` after renewal; `readFailure.test`, `processApi.test`. A `200` whose body lacks a part (a type with no `states`, no `backlogLevel`, a state with no `category`) was the crash screen at pass 1 (D-4) and is read failed since 651f27c: closed in re-check 1 |
| design / Retry and focus | «Повторить» → the skeleton replaces the alert → success shows the content and moves focus to the title; a second failure brings the alert back with focus on «Повторить» | component | ☑ "shows the skeleton while a retry runs, then the content, with focus on the title", "brings the alert back when the retry fails again, with focus on its new «Повторить»" (the retry's answer held, the clicked button gone, focus on another node: Re-check 2 below; as first written it passed with the focus call removed, Correction F6), "moves no focus on a first load"; e2e 20.3 (focus on the title after the retry). A retry that ended in not found dropped focus to the document at pass 1 (D-5); since bcef95f focus goes to the frame's `h1`: closed in re-check 1 |
| design / The identifier is not parsed, it is sent URL-encoded | the read goes to `/api/organizations/<encodeURIComponent(id)>/process`; an id holding `/`, `?`, `#`, a space or Cyrillic reaches the server as one segment and answers not found, never a read failure from a different route | component (request URL) + e2e | ☑ the two `states.test` cases; **(QA)** `ProcessDefinitionPage.addresses.test` (eight addresses, each request URL exact); the live API routes every one of those segments to the read's endpoint (Runs, the unauthenticated probe) |
| contract § 3 / The error code is one constant, one to one with the contract | the client's constant equals `organizations.organization.not_found` in `contracts/errors/organizations.json` (drift test) | unit | ☑ `errorCodes.test` (module `organizations`, the code present, status 404) |
| NFR Appearance / no colour literal, no fixed dimension outside `app/theme` | the slice and its changed shared files hold no hex/`rgb(` colour and no `px` size; mod-015's machine check covers the new slice | static check | ☑ `src/test/colourLiterals.test.ts` scans all of `src/` but `app/theme`, tests and `src/test`, so the slice is in it; a QA grep of `features/process` (non-test) for `px`, hex, `rgb(` and numeric widths/heights finds nothing; sizes are `theme.spacing` and `theme.layout.categoryDot` |
| NFR Language / design § Interface texts | every T-01..T-24 string on the screen exactly as written; T-26 superseded by mod-015 § 15 «Настройки / Организация» | component | ☑ T-01..T-05, T-07..T-11, T-18, T-19, T-21..T-24 asserted verbatim in the tests above; T-06 and T-17 are layout A / pre-R-26 and rightly absent; T-12..T-16 come from the answer (R-26); T-20 is `apiErrorText`. T-26 now reads «Настройки / Организация», shown once the context is real (status.md D-15), and `AppLayout.process.test` asserts that text: D-3 closed in re-check 1 |

### Additional scenarios (beyond the spec)

| # | Scenario | Expectation | Status |
|---|----------|----------|--------|
| F-A1 | The demonstration id typed in upper case | the content (contract § 1.1 accepts either case) | ☑ the client sends the id as typed (**(QA)** upper-case case of `ProcessDefinitionPage.addresses.test`); the server answers the upper-case seeded id with the process (Backend A-1), and the live API routes it to the read (Runs) |
| F-A2 | Ids: `not-a-guid`, all zeros, 32 hex digits without hyphens, `{…}`, a 300-character value, `%2F` inside the segment, `abc%3Fx`, `abc%23x`, Cyrillic | not found every time; never read failed, never a blank page or a crash screen | ☑ **(QA)** `?`, `#`, a space, Cyrillic, `%`, a broken percent escape (React Router keeps it raw and warns; the page re-encodes it, `%25E0%25A4%25A`), 300 characters, upper case: each one encoded segment and the not-found frame; `not-a-uuid` and `%2F` by the developer's tests and e2e 20.2; the GUID spellings are the server's (Backend A-2, A-3) since the client never parses (DD-4) |
| F-A3 | In one session, open the demonstration id, then an unknown id, then the demonstration id again (client-side navigation, back/forward) | each address shows its own state; never the previous id's content under the new address; loading in between | ☑ **(QA)** "never shows one id’s process under another id’s address, and keeps a read answer for the session" |
| F-A4 | An unknown appearance token in the answer | the theme's fallback mark, the token name not shown, nothing throws | ☑ `ProcessTypeCard.test` "draws an unknown appearance token with the theme’s fallback icon, and nothing fails" |
| F-A5 | An unknown category code in the answer | the answer's label shown, the fallback colour, never the code; nothing throws | ☑ `CategoryCell.test` "gives an unknown code a filled dot in the secondary text colour, and its label" |
| F-A6 | Types arrive in a different order than § 1.4 | rendered in the order received (contract § 1.2: the client does not sort) | ☑ **(QA)** "renders types and states in the order the answer gives them and sorts nothing itself" |
| F-A7 | Keyboard on the content state | nothing in the main region to Tab to; the chip never takes focus | ☑ "gives Tab nothing to reach inside the content after the title"; the chip assertions |
| F-A8 | The shell's settings navigation on this address while «Процесс» is hidden (design point 2, mod-015 BR-19): a holder of `dictionaries.languages.read`, and a user with no settings entry | as the approved design point 2 says, or a recorded decision that replaces it | ☑ re-check 1: at pass 1 ☒ **D-3** (no «Меню настроек», nothing current, no trail for a «Языки» reader, while design point 2 promised them; no decision recorded). Decided as status.md D-15 and design.md narrowed to match; `AppLayout.process.test` pins both rows |
| F-A9 | «На главную» in the not-found frame | opens `/home` | ☑ `states.test`, `routes.process.test` (the real «Главная»), e2e 20.2 |
| F-A10 | Window width below and above the theme's `lg` (1280) | one column of cards below, two at and above; no horizontal page scroll at 1024 | ☑ e2e 20.6 in a real browser: two cards on one line at 1280, one column at 640, no sideways page scroll at either; 1024 itself is not measured on this page (19.3 measures the shell there) |
| F-A11 | A read that answers 401 (token refused mid-session) | the panel's existing session handling, not «Организация не найдена» | ☑ the outline's "401 after the client’s own renewal": read failed with «Повторить» |
| F-A12 | The answer is kept for the session (`staleTime`): leave the page and come back | no second read for the same id; a different id reads again | ☑ **(QA)** the F-A3 test counts the reads: two for three visits |
| F-A13 | Found while testing: a read that failed (500), then «Повторить» gets the not-found code (contract § 4: an unmigrated stand migrated before its next start) | the not-found frame replaces the alert; focus does not drop to the document | ☑ **(QA)** "replaces the alert with the not-found frame when «Повторить» gets the not-found code"; focus: **D-5**, closed in re-check 1 (focus on the frame's `h1`) |
| F-A14 | Found while testing: a `200` whose body lacks a part (a type with no `states` or no `backlogLevel`, a state with no `category`) | read failed with «Повторить» (design § States: "an answer that cannot be read") | ☑ re-check 1: at pass 1 ☒ **D-4** (the app's crash screen); read failed since 651f27c |

Tests added by this pass are marked **(QA)**; they are in
`src/frontend/src/features/process/pages/ProcessDefinitionPage.addresses.test.tsx`
(a566e51; F-A13 in 32ec376). Each one was checked to fail
against the opposite behaviour by reading what it asserts: an id sent
unencoded would reach another URL, a refetch on return would add a third
read, a sorted list would not be reversed.

### Runs

All on 2026-10-04 in this worktree. The stand ran against this task's own
database `mustask_mod001` on the shared PostgreSQL (external mode, the
string from AppHost's user secrets with the database name replaced,
exported as `ConnectionStrings__mustask` for both `dotnet ef database
update` and AppHost); the database `mustask` was not touched. The stand
lock was free when taken (14:06) and released after the stand was stopped
(14:31).

| Date | What was run | Result |
|------|-----------------|-----------|
| 2026-10-04 | `npm ci` in `src/frontend` (no `node_modules` in this worktree) | exit 0 |
| 2026-10-04 | `npx vitest run …/ProcessDefinitionPage.addresses.test.tsx`, then again after adding F-A13 (a new test, not a re-run of a green set); `eslint` and `prettier --check` on the file | exit 0, 10/10, then 11/11; lint exit 0, formatted |
| 2026-10-04 | two temporary probes, deleted before any commit: a `200` with a type lacking `states` / `backlogLevel`, a state lacking `category`, `workItemTypes` an object; and `document.activeElement` after a retry that gets the not-found code | the first three render the crash screen «Что-то пошло не так» (`TypeError` in render), the fourth is read failed: **D-4**; focus is on `BODY`: **D-5** |
| 2026-10-04 | `dotnet build src/backend/Mustask.AppHost` | exit 0, 0 errors |
| 2026-10-04 | Stand run 1, from nothing (ports 3000/5000/5010 free, no stand container): `dotnet ef database update` (applied `20261002114458_AddProcessDefinitions` to `mustask_mod001`), AppHost with the default profile; realm, `/alive` and the panel answered 200 at 14:10:02; `scripts/keycloak-seed.sh` exit 0 at 14:10:33; then `E2E_ALLOW_DOCKER=1 npm run e2e` | e2e exit 0, 38 passed (6.9 min), spec 20: 6/6. `e2e-results/stand.json`: `{"stand": "warm", "waitedMs": 20, "url": "http://127.0.0.1:5000/health", "checkedAt": "2026-10-04T09:10:35.765Z"}`. Warm: the seed's 31 s outlasted the API's 30 s realm-check window. Per § 7, one repeat on a stand started from nothing |
| 2026-10-04 | Stand stopped: AppHost tree killed (`taskkill /T /F`), `mustask-e2e-keycloak-5010` removed, ports confirmed free | — |
| 2026-10-04 | Stand run 2, from nothing: `dotnet ef database update` ("No migrations were applied. The database is already up to date."), AppHost; a 1 s poll until realm, `/alive` and the panel answered 200 (14:21:22); the suite started at once, with `scripts/keycloak-seed.sh` running beside it (the suite signs in only `e2e-fixture` and its own throwaway users, never the seed's `mustask` and `member`, so the order does not matter to it; seed exit 0) | **e2e exit 0, 38 passed, 0 failed, 0 flaky, 0 skipped (7.8 min)**; Playwright JSON `stats`: `{"expected":38,"skipped":0,"unexpected":0,"flaky":0}`; spec 20: 20.1-20.6 all passed. `e2e-results/stand.json`, checkedAt after this run's start (09:21:24Z): `{"stand": "cold", "firstSeen": "HTTP 503: Unhealthy", "waitedMs": 9076, "url": "http://127.0.0.1:5000/health", "checkedAt": "2026-10-04T09:21:34.097Z"}`. **This is the run that counts** |
| 2026-10-04 | On the run 2 stand, before stopping it: `curl` without a token to `/api/organizations/<id>/process` for the demonstration id, it in upper case, `a%2Fb`, `abc%3Fx`, `abc%23x`, `abc%20x`, Cyrillic, `%25`, `%25E0%25A4%25A`, 300 × `a`, `not-a-uuid`; control `/api/organizations/a/b/process` | every id: 401 `application/problem+json` (real Kestrel matched the read's endpoint, so each is one segment); the control: 404. Not an E2E run |
| 2026-10-04 | Stand stopped as above; `Mustask.Api.exe` and `Mustask.AppHost.exe` gone, ports free, lock removed | — |
| 2026-10-04 | `bash scripts/verify.sh` on a566e51 plus the QA test addition, this qa.md and the status.md Log line: links, backlog ids, status.md budget, spec hygiene, agent configuration, backend build and the whole backend suite, frontend lint, build and tests | exit 0, RESULT: green; Domain 102, Shared 53, Architecture 68, Application 60, Contracts 61, Api.IntegrationTests 390, Infrastructure 114 (0 failed; the order-dependent `SessionLogoutEndpointTests` failure of bug-002 did not show this time); Vitest 67 files, 641 tests (630 + this pass's 11). Only this row was added after it. |

### Defects

#### D-3: Before mod-002 the process address shows no «Меню настроек» and no trail, unlike design point 2

- Severity: minor (navigation context only; the page itself is complete)
- Steps: as a user who holds `dictionaries.languages.read`, open
  `/organizations/d0000000-0000-4000-8000-000000000001/settings/process`.
- Expected / Actual: design.md § "Two renderings", point 2 (approved at
  gate-2) and `mod-015` BR-19: the address is a settings page while
  «Процесс» is hidden, so «Меню настроек» is shown with no entry current
  and the rail marks «Настройки» current; the trail reads «Настройки /
  Организация» (`mod-015` design § 3.4, § 15, which supersede T-26) / no
  «Меню настроек», the main navigation as kept with nothing current, no
  trail. The shell decides "settings page" by an entry's landing in the
  current selection (`isSettingsPage`, `app/navigation/visibility.ts`), and
  the placeholder selection is never the address's id.
  `AppLayout.process.test` pins the shell as merged. The developer raised
  exactly this as plan-frontend § 11 P-1 and went ahead with (a); no
  decision is recorded in status.md.
- Status at pass 1: open, for the orchestrator: P-1 (a) with a design delta
  that narrows point 2 to "once the context is real", or P-1 (b) as a shell
  line of its own (BR-M2-12 keeps it out of this task). It closes by itself
  when `mod-002` makes context addresses set the selection.
- Status: **closed, re-check 1 (2026-10-04)**, against status.md D-15
  (plan-frontend P-1 → (a)). design.md point 2, point 4, T-26, § Adopting
  mod-015 item 3 and the reconciliation rows now say what plan-frontend § 6.3
  says and what `AppLayout.process.test` pins; the prototype's placeholder
  context agrees (Re-check 1, check 1). Two small leftovers are D-6 and D-7
  below (minor); neither contradicts the decision.

#### D-4: A 200 whose body lacks a part crashes the panel instead of showing a failed read

- Severity: minor (the contract guarantees the shape and the backend pins
  it, `Get_SeededOrganization_HasExactlyTheContractShapeAndNoNulls`; a
  server or proxy fault is what reaches it)
- Steps: answer the read with § 1.4's body where `workItemTypes[0]` has no
  `states` (or no `backlogLevel`, or one state has no `category`); open
  the screen.
- Expected / Actual: design § States, Read failed: "an answer that cannot
  be read" is a failed read, with the title, the description and
  «Повторить» inside the shell / a `TypeError` in render reaches the app's
  `ErrorBoundary`: the crash screen «Что-то пошло не так» replaces the
  whole shell («Обновить страницу» only). `getProcessDefinition` checks only
  that `workItemTypes` is a non-empty array.
- Status at pass 1: open, the developer's call: check each type's `states`,
  `backlogLevel` and each state's `category` in `getProcessDefinition` and
  throw the read-failed error, with a test; or "won't fix" recorded with
  the reason.
- Status: **closed, re-check 1 (2026-10-04)** for the three missing parts and
  the wrong-shape variants of them (a null, a string, an object where an
  object or array belongs, a type or state that is not an object): each is
  read failed with «Повторить» inside the shell, never the crash screen
  (Re-check 1, check 2). Not handled, and accepted as not handled (review
  round 2 note 1, round 3): an object where a text belongs (`name: {}`);
  see Re-check 1, check 2.
- Fix (developer, 2026-10-04, review round 1 F2, 651f27c): the first
  option. `getProcessDefinition` throws `UnreadableProcessDefinitionError`
  (the renamed `EmptyProcessDefinitionError`) for each missing part, and
  also for a type or a state that is not an object. Tests: six rows in
  `processApi.test.ts`, and three rows of the failed-read outline in
  `ProcessDefinitionPage.states.test.tsx` (no states, no backlog level, no
  category: the alert with «Повторить» inside the shell). For QA to
  re-check.

#### D-5: A retry that ends in «Организация не найдена» drops focus to the document

- Severity: minor (accessibility, a rare path: contract § 4's unmigrated
  stand migrated before its next start; after `mod-002`/`mod-016` an
  organization deactivated or a membership removed between the two reads)
- Steps: answer the read 500, open the screen, make the next answer the
  not-found code, press «Повторить» (F-A13).
- Expected / Actual: design § Focus and announcements exists so that the
  removed «Повторить» does not drop focus to the document / focus is on
  `body`: `useRetryFocus` moves focus to the title on success and to
  `retryRef` on failure, and on not found neither element is mounted. The
  design names no target for this outcome; the frame's `h1` «Организация
  не найдена» or its «На главную» would be the natural one.
- Status at pass 1: open, the developer's call (with the designer if a
  target is to be named).
- Status: **closed, re-check 1 (2026-10-04)**: after a retry that ends in the
  not-found code, `document.activeElement` is the frame's `h1` «Организация
  не найдена» (`tabindex="-1"`), not `BODY`; design.md § Focus and
  announcements now names that target (Re-check 1, check 3).
- Fix (developer, 2026-10-04, review round 1 F4, bcef95f): focus moves to
  the frame's `h1` «Организация не найдена», the page's heading in that
  state, as the title is on success. The handle is an optional
  `headingRef` on `StateFrame` and `NotFound` (`tabIndex={-1}` only when
  given). Tests: "moves focus to the frame’s heading when the retry finds
  no organization" (fails without the ref) and "moves no focus when the
  first load finds no organization" in
  `ProcessDefinitionPage.states.test.tsx`, and two in
  `StateFrame.test.tsx`. design.md § Focus names no target for this
  outcome; a design delta would add one sentence. For QA to re-check.

### Verdict

blockers: 0 · majors: 0 · minors: 3 open (D-3 for the orchestrator, D-4
and D-5 the developer's call) · E2E: 38/38 on a cold stand (run 2) ·
result: pass

(Pass 1's verdict above stands as written; it is superseded by Re-check 1's
below, which closes D-3, D-4 and D-5.)

### Re-check 1

2026-10-04, qa-engineer (deep tier), clean context, over what changed since
pass 1 (`git log 38a3190..HEAD`): the two fixes (651f27c D-4, bcef95f D-5),
the decision (b7e14ae, D-15), the design delta (344c91f), the prototype
(732b959) and the review's prose commits. The checks below were fixed from
the task, D-3/D-4/D-5's own text and the review's F1, F2 and F4 before the fix
diff was read; the diff and the developer's tests were read next, only to see
which of the checks they already cover.

E2E was not re-run, by the orchestrator's instruction (ports 3000/5000/5010
belong to another task, and pass 1's two runs were on a cold stand). One limit
follows and is stated, not hidden: `useRetryFocus` changed after the cold-stand
run, and spec 20.3 (a 500, then «Повторить», focus on the title) was not re-run
in a browser; the same path is covered in jsdom by the developer's "shows the
skeleton while a retry runs, then the content, with focus on the title" and by
this re-check's own success-path test.

| # | Check | Result |
|---|-------|--------|
| 1 | **D-3 against D-15.** design.md § Two renderings point 2 and point 4, T-26, § Adopting mod-015 item 3, the two reconciliation rows (the trail row and the settings-page row), the layout B drawing and its lead-in, read against plan-frontend § 6.3 (rows 1-3) and `AppLayout.process.test.tsx` (the placeholder-source test, the real-source test, the real-source not-found test); then the prototype | ☑ Point 2: before `mod-002` no «Меню настроек», no trail, nothing marked current in the rail (a «Языки» reader included), main navigation as kept; once `mod-002` makes addresses set the selection, «Меню настроек», «Процесс» `aria-current="page"` and «Настройки» current — rows 1 and 2 of § 6.3, and the tests assert exactly these (a `LANGUAGES_READ` user: «Меню настроек» absent, no trail, zero `aria-current` in the main navigation; then the stub real source: `page`, `true`, trail text «Настройки/Организация», the trail not a link). Point 4, T-26, Adopting item 3 and the trail row: «Настройки / Организация», derived by the shell once the context is real, none before; tab «Процесс — Mustask» unchanged. The settings-page reconciliation row ends "Beside the not-found frame there is no «Меню настроек» at any time" = § 6.3 row 3 = the real-source not-found test. The layout B drawing is captioned as the real context. `spec.md`, the stories and `api-contract.md` do not mention the menu or the trail. Prototype, placeholder context: no menu, no trail, no rail mark; real context: all three; not found: no menu and no trail in either context. **D-3 closed.** Two small leftovers: D-6, D-7 (minor) |
| 2 | **D-4.** For each missing part — a type without `states`, a type without `backlogLevel`, a state without `category` — a `200` served by MSW to the real routes, the real shell and the real `ErrorBoundary` (App.tsx's composition): the alert «Не удалось загрузить процесс» with «Повторить», the shell's banner and the `h1` «Процесс» kept, no «Что-то пошло не так», no «Организация не найдена», no card; then the same parts in the last type and the last state; then wrong shapes (`states` null and an object, `backlogLevel` null and a string, `category` null and a string, a type that is a string, a state that is a number, `workItemTypes` an object or missing, a body that is an array, null or a string); a retry from a broken answer to a whole one (content, focus on the title); a retry from a broken answer to another (alert back, focus on «Повторить») | ☑ 18 cases (15 shapes, 3 body kinds) and 2 retry cases, all green (`ProcessDefinitionPage.unreadable.test.tsx`). The focus half of the second retry case, "alert back, focus on «Повторить»", passed vacuously as first written and is held since 76e1244 (Correction F6 below). The developer's tests already cover the three parts through MSW at page level (`ProcessDefinitionPage.states.test`: "a 200 whose type has no states / no backlog level / whose state has no category") and six rows in `processApi.test.ts`; this file adds the shell-plus-boundary composition, where a render throw would show as the crash screen. Mutation check: with `missingPart` cut back to the old "a non-empty array" check, 17 of the 26 tests in the file fail (13 of the 15 shapes — the other two, a non-array and a missing `workItemTypes`, the old check caught too — both retry tests, and two D-5 tests that start from a broken answer); the 9 that pass are the ones the old check also handled. **D-4 closed** |
| 2a | **Wrong leaf type** (review round 2 note 1): a temporary probe over sixteen variants, deleted before the commit | An object where a text belongs still crashes to «Что-то пошло не так»: type `name: {}`, type `key: {}`, backlog level `name: {}`, state `name: {}`, state `order: {}`, category `label: {}`. A missing or null text field renders as an empty cell, and an odd `appearanceToken` or category `code` falls back to the theme's mark: content, no crash. **Not handled, and accepted as not handled**: the contract's response records are non-null `string`/`int`/`bool` and serialize only as such, the review (rounds 2 and 3) accepted it, and the developer left it. Not raised as a defect; a later line would add the leaf kinds to `missingPart`, one row per field |
| 3 | **D-5.** 500, then the not-found code, then «Повторить»: `document.activeElement` is the frame's `h1` «Организация не найдена» (not `BODY`), `tabindex="-1"`, the only `h1`, the tab «Организация не найдена — Mustask», no «Повторить» left; the next Tab reaches «На главную»; 500, 500, not found (focus on «Повторить» after the second 500, on the `h1` after the not-found code); an unreadable `200` and then not found; 500 and then an unreadable `200` (focus back on «Повторить»); a first load that is already not found (no focus moves, `BODY`) | ☑ 6 cases, all green, in the same file. The two «Повторить» halves of that list, "focus on «Повторить» after the second 500" and "focus back on «Повторить»" after an unreadable `200`, passed vacuously as first written and are held since 76e1244 (Correction F6 below); the assertions on the `h1` were not affected (the not-found mutation fails 4 of the 6, as stated below). The developer's "moves focus to the frame’s heading when the retry finds no organization" asserts `toHaveFocus()` on the `h1` and `tabindex="-1"`, "moves no focus when the first load finds no organization" asserts `BODY`, and two `StateFrame` tests pin `headingRef` (given: ref and `tabindex="-1"`; absent: no `tabindex`). An assertion on the focused element was therefore present; this file adds it in the shell, the Tab step and the multi-attempt and mixed orders. Mutation check: with the hook's not-found branch disabled, 4 of the 6 fail (the four that end in not found after a retry); the other two never reach it. **D-5 closed** |
| 4 | **Regressions around the fixes**: the retry success path (focus on the title), the retry failure path (focus back on «Повторить»), the process page tests, `visibility.test.ts`, `AppLayout.process.test`, `routes.process.test`, `StateFrame.test`, then the whole suite, lint, `tsc -b` | ☑ the targeted run in the table below; the whole suite, lint and the build (`tsc -b` and the production build) through `scripts/verify.sh` |

#### Runs (Re-check 1)

| Date | What was run | Result |
|------|--------------|--------|
| 2026-10-04 | `npm ci` in `src/frontend` (no `node_modules` in this worktree) | exit 0 |
| 2026-10-04 | `npx vitest run …/ProcessDefinitionPage.unreadable.test.tsx` (the new file, 26 tests); `eslint` and `prettier --check` on it | exit 0, 26/26; lint exit 0, formatted |
| 2026-10-04 | Two mutation checks, each restored with `git checkout` before any commit: `missingPart` cut back to the old check (17 failed, 9 passed); the hook's not-found branch disabled in `useRetryFocus` (`-t D-5`: 4 failed, 2 passed) | as first written: "the new tests fail against the opposite behaviour, so they are not vacuous". **Corrected (code review round 4, F6):** that holds for these two mutations only. A third, the hook's «Повторить» focus call (`useRetryFocus.ts:42`) removed, was not run, and when the review ran it all 26 tests of this file passed, so the retry tests' focus assertions on «Повторить» were vacuous. After 76e1244 that mutation fails four tests (Correction F6 below) |
| 2026-10-04 | A temporary probe of sixteen wrong-leaf variants (check 2a), deleted before the commit | six crash screens (an object where a text belongs), ten no crash |
| 2026-10-04 | `npx vitest run src/features/process src/shared/ui/StateFrame.test.tsx src/app/navigation/visibility.test.ts src/app/AppLayout.process.test.tsx src/app/routes.process.test.tsx` | 13 files, 167 tests, all passed (the exit status was not captured, the summary line was read; the whole suite's exit status is the `verify.sh` row) |
| 2026-10-04 | `bash scripts/verify.sh` on c4a7360 (the branch with this re-check's test and qa.md; the default base `main`, so the whole branch classifies as code): links, backlog ids, status.md budget, spec hygiene, agent configuration, JSON, workflow compatibility, backend build, backend tests including integration, frontend lint, frontend build (`tsc -b` and the production build), frontend tests | **exit 0, `RESULT: green`**; every step `ok`; Vitest 68 files, 681 tests (the developer's 655 + this re-check's 26); the order-dependent `SessionLogoutEndpointTests` failure of bug-002 did not show (the script prints no per-project backend counts in this mode). Only this row, the Log line and the commit that holds them were added after it |
| 2026-10-04 | Frontend-developer, code review round 4 F6, on 76e1244: `npx vitest run` over `ProcessDefinitionPage.unreadable.test.tsx` and `ProcessDefinitionPage.states.test.tsx`, first with `retryRef.current?.focus()` commented out in `useRetryFocus.ts` (restored with the same edit; the tree held only the intended files), then as committed; then the whole suite, `npm run lint`, `tsc -b` | focus call removed: **4 failed, 46 passed**, all four on `toHaveFocus()` (named in Correction F6); as committed: 2 files, 50/50; the whole suite 68 files, 681 tests; lint and `tsc -b` exit 0. QA has not re-run its rows 2 and 3 over the rewritten tests: that stays QA's |

#### Correction F6 (code review round 4)

Four tests claimed, one assertion each, that focus returns to «Повторить» after
a repeated failure: the developer's `ProcessDefinitionPage.states.test.tsx`
"brings the alert back when the retry fails again…", and, in
`ProcessDefinitionPage.unreadable.test.tsx`, "brings the alert back, with focus
on its «Повторить», when the retry gets another broken answer", the second step
of "follows every attempt: «Повторить» after a second 500, the heading after the
not-found code", and "puts focus back on «Повторить» when a retry after a 500
gets an unreadable 200". Rows 2 and 3 above and the mutation row relied on them.

- **What was wrong.** Each test answered the retry at once. In jsdom the
  retry's fetching state and its failure settle in the same render, so the
  skeleton never replaced the alert (`ProcessDefinitionPage.tsx:41`, :59) and
  the «Повторить» that `user.click` had focused stayed mounted and kept that
  focus. The assertion was vacuous: with `retryRef.current?.focus()`
  (`useRetryFocus.ts:42`) removed, all 26 tests of the file, and every test in
  `src/features/process` and `src/shared/ui`, still passed (review round 4,
  mutation M1). The first-written claim "the new tests fail against the
  opposite behaviour, so they are not vacuous" was true of the two mutations
  that were run and said nothing of this one.
- **How it was fixed (76e1244, tests only, no product code).** Each of the four
  holds the retry's answer with `gate()` (moved from `states.test.tsx` to
  `src/test/gate.ts`, so both files use one). While the answer is held, the test
  asserts the status «Загрузка данных», no `alert`, and that the clicked button
  is no longer in the document. It then releases the answer and asserts that the
  new alert's «Повторить» is a different node from the clicked one and has
  focus.
- **Mutation, as it stands.** With the focus call removed, exactly four tests
  fail, each on `toHaveFocus()`: the `states.test.tsx` one and the three
  `unreadable.test.tsx` ones named above; 46 of the 50 tests of the two files
  pass. With the call restored all 50 pass. The success-path and not-found
  focus tests are unchanged and unaffected.
- **Still QA's.** Re-running rows 2 and 3 over the rewritten tests (review
  round 4, F6, fix step 3) is a QA step in its own session; nothing here
  replaces it.

#### Defects found by Re-check 1

##### D-6: The prototype marks «Настройки» current in the rail on «Организация не найдена» in the real context

- Severity: minor (a prototype, illustrative; no product effect)
- Steps: open `prototype/process-definition.html`, layout B, context
  «Real, after mod-002», state «Организация не найдена».
- Expected / Actual: plan-frontend § 6.3 row 3 ("Not found, at any time:
  no entry current") and `AppLayout.process.test` "on not found, still no
  «Меню настроек», no entry current and no trail" (zero `aria-current` in the
  main navigation) / `railCurrent` in `layoutB()` depends on the context only,
  so the rail's «Нс» carries `aria-current="true"` beside the not-found frame.
  design.md point 10 says "no settings entry is marked current", which is
  ambiguous about the rail.
- Status at re-check 1: open, the designer's call (the prototype's
  `railCurrent` takes `real && !notFound`, or point 10 names the rail).
- Status: resolved (8b900ed). The first option: `railCurrent` in `layoutB()`
  of `prototype/process-definition.html` now takes `real && !notFound`, so the
  rail has no current mark beside the not-found frame in either context; the
  page's note and the code comment say so. Checked by running the page's
  script in node against a stubbed DOM, `layoutB()` for both contexts by
  content, loading, error and not found: the rail mark, «Меню настроек» and
  the trail appear only in the real context outside not found, and the file
  as it was before shows the one mismatch (real, not found). design.md point
  10 is not changed; its «no settings entry is marked current» is read to
  cover the rail.

##### D-7: plan-frontend still reads P-1 as undecided

- Severity: minor (the decision is recorded in status.md D-15; the plan is
  history)
- Steps: read plan-frontend.md § 6.3's closing paragraph, § 7's last row,
  § 11 (P-1 has no outcome), § 12's first bullet and § 13's last bullet.
- Expected / Actual: after D-15 and the design delta, a reader of the plan
  finds the decision / § 13 says "F1 (P-1) is still undecided and is the
  orchestrator's" and § 6.3 says design.md point 2 "expected «Меню настроек»",
  which design.md no longer does.
- Status at re-check 1: open, the developer's (one line under § 11 or § 13:
  "P-1 decided (a), status.md D-15, 2026-10-04"); `AppLayout.process.test.tsx`
  already pins § 6.3's rows 1 and 2, so no test changes.
- Status: resolved (8b900ed). plan-frontend.md now reads P-1 as decided (a) by
  status.md D-15 (2026-10-04), in each place named under Steps: § 6.3's
  closing paragraph (what the shell shows before mod-002 and once mod-002
  makes addresses set the selection, and that design.md was narrowed instead
  of the shell changed), § 7's last row, § 11 (a "Decided: (a)" bullet under
  P-1), § 12's first bullet and § 13's last bullet. No other content of the
  plan changed, and no test.

#### Verdict (Re-check 1)

blockers: 0 · majors: 0 · minors: 2 open (D-6 for the designer, D-7 for the
developer; both documentation only) · D-3, D-4, D-5 closed · E2E not re-run (the
orchestrator's instruction; the limit is named above) · result: pass

(This verdict stands as written; D-6 and D-7 are recorded as resolved above,
after it, by documentation edits only.)

### Re-check 2 (F6)

2026-10-04, qa-engineer (deep tier), clean context, scoped to code review
round 4's F6: the test-only commit 76e1244 and the qa.md correction 0d0f983.
`git diff c4a7360..HEAD -- src` (c4a7360 is the commit Re-check 1 ran on) holds
nothing but the three test files of 76e1244, so no product code changed since
Re-check 1 and nothing outside F6 is re-opened. Read first: review.md round 4
F6 and this file's Re-check 1 and "Correction F6"; then the four tests, the
shared `src/test/gate.ts`, and `useRetryFocus.ts`, `ErrorBanner.tsx` and
`ProcessDefinitionPage.tsx` to see what each assertion can and cannot reach.
E2E was not re-run: no product code changed, and Re-check 1's named limit (spec
20.3's focus path in a browser) is the same as it was.

| # | Check | Result |
|---|-------|--------|
| 1 | **Do the four tests check what they claim?** `ProcessDefinitionPage.states.test.tsx` "brings the alert back when the retry fails again, with focus on its new «Повторить»", and in `ProcessDefinitionPage.unreadable.test.tsx` "brings the alert back, with focus on its «Повторить», when the retry gets another broken answer", "follows every attempt: «Повторить» after a second 500, the heading after the not-found code" and "puts focus back on «Повторить» when a retry after a 500 gets an unreadable 200" | ☑ Each answers the first read at once and holds the retry's (`gate()`: the handler awaits `held.opened`). After the click it asserts the status «Загрузка данных», no `alert`, and that the button it clicked is no longer in the document. Only then does it open the gate, wait for the failed-read frame (the three unreadable tests through `expectFailedRead`, which also asserts the title, the banner and no card, no crash screen, no «Организация не найдена») and assert that the new «Повторить» is not the clicked node and `toHaveFocus()`. The three unreadable ones share `retryHeld` and `expectFocusOnNewRetry`; the second step of "follows every attempt" (the retry that ends in not found) is the `h1` assertion and is not held, which is right: it has no «Повторить» to hold. Every `gate()` in the two files is opened (six uses), and `gate.ts` is the one copy (the local one in `states.test.tsx` is gone) |
| 2 | **Is the "different node" assertion real?** | ☑ The clicked button is asserted detached while the answer is held, and the ternary at `ProcessDefinitionPage.tsx:59-61` is why it is: the skeleton replaces the `ErrorBanner`, so the button unmounts and the new alert's button is a new element. jsdom drops focus to `body` when the focused element is removed, so focus on the new node can only come from something that runs after the held answer. `not.toBe(clicked)` is then a true statement about two nodes, not a tautology |
| 3 | **Any remaining way to pass without the focus call?** | ☑ None found. `ErrorBanner` sets no `autoFocus`, and the only writer of focus to that button is `retryRef.current?.focus()` (`useRetryFocus.ts:42`); the four mutations below each fail the four tests. Not covered, and not claimed: the focus call in a real browser (the same tests in jsdom; e2e 20.3 asserts the title after a successful retry, not «Повторить» after a second failure) |
| 4 | **Rows 2 and 3 of Re-check 1 re-run over the rewritten tests** (the two rows Correction F6 leaves to QA) | ☑ The whole of `ProcessDefinitionPage.unreadable.test.tsx` as committed: 26/26 (the 18 shapes and body kinds, the 2 retry cases of row 2, the 6 cases of row 3), the two «Повторить» halves of rows 2 and 3 now held. Row 2's mutation (`missingPart` cut back to the old "a non-empty array" check) re-run: **17 failed, 9 passed**, as at Re-check 1. Row 3's mutation (the not-found branch of `useRetryFocus` disabled, `-t D-5`) re-run: **4 failed, 2 passed**, as at Re-check 1 (the four that end in not found after a retry). Both restored with `git checkout --` |

#### Mutations (Re-check 2)

Each over the two files `ProcessDefinitionPage.states.test.tsx` and
`ProcessDefinitionPage.unreadable.test.tsx` (50 tests, 50 green as committed),
each restored with `git checkout -- <file>` before the next (the tree was clean
after every one).

| # | Mutation | Result |
|---|----------|--------|
| M1 | `retryRef.current?.focus()` commented out (`useRetryFocus.ts:42`), the mutation the review ran | **4 failed, 46 passed**, all four on `expect(element).toHaveFocus()` (`states.test.tsx:260`, `unreadable.test.tsx:151`): the `states.test.tsx` test and the three `unreadable.test.tsx` tests named in row 1, and no others |
| M2 | the same line focusing the title instead (`titleRef.current?.focus()`; the title is mounted during a failed read, so something does take focus) | the same four fail, 46 pass: a wrong target is caught, not only a missing call |
| M3 | the `isFetching` guard removed from the effect's first line (`if (!retryAsked.current)`): the focus request is spent while the retry is still running, when no button is mounted | **5 failed, 45 passed**: the four, plus the developer's held "shows the skeleton while a retry runs, then the content, with focus on the title". The un-held success test of `unreadable.test.tsx` passes under it, as it would (it never shows the fetching render); the guard is pinned only by tests that hold, which is the point of F6 |
| M4 | `ref={retryRef}` removed from the «Повторить» of `ErrorBanner.tsx` (the wiring, not the hook) | the same four fail, 46 pass |

#### Runs (Re-check 2)

| Date | What was run | Result |
|------|--------------|--------|
| 2026-10-04 | `npm ci` in `src/frontend` (no `node_modules` in this worktree) | exit 0 |
| 2026-10-04 | `npx vitest run` over the two files, as committed | exit 0, 2 files, 50/50 |
| 2026-10-04 | M1, M2, M3, M4 above, one run each over the two files | exit 1 each: 4, 4, 5, 4 failed; each restored, tree clean |
| 2026-10-04 | Row 2's and row 3's mutations over `ProcessDefinitionPage.unreadable.test.tsx` (row 4) | 17 failed / 9 passed, and 4 failed / 2 passed (`-t D-5`); each restored |
| 2026-10-04 | `npx vitest run` (the whole suite), `npm run lint`, `npx tsc -b`, `npx prettier --check` on `src/test/gate.ts` and `features/process/pages` | Vitest exit 0, 68 files, 681 tests; lint exit 0; `tsc -b` exit 0; prettier clean |

#### Defects found by Re-check 2

None. Not a claim that the tests are perfect, only that no way to pass without
the focus call was found, over four mutations that move the call, its target,
its timing and its wiring, and that the two earlier mutations of Re-check 1 give
the counts Re-check 1 recorded. The one limit carried over, not new: the focus
path after a second failure is asserted in jsdom only.

#### Verdict (Re-check 2)

blockers: 0 · majors: 0 · minors: 0 new (D-6 and D-7 stay resolved) · F6 closed
from QA's side: the four tests fail without the focus call and pass with it, and
Correction F6's statements (four fail, 46 of 50 pass, 50/50 restored) were
reproduced · E2E not re-run (no product code changed since Re-check 1) ·
result: pass
