# tech-002-apphost-external-database

- type: tech
- size: S
- branch: tech/002-apphost-external-database
- stage: complete
- epic: platform/stand
- deps: -
- gates: gate-3

## Gates

gate-3: approved · delegated by human 2026-10-01 · merged 2026-10-01 (local squash, no remote)

## Plan

1. `src/backend/Mustask.AppHost/AppHost.cs`: when `ConnectionStrings:mustask` is set (user secrets or
   `ConnectionStrings__mustask`), declare `AddConnectionString("mustask")` instead of
   `AddPostgres(...).WithDataVolume().AddDatabase("mustask")`; `api` references and waits for whichever
   resource was declared. The `mustask-db-password` parameter is declared only in the container branch.
   Default behaviour (no connection string) is unchanged.
2. Docs: `src/backend/AGENTS.md` (secrets/ports paragraph) and `docs/runbooks/local-migrations.md`
   gain the external-database mode: the user secret to set, that the database is created by
   `dotnet ef database update`, and that `Ports:Db` / `mustask-db-password` are then unused.
3. A guard test if one fits the existing AppHost/architecture test project; otherwise record why not.
4. Verified live: the stand comes up against the human's PostgreSQL on 5432 (credentials in the AppHost user secret).

## Log

- 2026-10-01 [orchestrator] gate-3: approved under the human's delegation of 2026-10-01 (the human told the orchestrator in chat to decide the gates and merge the series itself: «делай всё сам, продолжай»). No separate QA re-run after the round-1 fixes: the fixes are docs, comments and the guard test; the developer's full verify.sh and the round-2 review's mutation checks cover them, and finish-task.sh runs verify.sh once more. E2E not applicable (no UI change). The deferred docs drift is queued as tech-004-stand-docs-drift. Live check: the orchestrator's stand ran this switch against the human's PostgreSQL for the whole session
- 2026-10-01 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/tech-002-apphost-external-database
- 2026-10-01 [route] tech · S, route Dev → QA → Review → gate-3 (gate-1/gate-2 have no subject). Confirmed under the human's delegation of 2026-10-01: the human asked in chat to use the existing PostgreSQL on localhost:5432 (credentials in the AppHost user secret) and delegated every gate decision and the local squash of the in-flight series to the orchestrator. Not the light cycle: runtime code (AppHost) changes
- 2026-10-01 [dev] the AppHost switch was first written by the orchestrator in the consolidation worktree to bring the stand up against the human's database (migrations applied to database `mustask` on 5432, stand green: Keycloak, API /health, panel); moved here unchanged
- 2026-10-01 [dev] AppHost review: logic unchanged (live-verified); the new comments rewritten in Russian to match the surrounding block, plus the caveat that `ConnectionStrings__mustask` exported for `dotnet ef` switches an AppHost started from the same terminal
- 2026-10-01 [dev] docs: `src/backend/AGENTS.md` (external-database bullet, ports note) and `docs/runbooks/local-migrations.md` (section "External database": user secret, `dotnet ef database update` creates the database, `Ports:Db` / `mustask-db-password` unused, default https launch profile, how to go back); no real password anywhere
- 2026-10-01 [dev] guard test: `Mustask.Architecture.Tests/AppHostExternalDatabaseRules.cs` reads `AppHost.cs` like the existing `KeycloakStandClientSecretPlacementRules` (the architecture project already inspects AppHost sources; the top-level statements cannot be called, an Aspire testing host needs DCP/Docker and is not in the solution). Pins: one `ConnectionStrings:mustask` read; `AddParameter("mustask-db-password"`, `AddPostgres(` and `AddConnectionString("mustask")` exactly once and behind the switch; `api` wired through the single `database` variable. Mutation check: hoisting the password parameter above the switch fails it, restored → green
- 2026-10-01 [dev] `dotnet build Mustask.sln` → 0 warnings, 0 errors; `bash scripts/verify.sh` → exit 0 (RESULT: green; Architecture.Tests 68/68, Api.IntegrationTests 346/346, Infrastructure.Tests 74/74, all other projects green). Ready for QA
- 2026-10-01 [qa] pass, zero blockers (review.md, ## QA): default path unchanged by code reading, external branch wires api through the single `database` variable, docs match the code; stand evidence read-only (`/health` 200 incl. the MustaskDbContext check, migrations present in `mustask` on 5432, no Aspire-made DB container); own mutation M2 caught, M3 (inverted switch) survives the guard (QA-1, minor), docs omit that the user secret is shared by all worktrees (QA-2, minor); `dotnet build Mustask.sln` 0/0, `bash scripts/verify.sh` exit 0 (RESULT: green). E2E not applicable: no UI change
- 2026-10-01 [review] code review round 1 (backend): needs changes, 6 findings open (1 major: CR1; 5 minor: CR2-CR6); review.md, "Code review — Round 1 — backend"
- 2026-10-01 [dev] review round 1 fixes, CR1-CR6 all taken. CR1: runbook step 1 qualified, step 3 and the "needed always" note say to export `ConnectionStrings__mustask` in a terminal other than the one that starts AppHost (or unset it), and AppHost now prints one `Database:` line naming the chosen mode (the value is never printed; the default mode gains only that line). CR2: one scope sentence with the per-terminal alternative in `src/backend/AGENTS.md` and runbook external step 1. CR3: guard-test comment and message match Aspire 13.5.1 (unresolved parameter, dashboard prompt, nothing blocked). CR4: the guard asserts the exact `if (string.IsNullOrWhiteSpace(externalDb))` header and splits the if body from the else body; mutations of `AppHost.cs` (inverted condition, swapped branch assignments, password parameter hoisted / moved to the else, `WaitFor` removed, else removed) each fail 1 of 2 tests, restored → 2/2. CR5: both credential mentions in this file replaced. CR6: the lines this branch adds to `AppHost.cs` are English. Not taken, a separate docs follow-up: the pre-existing "missing configuration value" and random-port wording (`src/backend/AGENTS.md`, `MustaskDbContextFactory.cs`, ADR-0002). `bash scripts/verify.sh` → exit 0 (RESULT: green; Architecture.Tests 68/68, Api.IntegrationTests 346/346, Infrastructure.Tests 74/74). Ready for QA re-run over the runbook, `AGENTS.md`, `AppHost.cs` and the guard test
- 2026-10-01 [review] code review round 2 (backend): approved by AI, 0 findings open (CR1-CR6 resolved; R2-1, round 1's own quote of the credential, redacted by the reviewer); two observations for the orchestrator (QA re-run of the fix not logged, deferred docs follow-up not queued); review.md, "Code review — Round 2 — backend"

## Notes

Summary (from the queue line or an argument): AppHost external database: when ConnectionStrings:mustask is configured, the stand uses that PostgreSQL instead of starting its own container; docs for machines that already run PostgreSQL on 5432
