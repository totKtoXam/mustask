# QA report - tech-047-test-run-speedup

Single QA pass, route M tech (test infrastructure only; no spec, no screens). E2E: standing NO, not asked, not run.
Reviewed `git diff origin/main...HEAD` at d6080e77.

## Checklist

| Criterion | Result | Evidence |
|---|---|---|
| 1. Semantics unchanged: no test deleted, skipped, weakened | pass | `[Fact]`/`[Theory]` attribute count in the two backend test projects: 2148 on origin/main and on HEAD. The diff adds or removes no `[Fact]`, `[Theory]`, `Skip` or assertion line. `dotnet test --list-tests`: Infrastructure.Tests 440 (as on main), Api.IntegrationTests 3089 listed lines (3094 reported by the runner, which counts theory cases individually). Test-file edits are comments, fixtures and collection attributes only. Vitest: only `maxWorkers` 4 to 8 in `vite.config.ts` (count 5067 not re-run, per instructions). |
| 2. Isolation in parallel shards | pass | See "Isolation audit". |
| 3. `MUSTASK_API_TEST_SHARDS=1` works | pass | `MUSTASK_API_TEST_SHARDS=1 dotnet test ... --filter "FullyQualifiedName~Authorization\|FullyQualifiedName~Session"`: 296 passed, 0 failed, 0 skipped, 1 m 31 s. The filter covers serial-collection classes (DenyAllRuntimeTests, SessionLogoutEndpointTests) and shard classes together. |
| 4. `src/backend/AGENTS.md` accurate | pass, 2 minors | The six reasons, the `SerialClasses` list, `ProcessEnvironmentCollection` and the env variable name match the code. |

## Isolation audit

Grepped the Api test assembly for `Console.SetOut/SetError`, `ConsoleCapture`, `SetEnvironmentVariable`, `Log.Logger`, `CapturingLoggerProvider`, static mutable members, `Stopwatch`/budgets, `WebApplicationFactory<Program>` built directly.

- Every class using the console, env variables or log capture of a host (ApiExplorer, SessionLogout, DeletionAuditLog*, PushEnvelopeQa, PushNulCharacterQa, UserDirectoryLogging, Logging, ProductionLogging, RealmReadiness, TokenLeak, PreviousSecretAudit, PushChannelAuthentication, Cors, KeycloakOptions, ErrorHandling, PurgeWorkItem, ProductionCors) is in `SerialClasses`.
- The only static mutable flag, `TestEndpointsStartupFilter.DenyAllProbeHandlerExecuted`, is touched by DenyAllRuntimeTests and RealmReadinessTests, both serial.
- Timing-budget classes: all `*VolumeTests` by name, plus four listed by hand (GetAreaTree, GetTeams, GetWorkItems, UserDirectoryQa). The serial collection has `DisableParallelization`, so it runs alone after the shards.
- Classes without the `MustaskApiCollection` attribute (slim hosts, handler unit tests, Live*) touch none of the above; they get a collection per class as xUnit's default does.
- `StaticLoggerGuard` users (CreateOrganization, GetProcessDefinition, ParentWriteSecondCode) stay in shards. A concurrent restore of `Log.Logger` there can only change which logger a *log-asserting* test sees, and those tests are serial. No defect.
- `MustaskApiFactory.CreateHost` builds every host (also `WithWebHostBuilder`) under one lock with its own connection string; the live-factory counter sets and clears the shared env variables once. The `_counted` guard covers a failed init.
- Migration template: `new MustaskDbContext(options).Database.MigrateAsync()` is equivalent to `MustaskDbContextFactory.CreateDbContext` (it only reads the env variable and calls `UseNpgsql`).
- Infrastructure.Tests: all files touching `ConnectionStrings__mustask` or `MustaskDbContextFactory` (CodeUniqueIndexModel, WorkItemReferenceInventory, Migrations, MustaskDbContextFactory, ProjectedUserModel) are in `ProcessEnvironmentCollection`; the two database fixtures pass explicit connection strings. `MigrationsTests` still gets a clean empty database per test, so the "clean database, repeated, rollback" checks keep their meaning.
- Shard assignment is a stable FNV-1a hash of the class name, so a class lands in the same shard on every run.

## Not run (by instruction)

Full Api, Infrastructure and Vitest suites; the `*VolumeTests` budgets are judged by CI on a clean runner. The speed-up figures (1715 s to 649 s and others) are the orchestrator's measurements and were not reproduced here.

## Defects

None blocker, none major.

- M-1 (minor, docs): `src/backend/AGENTS.md` does not say the default shard count (`min(cores/2, 8)`, 2 on a 4-core runner) nor that `MUSTASK_API_TEST_SHARDS=1` still runs the serial classes apart (after the one shard). Reading the paragraph, a developer cannot tell what "1" does. Repro: read the Commands section. Carried to gate-3 as a debt.
- M-2 (minor, docs): the same AGENTS.md paragraph has a line far over the wrapping of its neighbours (the `SerialClasses` sentence); cosmetic.
- Residual risk (not a defect): a new log-asserting or console-capturing class forgotten in `SerialClasses` fails only intermittently, under parallel shards. There is no guard test (the review accepted this; the AGENTS.md rule is the only mitigation).
