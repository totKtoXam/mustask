# tech-040-messagepack-pin-to-cpm

- type: tech
- size: S
- branch: tech/040-messagepack-pin-to-cpm
- stage: 07-qa
- epic: platform/stand
- deps: -
- gates: gate-3

## Gates

gate-3: pending

## Plan

1. Baseline: Aspire.Hosting 13.5.1 pulls MessagePack 2.5.302 in through
   StreamJsonRpc; NU1902 (GHSA-qhrr-8q5h-9q3h) is an error under
   TreatWarningsAsErrors. Today the fix is `VersionOverride="2.5.303"` on a
   `PackageReference` in `Mustask.AppHost.csproj`, i.e. a version outside the
   central file.
2. Move the version into `Directory.Packages.props` as a `PackageVersion` with
   a comment naming the advisory and the drop condition (Aspire resolves
   MessagePack 2.5.303 or newer). Choose between an explicit reference in the
   AppHost and `CentralPackageTransitivePinningEnabled` by a restore
   experiment: the repo already pairs an explicit `PackageReference` with a
   `PackageVersion` for every pin (Npgsql, Microsoft.IdentityModel.*), and a
   global pinning switch would also apply those versions in projects that
   resolve them differently.
3. Check by command, not by reading: `dotnet restore` / `dotnet build` of the
   solution shows no NU1902, and `dotnet list package --include-transitive`
   resolves MessagePack 2.5.303 for `Mustask.AppHost`.
4. `bash scripts/verify.sh` once (Directory.Packages.props brings up the whole
   suite, s-route.md); the command and the exit code go into the Log.

## Log

- 2026-10-18 [new-task] task started (scripts/new-task.sh; its worktree was removed so a worktree-isolated agent could take the branch)
- 2026-10-18 [orchestrator] route S, ordinary cycle — confirmed by the human in chat («⭐ S, обычный цикл»)
- 2026-10-18 [backend-developer] dev: `PackageVersion MessagePack 2.5.303` in Directory.Packages.props (comment: advisory + drop condition); AppHost keeps a plain `PackageReference` without VersionOverride. Restore experiments on this tree: (A) PackageVersion alone, no explicit reference: AppHost restores MessagePack 2.5.302 and fails with NU1902, so the pin only works with the reference; (B) CentralPackageTransitivePinningEnabled instead of the reference: MessagePack resolves 2.5.303, but the solution restore fails with NU1109 (Npgsql 8.0.3 and Logging.Abstractions 10.0.0 entries downgrade Api, Infrastructure and the test projects), so the switch stays off. Chosen: explicit reference, the repo's pattern for every other pin. A first draft of the comment had a double hyphen inside the XML comment, which broke the Aspire SDK's package check at restore; it is worded around now.
- 2026-10-18 [backend-developer] proof: `dotnet restore Mustask.sln --force` exit 0; `dotnet build Mustask.sln --no-restore` exit 0, 0 errors, no NU1902 in the log (its 2 warnings are the EnableGitHooks `git config` lock race with a parallel worktree, not this change); `dotnet list package --include-transitive` for Mustask.AppHost: MessagePack requested 2.5.303, resolved 2.5.303; `--vulnerable --include-transitive`: no vulnerable packages.
- 2026-10-18 [backend-developer] `bash scripts/verify.sh` RESULT: green, exit 0 (backend build; backend tests including integration: Api.IntegrationTests 2304 passed, 17 skipped, 0 failed; Infrastructure.Tests 325, Domain.Tests 658, Application.Tests 709, Contracts.Tests 244, Architecture.Tests 83, Shared.Tests 53; links, ids and status.md budget checks ok). E2E not asked: standing answer no (paused 2026-10-12). stage: 07-qa.

## Notes

Summary (from the queue line or an argument): Move the MessagePack 2.5.303 pin (NU1902, GHSA-qhrr-8q5h-9q3h) from the VersionOverride in Mustask.AppHost.csproj into central package management, and drop it once Aspire resolves MessagePack 2.5.303 or newer
