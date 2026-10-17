# tech-040-messagepack-pin-to-cpm

- type: tech
- size: S
- branch: tech/040-messagepack-pin-to-cpm
- stage: complete
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

## Decisions

- The pin is an explicit AppHost `PackageReference` plus a central `PackageVersion`, not `CentralPackageTransitivePinningEnabled`: with the switch the solution restore fails with NU1109 (the Npgsql 8.0.3 and Logging.Abstractions 10.0.0 entries downgrade Api, Infrastructure and the tests); a `PackageVersion` alone, without the reference, leaves MessagePack at 2.5.302 and NU1902 fails the restore.
- No double hyphen inside an XML comment in Directory.Packages.props: it broke the Aspire SDK's package check at restore.
- E2E = NO: the standing answer since 2026-10-12

## Log

- 2026-10-18 [new-task] task started (scripts/new-task.sh; its worktree was removed so a worktree-isolated agent could take the branch)
- 2026-10-18 [orchestrator] route S, ordinary cycle — confirmed by the human in chat («⭐ S, обычный цикл»)
- 2026-10-18 [backend-developer] dev: PackageVersion MessagePack 2.5.303 in Directory.Packages.props, plain PackageReference in the AppHost (no VersionOverride); the two restore experiments are in the commit body and Decisions.
- 2026-10-18 [backend-developer] proof: restore --force exit 0; build exit 0 with 0 NU1902; `dotnet list package` (flag include transitive) on the AppHost resolves MessagePack 2.5.303; no vulnerable packages. The build's 2 warnings: the EnableGitHooks git-config lock race with a parallel worktree.
- 2026-10-18 [backend-developer] `bash scripts/verify.sh` RESULT: green, exit 0 (backend build and tests incl. integration: Api.IntegrationTests 2304 passed, 17 skipped, 0 failed). E2E not asked: standing answer no (paused 2026-10-12). stage: 07-qa.
- 2026-10-18 [qa-engineer] QA light, S: pass; 0 blocker, 0 major, 1 minor (D-1: nothing queues the pin's removal), review.md. verify.sh exit 0, resolved graph equal to base's, E2E = NO. stage: 06-code-review (07-qa was not a stage value).
- 2026-10-18 [code-reviewer] code review round 1: changes-requested; open 0 blocker, 1 major (F1, prose: the drop check in Directory.Packages.props), 1 minor (m-1), review.md.
- 2026-10-18 [backend-developer] review round 1, F1 fixed: the drop check in Directory.Packages.props now names the removal of the AppHost reference; comment only, MSBuild loads the file; resolution under F1 in review.md. m-1 not in this round.
- 2026-10-18 [code-reviewer] delta read 1: approve; F1 resolved; open 0 blocker, 0 major, 2 minors carried to gate-3 as debt (review m-1, QA D-1), review.md.
- 2026-10-18 [orchestrator] stage: complete; gate-3 stays pending for the human (Merge button). Minors left: m-1 (Decisions prose), D-1 (no queue line holds the pin's removal). Next: verify.sh, push, PR.

## Notes

Summary (from the queue line or an argument): Move the MessagePack 2.5.303 pin (NU1902, GHSA-qhrr-8q5h-9q3h) from the VersionOverride in Mustask.AppHost.csproj into central package management, and drop it once Aspire resolves MessagePack 2.5.303 or newer
