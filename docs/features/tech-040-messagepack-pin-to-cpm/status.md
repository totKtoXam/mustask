# tech-040-messagepack-pin-to-cpm

- type: tech
- size: S
- branch: tech/040-messagepack-pin-to-cpm
- stage: 04-backend
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

## Notes

Summary (from the queue line or an argument): Move the MessagePack 2.5.303 pin (NU1902, GHSA-qhrr-8q5h-9q3h) from the VersionOverride in Mustask.AppHost.csproj into central package management, and drop it once Aspire resolves MessagePack 2.5.303 or newer
